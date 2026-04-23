# GitHub Copilot Instructions

You are working in the qyl repository. When applying review feedback on a pull request, execute these rules without
deviation.

## How to apply PR feedback

When invoked via `@copilot apply changes based on this feedback` or similar:

1. Read every review comment in the thread — from Codex, CodeRabbit, Copilot AI review, GitHub Code Quality, and human
   reviewers. Apply all of them in a single commit unless they conflict, in which case apply the stricter one.
2. After applying, run `dotnet build qyl.slnx --nologo /clp:ErrorsOnly` locally in your sandbox and confirm 0 errors
   before pushing.
3. Commit message format: `fix(pr): address review feedback — <one-line summary>`. Never squash semantic changes into "
   apply review" commits; use a fresh commit per logical fix.
4. Never push to a branch you did not create in this session without first running `git fetch` and
   `git rebase origin/<branch>`. If merge conflicts occur, resolve them, rerun the build, and push. Do not abort and
   leave the PR in a broken state.

## Hard rules — violations fail CI

These mirror `.editorconfig`, `CLAUDE.md`, `AGENTS.md`, and the ruleset emitted by `eng/MSBuild/Shared.Claude.*`. Follow
them on every file you touch:

- **Encoding:** new `.cs` files are UTF-8 **with BOM**. Verify before committing.
- **Copyright header** on every new `.cs` file: `// Copyright (c) 2025-2026 ancplua` as line 1.
- **XML documentation** is required on every public class, method, property, and record. No exceptions. Tests included.
- **Async suffix** on every method returning `Task`, `ValueTask`, `Task<T>`, or `ValueTask<T>` — including `[Fact]`/
  `[Theory]` test methods.
- **Test structure:** every test body has explicit `// Arrange`, `// Act`, `// Assert` comments demarcating the
  sections.
- **Test mocks:** use `FakeChatClient` from `tests/qyl.collector.tests/Instrumentation/` for any `IChatClient` double.
  Never hand-roll `Moq<IChatClient>`.
- **Sealed by default:** every non-public class is `sealed` unless a subclass exists in the same assembly.
- **No suppression:** no `#pragma warning disable`, no `[SuppressMessage]`, no `<NoWarn>` additions, no `null!`. If a
  warning fires, fix the code.
- **No runtime reflection as control flow, no `dynamic`, no `ExpandoObject`, no `.Result`, no `.Wait()`.**
- **File-scoped namespaces, primary constructors, required init properties, switch expressions over if/else ladders,
  pattern matching over type-testing.** C# 14 preview features are enabled — use them.
- **TimeProvider** only. Never `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, or `Stopwatch.GetTimestamp()`
  for business logic. Tests use `FakeTimeProvider`.
- **Environment variables:** `UPPER_SNAKE_CASE` only.

## DuckDB store — read/write separation is non-negotiable

`DuckDbStore` in `services/qyl.collector/Storage/` exposes two access paths:

- `GetReadConnectionAsync(ct)` returns a lease with `ACCESS_MODE=READ_ONLY`. **SELECT only.** Any `INSERT`, `UPDATE`,
  `DELETE`, `CREATE`, `DROP`, or DDL through a read lease is a defect.
- `ExecuteWriteAsync(async (connection, ct) => ..., ct)` is the only legal write path. It uses the serialized write
  channel. The lambda signature is **`Func<DuckDBConnection, CancellationToken, ValueTask>`** — two arguments, always.
  Single-arg lambdas will not compile against the real signature and are a sign the code was written blind.

When reviewing or applying feedback on anything in `services/qyl.collector/Storage/`,
`services/qyl.collector/Workflows/`, `services/qyl.collector/AgentRuns/`, `services/qyl.collector/Errors/`, or any other
`services/qyl.collector/**/` file that touches the store: grep the diff for `UPDATE`, `INSERT`, `DELETE`, and confirm
each one is inside an `ExecuteWriteAsync` call, not a `GetReadConnectionAsync` lease.

## HTTP endpoints — contract is the source of truth

Endpoint implementations under `services/qyl.collector/**Endpoints.cs` must match the TypeSpec contract at
`core/specs/api/routes.tsp`:

- If the spec declares `NotFoundError` for an operation, the implementation returns `TypedResults.NotFound()` when the
  resource is absent. Do not return `200 OK` with an empty array.
- Pagination limits (`default`, `max`) come from the spec, not the implementation. When adding a list endpoint, copy the
  `default` and `max` values verbatim from `routes.tsp`.
- When review feedback cites `routes.tsp` line numbers, open that file and read the surrounding decorators before
  changing the implementation.

## HTTP client error handling

In `services/qyl.loom/CollectorClient.cs` and any other `HttpClient`-consuming code:

- Before calling `ReadFromJsonAsync` on a non-success response, check `response.Content.Headers.ContentType?.MediaType`
  is `application/json` and `response.Content.Headers.ContentLength > 0`. Otherwise, fall back to `response.StatusCode`
  as the error signal.
- Wrap `ReadFromJsonAsync` in `try { ... } catch (JsonException) { ... }` on non-success paths. Never let a
  deserialization exception propagate from an error path.
- Return a structured failure DTO. Never throw on an expected HTTP error status.

## Codegen boundaries — never edit generated files

Six generators produce code. Any file ending in `.g.cs`, `.g.ts`, `.g.sql`, or `.g.tsp` is off-limits for direct edits.
To change generated output, edit the source and regenerate:

| Generated output                                                              | Source                         | Regenerate with                                                            |
|-------------------------------------------------------------------------------|--------------------------------|----------------------------------------------------------------------------|
| `packages/Qyl.Contracts/Generated/**`, `packages/qyl-client/src/generated/**` | `core/specs/**/*.tsp`          | `nuke Generate`                                                            |
| `packages/Qyl.OpenTelemetry.SemanticConventions/Attributes/**`                | OTel upstream YAML             | `./eng/semconv/run-weaver.sh`                                              |
| `packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/Attributes/**`     | OTel upstream YAML             | `./eng/semconv/run-weaver.sh`                                              |
| `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`          | `eng/semconv/model/qyl/*.yaml` | `nuke GenerateSemconv`                                                     |
| `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`                                 | `eng/semconv/model/qyl/*.yaml` | `./eng/semconv/run-weaver.sh`                                              |
| `services/qyl.collector/Generated/generated/**`                               | `core/specs/api/routes.tsp`    | `nuke Generate` (patched by `core/specs/scripts/patch-emitted-csharp.mjs`) |
| `internal/*.generators/` outputs                                              | Roslyn source generators       | `dotnet build`                                                             |

If review feedback asks for a change that lives in a `.g.*` file, change the source and regenerate. Commit both the
source change and the regenerated output in the same commit.

## Semconv — no magic strings

Never hard-code `"qyl.*"` or standard OTel attribute keys as string literals. Use:
 
- `QylAttr.<Namespace>.<Name>` for internal services (from `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`).
- `QylAttributes.<PascalName>` for the public NuGet surface (from
  `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`).
- OTel upstream classes from `packages/Qyl.OpenTelemetry.SemanticConventions{,Incubating}` for standard attributes (
  `ServiceAttributes.ServiceName`, `HttpAttributes.HttpRequestMethod`, etc.).

If the attribute does not exist, add it to `eng/semconv/model/qyl/<namespace>.yaml`, run `./eng/semconv/run-weaver.sh`
and `nuke GenerateSemconv`, then use the generated constant.

## MCP tools and capabilities

In `services/qyl.mcp/`:

- Never register a tool manually in `Program.cs` or DI. Declare it with `[QylSkill]` + `[QylCapability]` on the tool
  class and let `internal/qyl.mcp.generators/` emit the registration.
- `TaskSupport` on `[McpServerTool]`:
    - `Required` for async pipelines with side effects (`qyl.approve_fix_run`, `qyl.generate_fix`).
    - `Optional` for agent-invoking meta-tools and long-form searches (`qyl.use_qyl`, `qyl.root_cause_analysis`,
      `qyl.summarize_*`, `qyl.search_spans`, `qyl.find_similar_errors`, `qyl.list_errors`, anything returning >10k
      rows).
- `LoomToolEnvelope` results use the non-generic companion: `LoomToolEnvelope.Ok(data)` and
  `LoomToolEnvelope.Fail<T>(error)`. Never instantiate the generic form directly.
- `InvestigationLineage.TryEnter()` is called before any tool that spawns investigations. Respect the depth (3) and
  spawn (10) budgets; they are enforced at runtime.

## MAF agent composition

For anything under `services/qyl.loom/`, `services/qyl.loom.patterns/`, or `services/qyl.mcp/Agents/`.

### Rules

- Use the Apex three-builder pattern: `IXxxChatClientBuilder` → `IXxxAgentsBuilder` → workflow code. Concrete contract
  in `services/qyl.loom.patterns/Agents/IQylLoomPatternsAgentsBuilder.cs` — **one `Build*Agent()` factory method per
  bounded agent**, not a fluent chain. Each returned `AIAgent` is already wrapped with telemetry at the construction
  site.
- Decorate at composition root. Three distinct middleware helpers in
  `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs` — do not confuse them:
    - `IChatClient` direct (short form): `innerClient.WithQylTelemetry(sourceName: "qyl.genai")` — no `AsBuilder()`
      wrapping. Line 53. See `tests/qyl.collector.tests/Instrumentation/WithQylTelemetryEmissionTests.cs:36`.
    - `ChatClientBuilder` fluent form: `new ChatClientBuilder(innerClient).UseQylTelemetry(sourceName: "qyl.genai")
      .UseFunctionInvocation(...).Build()` — prefer this when composing a pipeline. Line 100. See
      `services/qyl.loom.patterns/Clients/QylLoomPatternsChatClientBuilder.cs:62`.
    - `AIAgentBuilder` fluent form: `agent.AsBuilder().UseQylAgentTelemetry().Build()` — wraps the agent. Line 141.
      See `services/qyl.loom/Autofix/Workflow/Executors/RcaExecutor.cs:40` for the canonical shape.
- Both layers are mandatory — `IChatClient` **and** `AIAgent`. Wrapping only one loses half the OTel attributes.
- Do not instantiate `ActivitySource` directly in agent code. `[AgentTraced]` is **removed**. Do not reintroduce it.

### MAF entry-point cheat sheet — verified against `Microsoft.Agents.AI` 1.1.0 and live qyl call-sites

Reach for these before hand-rolling. Every row has a concrete qyl call-site — grep it before writing a new variant.

| Layer                         | Entry point                                                                                                                                                                   | qyl call-site                                                                                                                     |
|-------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------|
| **Agent — standalone**        | `llm.AsAIAgent(new ChatClientAgentOptions { Name, Description, ChatOptions = new() { Instructions } }).AsBuilder().UseQylAgentTelemetry().Build()`                            | `services/qyl.loom/Autofix/Workflow/Executors/RcaExecutor.cs:35-40` and every sibling executor                                    |
| **Agent — non-streaming**     | `await agent.RunAsync(userMessage, cancellationToken: ct)`                                                                                                                    | `RcaExecutor.cs:42` — the universal shape across Autofix executors and `TriagePipelineService`                                    |
| **Agent — streaming**         | `await foreach (var evt in streamingRun.WatchStreamAsync(ct)) { … }`                                                                                                          | `services/qyl.loom/Autofix/AutofixAgentService.cs:66-77`, `services/qyl.loom/Exploration/ExplorationOrchestrator.cs:37`           |
| **Agent — structured output** | `await agent.RunAsync<T>(prompt)` → `AgentResponse<T>.Result`                                                                                                                 | Use when `T` is a `LoomToolEnvelope<TData>` verdict (see `services/qyl.mcp/Tools/`)                                               |
| **Session**                   | `agent.CreateSessionAsync()` • `SerializeSessionAsync` / `DeserializeSessionAsync`                                                                                            | Required when the same agent must preserve context across MCP tool calls — gate on `LoomRunState`                                 |
| **Tools — local**             | `AIFunctionFactory.Create(methodInfo, instanceFactory, new AIFunctionFactoryOptions { Name, ... })`                                                                           | `internal/qyl.instrumentation/Instrumentation/Loom/LoomToolFactoryBridge.cs:99-119`                                               |
| **Workflow — build**          | `new WorkflowBuilder(start).AddEdge(a, b).AddFanOutEdge(src, [t1, t2, t3]).WithOutputFrom(last).Build()`                                                                      | `services/qyl.loom/Autofix/Workflow/AutofixWorkflowFactory.cs:44-51`, `services/qyl.loom/Exploration/Workflow/ExplorationWorkflowFactory.cs:23-28` |
| **Workflow — run**            | `InProcessExecution.RunStreamingAsync(workflow, input)` + `run.WatchStreamAsync(ct)`                                                                                          | `AutofixAgentService.cs:66`, `ExplorationOrchestrator.cs:37`                                                                      |
| **Observability**             | `IChatClient` decoration (`.WithQylTelemetry` short form or `.UseQylTelemetry` on `ChatClientBuilder` fluent form) **and** `agent.AsBuilder().UseQylAgentTelemetry().Build()` on `AIAgent`. Wrap **both** or half the OTel attributes vanish. | All executors + `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs:53,100,141`                            |

### Not used in qyl — if you reach for these, justify first

- `builder.AddAIAgent(...)` + `WithInMemorySessionStore` + `WithAITool` (hosted DI) — qyl uses the standalone
  `AsAIAgent(options)` pattern. Switching to hosted requires coordinating agent lifetime with `LoomRunState`.
- `TurnToken` — qyl workflows use custom `Executor<TIn, TOut>` subclasses, never agents-as-executors. `TurnToken` is
  only mandatory when an `AIAgent` is itself a workflow node.
- `McpClient.CreateAsync(...)` (client-side MCP consumption), `HostedMcpServerTool` (OpenAI-Responses hosted MCP),
  `A2ACardResolver` (A2A protocol). No qyl code consumes remote MCP or A2A servers today. If you add one, put the
  client behind an Apex builder under `services/qyl.loom.patterns/`.

## Test project conventions

Under `tests/qyl.collector.tests/`:

- xUnit v3 with Microsoft Testing Platform (MTP). Run via `dotnet test --project tests/qyl.collector.tests` — positional
  args do not work.
- Generate TRX via `--report-xunit-trx`.
- Temp files in tests: use `Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.<ext>")`. Never
  `Path.GetTempFileName()` — it orphans a `.tmp` file on disk.
- `IAsyncDisposable` over `IDisposable` whenever the test touches async resources.
- Every test class has a class-level XML `<summary>`. Every test method has a method-level XML `<summary>`.

## Frontend (`services/qyl.dashboard/`)

- The ESLint config in `services/qyl.dashboard/eslint.config.js` bans direct `@radix-ui/*` imports and the `asChild` /
  `Slot` pattern. Consume primitives through `src/components/ui/` wrappers.
- `no-unused-vars` is configured with `argsIgnorePattern: '^_'`. Prefix intentionally unused args with `_`.
- Playwright e2e tests in `tests/` (or `services/qyl.dashboard/e2e/`) must assert behavior, not render order. No
  arbitrary `waitForTimeout()`. Use `page.waitForResponse` / `page.waitForSelector` with specific selectors.

## When a review comment cites a spec file

If review feedback references `core/specs/api/routes.tsp`, `core/specs/intelligence/*.tsp`, or any other `.tsp` file
with a line number, **open that file and read the surrounding 20 lines before writing code**. The TypeSpec contract is
authoritative over the C# implementation in every case of disagreement.

## When you hit a rate limit

If you stop mid-task because of a rate limit, do not push partial work. Leave the branch in the state it was before you
started. The user will retry.

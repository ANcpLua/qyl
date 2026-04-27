# GitHub Copilot — Cloud Instructions

You are GitHub Copilot operating on qyl from the cloud (PR review, `@copilot` mentions, Copilot Workspace, github.com
code-completion). Local agents (Claude Code, Codex, aider) read `AGENTS.md` (and its `CLAUDE.md` symlink). Rules are
duplicated here so this file stays self-contained — cloud Copilot cannot reliably follow file references.

## When `@copilot apply changes` fires

1. Read every review comment in the thread (Codex, CodeRabbit, Copilot AI review, GitHub Code Quality, humans). Apply
   all of them — one commit if no conflicts and the change is small (<15 min); separate commits otherwise. When
   reviewers disagree, apply the stricter rule.
2. Run `dotnet build qyl.slnx --nologo /clp:ErrorsOnly` — must report 0 errors before push.
3. Commit message: `fix(pr): address review feedback — <one-line summary>`. One logical fix per commit; no squash.
4. Never push partial work. If a rate limit interrupts mid-task, leave the branch where it was.

## Hard rules — violations fail CI

- New `.cs` files: UTF-8 **with BOM**; line 1 = `// Copyright (c) 2025-2026 ancplua`.
- XML doc on every public class / method / property / record (tests included).
- `Async` suffix on every method returning `Task` / `ValueTask` (tests included).
- Test bodies carry explicit `// Arrange` / `// Act` / `// Assert` markers. `IChatClient` doubles use `FakeChatClient`
  from `tests/qyl.collector.tests/Instrumentation/` — never hand-roll `Moq<IChatClient>`.
- Non-public classes are `sealed` unless a subclass lives in the same assembly.
- No suppressions — `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `null!` are all defects. Fix the code.
- Banned: runtime reflection as control flow, `dynamic`, `ExpandoObject`, `.Result`, `.Wait()`.
- Required: file-scoped namespaces, primary constructors, required init properties, switch expressions, pattern
  matching. C# 14 preview features are enabled.
- Time: `TimeProvider` only. Tests use `FakeTimeProvider`. Never `DateTime.{UtcNow,Now}`, `DateTimeOffset.UtcNow`, or
  `Stopwatch.GetTimestamp()` for business logic.
- Environment variables: `UPPER_SNAKE_CASE`.

## DuckDB store — read/write separation

`DuckDbStore` (`services/qyl.collector/Storage/`):

- `GetReadConnectionAsync(ct)` → `ACCESS_MODE=READ_ONLY` lease. **SELECT only.** Any DDL/DML through it is a defect.
- `ExecuteWriteAsync(async (conn, ct) => …, ct)` is the only legal write path. Lambda signature is
  **`Func<DuckDBConnection, CancellationToken, ValueTask>`** — two arguments, always.

When reviewing diffs in `services/qyl.collector/{Storage,Workflows,AgentRuns,Errors}/` or any other store-touching file,
confirm every `INSERT` / `UPDATE` / `DELETE` is inside an `ExecuteWriteAsync` call.

## HTTP endpoints — TypeSpec contract is authoritative

Implementations in `services/qyl.collector/**Endpoints.cs` mirror `core/specs/api/routes.tsp`:

- Spec declares `NotFoundError` → return `TypedResults.NotFound()`. Never `200 OK` with an empty array.
- Pagination `default` / `max` are copied verbatim from the spec.
- Review feedback citing a `routes.tsp` line — open the file and read the surrounding 20 lines of decorators before
  writing C#.

## HTTP client error handling

`services/qyl.loom/CollectorClient.cs` and any other `HttpClient` consumer:

- Before `ReadFromJsonAsync` on a non-success response, check `Content.Headers.ContentType?.MediaType ==
  "application/json"` and `ContentLength > 0`. Otherwise fall back to `StatusCode` as the error signal.
- Wrap `ReadFromJsonAsync` on non-success paths in `try { … } catch (JsonException) { … }`. Never let deserialization
  escape an error path.
- Return a structured failure DTO. Never throw on an expected HTTP error status.

## Codegen — never edit `*.g.cs` / `*.g.ts` / `*.g.sql` / `*.g.tsp`

Edit the source, regenerate, commit both:

| Generated output                                                              | Source                         | Regenerate                                                   |
|-------------------------------------------------------------------------------|--------------------------------|--------------------------------------------------------------|
| `packages/Qyl.Contracts/Generated/**`, `packages/qyl-client/src/generated/**` | `core/specs/**/*.tsp`          | `nuke Generate`                                              |
| `packages/Qyl.OpenTelemetry.SemanticConventions{,.Incubating}/Attributes/**`  | OTel upstream YAML             | `./eng/semconv/run-weaver.sh`                                |
| `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`          | `eng/semconv/model/qyl/*.yaml` | `nuke GenerateSemconv`                                       |
| `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`                                 | `eng/semconv/model/qyl/*.yaml` | `./eng/semconv/run-weaver.sh`                                |
| `services/qyl.collector/Generated/generated/**`                               | `core/specs/api/routes.tsp`    | `nuke Generate` (patched by `core/specs/scripts/patch-emitted-csharp.mjs`) |
| `internal/*.generators/` outputs                                              | Roslyn source generators       | `dotnet build`                                               |

## Semconv — no magic strings

Use the generated constants. Never hand-write `"qyl.*"` or OTel attribute keys:

- Internal services: `QylAttr.<Namespace>.<Name>` (from `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`).
- Public NuGet surface: `QylAttributes.<PascalName>` (from `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`).
- Standard OTel: classes in `packages/Qyl.OpenTelemetry.SemanticConventions{,Incubating}` (e.g.
  `ServiceAttributes.ServiceName`, `HttpAttributes.HttpRequestMethod`).

Missing attribute → add it to `eng/semconv/model/qyl/<namespace>.yaml`, run `./eng/semconv/run-weaver.sh` and
`nuke GenerateSemconv`, then use the generated constant.

## MCP tools (`services/qyl.mcp/`)

- Declare with `[QylSkill]` + `[QylCapability]`. Never register manually in `Program.cs` or DI —
  `internal/qyl.mcp.generators/` emits the registration.
- `[McpServerTool]` `TaskSupport`:
    - `Required` — async pipelines with side effects (`qyl.approve_fix_run`, `qyl.generate_fix`).
    - `Optional` — agent-invoking meta-tools + long-form searches (`qyl.use_qyl`, `qyl.root_cause_analysis`,
      `qyl.summarize_*`, `qyl.search_spans`, `qyl.find_similar_errors`, `qyl.list_errors`, anything >10k rows).
- `LoomToolEnvelope` results use the non-generic companion: `LoomToolEnvelope.Ok(data)` /
  `LoomToolEnvelope.Fail<T>(error)`. Never instantiate the generic form.
- `InvestigationLineage.TryEnter()` before any tool that spawns investigations. Depth 3, spawn 10 — runtime-enforced.

## MAF agent composition (`services/qyl.loom*`, `services/qyl.mcp/Agents/`)

- qyl three-builder pattern: `IXxxChatClientBuilder` → `IXxxAgentsBuilder` → workflow code. Concrete contract:
  `services/qyl.loom.patterns/Agents/IQylLoomPatternsAgentsBuilder.cs`. **One `Build*Agent()` factory per bounded
  agent**, not a fluent chain. Returned `AIAgent` is telemetry-wrapped at the construction site.
- Telemetry middleware (`internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`) — three helpers,
  do not confuse them:
    - `IChatClient` short form: `client.WithQylTelemetry("qyl.genai")` (no `AsBuilder`). Line 53.
    - `ChatClientBuilder` fluent: `new ChatClientBuilder(client).UseQylTelemetry("qyl.genai").UseFunctionInvocation().Build()`.
      Line 100. Used in `services/qyl.loom.patterns/Clients/QylLoomPatternsChatClientBuilder.cs:62`.
    - `AIAgent` fluent: `agent.AsBuilder().UseQylAgentTelemetry().Build()`. Line 141. Canonical at
      `services/qyl.loom/Autofix/Workflow/Executors/RcaExecutor.cs:40`.
- Wrap **both** layers (`IChatClient` AND `AIAgent`). Wrap one only, lose half the OTel attributes.
- Never instantiate `ActivitySource` directly in agent code. `[AgentTraced]` is removed; do not reintroduce.

### Cheat sheet — verified against `Microsoft.Agents.AI` 1.1.0

| Layer                  | Entry point                                                                                       | qyl call-site                                                                                                  |
|------------------------|---------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| Agent — standalone     | `llm.AsAIAgent(options).AsBuilder().UseQylAgentTelemetry().Build()`                               | `services/qyl.loom/Autofix/Workflow/Executors/RcaExecutor.cs:35-40` and siblings                               |
| Agent — non-streaming  | `await agent.RunAsync(msg, cancellationToken: ct)`                                                | universal across Autofix executors and `TriagePipelineService`                                                 |
| Agent — streaming      | `await foreach (var evt in run.WatchStreamAsync(ct))`                                             | `services/qyl.loom/Autofix/AutofixAgentService.cs:66-77`, `services/qyl.loom/Exploration/ExplorationOrchestrator.cs:37` |
| Agent — structured     | `await agent.RunAsync<T>(prompt)` → `AgentResponse<T>.Result`                                     | use when `T` is a `LoomToolEnvelope<TData>` verdict (see `services/qyl.mcp/Tools/`)                            |
| Session                | `agent.CreateSessionAsync()` • `Serialize/Deserialize`                                            | gate on `LoomRunState` for cross-call context                                                                  |
| Tools — local          | `AIFunctionFactory.Create(method, factory, options)`                                              | `internal/qyl.instrumentation/Instrumentation/Loom/LoomToolFactoryBridge.cs:99-119`                            |
| Workflow — build       | `new WorkflowBuilder(start).AddEdge(a, b).WithOutputFrom(last).Build()`                           | `services/qyl.loom/Autofix/Workflow/AutofixWorkflowFactory.cs:44-51`                                           |
| Workflow — run         | `InProcessExecution.RunStreamingAsync(workflow, input)` + `WatchStreamAsync(ct)`                  | `AutofixAgentService.cs:66`, `ExplorationOrchestrator.cs:37`                                                   |

### Justify before reaching for these

- `builder.AddAIAgent(...)` + `WithInMemorySessionStore` + `WithAITool` (hosted DI). qyl uses standalone
  `AsAIAgent(options)`; switching requires coordinating agent lifetime with `LoomRunState`.
- `TurnToken` — qyl workflows use custom `Executor<TIn, TOut>` subclasses, never agents-as-executors. Only mandatory
  when an `AIAgent` IS a workflow node.
- `McpClient.CreateAsync(...)`, `HostedMcpServerTool`, `A2ACardResolver` — no qyl code consumes remote MCP or A2A. New
  consumers go behind a qyl builder under `services/qyl.loom.patterns/`.

## Tests (`tests/qyl.collector.tests/`)

- xUnit v3 + Microsoft Testing Platform (MTP). Run via `dotnet test --project tests/qyl.collector.tests` — positional
  args do not work. Generate TRX with `--report-xunit-trx`.
- Temp files: `Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.<ext>")`. Never `Path.GetTempFileName()` — it orphans
  a `.tmp` file on disk.
- Prefer `IAsyncDisposable` over `IDisposable` for async-resource tests.
- Every test class + every test method has its own XML `<summary>`.

## Frontend (`services/qyl.dashboard/`)

- ESLint bans direct `@radix-ui/*` imports and the `asChild` / `Slot` pattern. Consume primitives through
  `src/components/ui/` wrappers.
- `no-unused-vars` runs with `argsIgnorePattern: '^_'`. Prefix unused args with `_`.
- Playwright e2e (`tests/` or `services/qyl.dashboard/e2e/`) asserts behavior, not render order. No
  `waitForTimeout()` — use `page.waitForResponse` / `page.waitForSelector` with specific selectors.

## Spec citations

Review feedback citing `core/specs/**/*.tsp` with a line number → open the file and read the surrounding 20 lines
before writing code. The TypeSpec contract wins over the C# implementation in every disagreement.

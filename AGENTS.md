# qyl — Agent Instructions

You are an AI coding agent (Claude Code, Codex, aider, Gemini CLI, …) operating on the qyl repository **locally**.
Cloud GitHub Copilot has its own self-contained instructions at `.github/copilot-instructions.md` — do not edit that
file as a side-effect of refactoring this one. `CLAUDE.md` is a symlink to this file (Claude Code convention).

## Hard rules — violations fail CI

These mirror `.editorconfig`, `.github/copilot-instructions.md`, and the ruleset emitted by
`eng/MSBuild/Shared.Claude.*`.

- **Sealed by default:** every non-public class is `sealed` unless a subclass exists in the same assembly.
- **No suppression:** no `#pragma warning disable`, no `[SuppressMessage]`, no `<NoWarn>` additions, no `null!`. Fix
  the code.
- **No runtime reflection as control flow, no `dynamic`, no `ExpandoObject`, no `.Result`, no `.Wait()`.**
- **File-scoped namespaces, primary constructors, required init properties, switch expressions over if/else ladders,
  pattern matching over type-testing.** C# 14 preview features are enabled — use them.
- **`TimeProvider`** only. Never `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, or
  `Stopwatch.GetTimestamp()` for business logic. Tests use `FakeTimeProvider`.
- **Test mocks:** use `FakeChatClient` from `tests/qyl.collector.tests/Instrumentation/` for any `IChatClient` double.
  Never hand-roll `Moq<IChatClient>`.
- **Environment variables:** `UPPER_SNAKE_CASE`.
- **XML documentation** on public API surface that ships in NuGet packages. Skip on internal types and tests — names
  are the docs.

## Build verification

Before claiming a change is complete: `dotnet build qyl.slnx --nologo /clp:ErrorsOnly` must report 0 errors. UI work
must additionally be verified in a browser via Playwright with a screenshot.

## Nuke workflow

Top-level orchestration goes through Nuke (`./eng/build.sh <target>` or, with the global tool, `nuke <target>`).
Sub-targets (Verify*, FrontendInstall, TypeSpec*, etc.) are `.Unlisted()` so `nuke --help` shows only the
user-facing surface. The full list is still available via `nuke --plan` or by spelling the target name explicitly.

| Workflow                          | Target            | What it does                                                                |
|-----------------------------------|-------------------|-----------------------------------------------------------------------------|
| Daily inner loop                  | `nuke Dev`        | `compose up -d` + `Compile` + URL banner                                    |
| Build only                        | `nuke Compile`    | Restore + build the solution (`qyl.slnx`)                                   |
| Test                              | `nuke Test`       | xUnit v3 + MTP across `tests/`                                              |
| Coverage                          | `nuke Coverage`   | Test + Cobertura report                                                     |
| Regenerate codegen                | `nuke Generate`   | TypeSpec emitters + Weaver (semconv)                                        |
| Verify generated artefacts        | `nuke Verify`     | Compiles `*.g.cs`, validates DuckDB DDL, OTel snake_case, frontend types    |
| Reset & rebuild                   | `nuke Clean`      | Wipe `**/bin`, `**/obj`, `Artifacts/`                                       |
| CI gate (backend)                 | `nuke Ci`         | Clean → Coverage                                                            |
| CI gate (full)                    | `nuke Full`       | Ci + Generate + Verify + Frontend{Build,Test,Lint}                          |
| Cut a release                     | `nuke Release`    | Versionize bump + tag + CHANGELOG                                           |
| Compose stack — start             | `nuke DockerUp`   | `docker compose -f eng/compose.yaml up -d --remove-orphans`                 |
| Compose stack — stop              | `nuke DockerDown` | `docker compose down --remove-orphans`                                      |
| Compose stack — logs              | `nuke DockerLogs` | `compose logs -f`; pass `--service <name>` to filter                        |
| Build all images                  | `nuke DockerImageBuild` | parallel build of collector / loom / mcp / dashboard                  |
| Push images                       | `nuke DockerImagePush`  | requires `--registry <prefix>`                                        |
| Frontend dev server               | `nuke FrontendDev`      | Vite dev at <http://localhost:5173> (run after `nuke Dev`)            |
| Frontend production build         | `nuke FrontendBuild`    | tsc + vite build                                                      |

The Compose file lives at `eng/compose.yaml` (not the repo root). All Docker/Compose targets read it via
`IHazSourcePaths.ComposeFile`. The four services `qyl-collector`, `qyl-loom`, `qyl-mcp`, `qyl-dashboard` are all
orchestrated together — production-parity with the Railway deployment.

## DuckDB store — read/write separation is non-negotiable

`DuckDbStore` in `services/qyl.collector/Storage/` exposes two access paths:

- `GetReadConnectionAsync(ct)` returns a lease with `ACCESS_MODE=READ_ONLY`. **SELECT only.** Any
  `INSERT`/`UPDATE`/`DELETE`/`CREATE`/`DROP`/DDL through a read lease is a defect.
- `ExecuteWriteAsync(async (connection, ct) => ..., ct)` is the only legal write path. Lambda signature is
  **`Func<DuckDBConnection, CancellationToken, ValueTask>`** — two arguments. Single-arg lambdas will not compile
  against the real signature and signal code written blind.

When touching `services/qyl.collector/{Storage,Workflows,AgentRuns,Errors}/` or any other store-talking file, grep the
diff for `UPDATE`/`INSERT`/`DELETE` and confirm each one is inside an `ExecuteWriteAsync` call.

## HTTP endpoints — TypeSpec is the source of truth

Endpoint implementations under `services/qyl.collector/**Endpoints.cs` must match `core/specs/api/routes.tsp`:

- Spec declares `NotFoundError` → return `TypedResults.NotFound()`. No 200-with-empty-array.
- Pagination `default`/`max` come verbatim from the spec, not the implementation.
- Read the surrounding 20 lines of decorators in `routes.tsp` before writing the C#.

## HTTP client error handling

In `services/qyl.loom/CollectorClient.cs` and any other `HttpClient`-consuming code:

- Before `ReadFromJsonAsync` on a non-success response, check `Content.Headers.ContentType?.MediaType ==
  "application/json"` and `ContentLength > 0`; otherwise fall back to `StatusCode`.
- Wrap `ReadFromJsonAsync` in `try/catch (JsonException)` on non-success paths.
- Return a structured failure DTO. Never throw on an expected HTTP error status.

## Codegen boundaries — never edit generated files

Files ending in `.g.cs`, `.g.ts`, `.g.sql`, `.g.tsp` are off-limits. Edit the source and regenerate:

| Generated output                                                              | Source                         | Regenerate with                                                            |
|-------------------------------------------------------------------------------|--------------------------------|----------------------------------------------------------------------------|
| `packages/Qyl.Contracts/Generated/**`, `packages/qyl-client/src/generated/**` | `core/specs/**/*.tsp`          | `nuke Generate`                                                            |
| `packages/Qyl.OpenTelemetry.SemanticConventions{,Incubating}/Attributes/**`   | OTel upstream YAML             | `./eng/semconv/run-weaver.sh`                                              |
| `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`          | `eng/semconv/model/qyl/*.yaml` | `nuke GenerateSemconv`                                                     |
| `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`                                 | `eng/semconv/model/qyl/*.yaml` | `./eng/semconv/run-weaver.sh`                                              |
| `services/qyl.collector/Generated/generated/**`                               | `core/specs/api/routes.tsp`    | `nuke Generate` (patched by `core/specs/scripts/patch-emitted-csharp.mjs`) |
| `internal/*.generators/` outputs                                              | Roslyn source generators       | `dotnet build`                                                             |

Commit source change and regenerated output in the same commit.

## Semconv — no magic strings

Never hard-code `"qyl.*"` or standard OTel attribute keys. Use:

- `QylAttr.<Namespace>.<Name>` — internal services (from `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`).
- `QylAttributes.<PascalName>` — public NuGet surface (from
  `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`).
- OTel upstream classes from `packages/Qyl.OpenTelemetry.SemanticConventions{,Incubating}` for standard attributes
  (`ServiceAttributes.ServiceName`, `HttpAttributes.HttpRequestMethod`, etc.).

Missing attribute? Add to `eng/semconv/model/qyl/<namespace>.yaml`, run `./eng/semconv/run-weaver.sh` and
`nuke GenerateSemconv`.

## MCP tools and capabilities

In `services/qyl.mcp/`:

- Never register a tool manually. Declare with `[QylSkill]` + `[QylCapability]`; `internal/qyl.mcp.generators/`
  emits the registration.
- Tool classes are `partial`; every `[McpServerTool]`-decorated method is `partial` (placed directly before the
  return type, after `public/internal` and any `static`/`async`). The upstream
  `ModelContextProtocol.Analyzers.XmlToDescriptionGenerator` (1.2.0+) emits `[Description]` attributes onto a sibling
  partial counterpart from XML `<summary>` / `<param>` docs. Do **not** write manual `[Description("…")]` attributes
  on tool methods or parameters — XML docs are the single source. `MCP002` flags any tool method that violates this.
- `TaskSupport`:
    - `Required` — async pipelines with side effects (`qyl.approve_fix_run`, `qyl.generate_fix`).
    - `Optional` — agent-invoking meta-tools and long-form searches (`qyl.use_qyl`, `qyl.root_cause_analysis`,
      `qyl.summarize_*`, `qyl.search_spans`, `qyl.find_similar_errors`, `qyl.list_errors`, anything >10k rows).
- `LoomToolEnvelope` — use the non-generic companion (`Ok(data)`, `Fail<T>(error)`). Never the generic form.
- `InvestigationLineage.TryEnter()` before any tool that spawns investigations. Depth 3, spawn 10 — runtime-enforced.

## MAF agent composition

For `services/qyl.loom/`, `services/qyl.loom.patterns/`, `services/qyl.mcp/Agents/`.

Authoritative references: `~/.claude/skills/microsoft-agent-framework-qyl/SKILL.md` (qyl overlay) and
`~/.claude/skills/microsoft-agent-framework/SKILL.md` (core MAF). Read those before editing agent/workflow code.

Local invariants:

- Apex three-builder pattern: `IXxxChatClientBuilder` → `IXxxAgentsBuilder` → workflow. One `Build*Agent()` factory
  per bounded agent in `services/qyl.loom.patterns/Agents/IQylLoomPatternsAgentsBuilder.cs`. Returned `AIAgent` is
  telemetry-wrapped at the construction site.
- Decorate at composition root with the helpers in
  `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`. Both `IChatClient` **and** `AIAgent`
  layers must be wrapped — wrap one, lose half the OTel attributes.
- Never instantiate `ActivitySource` directly in agent code. `[AgentTraced]` is removed; do not reintroduce.

### MAF entry-point cheat sheet — verified against `Microsoft.Agents.AI` 1.1.0 and live qyl call-sites

Reach for these before hand-rolling. Every row has a concrete qyl call-site — grep it before writing a new variant.

| Layer                         | Entry point                                                                                                                                                                                                                                   | qyl call-site                                                                                                                                      |
|-------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| **Agent — standalone**        | `llm.AsAIAgent(new ChatClientAgentOptions { Name, Description, ChatOptions = new() { Instructions } }).AsBuilder().UseQylAgentTelemetry().Build()`                                                                                            | `services/qyl.loom/Autofix/Workflow/Executors/RcaExecutor.cs:35-40` and every sibling executor                                                     |
| **Agent — non-streaming**     | `await agent.RunAsync(userMessage, cancellationToken: ct)`                                                                                                                                                                                    | `RcaExecutor.cs:42` — the universal shape across Autofix executors and `TriagePipelineService`                                                     |
| **Agent — streaming**         | `await foreach (var evt in streamingRun.WatchStreamAsync(ct)) { … }`                                                                                                                                                                          | `services/qyl.loom/Autofix/AutofixAgentService.cs:66-77`, `services/qyl.loom/Exploration/ExplorationOrchestrator.cs:37`                            |
| **Agent — structured output** | `await agent.RunAsync<T>(prompt)` → `AgentResponse<T>.Result`                                                                                                                                                                                 | Use when `T` is a `LoomToolEnvelope<TData>` verdict (see `services/qyl.mcp/Tools/`)                                                                |
| **Session**                   | `agent.CreateSessionAsync()` • `SerializeSessionAsync` / `DeserializeSessionAsync`                                                                                                                                                            | Required when the same agent must preserve context across MCP tool calls — gate on `LoomRunState`                                                  |
| **Tools — local**             | `AIFunctionFactory.Create(methodInfo, instanceFactory, new AIFunctionFactoryOptions { Name, ... })`                                                                                                                                           | `internal/qyl.instrumentation/Instrumentation/Loom/LoomToolFactoryBridge.cs:99-119`                                                                |
| **Workflow — build**          | `new WorkflowBuilder(start).AddEdge(a, b).AddFanOutEdge(src, [t1, t2, t3]).WithOutputFrom(last).Build()`                                                                                                                                      | `services/qyl.loom/Autofix/Workflow/AutofixWorkflowFactory.cs:44-51`, `services/qyl.loom/Exploration/Workflow/ExplorationWorkflowFactory.cs:23-28` |
| **Workflow — run**            | `InProcessExecution.RunStreamingAsync(workflow, input)` + `run.WatchStreamAsync(ct)`                                                                                                                                                          | `AutofixAgentService.cs:66`, `ExplorationOrchestrator.cs:37`                                                                                       |
| **Observability**             | `IChatClient` decoration (`.WithQylTelemetry` short form or `.UseQylTelemetry` on `ChatClientBuilder` fluent form) **and** `agent.AsBuilder().UseQylAgentTelemetry().Build()` on `AIAgent`. Wrap **both** or half the OTel attributes vanish. | All executors + `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs:53,100,141`                                            |

## Test project conventions

Under `tests/qyl.collector.tests/`:

- xUnit v3 with Microsoft Testing Platform. Run via `dotnet test --project tests/qyl.collector.tests` — positional
  args don't work.
- TRX via `--report-xunit-trx`.
- Temp files: `Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.<ext>")`. Never `Path.GetTempFileName()` — orphans
  a `.tmp` file.
- `IAsyncDisposable` over `IDisposable` for async resources.

## Frontend (`services/qyl.dashboard/`)

- ESLint config bans direct `@radix-ui/*` imports and the `asChild`/`Slot` pattern. Consume primitives through
  `src/components/ui/` wrappers.
- `argsIgnorePattern: '^_'` — prefix unused args with `_`.
- Playwright e2e tests assert behavior, not render order. No `waitForTimeout()`. Use `page.waitForResponse` /
  `page.waitForSelector` with specific selectors.

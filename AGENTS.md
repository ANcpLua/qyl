# qyl — Agent Notes

You're an AI coding agent (Claude Code, Codex, aider, Gemini CLI, …) working on the qyl repo. `CLAUDE.md` is a symlink
to this file.

These are the conventions the codebase already follows. The analyzer ruleset shipped via `ANcpLua.NET.Sdk` enforces most
of them — when you write code that fits the patterns below, the build stays green by default.

## `ANcpLua.Agents` consumption

`ANcpLua.Agents` is the canonical agent / workflow / hosting / testing framework qyl builds on. Repo:
<https://github.com/ANcpLua/ANcpLua.Agents>. Local checkout: `~/RiderProjects/ANcpLua.Agents/`.

**Published packages on nuget.org** (1.4.x stable base; hosting + AGUI ride preview / alpha trains):

- `ANcpLua.Agents` — provider-agnostic agent + chat-client core
- `ANcpLua.Agents.Workflows` — workflow runtime + visualization
- `ANcpLua.Agents.Testing` and `ANcpLua.Agents.Testing.Workflows`
- `ANcpLua.Agents.Foundry`
- `ANcpLua.Agents.Hosting.{Anthropic,Azure,BitNet,DevUI,Foundry,OpenAI}` — provider/host pins

**Consumed by qyl today** (verified against `Directory.Packages.props` + csproj rows):

- `internal/qyl.instrumentation` → `ANcpLua.Agents`
- `services/qyl.mcp` → `ANcpLua.Agents`, `ANcpLua.Agents.Hosting.OpenAI`
- `services/qyl.loom.patterns` → `ANcpLua.Agents.Testing`
- `tests/qyl.collector.tests` → `ANcpLua.Agents`, `ANcpLua.Agents.Testing`

**Telemetry boundary — composition, not consolidation.** qyl owns the local-instrumentation facades on `IChatClient`
and `IMcpServerBuilder`; `ANcpLua.Agents.*` owns workflows / hosting bridges / testing fixtures. The two cooperate at
every agent composition root and the facades below do not overlap with anything `ANcpLua.Agents` ships:

| qyl-local extension                            | layer                   | covered by `ANcpLua.Agents`? |
|------------------------------------------------|-------------------------|------------------------------|
| `WithQylTelemetry(IChatClient)`                | `IChatClient` decorator | no                           |
| `UseQylTelemetry(ChatClientBuilder)`           | fluent builder form     | no                           |
| `UseQylAgentTelemetry(AIAgentBuilder)`         | `AIAgent` builder wrap  | no                           |
| `UseQylMcpInstrumentation(IMcpServerBuilder)`  | MCP transport spans     | no                           |

Future qyl-side helpers (e.g. a `QylClientModelExtensions` wrapper around `System.ClientModel`) belong as siblings
inside `internal/qyl.instrumentation/`, not in the upstream package.

## Style the codebase has settled on

Anchored in `.editorconfig` + the `ANcpLua.NET.Sdk` analyzers. Most of these are auto-fixable; the rest get flagged.

- **Sealed by default.** Non-public classes are `sealed` unless a subclass exists in the same assembly.
- **Fix at source.** No `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>` additions, or `null!`. If a
  diagnostic fires, the code wants a real change.
- **No runtime reflection as control flow.** `dynamic` / `ExpandoObject` / `.Result` / `.Wait()` don't appear in this
  repo. `await` everything async.
- **C# 14 surface.** File-scoped namespaces, primary constructors, required init properties, switch expressions, pattern
  matching. `<LangVersion>preview</LangVersion>` is enabled — preview features are fair game.
- **`TimeProvider` for time.** Business logic uses `TimeProvider.System`. Tests use `FakeTimeProvider`.
  `DateTime.{UtcNow,Now}`, `DateTimeOffset.UtcNow`, `Stopwatch.GetTimestamp()` are reserved for low-level perf code, not
  domain logic.
- **`FakeChatClient` for `IChatClient` doubles.** Lives in `tests/qyl.collector.tests/Instrumentation/`. Hand-rolled
  `Mock<IChatClient>` rots fast.
- **Env vars are `UPPER_SNAKE_CASE`.**
- **XML docs on public NuGet surface.** Internal types and tests skip them — names carry the docs.

## Build verification

`dotnet build qyl.slnx --nologo /clp:ErrorsOnly` should report 0 errors before claiming a change is done. UI work needs
a Playwright screenshot of the actual feature in a browser — type-checks don't verify rendering.

## Cron automation ledger

**does:** `eng/automation/CHANGELOG.md` tracks cron-runner workflows and the newest 10 material automation changes. Runners
read it before starting a matching automation run and after the final changelog write.
**does_not_do:** Product release notes, ordinary development narration, branch/PR authority, or a replacement for live git
and GitHub evidence.

## Nuke workflow

Top-level orchestration goes through Nuke (`./eng/build.sh <target>` or, with the global tool, `nuke <target>`).
Sub-targets are `.Unlisted()` so `nuke --help` shows only the user-facing surface; the full list is still in
`nuke --plan`.

| Workflow                   | Target                  | What it does                                                             |
|----------------------------|-------------------------|--------------------------------------------------------------------------|
| Daily inner loop           | `nuke Dev`              | `compose up -d` + `Compile` + URL banner                                 |
| Build only                 | `nuke Compile`          | Restore + build the solution (`qyl.slnx`)                                |
| Test                       | `nuke Test`             | xUnit v3 + MTP across `tests/`                                           |
| Coverage                   | `nuke Coverage`         | Test + Cobertura report                                                  |
| Regenerate codegen         | `nuke Generate`         | TypeSpec emitters + Weaver (semconv)                                     |
| Verify generated artefacts | `nuke Verify`           | Compiles `*.g.cs`, validates DuckDB DDL, OTel snake_case, frontend types |
| Reset & rebuild            | `nuke Clean`            | Wipe `**/bin`, `**/obj`, `Artifacts/`                                    |
| CI gate (backend)          | `nuke Ci`               | Clean → Coverage                                                         |
| CI gate (full)             | `nuke Full`             | Ci + Generate + Verify + Frontend{Build,Test,Lint}                       |
| Cut a release              | `nuke Release`          | Versionize bump + tag + CHANGELOG                                        |
| Compose stack — start      | `nuke DockerUp`         | `docker compose -f eng/compose.yaml up -d --remove-orphans`              |
| Compose stack — stop       | `nuke DockerDown`       | `docker compose down --remove-orphans`                                   |
| Compose stack — logs       | `nuke DockerLogs`       | `compose logs -f`; pass `--service <name>` to filter                     |
| Build all images           | `nuke DockerImageBuild` | parallel build of collector / loom / mcp / dashboard                     |
| Push images                | `nuke DockerImagePush`  | requires `--registry <prefix>`                                           |
| Frontend dev server        | `nuke FrontendDev`      | Vite dev at <http://localhost:5173> (run after `nuke Dev`)               |
| Frontend production build  | `nuke FrontendBuild`    | tsc + vite build                                                         |

The compose file lives at `eng/compose.yaml` (not the repo root). All Docker/Compose targets read it via
`IHazSourcePaths.ComposeFile`. Four services — `qyl-collector`, `qyl-loom`, `qyl-mcp`, `qyl-dashboard` — orchestrated
together for production-parity with Railway.

## DuckDB store — read/write separation

`DuckDbStore` in `services/qyl.collector/Storage/` exposes two access paths and the difference is structural:

- `GetReadConnectionAsync(ct)` returns a lease with `ACCESS_MODE=READ_ONLY`. SELECT only — any write through this lease
  is a defect the lease was designed to prevent.
- `ExecuteWriteAsync(async (connection, ct) => …, ct)` is the write path. Lambda signature is *
  *`Func<DuckDBConnection, CancellationToken, ValueTask>`** — two arguments. Single-arg lambdas don't compile against
  the real signature; if your editor lets you write one, the file you're editing isn't the live signature.

When touching `services/qyl.collector/{Storage,Workflows,AgentRuns,Errors}/` or another store-talking file, eyeball the
diff for `UPDATE`/`INSERT`/`DELETE` — each one belongs inside an `ExecuteWriteAsync` call.

## HTTP endpoints — TypeSpec drives the shape

Endpoint implementations under `services/qyl.collector/**Endpoints.cs` track `core/specs/api/routes.tsp`:

- Spec declares `NotFoundError` → return `TypedResults.NotFound()`. No 200-with-empty-array.
- Pagination `default`/`max` come from the spec, not the implementation.
- Decorators surrounding a route in `routes.tsp` carry the contract — worth a glance before writing the C#.

## HTTP client error handling

In `services/qyl.loom/CollectorClient.cs` and any other `HttpClient`-consuming code:

- Default: `response.EnsureSuccessStatusCode()` → `ReadFromJsonAsync(CollectorClientJsonContext.Default.<Type>, ct)`.
  Let unexpected HTTP failures throw; the AOT-friendly `JsonTypeInfo` context is mandatory — never the reflection
  overload.
- For endpoints with a documented failure body (e.g. `CreatePullRequestAsync` at `CollectorClient.cs:148-178`),
  branch on `IsSuccessStatusCode` and read either the success or failure DTO. Reserve this shape for contracts that
  actually define a structured error payload.

## Codegen boundaries

Files ending in `.g.cs`, `.g.ts`, `.g.sql`, `.g.tsp` are downstream artefacts — edit the source and regenerate.

| Generated output                                                              | Source                         | Regenerate with                                                            |
|-------------------------------------------------------------------------------|--------------------------------|----------------------------------------------------------------------------|
| `packages/Qyl.Contracts/Generated/**`, `packages/qyl-client/src/generated/**` | `core/specs/**/*.tsp`          | `nuke Generate`                                                            |
| `packages/Qyl.OpenTelemetry.SemanticConventions{,Incubating}/Attributes/**`   | OTel upstream YAML             | `nuke Generate`                                                            |
| `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`          | `eng/semconv/model/qyl/*.yaml` | `nuke OtelConventions`                                                     |
| `services/qyl.dashboard/src/lib/semconv.ts`, `services/qyl.collector/Storage/promoted-columns.g.sql`, `core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json`, `core/specs/generated/otel-keys.gen.tsp` | `.tools/semconv-upstream/model` | `./eng/semconv/run-weaver.sh`                                              |
| `packages/Qyl.Telemetry/Conventions/Qyl.g.cs`, `packages/qyl-client/src/conventions.ts`, `docs/attributes/qyl.attrs.md` | `eng/semconv/model/qyl/*.yaml` | `./eng/semconv/run-weaver.sh`                                              |
| `services/qyl.collector/Generated/generated/**`                               | `core/specs/api/routes.tsp`    | `nuke Generate` (patched by `core/specs/scripts/patch-emitted-csharp.mjs`) |
| `internal/*.generators/` outputs                                              | Roslyn source generators       | `dotnet build`                                                             |

Source change + regenerated output ship in the same commit.

The semconv toolchain has two steps:

- `./eng/semconv/bootstrap-weaver.sh` or `./eng/semconv/bootstrap-weaver.ps1` installs pinned Weaver `v0.23.0` into `.tools/weaver/` and checks that `.tools/semconv-upstream` is present.
- `./eng/semconv/run-weaver.sh` uses the pinned upstream model plus `eng/semconv/model/qyl` to regenerate the repo-local TypeScript, SQL, JSON, C#, Markdown, and TypeSpec outputs listed above.

### OTel signal models ship as an external TypeSpec library

The TypeSpec layer under `core/specs/otel/` now imports the OTel-shaped signal
models (`enums`, `resource`, `span`, `logs`, `metrics`, `profiles`) from
[`@o-ancpplua/otel-conventions-api`](https://github.com/O-ANcppLua/ANcpLua.OtelConventions.Api)
via the barrel `core/specs/otel/otel-conventions.tsp`. The package is
distributed on **GitHub Packages** under the `@o-ancpplua` scope; the
`core/specs/.npmrc` already declares the registry, and authentication uses
`GITHUB_TOKEN`. Only `core/specs/otel/storage.tsp` stays qyl-local because the
DuckDB storage models in the `Qyl.Storage` namespace are not part of the
published OTel surface.

## Semconv — typed attributes, not strings

`"qyl.*"` and standard OTel keys live as constants:

- `QylAttr.<Namespace>.<Name>` — internal services (`packages/Qyl.Telemetry/Conventions/Qyl.g.cs`).
- `QylAttributes.<PascalName>` — public NuGet surface (
  `packages/Qyl.SemanticConventions/Attributes/Qyl/QylAttributes.g.cs`).
- `Qyl.OpenTelemetry.SemanticConventions{,Incubating}` — standard OTel attributes (`ServiceAttributes.ServiceName`,
  `HttpAttributes.HttpRequestMethod`, …).

Missing attribute? Add to `eng/semconv/model/qyl/<namespace>.yaml`, run `./eng/semconv/run-weaver.sh` and
`nuke OtelConventions`.

## MCP tools and capabilities

In `services/qyl.mcp/`:

- Tools register via `[QylSkill]` + `[QylCapability]` — `internal/qyl.mcp.generators/` emits the registration. Manual
  registration drifts.
- Tool classes are `partial`. Every `[McpServerTool]`-decorated method is `partial` (placed directly before the return
  type, after `public/internal` and any `static`/`async`). The upstream
  `ModelContextProtocol.Analyzers.XmlToDescriptionGenerator` (1.2.0+) emits `[Description]` attributes onto a sibling
  partial counterpart from XML `<summary>` / `<param>` docs — the XML docs are the single source. `MCP002` flags manual
  `[Description("…")]` on tool methods or parameters.
- `TaskSupport`:
    - `Required` — async pipelines with side effects (`qyl.approve_fix_run`, `qyl.generate_fix`).
    - `Optional` — agent-invoking meta-tools and long-form searches (`qyl.use_qyl`, `qyl.root_cause_analysis`,
      `qyl.summarize_*`, `qyl.search_spans`, `qyl.find_similar_errors`, `qyl.list_errors`, anything >10k rows).
- `LoomToolEnvelope` — non-generic companion (`Ok(data)`, `Fail<T>(error)`); the generic form is reserved for
  tool-output schema generation.
- `InvestigationLineage.TryEnter()` before any tool that spawns investigations. Depth 3, spawn 10 — runtime-enforced.
- **MCP-server telemetry is one facade.** Every `IMcpServerBuilder` composition root (
  `services/qyl.mcp/Hosting/QylMcpServerRegistration.cs`, `services/qyl.loom/Program.cs`) calls
  `.UseQylMcpInstrumentation(activitySource, options => options.Transport = "http"|"stdio")` immediately after the
  transport. Since PR #172, `Transport` and `mcp.session.id` ride on every MCP span — qyl's *Transport Distribution* and
  conversation-grouping widgets see them. The facade in
  `internal/qyl.instrumentation/Instrumentation/Mcp/QylMcpServerInstrumentation.cs` emits the JSON-RPC envelope spans,
  the `gen_ai.execute_tool` / `mcp.resource.read` / `mcp.prompt.get` request spans, and maps both thrown exceptions and
  silent `CallToolResult.IsError = true` returns onto the span status. Business filters (admin denial, scope injection,
  response shaping) chain *after* the facade. `RecordInputs` / `RecordOutputs` are off by default — opt in with an
  explicit reason and a known-non-PII surface.

## MAF agent composition

For `services/qyl.loom/`, `services/qyl.loom.patterns/`, `services/qyl.mcp/Agents/`.

Local invariants:

- qyl three-builder pattern: `IXxxChatClientBuilder` → `IXxxAgentsBuilder` → workflow. One `Build*Agent()` factory per
  bounded agent in `services/qyl.loom.patterns/Agents/IQylLoomPatternsAgentsBuilder.cs`. Returned `AIAgent` is
  telemetry-wrapped at the construction site.
- Composition-root decoration uses helpers from
  `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs`. Both `IChatClient` and `AIAgent` layers
  want the wrap — wrap one and roughly half the OTel attributes vanish.
- `[AgentTraced]` is gone; new `ActivitySource` instances in agent code aren't the path forward.

### MAF entry-point cheat sheet — verified against `Microsoft.Agents.AI` 1.3.0 + live qyl call-sites

Reach for these before hand-rolling. Each row points at a concrete qyl call-site.

| Layer                         | Entry point                                                                                                                                                                                                                                   | qyl call-site                                                                                                                                      |
|-------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| **Agent — standalone**        | `llm.AsAIAgent(new ChatClientAgentOptions { Name, Description, ChatOptions = new() { Instructions } }).AsBuilder().UseQylAgentTelemetry().Build()`                                                                                            | `services/qyl.loom/Agents/QylLoomAgentsBuilder.cs:82-85` (every `Build*Agent()` factory)                                                           |
| **Agent — non-streaming**     | `await agent.RunAsync(userMessage, cancellationToken: ct)`                                                                                                                                                                                    | `services/qyl.loom/Autofix/Workflow/Executors/HypothesisExecutor.cs:38-40` — universal shape across Autofix executors                              |
| **Agent — streaming**         | `await foreach (var evt in streamingRun.WatchStreamAsync(ct)) { … }`                                                                                                                                                                          | `services/qyl.loom/Autofix/LoomAutofixRunner.cs:188`, `services/qyl.loom/Exploration/ExplorationOrchestrator.cs:37`                                |
| **Agent — structured output** | `await agent.RunAsync<T>(prompt)` → `AgentResponse<T>.Result`                                                                                                                                                                                 | When `T` is a `LoomToolEnvelope<TData>` verdict (see `services/qyl.mcp/Tools/`)                                                                    |
| **Session**                   | `agent.CreateSessionAsync()` • `SerializeSessionAsync` / `DeserializeSessionAsync`                                                                                                                                                            | When the same agent must preserve context across MCP tool calls — gate on `LoomRunState`                                                           |
| **Tools — local**             | `AIFunctionFactory.Create(methodInfo, new AIFunctionFactoryOptions { Name = "qyl.<area>.<verb>" })`                                                                                                                                           | `services/qyl.loom/Autofix/Workflow/AutofixContextToolFactories.cs:9-15`                                                                           |
| **Workflow — build**          | `new WorkflowBuilder(start).AddEdge(a, b).AddFanOutEdge(src, [t1, t2, t3]).WithOutputFrom(last).Build()`                                                                                                                                      | `services/qyl.loom/Autofix/Workflow/AutofixWorkflowFactory.cs:44-51`, `services/qyl.loom/Exploration/Workflow/ExplorationWorkflowFactory.cs:23-28` |
| **Workflow — run**            | `InProcessExecution.RunStreamingAsync(workflow, input)` + `run.WatchStreamAsync(ct)`                                                                                                                                                          | `services/qyl.loom/Autofix/LoomAutofixRunner.cs:182-188`, `services/qyl.loom/Exploration/ExplorationOrchestrator.cs:37`                            |
| **Observability**             | `IChatClient` decoration (`.WithQylTelemetry` short form or `.UseQylTelemetry` on `ChatClientBuilder` fluent form) **and** `agent.AsBuilder().UseQylAgentTelemetry().Build()` on `AIAgent`. Wrap both layers — wrapping one halves the spans. | All executors + `internal/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs:53,100,141`                                            |

## ANcpLua.Agents — consume, don't duplicate

Local checkout: `/Users/ancplua/framework/ANcpLua.Agents/` (repo `https://github.com/ANcpLua/ANcpLua.Agents`). The
package family is published on nuget.org and is the canonical home of the `Qyl*` consumer-pattern facades. When
working in `services/qyl.loom*`, `services/qyl.mcp`, or new `internal/qyl.instrumentation` code, reach for the
matching `<PackageReference>` instead of hand-rolling inside qyl.

Audit triage when migrating an existing extension: `keep-in-qyl` (qyl-domain-specific), `move-to-ANcpLua.Agents`
(provider-agnostic, would benefit other consumers), `delete` (duplicate of an existing facade).

### Package → public extension surface

Names verified against `~/RiderProjects/ANcpLua.Agents/src/**/Qyl*Extensions.cs`. Inspect the source for full method
signatures — qyl/AGENTS.md only tracks the package mapping, not the per-method API (which would rot fast).

| Package                              | Extension classes (in `src/<package>/**/Facades/` and siblings)                                                                                                                                                                                                                                                       |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ANcpLua.Agents`                     | provider-agnostic core — verify against `src/ANcpLua.Agents/**`                                                                                                                                                                                                                                                       |
| `ANcpLua.Agents.Workflows`           | `QylWorkflowBuilderExtensions`, `QylWorkflowContextExtensions`, `QylWorkflowExecutionExtensions`, `QylWorkflowFactoryExtensions`, `QylWorkflowVisualizationExtensions`, `QylAgentWorkflowExtensions`, `QylExecutorFactoryExtensions`, `QylCheckpointStoreExtensions`                                                  |
| `ANcpLua.Agents.Foundry`             | `QylFoundryAgentExtensions`, `QylFoundryDeclarativeWorkflowExtensions`, `QylFoundryEvalExtensions`, `QylFoundryMemoryExtensions`                                                                                                                                                                                       |
| `ANcpLua.Agents.Hosting.OpenAI`      | `QylOpenAIClientExtensions`, `QylOpenAIHostingExtensions`                                                                                                                                                                                                                                                              |
| `ANcpLua.Agents.Hosting.Anthropic`   | `QylAnthropicAgentExtensions`                                                                                                                                                                                                                                                                                          |
| `ANcpLua.Agents.Hosting.Azure`       | `QylAzureFunctionsHostingExtensions`                                                                                                                                                                                                                                                                                   |
| `ANcpLua.Agents.Hosting.Foundry`     | `QylFoundryHostingExtensions`                                                                                                                                                                                                                                                                                          |
| `ANcpLua.Agents.Hosting.DevUI`       | `QylDevUIExtensions`                                                                                                                                                                                                                                                                                                   |
| `ANcpLua.Agents.Testing`             | `IChatClient` doubles + harness — replaces hand-rolled `Mock<IChatClient>` (already consumed by qyl tests)                                                                                                                                                                                                            |
| `ANcpLua.Agents.Testing.Workflows`   | workflow-fixture harness                                                                                                                                                                                                                                                                                               |

If you reach for a facade name from an old sample (`QylA2A*`, `QylAGUI*`, `QylCosmosNoSql*`, `QylCopilotStudio*`,
`QylDeclarativeAgent*`, `QylDeclarativeMcp*`, `QylGitHubCopilot*`, `QylListenPort*`, `QylMcp*`, `QylPurview*`,
`QylTelemetry*`) and it's missing in the table above, that facade isn't published on `ANcpLua.Agents.*`. Check the
upstream source under `~/RiderProjects/ANcpLua.Agents/src/` for an equivalent under the current package layout
before re-implementing inside qyl.

## Test project conventions

Under `tests/qyl.collector.tests/`:

- xUnit v3 with Microsoft Testing Platform. Run via `dotnet test --project tests/qyl.collector.tests` — positional args
  don't work.
- TRX via `--report-xunit-trx`.
- Temp files: `Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.<ext>")`. `Path.GetTempFileName()` orphans a `.tmp`
  file every call.
- `IAsyncDisposable` over `IDisposable` for async resources.

## Frontend (`services/qyl.dashboard/`)

- ESLint config bans direct `@radix-ui/*` imports and the `asChild` / `Slot` pattern. Primitives go through
  `src/components/ui/` wrappers.
- `argsIgnorePattern: '^_'` — prefix unused args with `_`.
- Playwright e2e tests assert behavior, not render order. Skip `waitForTimeout()`; use `page.waitForResponse` /
  `page.waitForSelector` with specific selectors.

## In-flight planning

| Issue                                                                       | Repo                  | Topic                                                                                           |
|-----------------------------------------------------------------------------|-----------------------|-------------------------------------------------------------------------------------------------|
| [`ANcpLua.OtelConventions.Api`](https://github.com/O-ANcppLua/ANcpLua.OtelConventions.Api) | external | TypeSpec library (`@o-ancpplua/otel-conventions-api` on GitHub Packages). Barrel swap staged on `chore/swap-inlined-otel-for-otel-conventions-api` (6 files deleted, ~1,746 LOC). Branch held until the first GitHub release lands. |
| [#172](https://github.com/O-ANcppLua/qyl/pull/172)                          | qyl                   | merged — `mcp.transport` + `mcp.session.id` qyl-shape tagging                                   |
| [#173](https://github.com/O-ANcppLua/qyl/issues/173)                        | qyl                   | closed — PRD 1 (Observability roll-up: cost / conversations / inventory) on top of OTel + #172  |

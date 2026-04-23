# AGENTS.md

Instructions for AI coding agents working in the qyl codebase.

## Priority of conventions

When MAF (Microsoft Agent Framework) conventions conflict with qyl conventions, **MAF wins**. qyl consumes MAF and must
stay aligned with upstream .NET patterns — read the `microsoft-agent-framework` skill plus the
`microsoft-agent-framework-qyl` overlay (both global skills), and go to upstream
at <https://github.com/microsoft/agent-framework/tree/main/dotnet> for anything the skill doesn't cover. Never paste a
qyl-specific shortcut over a MAF rule.

## Reference implementation — Apex

`~/Apex.AgenticEntityExtractor/Apex.AgenticEntityExtractor/` is the canonical shape for how qyl consumer code looks.
Mirror it. Key files: `Agents/ExtractorAgentsBuilder.cs`, `Clients/ExtractorChatClientBuilder.cs`,
`Workflows/ExtractorWorkflowBuilder.cs`, `Program.cs`. Every qyl service that constructs MAF agents must follow the
three-builder-interface + fluent-middleware pattern documented in the `microsoft-agent-framework-qyl` skill overlay.

## Collapse note (2026-04)

The attribute+generator stack for **agent/chat-level telemetry** was collapsed to MAF's fluent middleware pipeline:
`[AgentTraced]` + `AgentCallSiteAnalyzer` + `AgentInterceptorEmitter` + `AgentInterceptors.g.cs` + `GenAiCallSiteAnalyzer`
are gone. Replacement is `.AsBuilder().UseOpenTelemetry("qyl.agent").Build()` at the composition root. Attribute-based
instrumentation remains **only** for arbitrary non-chat methods/metrics (`[Traced]`/`[Meter]`/`[Counter]`/etc.) where
no builder surface exists.

## Execution style

- Source code is truth. Read `.cs` files before docs, plans, or summaries from previous sessions. Previous agents' plans
  may be wrong — the code is always current.
- Token abundance: 1M window, rarely past 250k. Don't compress, don't skip reads.
- Never ask for confirmation — EXCEPT: delete, stash, revert. For those: commit+push first so the remote is the safety
  net, then ask.
- Bulk operations (perl/sed over many files): build the specific project immediately after the script to catch problems
  before continuing. If the script broke files, fix them before doing more work — don't stack more changes on top.
- Checkpoints, not partial completions. Propose plan, execute in segments.
- Map before changing. Map current state, proposed state, highlight delta.

## Build, Test, and Lint Commands

```bash
# From repo root — prefer per-project builds over full solution
dotnet restore qyl.slnx
dotnet build src/<project>/<project>.csproj        # Default verification
dotnet test --project tests/<project>.tests        # MTP requires --project
dotnet format src/<project>

# Single test (MTP syntax — Microsoft Testing Platform via xUnit v3)
dotnet test --project tests/qyl.collector.tests \
    --filter-query "/*/*/DuckDbStoreRegressionTests/*" \
    --ignore-exit-code 8

# Generators + full pipeline (release / CI parity)
nuke Generate                                       # Regenerate TypeSpec + Roslyn outputs
nuke Build

# One-time setup on a fresh clone (if not cloned with --recurse-submodules)
git submodule update --init .tools/semconv-upstream
```

Rules:

- Full-solution `dotnet build qyl.slnx` is the exception, not the default. Other projects have WIP test failures that
  aren't your problem.
- `nuke Generate` is idempotent — run it any time. It regenerates `.g.cs`, `openapi.yaml`, and DuckDB DDL.
- Never hand-edit `*.g.cs`, `*.g.tsp`, `*.g.sql`, `*.g.ts`, or `core/openapi/openapi.yaml`. Fix the generator input (
  TypeSpec model, attribute, routing table) instead.
- Use `--tl:off` on `dotnet build` to avoid terminal flicker in agent sessions.
- Capture long build/test output to a temp file first (`dotnet build --tl:off 2>&1 | tee /tmp/build.log`), then analyze.
  This avoids re-running expensive commands when the initial analysis misses something.

## Project Structure

```
qyl/
├── src/
│   ├── qyl.collector/                    # OTLP ingest (gRPC 4317 / HTTP 4318), REST API :5100, DuckDB 1.5.0 storage
│   ├── qyl.contracts/                    # BCL-only types (no MAF, no OTel SDK, no provider code)
│   ├── qyl.mcp/                          # 77 MCP tools — stdio + Streamable HTTP
│   ├── qyl.mcp.generators/               # Interim Roslyn generator — skill-aware tool manifest
│   ├── qyl.loom/                         # Standalone agent Exe — triage, RCA, fix, code review (HTTP-only to collector)
│   ├── qyl.instrumentation/              # Runtime OTel SDK surface (GenAI semconv 1.40)
│   ├── qyl.instrumentation.generators/   # Roslyn generator — instrumentation boilerplate
│   ├── qyl.collector.storage.generators/ # Roslyn generator — DuckDB insert/map code
│   ├── qyl.dashboard/                    # React 19 + Vite 7 + Tailwind 4 + Base UI 1.3.0 + lucide-react
│   ├── Qyl.Agents/                       # (merged from netagents) shared agent plumbing
│   ├── Qyl.Agents.Abstractions/          # (merged) attribute surface: [Tool] [Prompt] [Resource]
│   └── Qyl.Agents.Generator/             # (merged) full MCP server generator (dispatch, schemas, OTel, JSON contexts)
├── tests/                                # Per-project test assemblies (xUnit v3 + MTP)
├── samples/                              # Currently empty — see §Sample Structure
├── docs/
│   ├── ARCHITECTURE.md                   # C4 Context / Container / Component
│   ├── THREAT_MODEL.md                   # 20 attacker stories with P0–P3 priority
│   ├── OPEN_WORK.md                      # Consolidated open work items
│   └── planned/                          # Execute-ready prompts for deferred features
├── eng/                                  # NUKE 10.1.0 build, MSBuild props, semconv generator
└── core/                                 # TypeSpec schemas -> openapi.yaml
```

The authoritative list of real projects and forbidden names lives in `QYL_GROUND_TRUTH` emitted by the SessionStart
hook. Read it at the start of every session and do not enumerate ghost projects in this file.

### Core types

- `AIAgent` (MAF): abstract base for every agent. Hosted:
  `builder.AddAIAgent(key, instructions, ServiceLifetime.Scoped).WithInMemorySessionStore()`. Standalone:
  `chatClient.AsAIAgent(name, instructions)`. Both patterns are valid — qyl.collector uses hosted.
- `AgentSession` (MAF): multi-turn conversation state. Obtain via `agent.CreateSessionAsync(ct)`. Each bounded agent
  owns its own session; do not pretend a shared conversation id unifies them.
- `ChatClientAgent` (MAF): `AIAgent` implementation over an `IChatClient`.
- `IChatClient` (`Microsoft.Extensions.AI`): provider-agnostic chat surface. Build behind `IXxxChatClientBuilder`
  (Apex pattern — enum-switch over `ChatProvider`, returns raw `IChatClient`). Decorate at the composition root via
  `.AsBuilder().UseQylTelemetry("qyl.genai").Build()` (the qyl wrapper over `UseFunctionInvocation` +
  `UseOpenTelemetry` + `ToolDecoratingChatClient`). Pair with `.AsBuilder().UseOpenTelemetry("qyl.agent").Build()` on
  the `AIAgent` layer.
- `AITool` / `AIFunction` / `AIFunctionFactory.Create(delegate, name, description)`: tool registration surface.
- `LoomToolEnvelope<T>`: qyl's tool-result wrapper. Construct via the **non-generic companion** —
  `LoomToolEnvelope.Ok(data)` / `LoomToolEnvelope.Fail<T>(error)`. NEVER `LoomToolEnvelope<T>.Ok/Fail`.
- `[QylSkill(QylSkillKind.X)]`: sole declaration of skill ownership on a `[McpServerToolType]` class.
- `[QylCapability("id", Starting|FollowUp)]`: links a tool method to a capability id at compile time.
- `[QylCapabilityDefinition("id", QylSkillKind.X)]`: capability metadata on marker classes in
  `Capabilities/Definitions/`.
- `InvestigationLineage`: `AsyncLocal` guard. Max depth 3, root spawn budget 10, cycle detection. Agent tools call
  `InvestigationLineage.TryEnter()` before starting investigations. Env overrides: `QYL_AGENT_MAX_DEPTH`,
  `QYL_AGENT_MAX_SPAWNS`.

### External Dependencies

qyl integrates with these upstream stacks. **Do not replace with alternatives.** Dead APIs are enumerated in the
`QYL_GROUND_TRUTH` SessionStart hook — a hook blocks edits that reintroduce them.

| Package                                                | Purpose                                                           | Non-negotiable                                                                                                              |
|--------------------------------------------------------|-------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| `Microsoft.Agents.AI` / `Microsoft.Agents.AI.Hosting`  | Agent runtime (MAF RC)                                            | Hosted: `AddAIAgent()` + `IHostedAgentBuilder` + `WithInMemorySessionStore()`. Standalone: `chatClient.AsAIAgent()`.        |
| `Microsoft.Extensions.AI`                              | `IChatClient`, `AITool`, `AIFunction`, `ChatMessage`, `AIContent` | Wrap with `.AsBuilder().UseFunctionInvocation().UseOpenTelemetry(...).Build()`.                                             |
| `ModelContextProtocol` 1.1.0                           | MCP server in `qyl.mcp`                                           | No hand-written tool registration — `[McpServerToolType]` + generator handles DI.                                           |
| `OpenTelemetry` SDK 1.15.0 + Semantic Conventions 1.40 | Telemetry                                                         | Different version tracks; do not conflate. See **Observability** below.                                                     |
| `DuckDB.NET` 1.5.0                                     | Collector storage                                                 | glibc required (Debian image, not Alpine). Single-writer. Collector owns storage; `qyl.loom` is HTTP-only to the collector. |
| `Base UI` 1.3.0 + `lucide-react`                       | Dashboard primitives                                              | NEVER shadcn/ui, Radix UI, or Phosphor icons.                                                                               |
| `xUnit v3` + `Microsoft.Testing.Platform`              | Test runner                                                       | `dotnet test --project` is required — positional args no longer work. TRX via `--report-xunit-trx`.                         |

## Key Conventions

Imported from MAF `dotnet/AGENTS.md` (override anything in older qyl docs):

- **Encoding**: New `.cs` files must be **UTF-8 with BOM**. Required for `dotnet format` to behave. `.editorconfig`
  declares `charset = utf-8-bom` under `[*.cs]`.
- **Copyright header**: qyl uses `// Copyright (c) 2025-2026 ancplua` on top of `.cs` files (personal repo; MAF uses the
  Microsoft header).
- **XML docs**: Required on all public methods and classes. `GenerateDocumentationFile=true` is set in
  `Directory.Build.props`.
- **Async**: Use the `Async` suffix for any method returning `Task` / `ValueTask` — including test methods.
- **Private classes**: Declare `sealed` unless intentionally subclassed.
- **Config**: Environment variables use `UPPER_SNAKE_CASE` (`QYL_AGENT_MAX_DEPTH`, `OTEL_EXPORTER_OTLP_ENDPOINT`).
- **Tests**: Arrange / Act / Assert comments. Use the project's `FakeChatClient` (
  tests/qyl.collector.tests/Instrumentation/) for `IChatClient` doubles — do NOT hand-roll `Moq<IChatClient>` (replaced
  in commit `645bd970`). Async test methods use the `Async` suffix.
- **C# 14** with preview features enabled. File-scoped namespaces, primary constructors, required init properties,
  pattern matching, switch expressions over if-else.

qyl-specific rules layered on top:

- **No suppression**. No `#pragma warning disable`, no `[SuppressMessage]`, no `<NoWarn>` (exception: upstream sample
  repos demonstrating experimental APIs). qyl's `WarningsAsErrors=CA1816;CA2012;CA2016` is already minimal — add more
  rules, never subtract.
- **Generator discipline**: `IIncrementalGenerator` only, `ForAttributeWithMetadataName`, value-equatable models, raw
  strings over `SyntaxFactory`. Never store `ISymbol` in models. Test generators via `ANcpLua.Roslyn.Utilities` test
  infrastructure.
- **Never**: runtime reflection as a control mechanism, `dynamic` / `ExpandoObject`, blocking async (`.Result` /
  `.Wait()`), any analyzer besides `ANcpLua.Analyzers`, suppressing `null !` when the code can be rewritten.

## Key Design Principles

Verbatim from MAF — verify adherence when reviewing any change:

- **DRY**: Avoid duplication by moving common logic into helper methods or helper classes.
- **Single Responsibility**: Each class has one clear responsibility.
- **Encapsulation**: Keep implementation details private and expose only necessary public APIs.
- **Strong Typing**: Use strong typing so the code is self-documenting and errors are caught at compile time.

qyl additions:

- **Compile-time wiring over runtime reflection.** The generator owns DI registration, MCP tool registration, and
  capability catalogs. If you're tempted to hand-register a tool, add the `[QylSkill]` + `[QylCapability]` attributes
  instead.
- **Dependency direction is one-way.** `qyl.contracts` at the bottom (BCL-only), generators in their own projects (no
  runtime deps), runtime projects depend on contracts + generator outputs. Do not let MAF types leak into
  `qyl.contracts`. Do not let the dashboard call runtime code directly — it goes through the collector REST API.

## Observability

qyl *is* the observability product, so MAF's OTel conventions are non-negotiable for every qyl service. Synthesized from
upstream OTel samples
at <https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/AgentOpenTelemetry> and the
Python observability reference.

### Resource attributes (required on every service)

```csharp
var resource = ResourceBuilder.CreateDefault()
    .AddService(serviceName: "qyl.collector", serviceVersion: "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["service.instance.id"] = Environment.MachineName,
        ["deployment.environment"] = Environment.GetEnvironmentVariable("QYL_DEPLOYMENT_ENVIRONMENT") ?? "development",
    })
    .Build();
```

### Chat client + agent instrumentation

Wrap every `IChatClient` AND the `AIAgent` with the telemetry pipeline — both layers, not one. Use
`UseQylTelemetry` (the qyl wrapper) on the chat client, not a hand-rolled
`UseFunctionInvocation().UseOpenTelemetry(...)` chain — the wrapper includes `ToolDecoratingChatClient` and
refuses to double-wrap:

```csharp
using IChatClient instrumented = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseQylTelemetry(sourceName: "qyl.genai", configure: cfg => cfg.EnableSensitiveData = null /* env */)
    .Build();

var agent = new ChatClientAgent(instrumented, name: "...", instructions: "...", tools: [...])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "qyl.agent", configure: cfg => cfg.EnableSensitiveData = null)
    .Build();
```

- `EnableSensitiveData = null` defers to `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`. Never pass `true`
  as a literal in prod.
- `sourceName` is the **application's own** `ActivitySource` name, not the framework's. qyl-owned names are
  enumerated below.
- Both layers live at the composition root, never scattered through call sites. No attribute-based agent tracing
  — `[AgentTraced]` was removed in the 2026-04 collapse.

### Environment variables (standard OTel, not qyl-invented)

Prefer standard OTel env vars — MAF's python samples normalize everything on these.

| Variable                                     | Purpose                                                       |
|----------------------------------------------|---------------------------------------------------------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT`                | Base endpoint (default `http://localhost:4317` gRPC)          |
| `OTEL_EXPORTER_OTLP_PROTOCOL`                | `grpc` or `http`                                              |
| `OTEL_EXPORTER_OTLP_HEADERS`                 | `Authorization=Bearer ...` for auth'd backends                |
| `OTEL_SERVICE_NAME` / `OTEL_SERVICE_VERSION` | Service identification                                        |
| `OTEL_RESOURCE_ATTRIBUTES`                   | Additional k=v pairs                                          |
| `ENABLE_INSTRUMENTATION`                     | Turns on agent-framework telemetry code paths                 |
| `ENABLE_SENSITIVE_DATA`                      | Dev-only: prompts + completions on spans                      |
| `ENABLE_CONSOLE_EXPORTERS`                   | Dev-only: console fallback when no OTLP backend is configured |

### Trace propagation across MCP

MAF automatically propagates W3C Trace Context through MCP `tools/call` via the `params._meta` field. qyl's MCP tools
inherit this for free. **Do not** invent a custom `traceparent` header or mutate `_meta` by hand. Custom propagators (
B3, Jaeger) are supported via the global OTel propagator registration.

### Semantic conventions

qyl emits **OTel GenAI semconv 1.40**. Required attributes:

| Attribute                                                  | Where                                |
|------------------------------------------------------------|--------------------------------------|
| `gen_ai.system`                                            | Every GenAI span                     |
| `gen_ai.request.model` / `gen_ai.response.model`           | Chat spans                           |
| `gen_ai.usage.input_tokens` / `gen_ai.usage.output_tokens` | Token counters                       |
| `gen_ai.operation.name`                                    | `chat`, `execute_tool`, `embeddings` |
| `gen_ai.tool.call.id` / `gen_ai.tool.name`                 | Tool invocations                     |
| `gen_ai.agent.name` / `gen_ai.agent.id`                    | Agent-scoped spans                   |

qyl-owned `ActivitySource` names:

- `qyl.genai` — GenAI request/response spans
- `qyl.agent` — agent lifecycle spans
- `qyl.collector.ingestion` — OTLP receive path
- `qyl.mcp` — MCP tool invocations

### Dev backend

Aspire Dashboard (Docker) is the default local OTel backend:

```bash
docker run --rm -it -d \
    -p 18888:18888 \
    -p 4317:18889 \
    --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

- Web UI: <http://localhost:18888>
- OTLP gRPC: `http://localhost:4317`
- Anonymous access for dev: `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`

## Sample Structure

**`samples/` is currently empty.** The Feb-2026 Loom monolith (`maf-agent-qyl/`) was removed on 2026-04-17. The
production Loom pipeline lives under `services/qyl.loom/` (LLM-driven `AutofixAgentService` + `ExplorationOrchestrator`), not
as a hosted-agents demo.

### Where to learn what

| You want to learn                                                                                                                                              | Go to                                                                           |
|----------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------|
| MAF fundamentals (agents, sessions, tools, streaming, structured output, RAG)                                                                                  | `~/AgentFrameworkBook/` or upstream `microsoft/agent-framework/dotnet/samples/` |
| qyl-specific MAF delta (`WithQylTelemetry`, `LoomRunState`, hosted `AddAIAgent`)                                                                               | `microsoft-agent-framework-qyl` skill (global)                                  |
| qyl's production autofix pipeline (real LLM chain, collector-integrated)                                                                                       | `services/qyl.loom/Autofix/AutofixAgentService.cs`                                   |
| qyl's interactive exploration (SSE streaming, diagnostician + strategist)                                                                                      | `services/qyl.loom/Exploration/ExplorationOrchestrator.cs`                           |
| Loom generator attributes (`[LoomContract]` / `[LoomStep]` / `[LoomTool]` / `[LoomWorkflow]`) with real MAF `Executor` + `ProtocolBuilder` + `WorkflowBuilder` | `services/qyl.loom/CompilerDemo/LoomDemoWorkflow.cs`                                 |
| qyl MCP tool pattern (attributes + generator)                                                                                                                  | `services/qyl.mcp/Tools/` + the overlay's "Generator-driven attributes" section      |

### When to add a new sample

Only when the sample demonstrates something **qyl-specific** that neither upstream nor `~/AgentFrameworkBook/` covers:

- `WithQylTelemetry` wiring against a non-obvious provider
- End-to-end `[QylSkill]` + `[QylCapability]` tool declaration hitting the qyl.mcp generator output
- `InvestigationLineage` guard exercised in a multi-step agent sequence
- `LoomRunState` discipline across bounded agents with real trace assertions

Anything that just teaches MAF basics belongs upstream or in `~/AgentFrameworkBook/`, not here.

### Rules for a qyl-specific sample

- Standalone project at `samples/<category>/<name>/` (categories mirror MAF: `01-get-started`, `02-agents`,
  `03-workflows`, `04-hosting`, `05-end-to-end`).
- Add the `.csproj` to `qyl.slnx`.
- Include a `README.md` with required env vars, the exact `dotnet run --project ...` invocation, and expected output.
- Configure via environment variables — never hardcode secrets.
- Wire OTel on every sample. qyl is observability; every sample should show up in the Aspire Dashboard.
- Reference `FakeChatClient` from `ANcpLua.Roslyn.Utilities.Testing.AgentTesting.ChatClients` for mock chat behavior (
  never hand-roll `Moq<IChatClient>`).
- UTF-8 with BOM on new `.cs` files. Copyright `// Copyright (c) 2025-2026 ancplua`.

## Compile-time wiring — single source of truth

- `[QylSkill(QylSkillKind.X)]` on each `[McpServerToolType]` class is the only place skill ownership is declared.
- `[QylCapability("id", Starting|FollowUp)]` on tool methods links tools to capabilities at compile time.
- `[QylCapabilityDefinition("id", QylSkillKind.X)]` on marker classes in `Capabilities/Definitions/` defines capability
  metadata.
- The generator produces `RegisterTools()`, `RegisterServices()`, `Capabilities[]`, `ToolDescriptors[]`.
- Do NOT hand-add tools to DI, MCP registration, or skill catalogs — the generator handles it from the attribute.
- Tools without `[QylSkill]` (`CapabilityTools`, `ArtifactTools`) keep manual registration and are excluded from the
  generator's output.

## Agent bounded autonomy

- `InvestigationLineage` (AsyncLocal) enforces max depth (3), root spawn budget (10), cycle detection.
- `UseQylTools` and `RcaTools` call `InvestigationLineage.TryEnter()` before starting investigations.
- Env overrides: `QYL_AGENT_MAX_DEPTH`, `QYL_AGENT_MAX_SPAWNS`.

## Known debt — architectural

- `collector/Autofix/` still contains embedded Loom intelligence (`LoomOrchestrator`, `LoomDiagnostician`,
  `LoomStrategist`, `LoomPrompts`, etc.) that should live in `qyl.loom/` only. The collector should expose read/write
  endpoints over telemetry state, not own LLM orchestration.
- `collector/AgentRuns/` is correct — pure read-only DuckDB queries for agent run observability.
- **Schema Drift (CI Schema Drift job stays red; Backend is green).** The TypeSpec → C# pipeline emits `SpanRecord` into
  three different places and the collector depends on the one CI's fresh regenerate wants to delete:
    - `Qyl.Models.SpanRecord` in `packages/Qyl.Contracts/Models/Models.g.cs` — uses `Qyl.OTel.Enums.SpanKind` /
      `SpanStatusCode`. This is what the collector's `Mappers.cs`, `SpanRingBuffer.cs`, `HealthUiService.cs` (14 call
      sites) resolve to via `global using Qyl.Models;` in `services/qyl.collector/GlobalUsings.cs`.
    - `Qyl.Storage.SpanRecord` in `packages/Qyl.Contracts/Models/Storage.g.cs` — also uses `Qyl.OTel.Enums.*`. The routing
      table (`eng/build/NamespaceRoutingTable.cs:39`) routes `Qyl.Storage.*` here, so CI's fresh generate only emits to
      this file and removes the record from `Models.g.cs` (that's the 115-line diff CI reports).
    - `Qyl.Contracts.Models.SpanRecord` in `packages/Qyl.Contracts/Models/SpanRecord.cs` — hand-written, uses
      `Qyl.Contracts.Enums.SpanKind` / `SpanStatusCode`. Not interchangeable with the generated ones.
    - **Why this is stuck**: simply deleting `Qyl.Models.SpanRecord` (to match what CI regenerates) breaks the collector
      because the enum-namespace mismatch is real, not cosmetic. Simply adding `global using Qyl.Storage;` collides with
      `Qyl.Models` and still doesn't solve the callsite enum references.
    - **Options for the real fix** (each needs a ruhig-Moment session):
        1. Change the TypeSpec source so `SpanRecord` is not emitted into the `Qyl.Models` bucket at all — then the
           committed `Models.g.cs` matches a fresh regenerate.
        2. Switch `global using Qyl.Models;` → `global using Qyl.Storage;` in `qyl.collector/GlobalUsings.cs` AND update
           `Mappers.cs` et al. so the enum references match the `Qyl.Storage.SpanRecord` variant (which also uses
           `Qyl.OTel.Enums.*` — so ideally drop-in, but verify).
        3. Delete the manual `Qyl.Contracts.Models.SpanRecord` if it's redundant, consolidate on one emitted record, one
           enum namespace.
    - The failed quick-fix attempt lives in git history as the 4c9fd8c9 → 799390e0 revert pair. Do not simply re-delete
      `SpanRecord` from `Models.g.cs` — verify the collector still builds first.
    - Schema Drift is the only red CI job; Backend, Frontend, Coverage, Dependency Audit are all green on `main`.
- **Open Dependabot PR #123 — Vite 7→8 major bump on `services/qyl.dashboard`.** Held open because it's a real Frontend (
  React) CI failure (not the inherited Schema-Drift flake). Needs active migration work: review Vite 8 breaking changes
  against the dashboard's `vite.config.ts`, test-runner integration (`vite` is a devDependency of the coverage setup),
  and any plugin ecosystem incompatibilities. Do not blind-merge. All other open Dependabot PRs as of 2026-04-19 were
  closed (#136, .NET 11 preview — we stay on 10 LTS) or squash-merged (#128 `actions/configure-pages@v6`, #129
  `actions/deploy-pages@v5`, #137 dashboard npm patch-minor bundle).

## Merged repos (2026-04-10)

- `qyl.mcp` (77 MCP tools) and `qyl.mcp.generators` (interim tool manifest generator) merged from a standalone repo.
- `Qyl.Agents`, `Qyl.Agents.Abstractions`, `Qyl.Agents.Generator` merged from the netagents repo.
- `qyl.mcp.generators` emits `QylToolManifest` with `ToolDescriptors` (skill-aware), `Capabilities`, `RegisterTools`,
  `RegisterServices`, `CreateTools`.
- `Qyl.Agents.Generator` is the full generator (dispatch, schemas, OTel, JSON contexts) — convergence target.

## Dashboard deep links

- `TracesPage` / `SpanDetails` panel has an "Investigate in Claude Code" button using a `claude-cli://open?q=...` deep
  link.
- The link pre-fills trace id, span id, span name, service, status, and duration into the Claude Code prompt.

## Reference docs

- `~/Apex.AgenticEntityExtractor/` — canonical qyl consumer-code shape (three builder interfaces + fluent middleware
  + three orchestration strategies). Mirror this pattern; do not invent a bespoke composition.
- `docs/ARCHITECTURE.md` — C4 Context / Container / Component diagrams
- `docs/THREAT_MODEL.md` — 20 attacker stories with P0–P3 prioritization
- `docs/OPEN_WORK.md` — consolidated open work items (from the former specs/ tree)
- `docs/aot-assessment.md`, `docs/attribute.md`, `docs/generator.md`, `docs/emitters.md` — generator ecosystem reference
- `microsoft-agent-framework-qyl` global skill — qyl MAF overlay (delta only; core MAF via the `microsoft-agent-framework`
  global skill + upstream <https://github.com/microsoft/agent-framework/tree/main/dotnet>)
- `docs/planned/*` — execute-ready prompts for LSP Phase 2/3 and OAuth E2E (indexed from OPEN_WORK.md §11)

## A note to the agent

When you learn something non-obvious, update this AGENTS.md (root) so future sessions benefit. Never ask for
confirmation on non-destructive edits.

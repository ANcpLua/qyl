---
name: microsoft-agent-framework-qyl
description: qyl-specific MAF consumer patterns. Extends ~/.claude/skills/microsoft-agent-framework/SKILL.md with WithQylTelemetry, hosted AddAIAgent, LoomRunState session discipline, and qyl verification. Triggers on WithQylTelemetry, UseQylTelemetry, LoomRunState, LoomToolEnvelope, InvestigationLineage, AddAIAgent hosted, QylSkill, QylCapability, or any qyl-specific MAF task. The global skill covers core MAF (4 pillars, life-of-a-call, workflows, package matrix, testing, advanced surfaces). This file adds only qyl delta.
---

# MAF — qyl overlay

**Read `~/.claude/skills/microsoft-agent-framework/SKILL.md` first** for core MAF: the four pillars (chat / tools / structured output / RAG), life-of-an-LLM-call, workflows, package matrix, `FakeChatClient` API, conformance suites, decision trees, and the combined worked example. This file adds **only** what is specific to consuming MAF inside `~/qyl`.

## WithQylTelemetry — the qyl OTel pipeline

`src/qyl.instrumentation/Instrumentation/GenAi/GenAiInstrumentation.cs` ships two extensions that wrap `OpenTelemetryChatClient` + `ToolInstrumentingChatClient` and refuse to double-wrap. **Use these, not the generic `.UseFunctionInvocation().UseOpenTelemetry(...)` chain.**

```csharp
// Short form
IChatClient instrumented = baseClient.WithQylTelemetry(
    sourceName: "qyl.genai",
    enableSensitiveData: null);  // null = use OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT env var

// Builder form (preferred inside a pipeline)
var builder = new ChatClientBuilder(baseClient);
builder.UseQylTelemetry(sourceName: "qyl.genai", configure: cfg => cfg.EnableSensitiveData = true);
IChatClient instrumented = builder.Build();
```

Wrap the agent too — both layers, always:

```csharp
var agent = new ChatClientAgent(instrumented, name: "loom.diagnostician", instructions: "…")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "qyl.agent", configure: cfg => cfg.EnableSensitiveData = true)
    .Build();
```

The analyzer at `src/qyl.instrumentation.generators/Analyzers/AgentCallSiteAnalyzer.cs` flags direct `AIAgent.InvokeAsync` / `ChatClientAgent.InvokeAsync` calls that bypass this pipeline.

## Hosted pattern — `AddAIAgent`

qyl's default construction when a service wires MAF agents through DI (e.g. `qyl.collector` per `QYL_GROUND_TRUTH`). For MAF fundamentals see upstream `microsoft/agent-framework/dotnet/samples/` or `~/AgentFrameworkBook/`. Canonical shape:

```csharp
builder.AddAIAgent(
        key:          "loom.coder",
        instructions: LoomInstructions.Coder,
        lifetime:     ServiceLifetime.Scoped)
    .WithInMemorySessionStore()
    .WithAITool(
        static _ => AIFunctionFactory.Create(
            static (string repository, string branch, string title) =>
                PullRequestToolService.CreatePullRequest(repository, branch, title),
            name:        "CreatePullRequest",
            description: "Creates a pull request for the proposed fix."),
        ServiceLifetime.Scoped);
```

Resolve with `GetRequiredKeyedService<AIAgent>("loom.coder")`. Always `.WithInMemorySessionStore()` or sessions fail. Register `IChatClient` once as singleton — all agents share it.

## Session discipline — one session per agent

From `qyl/AGENTS.md`: *"Each bounded agent owns its own session; do not pretend a shared conversation id unifies them."*

```csharp
internal sealed class LoomRunState
{
    private readonly Dictionary<string, AgentSession> _agentSessions = new(StringComparer.Ordinal);

    public async ValueTask<AgentSession> GetOrCreateSessionAsync(
        string agentKey, AIAgent agent, CancellationToken ct)
    {
        if (_agentSessions.TryGetValue(agentKey, out var existing)) return existing;
        var created = await agent.CreateSessionAsync(ct).ConfigureAwait(false);
        _agentSessions.Add(agentKey, created);
        return created;
    }
}
```

Five agents = five `AgentSession` instances. Cross-agent state (`Diagnosis`, `Plan`, `Review`) is carried as **text in the next prompt**, never as shared history.

## Generator-driven attributes — 33 across 5 pipelines

Every qyl attribute below is consumed by an `IIncrementalGenerator` using `ForAttributeWithMetadataName`. **Zero runtime reflection.** Run `nuke Generate` after any attribute change.

Canonical MCP tool example:

```csharp
[McpServerToolType]
[QylSkill(QylSkillKind.Observability)]
public static class TraceTools
{
    [McpServerTool, Description("Get a trace by id.")]
    [QylCapability("trace.get", CapabilityRole.Starting)]
    [ToolSideEffect(ToolSideEffect.ReadOnly)]
    [EmitsStructuredOutput(typeof(TraceDto))]
    public static async ValueTask<LoomToolEnvelope<TraceDto>> GetTraceAsync(
        [Description("The trace id.")] string traceId, CancellationToken ct)
    {
        if (!InvestigationLineage.TryEnter())
            return LoomToolEnvelope.Fail<TraceDto>("Investigation depth/spawn budget exceeded.");
        return LoomToolEnvelope.Ok(dto);
    }
}
```

**Hard rules:**
- Never hand-add tools to DI — generators own registration.
- `LoomToolEnvelope.Ok(data)` / `LoomToolEnvelope.Fail<T>(error)` via the non-generic companion.
- Investigation-spawning tools call `InvestigationLineage.TryEnter()` first. Max depth 3, spawn budget 10.
- `src/qyl.contracts/Attributes/*.g.cs` (GenAi, Db, Mcp) are **not** generator triggers — they are emitted constant classes holding semconv keys consumed by `[OTel(…)]` and `SetTag` call sites.

### A. MCP tool manifest — `qyl.mcp.generators/ToolManifestGenerator.cs`

| Attribute | Target | Emits |
|---|---|---|
| `[QylSkill(QylSkillKind)]` | class | Entry in `QylToolManifest.RegisterTools/RegisterServices`, populates `ToolTypes[]`, `ToolDescriptors[]` |
| `[QylCapability(id, Role)]` | method | Joins tool ↔ capability id; resolved against `[McpServerTool(Name=…)]` (dangling refs → diagnostic) |
| `[QylCapabilityDefinition(id, SkillKind?)]` | marker class | Populates `QylToolManifest.Capabilities[]` |
| `[McpServerToolType]` | class | Upstream MCP attr — seeds tool discovery |

### B. Full MCP server — `Qyl.Agents.Generator/McpServerGenerator.cs` (convergence target)

| Attribute | Emits |
|---|---|
| `[McpServer(name?)]` on class | Dispatch, JSON schema, OTel spans, `JsonSerializerContext` |
| `[Tool(name?)]` on method | Tool dispatch + schema + hints (`ReadOnly`/`Idempotent`/`Destructive`/`OpenWorld`) |
| `[Prompt(name)]` on method | Prompt dispatch + metadata |
| `[Resource(uri)]` on method | Resource dispatch + MIME metadata |

### C. Loom workflow — `qyl.instrumentation.generators/Loom/LoomSourceGenerator.cs`

| Attribute | Emits into `LoomGeneratedRegistry` |
|---|---|
| `[LoomTool(name, Phase, UseOnlyWhen, DoNotUseWhen)]` | `AIFunction` bridge metadata |
| `[LoomContract(name)]` on class/struct | Contract descriptor + JSON schema |
| `[LoomStep(id, Phase)]` on class | Step descriptor |
| `[LoomWorkflow(id, runStateType, stepIds…)]` on class | Workflow registry entry |
| `[RequiresCapability(id)]` | Gate metadata |
| `[RequiresApproval]` | Approval flag |
| `[ToolSideEffect(ToolSideEffect)]` | Side-effect classification |
| `[EmitsStructuredOutput(Type)]` | Output schema |
| `[LoomBudget(MaxAttempts, MaxToolCalls, MaxTokens)]` | Budget descriptor |

### D. Runtime telemetry — `qyl.instrumentation.generators/ServiceDefaultsSourceGenerator.cs` + meter/traced emitters

| Attribute | Emits |
|---|---|
| `[Meter(name, Version?)]` on class | Static `Meter` instance |
| `[Counter/Histogram/Gauge/UpDownCounter(name, Unit, Description)]` on partial method | Instrument field + method body |
| `[Tag(name?)]` on parameter | Metric dimension |
| `[Traced(activitySourceName, SpanName, Kind, RootSpan)]` on class/method | Interceptor that wraps with `Activity` span |
| `[AgentTraced(AgentName?)]` on method | `gen_ai.agent.invoke` span wrapper (`qyl.agent` source) |
| `[NoTrace]` | Opt-out from class-level tracing |
| `[TracedTag(name?, SkipIfNull, SkipIfDefault)]` on param/prop | Captured as span tag |
| `[return: TracedReturn(tagName, Property=…)]` | Result captured as span attr |
| `[OTel(name)]` on param/prop | Overrides tag name with semconv key |
| Assembly-level `[GeneratedActivitySource]` / `[GeneratedMeter]` / `[GeneratedCapability(kind, value)]` | Auto-registration hooks |

### E. DuckDB storage — `qyl.collector.storage.generators/DuckDbInsertGenerator.cs`

Emitted via `PostInitializationOutput`. Not MAF-facing, included for completeness.

| Attribute | Emits |
|---|---|
| `[DuckDbTable(name, OnConflict?)]` on class/struct | `AddParameters(DuckDbCommand)`, `MapFromReader(DbDataReader)`, `Columns` constants |
| `[DuckDbColumn(name?, IsUBigInt, ExcludeFromInsert, Ordinal)]` on property | Column mapping |
| `[DuckDbIgnore]` on property | Skipped entirely |

## qyl anti-patterns

1. Do not share an `AgentSession` across agent keys.
2. Do not hand-register MCP tools — use `[McpServerToolType]` + `[QylSkill]` + `[QylCapability]`.
3. Do not hand-roll `Moq<IChatClient>` — use `FakeChatClient` from `ANcpLua.Roslyn.Utilities.Testing.AgentTesting.ChatClients`.
4. Do not wrap only one layer with telemetry — both `IChatClient` (`WithQylTelemetry`) AND `AIAgent` (`.UseOpenTelemetry`).
5. Do not hand-chain `.UseFunctionInvocation().UseOpenTelemetry(...)` — use `WithQylTelemetry` / `UseQylTelemetry`.
6. Do not construct `LoomToolEnvelope<T>` via the generic `LoomToolEnvelope<T>.Ok` — use the non-generic companion.
7. Do not hand-edit `*.g.cs` — fix generator inputs.
8. Do not enable `EnableSensitiveData = true` in production — gate behind env var.
9. Do not inline NuGet versions — CPM via `Directory.Packages.props`.
10. Do not put `Microsoft.Agents.AI.*` in `qyl.contracts` (BCL-only).

For universal MAF anti-patterns (no `Kernel`, no `AgentThread`, no `.Result`/`.Wait()`, etc.) see the global skill §13.

## qyl verification

```bash
dotnet restore qyl.slnx
dotnet build src/<project>/<project>.csproj --tl:off 2>&1 | tee /tmp/build.log
dotnet test --project tests/<project>.tests --ignore-exit-code 8

# Touched generator inputs?
nuke Generate && nuke Build

# Touched [McpServerToolType]?
dotnet build src/qyl.mcp/qyl.mcp.csproj --tl:off

# Touched telemetry?
# Start Aspire Dashboard, run sample, confirm qyl.genai + qyl.agent spans.
```

Copyright header: `// Copyright (c) 2025-2026 ancplua` (qyl personal header, NOT the Microsoft one). UTF-8 with BOM on all new `.cs` files.

## ANcpLua.Roslyn.Utilities integration

qyl depends on `ANcpLua.Roslyn.Utilities` — the second in-house framework at `~/ANcpLua.Roslyn.Utilities/`. Four package tiers:

| Package | qyl consumers |
|---|---|
| `ANcpLua.Roslyn.Utilities` (runtime) | `qyl.collector`, `qyl.mcp`, `qyl.instrumentation` — `Guard`, `EquatableArray<T>`, `Invoke` DSL, `Result<T>`, `SseClient` |
| `ANcpLua.Roslyn.Utilities.Sources` (source-only) | `qyl.collector.storage.generators`, `qyl.instrumentation.generators`, `Qyl.Agents.Generator` |
| `ANcpLua.Roslyn.Utilities.Testing.AgentTesting` | `tests/qyl.collector.tests/Instrumentation/*`, `tests/Qyl.Agents.Generator.Tests` |
| `ANcpLua.Roslyn.Utilities.Testing.Workflows` | Workflow test infrastructure — `WorkflowFixture`, `WorkflowHarness`, `InMemoryJsonStore`, 9 samples |

For the full testing API (FakeChatClient, MockChatClients, 6 fake agents, 6 conformance fixtures, ActivityCollector, WorkflowFixture) see the global skill §12.

For the full utilities catalog (compile-time + runtime + testing) see `~/.claude/skills/ancplua-roslyn-utilities/SKILL.md`.
---
name: microsoft-agent-framework-qyl
description: qyl MAF consumer patterns — Apex-aligned fluent. Extends ~/.claude/skills/microsoft-agent-framework/SKILL.md with the Apex fluent pattern (IXxxBuilder abstractions + .AsBuilder().Use(middleware).Build() + provider-agnostic chat-client factory + file-based instructions + three-strategy orchestration trichotomy + custom-executor disciplines) plus qyl delta (WithQylTelemetry/UseQylTelemetry at composition root, AddQylServiceDefaults, LoomRunState session discipline, qyl.mcp generator-driven tool registration, InvestigationLineage bounded autonomy). DROPPED 2026-04 — [AgentTraced] + AgentCallSiteAnalyzer + AgentInterceptorEmitter + GenAiCallSiteAnalyzer — replaced by .AsBuilder().UseOpenTelemetry("qyl.agent").Build() at composition root. Triggers on qyl MAF code, Apex-pattern references, ExtractorAgentsBuilder, IExtractorChatClientBuilder, IExtractorWorkflowBuilder, UseQylTelemetry, WithQylTelemetry, LoomRunState, LoomToolEnvelope, InvestigationLineage, QylSkill, QylCapability.
---

# MAF — qyl overlay (Apex-aligned)

**Read `~/.claude/skills/microsoft-agent-framework/SKILL.md` first** for core MAF (four pillars, life-of-a-call, workflows, package matrix, testing, advanced surfaces).

**Reference implementation:** `~/Apex.AgenticEntityExtractor/Apex.AgenticEntityExtractor/` — the canonical shape for how qyl consumer code looks. When in doubt, mirror it. Key files:

- `Agents/ExtractorAgentsBuilder.cs` — agent factory + fluent middleware chain
- `Clients/ExtractorChatClientBuilder.cs` — provider-agnostic chat client factory
- `Workflows/ExtractorWorkflowBuilder.cs` — three orchestration strategies side-by-side
- `Program.cs` — DI wiring, DevUI registration, composition root

## What changed (2026-04 collapse)

The attribute+generator stack for **agent/chat-level telemetry** was collapsed to the MAF fluent middleware pipeline:

- `[AgentTraced]` + `AgentCallSiteAnalyzer` + `AgentInterceptorEmitter` + `AgentInterceptors.g.cs` + `GenAiCallSiteAnalyzer` — **dropped**.
- Replacement — `.AsBuilder().UseOpenTelemetry("qyl.agent").Build()` at the composition root, emitted once per agent.

Attribute-based instrumentation is kept **only** for arbitrary non-chat business-logic methods and metrics — `[Traced]` / `[Meter]` / `[Counter]` / `[Histogram]` / `[Gauge]` / `[UpDownCounter]` / `[Tag]` — because no builder surface exists there.

## The Apex fluent pattern — qyl's shape

### 1. Three dedicated builder interfaces

```csharp
public interface IXxxChatClientBuilder { IChatClient BuildChatClient(ChatProvider provider); }
public interface IXxxAgentsBuilder     { AIAgent BuildEntitiesAgent(string suffix = "", ChatProvider? provider = null); /* ... */ }
public interface IXxxWorkflowBuilder   { Workflow BuildHighLevelPatterns(string name); Workflow BuildLowLevelFullCustomWorkflow(string name); }
```

- **ChatClientBuilder** — provider-agnostic `IChatClient` factory (Azure / OpenAI / Anthropic / Ollama via enum switch).
- **AgentsBuilder** — one factory method per named agent; instructions loaded from `.md` files and cached in a `ConcurrentDictionary`.
- **WorkflowBuilder** — workflow composition methods returning `Workflow`.

All three registered as singletons in `Program.cs`.

### 2. Fluent middleware chain at agent construction

```csharp
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = "EntAgent",
        ChatOptions = new ChatOptions
        {
            Instructions   = LoadInstructions("EntitiesAgent.md"),
            ResponseFormat = ChatResponseFormat.ForJsonSchema<Entities>(),
            Tools          = [AIFunctionFactory.Create(OntologyTools.LoadEntitiesOntologyAsync, "load_entities_ontology")],
            ToolMode       = ChatToolMode.RequireAny,
            Reasoning      = GetReasoningOptions(),
        }
    })
    .AsBuilder()
    .Use(toolResponseMiddleware.CacheToolResponseAsync)   // tool-response caching
    .Build();
```

`.AsBuilder().Use(...).Build()` is the composition surface for **every** cross-cutting concern — telemetry, caching, logging, fallback, distributed cache, function invocation. Middleware delegates plug in here, not via attributes.

### 3. Instructions from `.md` files, cached

```csharp
private static readonly ConcurrentDictionary<string, string> _instructionsCache = new();
private static string LoadInstructions(string fileName) =>
    _instructionsCache.GetOrAdd(fileName, f => File.ReadAllText(Path.Combine("Data", "Instructions", f)));
```

Never inline system prompts. One `.md` per agent under `Data/Instructions/`, `CopyToOutputDirectory=Always` in the `.csproj`.

### 4. Tool-response middleware (cross-cutting, not just telemetry)

```csharp
public interface IToolResponseMiddleware
{
    Func<IEnumerable<ChatMessage>, ChatOptions?, IChatClient, CancellationToken, Task<ChatResponse>> CacheToolResponseAsync { get; }
}
```

Back it with `IDistributedCache` (1h TTL default), key over `(toolName, argsHash)`. Same shape for any AI-adjacent cross-cutting concern — retry, rate-limiting, audit, redaction. Plug via `.Use(...)`, never via interceptor codegen.

### 5. Three orchestration strategies — decision tree

| Strategy | When | API |
|---|---|---|
| **Solo agent** | Entire task fits one LLM call, no branching | `chatClient.AsAIAgent(...)` + single invocation |
| **High-level patterns** | Sequential / concurrent / group-chat | `AgentWorkflowBuilder.BuildSequential` / `BuildConcurrent` / `CreateGroupChatBuilderWith` |
| **Low-level custom workflow** | Non-agent executors, typed handoff, conditional routing, stateful fan-in | `WorkflowBuilder` + custom `Executor` classes |

Rule of thumb — **start high-level**. Drop to manual only when you hit one of:

1. Non-agent executor in the pipeline (normalization, dedup, validation).
2. Typed record needs to flow between stages (not just `List<ChatMessage>`).
3. Topology beyond round-robin (star, conditional routing, gated termination).
4. Stateful fan-in barrier with custom aggregation.
5. Conditional termination logic (loop until approved, max-iteration cap).

### 6. Custom executors — three disciplines

Every qyl custom executor:

- **`partial` class** (codegen-friendly).
- **`declareCrossRunShareable: true`** in executor metadata.
- **Implements `IResettableExecutor`** — workflow re-runs must not leak state.

**Two-phase message protocol** — sync handler buffers, async handler acts on `TurnToken`:

```csharp
// Phase 1 (sync): buffer
HandleMessages(List<ChatMessage> messages) => _messages = messages;

// Phase 2 (async): process
HandleTurnAsync(TurnToken token) { /* use _messages, send/yield */ }
```

**Swap-and-clear** — every stateful executor captures and immediately clears:

```csharp
List<ChatMessage> messages = _messages;
_messages = [];
```

**Typed inter-stage handoff** — use a record, not `List<ChatMessage>`:

```csharp
public sealed record ExtractionContext(
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<Relationship> Relationships);
```

The aggregator executor merges fan-in branches into the record, forwards with a `TurnToken` to the next stage.

### 7. DevUI registration — factory lambdas over DI

```csharp
builder.AddAIAgent("EntAgent_1", (sp, _) =>
{
    var b   = sp.GetRequiredService<IExtractorAgentsBuilder>();
    var cfg = sp.GetRequiredService<IConfiguration>();
    return b.BuildEntitiesAgent("1", cfg.GetValue<ChatProvider>("Provider"));
});

builder.AddWorkflow("FullCustomWorkflow", (sp, name) =>
    sp.GetRequiredService<IExtractorWorkflowBuilder>().BuildLowLevelFullCustomWorkflow(name)
).AddAsAIAgent();
```

One named registration per agent / workflow. Factory resolves its builder from DI, reads provider config, returns the instance. If you have more than ~3 near-identical lambdas, extract a helper extension.

## Telemetry — composition root only

### AddQylServiceDefaults

```csharp
builder.AddQylServiceDefaults();   // OTel sources, resource attrs, redaction, exporter config
builder.AddAzureChatCompletionsClient("foundry").AddChatClient("gpt41");  // upstream UseOpenTelemetry auto-wires
builder.AddAIAgent("writer", "You write short stories...");                // inherits telemetry
```

Everything that used to live in `[AgentTraced]` / `[Traced("qyl.genai")]` attributes lives here now. One call at the top of `Program.cs`.

### WithQylTelemetry / UseQylTelemetry — thin wrappers

Both remain as wrappers over `UseOpenTelemetry` + `ToolDecoratingChatClient` that refuse to double-wrap. Use at the chat-client factory, not sprinkled through call sites.

```csharp
// Chat-client layer → qyl.genai spans
IChatClient instrumented = baseClient
    .AsBuilder()
    .UseQylTelemetry(sourceName: "qyl.genai", configure: cfg => cfg.EnableSensitiveData = null /* env */)
    .Build();

// Agent layer → qyl.agent spans
AIAgent agent = new ChatClientAgent(instrumented, name: "...", instructions: "...")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "qyl.agent", configure: cfg => cfg.EnableSensitiveData = null)
    .Build();
```

Wrap both layers. Missing the agent layer = no agent spans, only per-call GenAI spans.

## Session discipline — one session per agent

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

Each bounded agent owns its own session. Cross-agent state (`Diagnosis`, `Plan`, `Review`) is carried as **text in the next prompt**, never via a shared session id.

## Hosted pattern — `AddAIAgent` with `WithInMemorySessionStore`

```csharp
builder.AddAIAgent(
        key:          "loom.coder",
        instructions: LoomInstructions.Coder,
        lifetime:     ServiceLifetime.Scoped)
    .WithInMemorySessionStore()
    .WithAITool(
        static _ => AIFunctionFactory.Create(
            static (string repo, string branch, string title) =>
                PullRequestToolService.CreatePullRequest(repo, branch, title),
            name:        "CreatePullRequest",
            description: "Creates a pull request for the proposed fix."),
        ServiceLifetime.Scoped);
```

Resolve via `GetRequiredKeyedService<AIAgent>("loom.coder")`. Always `.WithInMemorySessionStore()`. Register `IChatClient` once as singleton — all agents share it.

## Generator-driven attributes — KEPT

Attribute stacks with no upstream middleware equivalent remain generator-driven. Run `nuke Generate` after any attribute change.

### A. MCP tool manifest — `qyl.mcp.generators/ToolManifestGenerator.cs`

| Attribute | Target | Emits |
|---|---|---|
| `[QylSkill(QylSkillKind)]` | class | Entry in `QylToolManifest.RegisterTools` / `RegisterServices` |
| `[QylCapability(id, Role)]` | method | Joins tool ↔ capability id |
| `[QylCapabilityDefinition(id, SkillKind?)]` | marker class | `QylToolManifest.Capabilities[]` |
| `[McpServerToolType]` | class | Upstream MCP seed |

Canonical tool:

```csharp
[McpServerToolType]
[QylSkill(QylSkillKind.Observability)]
public static class TraceTools
{
    [McpServerTool, Description("Get a trace by id.")]
    [QylCapability("trace.get", QylCapabilityRole.Starting)]
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

### B. Full MCP server — `Qyl.Agents.Generator/McpServerGenerator.cs`

| Attribute | Emits |
|---|---|
| `[McpServer(name?)]` on class | Dispatch, JSON schema, OTel spans, `JsonSerializerContext` |
| `[Tool(name?)]` on method | Tool dispatch + schema + hints (`ReadOnly` / `Idempotent` / `Destructive` / `OpenWorld`) |
| `[Prompt(name)]` on method | Prompt dispatch + metadata |
| `[Resource(uri)]` on method | Resource dispatch + MIME metadata |

### C. Loom workflow — `qyl.instrumentation.generators/Loom/LoomSourceGenerator.cs`

| Attribute | Emits into `LoomGeneratedRegistry` |
|---|---|
| `[LoomTool(name, Phase, UseOnlyWhen, DoNotUseWhen)]` | `AIFunction` bridge metadata |
| `[LoomContract(name)]` | Contract descriptor + JSON schema |
| `[LoomStep(id, Phase)]` | Step descriptor |
| `[LoomWorkflow(id, runStateType, stepIds…)]` | Workflow registry entry |
| `[RequiresCapability(id)]` | Gate metadata |
| `[RequiresApproval]` | Approval flag |
| `[ToolSideEffect(ToolSideEffect)]` | Side-effect classification |
| `[EmitsStructuredOutput(Type)]` | Output schema |
| `[LoomBudget(MaxAttempts, MaxToolCalls, MaxTokens)]` | Budget descriptor |

### D. Business-logic metrics — kept, agent/chat telemetry removed

| Attribute | Target | Emits |
|---|---|---|
| `[Meter(name, Version?)]` | class | Static `Meter` instance |
| `[Counter / Histogram / Gauge / UpDownCounter(name, Unit, Description)]` | partial method | Instrument field + body |
| `[Tag(name?)]` | parameter | Metric dimension |
| `[Traced(activitySourceName, SpanName, Kind, RootSpan)]` | **non-chat** class/method | Activity span wrapper |
| `[NoTrace]` | | Opt-out |
| `[TracedTag(name?, SkipIfNull, SkipIfDefault)]` | param/prop | Captured as span tag |
| `[return: TracedReturn(tagName, Property=…)]` | | Result captured as span attr |
| `[OTel(name)]` | param/prop | Overrides tag name with semconv key |

**REMOVED** — `[AgentTraced(AgentName?)]`. Replaced by `.AsBuilder().UseOpenTelemetry("qyl.agent").Build()` at composition root. Do not reintroduce.

### E. DuckDB storage — `qyl.collector.storage.generators/DuckDbInsertGenerator.cs`

| Attribute | Emits |
|---|---|
| `[DuckDbTable(name, OnConflict?)]` | `AddParameters`, `MapFromReader`, `Columns` |
| `[DuckDbColumn(name?, IsUBigInt, ExcludeFromInsert, Ordinal)]` | Column mapping |
| `[DuckDbIgnore]` | Skipped |

## qyl anti-patterns

1. Do not reintroduce `[AgentTraced]` or a bypass-detection analyzer for agent spans. Agent spans come from `.UseOpenTelemetry("qyl.agent")` at composition root.
2. Do not share an `AgentSession` across agent keys.
3. Do not hand-register MCP tools — use `[McpServerToolType]` + `[QylSkill]` + `[QylCapability]`.
4. Do not hand-roll `Moq<IChatClient>` — use `FakeChatClient` from `ANcpLua.Roslyn.Utilities.Testing.AgentTesting.ChatClients`.
5. Do not wrap only one layer with telemetry — both `IChatClient` (`UseQylTelemetry` / `WithQylTelemetry`) AND `AIAgent` (`.UseOpenTelemetry("qyl.agent")`).
6. Do not construct `LoomToolEnvelope<T>` via the generic — use `LoomToolEnvelope.Ok(data)` / `LoomToolEnvelope.Fail<T>(error)`.
7. Do not hand-edit `*.g.cs` — fix generator inputs.
8. Do not enable `EnableSensitiveData = true` in prod — pass `null` (defers to `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`) or bind from config.
9. Do not inline NuGet versions — CPM via `Directory.Packages.props`.
10. Do not put `Microsoft.Agents.AI.*` in `qyl.contracts` (BCL-only).
11. Do not inline system prompts in C# — load from `.md` with `ConcurrentDictionary` caching (Apex §3).
12. Do not fetch the same `ChatProvider`/config value N times in one method — capture in a local once.
13. Do not skip the three builder interfaces (`IXxxChatClientBuilder` / `IXxxAgentsBuilder` / `IXxxWorkflowBuilder`) when a service constructs agents — this is the Apex shape and the seam for testing/swapping providers.

## qyl verification

```bash
dotnet restore qyl.slnx
dotnet build src/<project>/<project>.csproj --tl:off 2>&1 | tee /tmp/build.log
dotnet test --project tests/<project>.tests --ignore-exit-code 8

# Touched generator inputs?
nuke Generate && nuke Build

# Touched [McpServerToolType] / [QylSkill]?
dotnet build services/qyl.mcp/qyl.mcp.csproj --tl:off

# Touched telemetry composition?
# Start Aspire Dashboard, run sample, confirm qyl.genai + qyl.agent spans emit from composition-root wiring only.
```

Copyright header: `// Copyright (c) 2025-2026 ancplua` (qyl personal header, NOT the Microsoft one). UTF-8 with BOM on all new `.cs` files.

## ANcpLua.Roslyn.Utilities integration

qyl depends on `ANcpLua.Roslyn.Utilities`. Four package tiers:

| Package | qyl consumers |
|---|---|
| `ANcpLua.Roslyn.Utilities` (runtime) | `qyl.collector`, `qyl.mcp`, `qyl.instrumentation` — `Guard`, `EquatableArray<T>`, `Invoke` DSL, `Result<T>`, `SseClient` |
| `ANcpLua.Roslyn.Utilities.Sources` (source-only) | `qyl.collector.storage.generators`, `qyl.instrumentation.generators`, `Qyl.Agents.Generator` |
| `ANcpLua.Roslyn.Utilities.Testing.AgentTesting` | `tests/qyl.collector.tests/Instrumentation/*`, `tests/Qyl.Agents.Generator.Tests` |
| `ANcpLua.Roslyn.Utilities.Testing.Workflows` | Workflow test infrastructure — `WorkflowFixture`, `WorkflowHarness`, `InMemoryJsonStore` |

For the full testing API (FakeChatClient, MockChatClients, conformance fixtures, ActivityCollector, WorkflowFixture) see the global skill §12.

For the full utilities catalog see `~/.claude/skills/ancplua-roslyn-utilities/SKILL.md`.

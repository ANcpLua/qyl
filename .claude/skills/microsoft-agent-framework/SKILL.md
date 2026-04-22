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


```csharp
public sealed record ExtractionContext(
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<Relationship> Relationships);
```


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



### AddQylServiceDefaults

```csharp
builder.AddQylServiceDefaults();   // OTel sources, resource attrs, redaction, exporter config
builder.AddAIAgent("writer", "You write short stories...");                // inherits telemetry
```


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
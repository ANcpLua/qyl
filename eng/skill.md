name: microsoft-agent-framework-qyl                                                                                                                                                                                                                   
description: qyl MAF consumer patterns — Apex-aligned fluent. Extends ~/.claude/skills/microsoft-agent-framework/SKILL.md with the Apex fluent pattern (IXxxBuilder abstractions + .AsBuilder().Use(middleware).Build() + provider-agnostic           
chat-client factory + file-based instructions + three-strategy orchestration trichotomy + custom-executor disciplines) plus qyl delta (WithQylTelemetry/UseQylTelemetry at composition root, AddQylServiceDefaults, LoomRunState session
discipline, qyl.mcp generator-driven tool registration, InvestigationLineage bounded autonomy). DROPPED 2026-04 — [AgentTraced] + AgentCallSiteAnalyzer + AgentInterceptorEmitter + GenAiCallSiteAnalyzer — replaced by
.AsBuilder().UseOpenTelemetry("qyl.agent").Build() at composition root. Triggers on qyl MAF code, Apex-pattern references, ExtractorAgentsBuilder, IExtractorChatClientBuilder, IExtractorWorkflowBuilder, UseQylTelemetry, WithQylTelemetry,
LoomRunState, LoomToolEnvelope, InvestigationLineage, QylSkill, QylCapability.
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

Attribute-based instrumentation is kept **only** for arbitrary non-chat business-logic methods and metrics — `[Traced]` / `[Meter]` / `[Counter]` / `[Histogram]` / `[Gauge]` / `[UpDownCounter]` / `[Tag]` — because no builder surface exists
there.

## The Apex fluent pattern — qyl's shape

> **Canonical `Program.cs` composition root** (with `file:line` citations for every MAF API — `AddAIAgent`,
> `AddWorkflow`, `.AddAsAIAgent`, `AddDevUI`, `AddOpenAIResponses`, `AddOpenAIConversations`, `MapDevUI`,
> `MapOpenAIResponses`, `MapOpenAIConversations`) lives in
> `~/RiderProjects/Kochrezepte/Workflowfluentapi.md` → section *"Canonical `Program.cs` — the composition
> root"*. That is the single source of truth — all other docs reference it rather than duplicating the code.
>
> **qyl current state (2026-04-23):** only two of the three builder interfaces exist —
> `services/qyl.loom.patterns/Clients/IQylLoomPatternsChatClientBuilder.cs` and
> `services/qyl.loom.patterns/Agents/IQylLoomPatternsAgentsBuilder.cs`. No `IQylLoomPatternsWorkflowBuilder` yet;
> workflows are built inline in `services/qyl.loom/Autofix/Workflow/AutofixWorkflowFactory.cs` and
> `services/qyl.loom/Exploration/Workflow/ExplorationWorkflowFactory.cs` via `new WorkflowBuilder(start)`. No
> `Program.cs` in qyl today uses `AddAIAgent`/`AddDevUI`/`AddOpenAIResponses` — standalone `.AsAIAgent(options)
> .AsBuilder().UseQylAgentTelemetry().Build()` at every call-site (17 sites — see overlay §"17 call-sites").

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

### 5. `ChatToolMode` — forcing the tool-use contract

  ```csharp
  ChatOptions = new ChatOptions
  {
      Tools    = [.. MotorTools.AsAITools()],
      ToolMode = ChatToolMode.Auto,                              // model decides per turn
      // ToolMode = ChatToolMode.None,                           // tools advertised but never invoked this turn
      // ToolMode = ChatToolMode.RequireAny,                     // must invoke ≥1 tool
      // ToolMode = ChatToolMode.RequireSpecific("BackwardAsync"), // must invoke this exact tool
  }
  ```

- **`Auto`** — default. Use for conversational agents where tool use is optional.
- **`None`** — tools remain discoverable but are suppressed from the call. Pairs well with a "plan then execute" two-turn flow: first turn `None`, second turn `Auto` / `RequireAny`.
- **`RequireAny`** — forces at least one function call. **qyl default for structured-output agents with tools** (see `EntAgent` / `RelAgent` in `ExtractorAgentsBuilder`: it guarantees the ontology tool runs before `ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()` binds).
- **`RequireSpecific(name)`** — forces a named call. `name` must match `AIFunction.Name` exactly (method name by default, or the explicit 2nd arg to `AIFunctionFactory.Create(..., name: "...")`). Throws/ignores silently per-provider if the name is not in `Tools`.

Constraint: `RequireAny` and `RequireSpecific` require `Tools` to be non-empty. Provider SDKs differ on whether an empty tool list surfaces a clear error or silently degrades to `Auto`.

### 6. Chat-client middleware — reducers and the `MEAI001` exception

  ```csharp
  // Suppress MEAI001 for MessageCountingChatReducer / SummarizingChatReducer usage
  #pragma warning disable MEAI001
  IChatClient chatClient = new ChatClientBuilder(baseClient)
      .UseFunctionInvocation()
      .UseChatReducer(new MessageCountingChatReducer(10))
      // .UseChatReducer(new SummarizingChatReducer(baseClient, 20, 10))
      .Build();
  #pragma warning restore MEAI001
  ```

- **`UseFunctionInvocation()`** — required when `ChatOptions.Tools` is non-empty. Executes the tool loop (model → tool-call → tool-result → model) transparently under `GetResponseAsync` / `GetStreamingResponseAsync`. Without it, tool-call messages surface to the caller and nothing executes.
- **`UseChatReducer`** — bounded-context strategy. Apply at the `IChatClient` layer so every agent/turn gets the same budget:
  - `MessageCountingChatReducer(N)` — keep last `N` messages. Cheap, deterministic, dumb about salience.
  - `SummarizingChatReducer(summarizer, bufferSize, retainLast)` — summarize older messages into one `system` message via a second `IChatClient`. Costs a call, recovers semantic continuity.
- **`MEAI001` exception**: both reducer types ship as `[Experimental("MEAI001")]`. This is the **only** sanctioned `#pragma warning disable` in qyl — suppression policy allows experimental-API diagnostics because there's nothing to "fix at source." Wrap narrowly: `disable` on the line above, `restore` after `.Build()`. Do **not** disable in `.csproj` / `Directory.Build.props`.

### 7. `ChatOptions` reference — full surface

  ```csharp
  ChatOptions options = new()
  {
      Instructions     = LoadInstructions("Agent.md"),
      Tools            = [.. MotorTools.AsAITools()],
      ToolMode         = ChatToolMode.Auto,
      AllowMultipleToolCalls = true,                          // parallel tool calls in one turn
      ResponseFormat   = ChatResponseFormat.ForJsonSchema<StepsResponse>(),
      Temperature      = 0.4f,                                // 0.0–2.0
      TopP             = 0.9f,                                // nucleus sampling — pick Temperature XOR TopP, not both
      MaxOutputTokens  = 512,
      FrequencyPenalty = 0.5f,                                // reduce verbatim repetition
      PresencePenalty  = 0.3f,                                // encourage topic diversity
      StopSequences    = ["END"],
      Reasoning        = GetReasoningOptions(),               // reasoning-model budget / effort
  };
  ```

- **`AllowMultipleToolCalls = true`** — lets the model emit multiple tool calls in a single assistant turn, executed in parallel by `UseFunctionInvocation()`. Use when tools are independent (e.g., `GetWeather("Vienna")` + `GetWeather("Graz")`). Leave default (`false`) when tool order matters or tools mutate shared state.
- **Multi-turn accumulation** — after `GetResponseAsync`, append the whole trace, not just the final text, so the next turn sees tool-calls/tool-results:

  ```csharp
  ChatResponse response = await chatClient.GetResponseAsync(conversation, options);
  conversation.AddRange(response.Messages);   // includes assistant tool-calls + tool messages
  ```

- **Reasoning-model caveat** — reasoning models (o-series, Claude extended thinking, etc.) **ignore or reject** most sampling knobs: `Temperature`, `TopP`, `FrequencyPenalty`, `PresencePenalty`, `StopSequences`. Keep those for non-reasoning calls; on reasoning models use `Reasoning` options only. `Temperature` + `TopP` together is also a mutual-exclusion footgun — pick one.
- **`ResponseFormat.ForJsonSchema<T>()`** — pair with `ToolMode = RequireAny` when the schema depends on tool output (ontology / RAG / lookup first, then typed response).

### 8. `AIAgent` — the only two consumer methods that matter

Everything else on `AIAgent` is internal (`IdCore`, `CurrentRunContext`, `DebuggerDisplay`) or a protected hook for custom agent authors (`CreateSessionCoreAsync`). Consumers touch exactly two:

  ```csharp
  // Escape hatch to reach anything wrapped by the .AsBuilder().Use(...).Build() chain
  public TService? GetService<TService>(object? serviceKey = null);

  // The ONLY entry point to conversation state
  public ValueTask<AgentSession> CreateSessionAsync(CancellationToken ct = default);
  ```

- **`GetService<T>()`** — walks the middleware chain. Each layer forwards to the next; returns `null` if nothing exposes `T`. Use it to reach:
  - `AIAgentMetadata` — for telemetry source / provider identification
  - the underlying `IChatClient` — past `UseOpenTelemetry` / `UseFunctionInvocation` / `UseQylTelemetry`
  - any diagnostic or provider-specific surface a wrapper chose to expose

  ```csharp
  var md = agent.GetService<AIAgentMetadata>();
  var inner = agent.GetService<IChatClient>();   // unwrapped client, past all middleware
  ```

- **`CreateSessionAsync()`** — the durable conversation container (messages, tool state, context-provider memory). Session lifetime is independent of the agent instance.

  **qyl rule**: one session per **logical conversation**, not per turn. Cache by `agentKey` in `LoomRunState` (§ "Session discipline" below). A session per turn discards context and re-pays the provider's session-open cost on service-backed agents (Azure Responses, OpenAI Responses).

  ```csharp
  // Phase 1 (sync): buffer
  HandleMessages(List<ChatMessage> messages) => _messages = messages;

  // Phase 2 (async): process
  HandleTurnAsync(TurnToken token) { /* use _messages, send/yield */ }
  ```

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


## Session discipline — `LoomRunState`

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

# AIContextProvider Integration Design

## Problem

Three components manually build LLM context through string concatenation, bypassing the Microsoft Agent Framework's `AIContextProvider` system:

1. **`QylCopilotAdapter.ChatAsync()`** — concatenates session history as XML tags (`<user>...</user><assistant>...</assistant>`) into a single prompt string, then calls `RunStreamingAsync(string)`. This is a concrete bug: the LLM receives a flat string instead of properly typed `ChatMessage` objects, losing role metadata and making multi-turn reasoning unreliable.

2. **`LoomExplorerService.BuildContextBlock()`** / **`LoomInsightService.BuildContextBlock()`** — build error/event summary strings and interpolate them into prompts. These work correctly but duplicate context-building logic across two services.

3. **`QylCopilotAdapter.ExecuteWorkflowAsync()`** — manually substitutes template parameters and concatenates `AdditionalContext` into instructions. **Deferred:** This is a workflow-template concern, not a context-provider concern. The `{{parameter}}` substitution pattern is intentional for declarative workflows and doesn't benefit from `AIContextProvider`.

## Approach: Hybrid (providers where AIAgent is used, shared builder where IChatClient is used)

Wire `AIContextProvider` and `ChatHistoryProvider` into the two paths that already use `AIAgent` (`QylAgentBuilder.FromChatClient` and `QylCopilotAdapter`). Extract Loom's `BuildContextBlock` into a shared service that both `IChatClient` callers and future `AIAgent` callers can use.

### Why not lift Loom to AIAgent?

Loom's 5-phase pipeline makes multiple sequential LLM calls with different prompts (monologue prompt, then solution prompt). This doesn't map to a single `AIAgent.RunAsync` invocation — it would require either multiple `RunAsync` calls (losing the "one provider enriches one invocation" model) or a complex orchestrating agent. The IChatClient pattern is correct for Loom's multi-call design.

## Components

### 1. IssueContextBuilder (shared context data source)

**Location:** `src/qyl.collector/Autofix/IssueContextBuilder.cs`

Extracts the duplicated `BuildContextBlock` logic from `LoomExplorerService` and `LoomInsightService` into a reusable service.

```csharp
public sealed class IssueContextBuilder(DuckDbStore store, IssueService issueService)
{
    /// <summary>
    ///     Loads issue data and formats it into a structured context block.
    /// </summary>
    public async Task<IssueContext> BuildAsync(
        string issueId,
        string? userContext = null,
        int maxEvents = 5,
        CancellationToken ct = default)
    {
        IssueSummary? issue = await store.GetIssueByIdAsync(issueId, ct);
        if (issue is null) return IssueContext.Empty;

        IReadOnlyList<ErrorIssueEventRow> events =
            await issueService.GetEventsAsync(issueId, maxEvents, ct);

        string block = FormatBlock(issue, events, userContext);
        return new IssueContext(issue, events, userContext, block);
    }

    /// <summary>
    ///     Unified formatting for issue context blocks. Standardizes on
    ///     LoomExplorerService's format (richer: includes Env, 800-char stack
    ///     truncation, userContext support). LoomInsightService's shorter format
    ///     (500-char stacks, no Env, "Type:" label) is superseded.
    /// </summary>
    private static string FormatBlock(
        IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events,
        string? userContext)
    {
        // Adopts LoomExplorerService.BuildContextBlock format:
        // - "Error type:" label (not "Type:")
        // - Stack truncation at 800 chars (not 500)
        // - Includes Env field
        // - Supports optional userContext parameter
    }
}

public sealed record IssueContext(
    IssueSummary? Issue,
    IReadOnlyList<ErrorIssueEventRow> Events,
    string? UserContext,
    string FormattedBlock)
{
    public static IssueContext Empty { get; } = new(null, [], null, string.Empty);
    public bool IsEmpty => Issue is null;
}
```

**DI registration:** `builder.Services.AddSingleton<IssueContextBuilder>()` in `Program.cs`.

**Consumer changes:**
- `LoomExplorerService` replaces inline `BuildContextBlock` with `IssueContextBuilder.BuildAsync()`
- `LoomInsightService` replaces its `BuildContextBlock` with `IssueContextBuilder.BuildAsync()`
- Both continue using `IChatClient` directly — no behavioral change

### 2. ObservabilityContextProvider (for AIAgent path)

**Location:** `src/qyl.agents/Context/ObservabilityContextProvider.cs`

Provides issue context as system messages when an `AIAgent` is invoked with an issue ID in the session state.

```csharp
public sealed class ObservabilityContextProvider(IIssueContextSource contextSource)
    : MessageAIContextProvider
{
    /// <summary>
    ///     Key in AgentSession.StateBag that holds the issue ID to contextualize.
    /// </summary>
    public const string IssueIdKey = "qyl.issueId";

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        MessageAIContextProvider.InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        string? issueId = context.Session.StateBag.GetValue<string>(IssueIdKey);
        if (issueId is null) return [];

        string formatted = await contextSource
            .GetFormattedContextAsync(issueId, ct: cancellationToken);

        if (string.IsNullOrEmpty(formatted)) return [];

        return [new ChatMessage(ChatRole.System,
            $"## Error Context\n{formatted}")];
    }
}
```

**When this fires:** Only when `StateBag["qyl.issueId"]` is set. Normal chat requests (no issue context) produce zero messages — zero overhead.

**DI registration:** `builder.Services.AddSingleton<ObservabilityContextProvider>()` in `Program.cs`.

### 3. QylAgentBuilder changes (ChatClient path)

**Location:** `src/qyl.agents/Agents/QylAgentBuilder.cs`

Wire `InMemoryChatHistoryProvider` and `ObservabilityContextProvider` through `ChatClientAgentOptions`.

```csharp
public static AIAgent FromChatClient(
    IChatClient chatClient,
    string agentName = "qyl-assistant",
    string description = "qyl AI assistant",
    string? instructions = null,
    IReadOnlyList<AITool>? tools = null,
    IReadOnlyList<AIContextProvider>? contextProviders = null,
    TimeProvider? timeProvider = null)
{
    InstrumentedChatClient instrumented = new(chatClient, agentName, timeProvider);

    var options = new ChatClientAgentOptions
    {
        Name = agentName,
        Description = description,
        ChatHistoryProvider = new InMemoryChatHistoryProvider(),
    };

    if (instructions is not null)
        options.ChatOptions = new() { Instructions = instructions };

    if (contextProviders is { Count: > 0 })
    {
        options.AIContextProviders ??= [];
        options.AIContextProviders.AddRange(contextProviders);
    }

    // AsAIAgent with options instead of positional parameters
    return new ChatClientAgent(instrumented, options);
}
```

**Note:** `ChatClientAgentOptions.AIContextProviders` accepts `AIContextProvider` (the base class), so both `ObservabilityContextProvider` (a `MessageAIContextProvider`) and any future `AIContextProvider` subclass work.

### 4. QylCopilotAdapter session fix (Copilot path)

**Location:** `src/qyl.agents/Adapters/QylCopilotAdapter.cs`

Replace the XML tag concat + `CopilotSessionStore` with proper `ChatMessage` list passing.

**Before (bug):**
```csharp
// Lines 168-178 — builds XML string
var history = string.Join('\n', sessionHistory.Select(m => $"<{m.Role}>{m.Content}</{m.Role}>"));
effectivePrompt = $"{history}\n{prompt}";
// ...
var enumerator = _agent.RunStreamingAsync(effectivePrompt, ct)...
```

**After:**
```csharp
// Build proper ChatMessage list
List<ChatMessage> messages = [];
if (sessionHistory is { Count: > 0 })
{
    foreach (var (role, content) in sessionHistory)
        messages.Add(new ChatMessage(
            role == "user" ? ChatRole.User : ChatRole.Assistant, content));
}
messages.Add(new ChatMessage(ChatRole.User, prompt));

// Pass typed messages instead of concatenated string
// AIAgent.RunStreamingAsync(IEnumerable<ChatMessage>, AgentSession, AgentRunOptions, CancellationToken)
// Verified: exists on AIAgent (Microsoft.Agents.AI.Abstractions rc3)
var enumerator = _agent.RunStreamingAsync(messages, cancellationToken: ct)
    .GetAsyncEnumerator(ct);
```

The `CopilotSessionStore` continues to be used for the Copilot path since `GitHubCopilotAgent` manages its own server-side history. The fix here is the message format, not the storage mechanism. Migration to `InMemoryChatHistoryProvider` for the Copilot path is deferred — the Copilot SDK's session handling may conflict with it.

### 5. CopilotAguiEndpoints integration (future — not in initial PR)

**Location:** `src/qyl.collector/Copilot/CopilotAguiEndpoints.cs`

No existing endpoint currently creates an `AgentSession` with issue-specific state. This component defines the **integration pattern** for future endpoints (e.g., a Loom-specific AG-UI chat endpoint) that need issue context injection:

```csharp
// Pattern for future issue-aware agent endpoints:
var session = await agent.CreateSessionAsync(ct);
session.StateBag.SetValue(ObservabilityContextProvider.IssueIdKey, issueId);
// All subsequent RunAsync/RunStreamingAsync calls on this session
// will have issue context injected automatically by ObservabilityContextProvider.
```

The `issueId` would arrive as a route parameter (e.g., `POST /api/v1/loom/{issueId}/chat`). This is explicitly **not part of the initial PR** — it becomes actionable when a Loom chat endpoint is built on top of `AIAgent` instead of raw `IChatClient`.

## What doesn't change

- Loom streaming pipeline (`LoomExplorerService`, `LoomInsightService`) stays on `IChatClient`
- `DeclarativeEngine` / `WorkflowEngine` — untouched
- OTel compile-time interceptors — untouched
- AG-UI endpoint registration (`MapAGUI`) — untouched
- `ToolEventInterceptor` pattern in `QylCopilotAdapter` — untouched

## Dependency direction

```text
qyl.contracts (IIssueContextSource interface)
       ↑ references                ↑ references
qyl.agents                   qyl.collector
(ObservabilityContextProvider    (IssueContextBuilder : IIssueContextSource)
 depends on IIssueContextSource)
       ↑ ProjectReference
qyl.collector
(DI wires IssueContextBuilder → IIssueContextSource)
```

`qyl.agents` depends on `qyl.contracts` (existing ProjectReference). `ObservabilityContextProvider` takes `IIssueContextSource` — a BCL-only interface in `qyl.contracts`. `IssueContextBuilder` in `qyl.collector` implements `IIssueContextSource`. No circular reference.

```csharp
// qyl.contracts/Copilot/IIssueContextSource.cs
public interface IIssueContextSource
{
    Task<string> GetFormattedContextAsync(string issueId, string? userContext = null, CancellationToken ct = default);
}
```

`IssueContextBuilder` implements `IIssueContextSource`. The collector's DI container wires them:

```csharp
builder.Services.AddSingleton<IssueContextBuilder>();
builder.Services.AddSingleton<IIssueContextSource>(sp => sp.GetRequiredService<IssueContextBuilder>());
```

## Migration path

When Loom eventually moves from `IChatClient` to `AIAgent`:
1. `ObservabilityContextProvider` already exists and is wired
2. Switch Loom's LLM calls from `IChatClient.GetStreamingResponseAsync` to `AIAgent.RunStreamingAsync`
3. Set `StateBag["qyl.issueId"]` on the session
4. Issue context injection happens automatically — delete the manual `contextBuilder.BuildAsync()` call

## Files changed

| File | Change |
|------|--------|
| `src/qyl.contracts/Copilot/IIssueContextSource.cs` | NEW — interface |
| `src/qyl.collector/Autofix/IssueContextBuilder.cs` | NEW — extracted from Loom services |
| `src/qyl.collector/Autofix/LoomExplorerService.cs` | MODIFIED — delegate to IssueContextBuilder |
| `src/qyl.collector/Autofix/LoomInsightService.cs` | MODIFIED — delegate to IssueContextBuilder |
| `src/qyl.agents/Context/ObservabilityContextProvider.cs` | NEW — MessageAIContextProvider subclass |
| `src/qyl.agents/Agents/QylAgentBuilder.cs` | MODIFIED — ChatClientAgentOptions with providers |
| `src/qyl.agents/Adapters/QylCopilotAdapter.cs` | MODIFIED — ChatMessage list instead of XML concat |
| `src/qyl.collector/Program.cs` | MODIFIED — DI registration |

## SDK types used

| Type | Package | Version | Purpose |
|------|---------|---------|---------|
| `MessageAIContextProvider` | Microsoft.Agents.AI.Abstractions | 1.0.0-rc3 | Base class for ObservabilityContextProvider |
| `InMemoryChatHistoryProvider` | Microsoft.Agents.AI.Abstractions | 1.0.0-rc3 | Replaces manual CopilotSessionStore for ChatClient path |
| `ChatClientAgentOptions` | Microsoft.Agents.AI | 1.0.0-rc3 | Wires providers + history into ChatClientAgent |
| `ChatClientAgent` | Microsoft.Agents.AI | 1.0.0-rc3 | Constructed directly instead of via AsAIAgent() |
| `AgentSession.StateBag` | Microsoft.Agents.AI.Abstractions | 1.0.0-rc3 | Per-session state for issue ID |

## Risks

1. **`ChatClientAgent` constructor API stability** — we're on rc3 pre-release. Constructor signature may change.
   - Mitigation: Pin version, wrap construction in `QylAgentBuilder` (already the case).

2. **`GitHubCopilotAgent` + `InMemoryChatHistoryProvider` interaction** — the Copilot SDK may manage history server-side, conflicting with a local provider.
   - Mitigation: Copilot path keeps `CopilotSessionStore` for now; only the message format is fixed. Defer full provider migration for Copilot path.

3. **`IIssueContextSource` in qyl.contracts** — adds a dependency-free interface to the contracts package.
   - This is allowed per dependency rules (`contracts -> any-package` is forbidden, but BCL-only interfaces are fine).

4. **Service lifetime safety** — all relevant services (`DuckDbStore`, `IssueService`, `IssueContextBuilder`, `ObservabilityContextProvider`) are registered as `Singleton` in `Program.cs`. No captive dependency risk. If any dependency changes to `Scoped` in the future, `IssueContextBuilder` must change to match.

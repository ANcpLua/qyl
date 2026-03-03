# qyl AG-UI + Declarative Workflows Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire AG-UI endpoint (`MapAGUI`), `QylAgentBuilder`, and `DeclarativeEngine` into qyl so any CopilotKit React/Angular client can connect over SSE.

**Architecture:** Three new files + package additions. `QylAgentBuilder` exposes `IChatClient` or `QylCopilotAdapter._agent` as a serveable `AIAgent`. `CopilotAguiEndpoints` registers `AddAGUI()` and maps `MapAGUI()`. `DeclarativeEngine` wraps `DeclarativeWorkflowBuilder` with an in-memory `ChatClientResponseAgentProvider`.

**Tech Stack:** `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` (1.0.0-preview.260225.1), `Microsoft.Agents.AI.Hosting` (1.0.0-preview.260225.1), `Microsoft.Agents.AI.Workflows.Declarative` (1.0.0-rc2), C# 14, net10.0.

---

## Key constraints to know

- **CPM (Central Package Management)**: all versions live in `Version.props` as `$(VariableName)`, then as `<PackageVersion>` entries in `Directory.Packages.props`. `.csproj` files must NOT have `Version=` on `<PackageReference>`.
- **Ralph hook**: no `catch (Exception)` / bare `catch`. Use specific types only.
- **`QylCopilotAdapter._agent`** is private — we must add `internal AIAgent GetInnerAgent() => _agent;` to expose it.
- **`DeclarativeWorkflowOptions` requires a non-null `ResponseAgentProvider`** (abstract class). We implement a private `ChatClientResponseAgentProvider` in `DeclarativeEngine.cs`.
- **`AgentResponseUpdate` constructors**: `(ChatRole? role, string text)` and `(ChatResponseUpdate)` — use the first.
- **`InProcessExecution.Default.RunStreamingAsync<string>(workflow, input)`** returns `StreamingRun`; `run.WatchStreamAsync()` yields `WorkflowEvent` objects with a `Data` property.
- **Generator auto-OTel**: `qyl.servicedefaults.generator` already intercepts `AIAgent.RunAsync()` at compile time — no need for `UseOpenTelemetry()`.

---

## Task 1: Add package versions to `Version.props`

**Files:**
- Modify: `/Users/ancplua/qyl/Version.props`

**Step 1: Add three version properties**

Add to the `<!-- Microsoft.Agents.AI -->` section (after `MicrosoftAgentsCopilotVersion`):

```xml
<MicrosoftAgentsHostingVersion>1.0.0-preview.260225.1</MicrosoftAgentsHostingVersion>
<MicrosoftAgentsHostingAguiAspNetCoreVersion>1.0.0-preview.260225.1</MicrosoftAgentsHostingAguiAspNetCoreVersion>
<MicrosoftAgentsWorkflowsDeclarativeVersion>1.0.0-rc2</MicrosoftAgentsWorkflowsDeclarativeVersion>
```

**Step 2: Verify no duplicates**

Run: `grep "MicrosoftAgentsHosting\|MicrosoftAgentsWorkflows" /Users/ancplua/qyl/Version.props`
Expected: exactly 3 matches.

**Step 3: Commit**

```bash
git -C /Users/ancplua/qyl add Version.props
git -C /Users/ancplua/qyl commit -m "build: add Agents.AI.Hosting + Declarative versions"
```

---

## Task 2: Register packages in `Directory.Packages.props`

**Files:**
- Modify: `/Users/ancplua/qyl/Directory.Packages.props`

**Step 1: Add three `PackageVersion` entries**

Add inside the `<!-- Microsoft.Agents.AI -->` ItemGroup (after the existing `GitHub.Copilot` entry):

```xml
<PackageVersion Include="Microsoft.Agents.AI.Hosting" Version="$(MicrosoftAgentsHostingVersion)"/>
<PackageVersion Include="Microsoft.Agents.AI.Hosting.AGUI.AspNetCore" Version="$(MicrosoftAgentsHostingAguiAspNetCoreVersion)"/>
<PackageVersion Include="Microsoft.Agents.AI.Workflows.Declarative" Version="$(MicrosoftAgentsWorkflowsDeclarativeVersion)"/>
```

**Step 2: Lint check**

Run: `/lint-dotnet` (or `bash lint-dotnet.sh .` from qyl root)
Expected: No Rule A/B/G violations.

**Step 3: Commit**

```bash
git -C /Users/ancplua/qyl add Directory.Packages.props
git -C /Users/ancplua/qyl commit -m "build(deps): add Agents.AI Hosting + Declarative CPM entries"
```

---

## Task 3: Add package references to project files

**Files:**
- Modify: `/Users/ancplua/qyl/src/qyl.copilot/qyl.copilot.csproj`
- Modify: `/Users/ancplua/qyl/src/qyl.collector/qyl.collector.csproj`

**Step 1: Add to `qyl.copilot.csproj`**

In the `<!-- GitHub Copilot Agent SDK -->` ItemGroup, after `Microsoft.Agents.AI.Abstractions`:

```xml
<PackageReference Include="Microsoft.Agents.AI.Hosting"/>
<PackageReference Include="Microsoft.Agents.AI.Workflows.Declarative"/>
```

**Step 2: Add to `qyl.collector.csproj`**

Add a new ItemGroup (e.g., after the `<!-- M.E.AI -->` group):

```xml
<!-- AG-UI endpoint (CopilotKit-compatible SSE) -->
<ItemGroup>
  <PackageReference Include="Microsoft.Agents.AI.Hosting.AGUI.AspNetCore"/>
</ItemGroup>
```

**Step 3: Build to confirm packages restore**

Run: `dotnet build /Users/ancplua/qyl/src/qyl.copilot/qyl.copilot.csproj`
Then: `dotnet build /Users/ancplua/qyl/src/qyl.collector/qyl.collector.csproj`
Expected: Build succeeded (0 errors).

**Step 4: Commit**

```bash
git -C /Users/ancplua/qyl add src/qyl.copilot/qyl.copilot.csproj src/qyl.collector/qyl.collector.csproj
git -C /Users/ancplua/qyl commit -m "build(deps): reference Hosting + Declarative in copilot/collector"
```

---

## Task 4: Expose `GetInnerAgent()` on `QylCopilotAdapter`

**Files:**
- Modify: `/Users/ancplua/qyl/src/qyl.copilot/Adapters/QylCopilotAdapter.cs`

**Step 1: Add method after `GetAuthStatusAsync`**

Add after line ~142 (after `GetAuthStatusAsync`):

```csharp
/// <summary>
///     Exposes the inner <see cref="AIAgent"/> for AG-UI endpoint registration.
///     Used by <see cref="QylAgentBuilder.FromCopilotAdapter"/>.
/// </summary>
internal AIAgent GetInnerAgent() => _agent;
```

**Step 2: Build qyl.copilot**

Run: `dotnet build /Users/ancplua/qyl/src/qyl.copilot/qyl.copilot.csproj`
Expected: 0 errors.

**Step 3: Commit**

```bash
git -C /Users/ancplua/qyl add src/qyl.copilot/Adapters/QylCopilotAdapter.cs
git -C /Users/ancplua/qyl commit -m "feat(copilot): expose GetInnerAgent() for AG-UI wiring"
```

---

## Task 5: Implement `QylAgentBuilder`

**Files:**
- Create: `/Users/ancplua/qyl/src/qyl.copilot/Agents/QylAgentBuilder.cs`

**Step 1: Create the file**

```csharp
// =============================================================================
// qyl.copilot - QylAgentBuilder
// Factory for creating AIAgent instances ready for MapAGUI() endpoint exposure.
// Two paths:
//   1. GitHub Copilot   -> QylAgentBuilder.FromCopilotAdapter(adapter)
//   2. IChatClient      -> QylAgentBuilder.FromChatClient(chatClient, ...)
// Both paths return an AIAgent wired with InstrumentedChatClient for OTel spans.
// Note: qyl.servicedefaults.generator already intercepts AIAgent.RunAsync() at
// compile time, so UseOpenTelemetry() is NOT called here.
// =============================================================================

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using qyl.copilot.Adapters;

namespace qyl.copilot.Agents;

/// <summary>
///     Factory that produces <see cref="AIAgent"/> instances ready to be served
///     via AG-UI (<c>MapAGUI()</c>). Handles both the GitHub Copilot and
///     provider-agnostic <see cref="IChatClient"/> code paths.
/// </summary>
public static class QylAgentBuilder
{
    /// <summary>
    ///     Exposes the already-initialized agent inside a
    ///     <see cref="QylCopilotAdapter"/> for AG-UI endpoint registration.
    ///     Call this after the adapter has been created (e.g., via
    ///     <see cref="CopilotAdapterFactory.GetAdapterAsync"/>).
    /// </summary>
    /// <param name="adapter">The live adapter whose inner agent to expose.</param>
    /// <returns>The underlying <see cref="AIAgent"/>.</returns>
    public static AIAgent FromCopilotAdapter(QylCopilotAdapter adapter)
    {
        Guard.NotNull(adapter);
        return adapter.GetInnerAgent();
    }

    /// <summary>
    ///     Wraps an <see cref="IChatClient"/> as an <see cref="AIAgent"/>,
    ///     adding qyl OTel instrumentation via <see cref="InstrumentedChatClient"/>.
    /// </summary>
    /// <param name="chatClient">The upstream chat client (Ollama, OpenAI, etc.).</param>
    /// <param name="agentName">Identifies the agent in OTel spans and metadata.</param>
    /// <param name="description">Short description shown in AG-UI clients.</param>
    /// <param name="instructions">System instructions injected at the start of each session.</param>
    /// <param name="tools">Optional tools the agent may call during a run.</param>
    /// <param name="timeProvider">Time provider for OTel timestamps.</param>
    /// <returns>A fully wired <see cref="AIAgent"/>.</returns>
    public static AIAgent FromChatClient(
        IChatClient chatClient,
        string agentName = "qyl-assistant",
        string description = "qyl AI assistant",
        string? instructions = null,
        IReadOnlyList<AITool>? tools = null,
        TimeProvider? timeProvider = null)
    {
        Guard.NotNull(chatClient);

        // Wrap with OTel instrumentation at the IChatClient level (gen_ai.* spans + metrics)
        IChatClient instrumented = chatClient.UseQylInstrumentation(agentName, timeProvider);

        return instrumented.AsAIAgent(
            name: agentName,
            description: description,
            instructions: instructions,
            tools: tools is null ? null : [.. tools]);
    }
}
```

**Step 2: Build qyl.copilot**

Run: `dotnet build /Users/ancplua/qyl/src/qyl.copilot/qyl.copilot.csproj`
Expected: 0 errors.

**Step 3: Commit**

```bash
git -C /Users/ancplua/qyl add src/qyl.copilot/Agents/QylAgentBuilder.cs
git -C /Users/ancplua/qyl commit -m "feat(copilot): add QylAgentBuilder factory"
```

---

## Task 6: Implement `CopilotAguiEndpoints`

**Files:**
- Create: `/Users/ancplua/qyl/src/qyl.collector/Copilot/CopilotAguiEndpoints.cs`

**Step 1: Create the file**

```csharp
// =============================================================================
// qyl.collector - CopilotAguiEndpoints
// Registers and maps the AG-UI SSE endpoint for CopilotKit browser SDKs.
//
// Usage in Program.cs:
//   builder.Services.AddQylAgui();
//   // ... after var app = builder.Build():
//   app.MapQylAguiChat(agent);   // uses default path /api/v1/copilot/chat
// =============================================================================

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.collector.Copilot;

/// <summary>
///     Extension methods that expose a <see cref="AIAgent"/> over the AG-UI SSE
///     protocol, making it consumable by CopilotKit React / Angular / Vanilla JS.
/// </summary>
public static class CopilotAguiEndpoints
{
    /// <summary>
    ///     Registers the AG-UI infrastructure services (serialization, SSE helpers).
    ///     Call this on <c>builder.Services</c> before <c>builder.Build()</c>.
    /// </summary>
    public static IServiceCollection AddQylAgui(this IServiceCollection services)
    {
        Guard.NotNull(services);
        services.AddAGUI();
        return services;
    }

    /// <summary>
    ///     Maps the AG-UI endpoint at <paramref name="path"/>.
    ///     POST body: <c>{ threadId, runId, messages: [{role, content}], context? }</c>
    ///     Response: SSE stream of AG-UI events (RUN_STARTED … RUN_FINISHED).
    ///     Errors during streaming → RUN_ERROR event (not HTTP 5xx).
    ///     Cancellation → stream closes silently.
    /// </summary>
    /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
    /// <param name="agent">The agent to serve at this endpoint.</param>
    /// <param name="path">URL path (default: <c>/api/v1/copilot/chat</c>).</param>
    public static IEndpointRouteBuilder MapQylAguiChat(
        this IEndpointRouteBuilder endpoints,
        AIAgent agent,
        string path = "/api/v1/copilot/chat")
    {
        Guard.NotNull(endpoints);
        Guard.NotNull(agent);
        endpoints.MapAGUI(path, agent);
        return endpoints;
    }
}
```

**Step 2: Build qyl.collector**

Run: `dotnet build /Users/ancplua/qyl/src/qyl.collector/qyl.collector.csproj`
Expected: 0 errors.

**Step 3: Commit**

```bash
git -C /Users/ancplua/qyl add src/qyl.collector/Copilot/CopilotAguiEndpoints.cs
git -C /Users/ancplua/qyl commit -m "feat(collector): add CopilotAguiEndpoints (MapQylAguiChat)"
```

---

## Task 7: Wire AG-UI into collector `Program.cs`

**Files:**
- Modify: `/Users/ancplua/qyl/src/qyl.collector/Program.cs`

**Step 1: Add `AddQylAgui()` call**

After line ~180 (`builder.Services.AddQylCopilot(builder.Configuration);`), add:

```csharp
// AG-UI SSE infrastructure (CopilotKit-compatible protocol)
builder.Services.AddQylAgui();
```

**Step 2: Add `MapQylAguiChat()` call**

After line ~330 (`app.MapCopilotEndpoints();`), add:

```csharp
// AG-UI endpoint — wired to IChatClient when LLM provider is configured,
// or to GitHub Copilot agent when Copilot token is available.
// Priority: GitHub Copilot (richer OTel) > IChatClient.
{
    var adapterFactory = app.Services.GetRequiredService<CopilotAdapterFactory>();
    var chatClientOrNull = app.Services.GetService<IChatClient>();
    AIAgent? aguiAgent = null;

    if (chatClientOrNull is not null)
    {
        aguiAgent = QylAgentBuilder.FromChatClient(chatClientOrNull, agentName: "qyl-llm");
    }

    // Note: Copilot agent requires async init (auth flow), so we skip it here.
    // Copilot chat continues via /api/v1/copilot/chat (existing REST+SSE).
    if (aguiAgent is not null)
    {
        app.MapQylAguiChat(aguiAgent);
    }
}
```

**Step 3: Add missing usings**

At the top of `Program.cs`, add (if not already present):

```csharp
using Microsoft.Agents.AI;
using qyl.collector.Copilot;
using qyl.copilot.Agents;
```

**Step 4: Build collector**

Run: `dotnet build /Users/ancplua/qyl/src/qyl.collector/qyl.collector.csproj`
Expected: 0 errors.

**Step 5: Commit**

```bash
git -C /Users/ancplua/qyl add src/qyl.collector/Program.cs
git -C /Users/ancplua/qyl commit -m "feat(collector): wire AG-UI endpoint via QylAgentBuilder"
```

---

## Task 8: Implement `DeclarativeEngine`

**Files:**
- Create: `/Users/ancplua/qyl/src/qyl.copilot/Workflows/DeclarativeEngine.cs`

**Step 1: Create the file**

```csharp
// =============================================================================
// qyl.copilot - DeclarativeEngine
// Thin adapter over DeclarativeWorkflowBuilder: loads .yaml AdaptiveDialog
// workflows, wires an IChatClient via ChatClientResponseAgentProvider, and
// streams results as IAsyncEnumerable<StreamUpdate>.
//
// Produces the same StreamUpdate contract as WorkflowEngine (markdown-based),
// making both engines interchangeable from the collector layer's perspective.
//
// Usage:
//   var engine = new DeclarativeEngine(chatClient, agentName: "daily-qa");
//   await engine.LoadAsync(".qyl/workflows/daily-qa.yaml");
//   await foreach (var update in engine.ExecuteAsync(input, ct)) { ... }
// =============================================================================

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;
using qyl.protocol.Copilot;

namespace qyl.copilot.Workflows;

/// <summary>
///     Executes YAML <c>AdaptiveDialog</c> workflows using
///     <see cref="DeclarativeWorkflowBuilder"/> + an in-process executor,
///     streaming results as <see cref="StreamUpdate"/> events.
/// </summary>
public sealed class DeclarativeEngine
{
    private readonly string _agentName;
    private readonly IChatClient _chatClient;
    private Workflow? _workflow;

    /// <summary>
    ///     Creates a new engine.
    /// </summary>
    /// <param name="chatClient">The chat client used by the agent provider.</param>
    /// <param name="agentName">Identifies the agent in OTel spans.</param>
    public DeclarativeEngine(IChatClient chatClient, string agentName = "declarative-agent")
    {
        _chatClient = Guard.NotNull(chatClient);
        _agentName = Guard.NotNullOrWhiteSpace(agentName);
    }

    /// <summary>
    ///     Loads and compiles a YAML workflow file.
    ///     Must be called before <see cref="ExecuteAsync"/>.
    /// </summary>
    /// <param name="yamlFile">Absolute or relative path to the <c>.yaml</c> workflow.</param>
    public ValueTask LoadAsync(string yamlFile)
    {
        Guard.NotNullOrWhiteSpace(yamlFile);

        var agentProvider = new ChatClientResponseAgentProvider(_chatClient);
        var options = new DeclarativeWorkflowOptions(agentProvider);
        _workflow = DeclarativeWorkflowBuilder.Build<string>(yamlFile, options);
        return default;
    }

    /// <summary>
    ///     Executes the loaded workflow, streaming <see cref="StreamUpdate"/> events.
    /// </summary>
    /// <param name="input">Free-text input passed as the first user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     Stream of <see cref="StreamUpdate"/> events, ending with
    ///     <see cref="StreamUpdateKind.Completed"/> or <see cref="StreamUpdateKind.Error"/>.
    /// </returns>
    public async IAsyncEnumerable<StreamUpdate> ExecuteAsync(
        string input,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_workflow is null)
            throw new InvalidOperationException("Call LoadAsync() before ExecuteAsync().");

        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var run = await InProcessExecution.Default
            .RunStreamingAsync(_workflow, input, cancellationToken: ct)
            .ConfigureAwait(false);

        var updates = new List<StreamUpdate>();
        Exception? caughtException = null;

        var enumerator = run.WatchStreamAsync(ct).GetAsyncEnumerator(ct);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                WorkflowEvent evt = enumerator.Current;
                switch (evt.Data)
                {
                    case AgentResponseUpdateEvent updateEvent:
                        var text = updateEvent.Update.Text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            updates.Add(new StreamUpdate
                            {
                                Kind = StreamUpdateKind.Content,
                                Content = text,
                                Timestamp = TimeProvider.System.GetUtcNow()
                            });
                        }
                        break;

                    case AgentResponseEvent:
                        // Final agent response — the loop will finish naturally
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            caughtException = ex;
        }
        catch (HttpRequestException ex)
        {
            caughtException = ex;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        foreach (StreamUpdate update in updates)
            yield return update;

        if (caughtException is not null)
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Error,
                Error = caughtException.Message,
                Timestamp = TimeProvider.System.GetUtcNow()
            };
        }
        else
        {
            yield return new StreamUpdate
            {
                Kind = StreamUpdateKind.Completed,
                Timestamp = TimeProvider.System.GetUtcNow()
            };
        }
    }

    // ── Private: IChatClient → ResponseAgentProvider adapter ─────────────────

    /// <summary>
    ///     Bridges any <see cref="IChatClient"/> into the
    ///     <see cref="ResponseAgentProvider"/> contract required by
    ///     <see cref="DeclarativeWorkflowOptions"/>.
    ///     Maintains in-memory conversation history per conversation ID.
    /// </summary>
    private sealed class ChatClientResponseAgentProvider(IChatClient chatClient)
        : ResponseAgentProvider
    {
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();
        private readonly ConcurrentDictionary<string, ChatMessage> _messages = new();

        public override Task<string> CreateConversationAsync(CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid().ToString("N");
            _conversations[id] = [];
            return Task.FromResult(id);
        }

        public override Task<ChatMessage> CreateMessageAsync(
            string conversationId,
            ChatMessage conversationMessage,
            CancellationToken cancellationToken = default)
        {
            var messageId = Guid.NewGuid().ToString("N");
            // Store message with a synthetic ID in AdditionalProperties
            var stored = new ChatMessage(conversationMessage.Role, conversationMessage.Contents)
            {
                MessageId = messageId
            };

            _messages[messageId] = stored;
            if (_conversations.TryGetValue(conversationId, out var history))
                history.Add(stored);

            return Task.FromResult(stored);
        }

        public override Task<ChatMessage> GetMessageAsync(
            string conversationId,
            string messageId,
            CancellationToken cancellationToken = default) =>
            _messages.TryGetValue(messageId, out var msg)
                ? Task.FromResult(msg)
                : Task.FromException<ChatMessage>(
                    new KeyNotFoundException($"Message {messageId} not found."));

        public override async IAsyncEnumerable<AgentResponseUpdate> InvokeAgentAsync(
            string agentId,
            string? agentVersion,
            string? conversationId,
            IEnumerable<ChatMessage>? messages,
            IDictionary<string, object?>? inputArguments,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Build conversation history from stored + incoming messages
            var history = new List<ChatMessage>();
            if (conversationId is not null &&
                _conversations.TryGetValue(conversationId, out var stored))
            {
                history.AddRange(stored);
            }
            if (messages is not null)
                history.AddRange(messages);

            await foreach (var update in chatClient
                .GetStreamingResponseAsync(history, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                var text = update.Text;
                if (!string.IsNullOrEmpty(text))
                    yield return new AgentResponseUpdate(ChatRole.Assistant, text);
            }
        }

        public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
            string conversationId,
            int? limit = null,
            string? after = null,
            string? before = null,
            bool newestFirst = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_conversations.TryGetValue(conversationId, out var history))
                yield break;

            IEnumerable<ChatMessage> items = newestFirst
                ? ((IEnumerable<ChatMessage>)history).Reverse()
                : history;

            if (limit.HasValue)
                items = items.Take(limit.Value);

            foreach (ChatMessage message in items)
                yield return message;

            await Task.CompletedTask.ConfigureAwait(false); // satisfy async requirement
        }
    }
}
```

**Step 2: Build qyl.copilot**

Run: `dotnet build /Users/ancplua/qyl/src/qyl.copilot/qyl.copilot.csproj`
Expected: 0 errors.

If you see `CS0246` for `AgentResponseUpdate` or `WorkflowEvent`: confirm `Microsoft.Agents.AI.Workflows.Declarative` is restored and namespaces are `Microsoft.Agents.AI` + `Microsoft.Agents.AI.Workflows`.

**Step 3: Commit**

```bash
git -C /Users/ancplua/qyl add src/qyl.copilot/Workflows/DeclarativeEngine.cs
git -C /Users/ancplua/qyl commit -m "feat(copilot): add DeclarativeEngine (YAML workflow runtime)"
```

---

## Task 9: Smoke test — build full solution

**Step 1: Build everything**

Run: `dotnet build /Users/ancplua/qyl/qyl.sln`
Expected: Build succeeded with 0 errors.

If errors occur, check:
- Missing `using` directives (`Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`)
- `Guard.NotNullOrWhiteSpace` — if missing, replace with `ArgumentException.ThrowIfNullOrWhiteSpace()`
- `IChatClient.GetStreamingResponseAsync` signature — in M.E.AI 10.x it may be `GetStreamingResponseAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken)`, adjust accordingly

**Step 2: Check for diagnostics**

Run: (use mcp__rider__get_file_problems on changed files)
Expected: No errors or suppressions needed.

**Step 3: Commit if clean**

```bash
git -C /Users/ancplua/qyl add -A
git -C /Users/ancplua/qyl commit -m "chore: verify full solution build (AG-UI + DeclarativeEngine)"
```

---

## Task 10: Verify AG-UI endpoint works (optional Playwright smoke test)

**Step 1: Start the collector**

```bash
cd /Users/ancplua/qyl && dotnet run --project src/qyl.collector/qyl.collector.csproj &
```

Wait for: `[qyl] Application started and listening on port 5100`

**Step 2: Curl the AG-UI endpoint**

```bash
curl -N -X POST http://localhost:5100/api/v1/copilot/chat \
  -H "Content-Type: application/json" \
  -d '{"threadId":"t1","runId":"r1","messages":[{"role":"user","content":"say hello"}]}'
```

Expected SSE stream:
```
data: {"type":"RUN_STARTED","runId":"r1","threadId":"t1"}
data: {"type":"TEXT_MESSAGE_START","messageId":"...","role":"assistant"}
data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":"Hello"}
...
data: {"type":"TEXT_MESSAGE_END","messageId":"..."}
data: {"type":"RUN_FINISHED","runId":"r1","threadId":"t1"}
```

> If `IChatClient` is not configured (no `QYL_LLM_*` env vars), the endpoint returns a 404 (not registered). Configure `QYL_LLM_PROVIDER=openai` + `QYL_LLM_API_KEY` or `QYL_LLM_PROVIDER=ollama` + `QYL_LLM_BASE_URL` to activate.

**Step 3: Stop the collector**

```bash
pkill -f "qyl.collector"
```

---

## Summary of changed files

| File | Action | Purpose |
|------|--------|---------|
| `Version.props` | Modify | Add 3 version variables |
| `Directory.Packages.props` | Modify | Add 3 `PackageVersion` entries |
| `qyl.copilot.csproj` | Modify | Add `Hosting` + `Workflows.Declarative` refs |
| `qyl.collector.csproj` | Modify | Add `Hosting.AGUI.AspNetCore` ref |
| `QylCopilotAdapter.cs` | Modify | Add `GetInnerAgent()` internal method |
| `Agents/QylAgentBuilder.cs` | **Create** | Fluent `AIAgent` factory |
| `Copilot/CopilotAguiEndpoints.cs` | **Create** | `AddQylAgui()` + `MapQylAguiChat()` |
| `Workflows/DeclarativeEngine.cs` | **Create** | YAML workflow runtime |
| `Program.cs` | Modify | Wire `AddQylAgui()` + `MapQylAguiChat()` |

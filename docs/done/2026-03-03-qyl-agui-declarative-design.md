# qyl — AG-UI + Declarative Workflows Design

**Date:** 2026-03-03
**Status:** Approved

## Goal

Complete the observable AI stack: browser → AG-UI SSE → qyl OTel instrumentation → LLM.
Add two new runtimes to qyl: AG-UI endpoint (CopilotKit-compatible) and declarative YAML workflows.

## Architecture

```
CopilotKit React / Angular / Vanilla JS
       ↕  AG-UI protocol (SSE)
   qyl.collector  MapQylAguiChat("/api/v1/copilot/chat")
       ↕
   QylAgentBuilder → AIAgent (instrumented)
       ↕  InstrumentedChatClient (OTel spans + CopilotMetrics)
   GitHub Copilot / Azure OpenAI / Ollama / GitHub Models
       ↕  MCP tools via InstrumentedAIFunction spans
```

## New Files

### qyl.copilot

| File | Purpose |
|------|---------|
| `Agents/QylAgentBuilder.cs` | Fluent `AIAgent` factory: wraps any provider with `InstrumentedChatClient` + optional `UseOpenTelemetry()` |
| `Workflows/DeclarativeEngine.cs` | Thin adapter over `DeclarativeWorkflowBuilder`: loads `.yaml` files, wires `InstrumentedChatClient`, streams `StreamUpdate` |

### qyl.collector

| File | Purpose |
|------|---------|
| `Copilot/CopilotAguiEndpoints.cs` | `MapQylAguiChat()` — registers `AddAGUI()` + `MapAGUI("/api/v1/copilot/chat", agent)` |

## New Packages

| Package | Used by | Provides |
|---------|---------|---------|
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | qyl.collector | `AddAGUI()`, `MapAGUI()` |
| `Microsoft.Agents.AI.GitHub.Copilot` | qyl.copilot | `CopilotClient.AsAIAgent()` |
| `Microsoft.Agents.AI.Hosting` | qyl.copilot | `UseOpenTelemetry()` builder extension |
| `Microsoft.Agents.AI.Workflows.Declarative` | qyl.copilot | `DeclarativeWorkflowBuilder`, PowerFx engine |

## QylAgentBuilder API

```csharp
// From LlmProviderFactory-registered IChatClient
var agent = QylAgentBuilder
    .FromChatClient(chatClient)
    .WithInstrumentation(agentName: "qyl-copilot")
    .WithOpenTelemetry()
    .Build();

// From GitHub Copilot
var agent = QylAgentBuilder
    .FromCopilot(copilotClient, sessionConfig)
    .WithOpenTelemetry()
    .Build();
```

## AG-UI Data Flow

```
POST /api/v1/copilot/chat
  Body: { threadId, runId, messages: [{role, content}], context? }
  ↓
  MapAGUI() deserializes RunAgentInput
  ↓
  AIAgent.RunStreamingAsync(messages, session)
    ↓ InstrumentedChatClient.GetStreamingResponseAsync()
      → gen_ai.chat span, token usage metrics
  ↑
  SSE stream:
    data: {"type":"RUN_STARTED","runId":"...","threadId":"..."}
    data: {"type":"TEXT_MESSAGE_START","messageId":"...","role":"assistant"}
    data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"...","delta":"Hello"}
    ...
    data: {"type":"TEXT_MESSAGE_END","messageId":"..."}
    data: {"type":"RUN_FINISHED","runId":"...","threadId":"..."}
```

Errors during streaming → `RUN_ERROR` event (not HTTP 5xx).
Cancellation → stream closes, no `RUN_ERROR`.

## Declarative Workflow API

```csharp
// Load a YAML AdaptiveDialog workflow, wire InstrumentedChatClient
var engine = new DeclarativeEngine(chatClient, agentName: "daily-qa");
await engine.LoadAsync(".qyl/workflows/daily-qa.yaml");
await foreach (var update in engine.ExecuteAsync(input, ct))
{
    // StreamUpdate events, same contract as WorkflowEngine
}
```

YAML format:
```yaml
kind: AdaptiveDialog
actions:
  - kind: InvokeMcpToolExecutor
    toolName: qyl_get_errors
  - kind: ConditionGroup
    condition: "=CountRows(errors) > 0"
    actions:
      - kind: SendActivity
        activity: "Found ${CountRows(errors)} errors"
```

Declarative engine sits alongside existing `WorkflowEngine` (markdown).
Both produce `IAsyncEnumerable<StreamUpdate>` — the collector layer is the same.

## Error Handling

- `InstrumentedChatClient`: catches `HttpRequestException` + `JsonException` only
- AG-UI transport errors → `RUN_ERROR` SSE event
- Declarative action failures → `StreamUpdate { Kind = Error }` → caller wraps in `RUN_ERROR`
- Cancellation: status stays `Unset` (not Error) at both IChatClient and AIAgent level

## Explicitly Out of Scope

- Unit test infrastructure (use Playwright + Docker for integration verification)
- A2A multi-agent routing (phase 2)
- Agent-as-MCP-tool surface (phase 2)

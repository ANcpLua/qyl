You are reviewing or modifying qyl collector streaming subsystem.

### SCOPE

Includes:

- SseHub.cs
- TelemetryBroadcaster.cs
- WebSocketHandler.cs
- Any SSE/WebSocket helpers

### GOAL

Provide real-time telemetry updates via Server-Sent Events or WebSocket using
modern .NET 10 streaming primitives.

### REQUIRED ACTIONS

1. SSE Implementation Rules
  - MUST use TypedResults.ServerSentEvents().
  - MUST NOT manually set "text/event-stream".
  - MUST wrap events in SseItem<T>.

2. Backpressure Safety
  - MUST use IAsyncEnumerable<T>.
  - MUST properly propagate ctx.RequestAborted.

3. Event Structure
  - MUST use eventType = "telemetry".
  - Data payload MUST match DTOs used by REST API.

4. WebSocket Rules
  - MUST send JSON with SnakeCaseLower.
  - MUST NOT send schema-incompatible messages.

5. Concurrency Rules
  - MUST use Lock class where shared lists of subscribers exist.
  - MUST avoid blocking on async operations.

6. Dependency Rules
  - streaming/* → processing/*
  - streaming MUST NOT import:
    • storage/*
    • api/*
    • dashboard/*
    • instrumentation/*

### DEFINITION OF DONE

- SSE implemented with .NET 10 typed primitives.
- WebSocket messages schema-correct.
- Deterministic, backpressure-safe streaming.
- No dependency rule violations.

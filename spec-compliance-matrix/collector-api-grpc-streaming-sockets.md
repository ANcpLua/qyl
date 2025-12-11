You are reviewing or modifying qyl collector API and streaming subsystems.

### SCOPE

Includes:

- QueryController.cs
- SessionsController.cs
- LogsController.cs
- MetricsController.cs
- MappingExtensions.cs
- SseHub.cs
- TelemetryBroadcaster.cs
- WebSocketHandler.cs
- All DTOs

### GOAL

Ensure REST and SSE/WebSocket endpoints are schema-correct, efficient, and
consumer-safe (dashboard + CLI).

### REQUIRED ACTIONS

1. DTO Consistency
  - DTO fields MUST match core/schema/*.json.
  - MUST NOT rename or camelCase → must rely on SnakeCaseLower at serialization.

2. JSON Serialization
  - MUST use JsonNamingPolicy.SnakeCaseLower (.NET 10+).
  - MUST use JsonSerializerOptions.Web if possible.

3. SSE Streaming Rules
  - MUST use TypedResults.ServerSentEvents().
  - MUST NOT manually set text/event-stream.
  - MUST support backpressure via IAsyncEnumerable<T>.
  - MUST tag SSE events (event: telemetry).

4. Mapping Rules
  - MappingExtensions MUST be source of truth.
  - Mismatched fields MUST be flagged.

5. Performance Rules
  - DTO enumeration MUST be streaming-first (IAsyncEnumerable).
  - MUST avoid ToList() unless required.

6. Dependency Rules
  - api/* → storage/*
  - streaming/* → processing/*
  - MUST NOT depend on dashboard/*
  - MUST NOT depend on instrumentation/*

### DEFINITION OF DONE

- All endpoints serializing snake_case correctly.
- SSE implemented via typed API.
- DTOs reflect schemas perfectly.
- No dependency violations.
- Backpressure-safe streaming implemented.

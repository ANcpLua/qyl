# C4: qyl.collector

> OTLP/HTTP ingestion, storage, query, and real-time streaming service

## Overview

The collector is the core backend service that receives telemetry via OTLP/HTTP, stores it in DuckDB, aggregates
sessions, and streams updates to clients via SSE/WebSocket. It runs as a single Native AOT binary.

## Key Classes/Modules

| Class               | Purpose                                        | Location                         |
|---------------------|------------------------------------------------|----------------------------------|
| `GenAiExtractor`    | Extract GenAI semconv (v1.38 + v1.36 fallback) | `Ingestion/GenAiExtractor.cs`    |
| `SchemaNormalizer`  | Normalize deprecated attrs to v1.38            | `Ingestion/SchemaNormalizer.cs`  |
| `SessionAggregator` | Real-time session statistics                   | `Query/SessionAggregator.cs`     |
| `DuckDbStore`       | DuckDB persistence layer                       | `Storage/DuckDbStore.cs`         |
| `SseHub`            | Server-Sent Events broadcaster                 | `Realtime/SseHub.cs`             |
| `TokenAuth`         | JWT-like token validation                      | `Auth/TokenAuth.cs`              |
| `McpServer`         | MCP tools for AI assistants                    | `Mcp/McpServer.cs`               |
| `ConsoleBridge`     | Console output redirection                     | `ConsoleBridge/ConsoleBridge.cs` |

## Dependencies

**Internal:** qyl.grpc (models), qyl.agents.telemetry (GenAI constants)

**External:** DuckDB.NET, System.Text.Json, Microsoft.AspNetCore

## Data Flow

```
OTLP/HTTP Request
    ↓
Parse JSON (OtlpTypes)
    ↓
Extract GenAI attrs ──→ Normalize schema
    ↓
SessionAggregator ──→ DuckDbStore
    ↓
SseHub broadcasts ──→ Dashboard
```

## Patterns Used

- **Static Utility**: GenAiExtractor is stateless pure functions
- **Ring Buffer**: Session stats use ConcurrentDictionary
- **Observer**: SseHub broadcasts via events
- **Adapter**: OtlpTypes maps JSON ↔ internal models

# Architecture

## System Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              USER APPLICATIONS                               │
│                                                                             │
│  Standard OpenTelemetry SDK — NO custom qyl SDK needed                      │
│  services.AddOpenTelemetry()                                                │
│      .WithTracing(b => b.AddOtlpExporter(o =>                               │
│          o.Endpoint = new Uri("http://qyl:4318")));                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                   │
                                   │ OTLP (HTTP :4318 / gRPC :4317)
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                               qyl.collector                                  │
│                         (Backend · Native AOT)                               │
│                                                                             │
│  • OTLP ingestion (HTTP + gRPC)     • REST API (/api/v1/*)                  │
│  • gen_ai.* extraction              • SSE streaming                         │
│  • DuckDB storage                   • Health endpoints                      │
└─────────────────────────────────────────────────────────────────────────────┘
             │                                     │
             │ HTTP (REST)                         │ HTTP (REST + SSE)
             ▼                                     ▼
┌─────────────────────┐               ┌─────────────────────┐
│       qyl.mcp       │               │    qyl.dashboard    │
│    (MCP server)     │               │     (React UI)      │
│   stdio transport   │               │  REST + SSE client  │
└─────────────────────┘               └─────────────────────┘
                    │
                    ▼ stdio
            ┌───────────────┐
            │ Claude / AI   │
            └───────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                               qyl.protocol                                   │
│                          (Shared Contracts · LEAF)                           │
│                                                                             │
│  Primitives: SessionId, UnixNano                                            │
│  Models: SpanRecord, SessionSummary, GenAiSpanData, TraceNode               │
│  Attributes: GenAiAttributes (OTel 1.38 constants)                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Dependency Rules

```
qyl.dashboard ──HTTP──► qyl.collector ◄──HTTP── qyl.mcp
                               │
                               ▼
                         qyl.protocol
```

| Layer         | MAY reference | MUST NOT reference         |
|---------------|---------------|----------------------------|
| qyl.protocol  | BCL only      | Any qyl.*                  |
| qyl.collector | qyl.protocol  | qyl.mcp, qyl.dashboard     |
| qyl.mcp       | qyl.protocol  | qyl.collector (HTTP only!) |
| qyl.dashboard | Nothing .NET  | Any .csproj                |

## Data Flow

```
1. User app creates span with gen_ai.* attributes
2. OTel SDK exports via OTLP to collector
3. Collector extracts gen_ai.*, normalizes deprecated attrs
4. Collector stores in DuckDB (promoted columns + JSON overflow)
5. Collector broadcasts via SSE
6. Dashboard receives SSE, invalidates TanStack Query cache
7. MCP tools query collector REST API for AI agent context
```

## OTel Semantic Conventions (v1.38)

### Current Attributes

| Key                          | Type   | Example                                 |
|------------------------------|--------|-----------------------------------------|
| `gen_ai.operation.name`      | string | `chat`, `text_completion`, `embeddings` |
| `gen_ai.provider.name`       | string | `anthropic`, `openai`, `google`         |
| `gen_ai.request.model`       | string | `claude-3-opus`                         |
| `gen_ai.response.model`      | string | `claude-3-opus-20240229`                |
| `gen_ai.usage.input_tokens`  | int    | `1500`                                  |
| `gen_ai.usage.output_tokens` | int    | `500`                                   |

### Migration (normalized on ingest)

| Deprecated                       | Current                      |
|----------------------------------|------------------------------|
| `gen_ai.system`                  | `gen_ai.provider.name`       |
| `gen_ai.usage.prompt_tokens`     | `gen_ai.usage.input_tokens`  |
| `gen_ai.usage.completion_tokens` | `gen_ai.usage.output_tokens` |

## Single Source Rules

| Concern         | File                                             | Notes                |
|-----------------|--------------------------------------------------|----------------------|
| DuckDB Schema   | `src/qyl.collector/Storage/DuckDbSchema.cs`      | All DDL here         |
| GenAI Constants | `src/qyl.protocol/Attributes/GenAiAttributes.cs` | All OTel keys        |
| Session Queries | `src/qyl.collector/Query/SessionQueryService.cs` | SQL aggregation      |
| Guard Clauses   | `src/Shared/Throw/Throw.cs`                      | Injected via MSBuild |

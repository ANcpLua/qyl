# qyl. Architecture Reference (AI-Optimized)

> **Version**: 2.0.0 | **OTel SemConv**: 1.38 | **.NET**: 10.0

---

## Quick Reference

```
PRODUCERS (instrumentation/)     →  COLLECTOR (qyl.collector/)  →  CONSUMERS
  .NET / Python / TypeScript         Receivers → Processing        Dashboard (React 19)
                                     → Storage → API/Streaming     CLI / MCP
                                            ↓
                                    TypeSpec Schema (54 files)
                                    core/specs/
```

---

## OTel 1.38 GenAI Attributes (MANDATORY)

### Required Attributes

| Attribute                    | Type   | Notes                                                    |
|------------------------------|--------|----------------------------------------------------------|
| `gen_ai.provider.name`       | string | `openai`, `anthropic`, `gcp.gemini`, `aws.bedrock`, etc. |
| `gen_ai.request.model`       | string | Model ID for request                                     |
| `gen_ai.response.model`      | string | Model ID in response                                     |
| `gen_ai.operation.name`      | string | `chat`, `text_completion`, `embeddings`, `invoke_agent`  |
| `gen_ai.usage.input_tokens`  | int    | Token count for prompt/input                             |
| `gen_ai.usage.output_tokens` | int    | Token count for completion/output                        |

### Optional Attributes

| Attribute                        | Type     | Notes                     |
|----------------------------------|----------|---------------------------|
| `gen_ai.agent.id`                | string   | Agent identifier          |
| `gen_ai.agent.name`              | string   | Human-readable agent name |
| `gen_ai.conversation.id`         | string   | Session/thread ID         |
| `gen_ai.tool.name`               | string   | Tool utilized by agent    |
| `gen_ai.tool.call.id`            | string   | Tool call identifier      |
| `gen_ai.request.max_tokens`      | int      | Max tokens for response   |
| `gen_ai.request.temperature`     | double   | Temperature setting       |
| `gen_ai.response.id`             | string   | Completion identifier     |
| `gen_ai.response.finish_reasons` | string[] | Stop reasons              |

### Deprecated Attributes (REJECT)

| ❌ Deprecated                     | ✅ Use Instead                | Removed In |
|----------------------------------|------------------------------|------------|
| `gen_ai.system`                  | `gen_ai.provider.name`       | 1.37       |
| `gen_ai.usage.prompt_tokens`     | `gen_ai.usage.input_tokens`  | 1.27       |
| `gen_ai.usage.completion_tokens` | `gen_ai.usage.output_tokens` | 1.27       |
| `gen_ai.prompt`                  | Event API                    | obsoleted  |
| `gen_ai.completion`              | Event API                    | obsoleted  |
| `gen_ai.openai.request.seed`     | `gen_ai.request.seed`        | 1.30       |

---

## .NET 10 API Rules (MANDATORY)

### Must Use

| API                               | Usage                    | Location                      |
|-----------------------------------|--------------------------|-------------------------------|
| `CountBy<T>()`                    | Session statistics       | `SessionAggregator.cs`        |
| `AggregateBy<T>()`                | Token aggregation        | `SessionAggregator.cs`        |
| `Lock`                            | Thread-safe sync         | `SessionAggregator`, `SseHub` |
| `Task.WhenEach()`                 | Parallel ingestion       | Receivers                     |
| `SearchValues<char>`              | SIMD delimiter detection | `GenAiExtractor.cs`           |
| Direct `StartsWith`               | Prefix detection (gen_ai.*, agents.*) | `GenAiExtractor.cs`    |
| `OrderedDictionary<K,V>`          | Deterministic ordering   | Storage                       |
| `TypedResults.ServerSentEvents()` | SSE streaming            | `SseHub.cs`                   |
| `JsonNamingPolicy.SnakeCaseLower` | API serialization        | Controllers                   |

### Anti-Patterns (REJECT)

| ❌ Bad                                            | ✅ Good                                            | Why                 |
|--------------------------------------------------|---------------------------------------------------|---------------------|
| `private readonly object _lock = new();`         | `private readonly Lock _lock = new();`            | .NET 9+ Lock class  |
| `.GroupBy().ToDictionary(g => g.Count())`        | `.CountBy()`                                      | .NET 9+ CountBy     |
| `ctx.Response.ContentType = "text/event-stream"` | `TypedResults.ServerSentEvents()`                 | .NET 10+ SSE        |
| `key.Contains("value")`                          | `key.Contains("value", StringComparison.Ordinal)` | Explicit comparison |
| `new Dictionary<K,V>()` when order matters       | `new OrderedDictionary<K,V>()`                    | Preserve order      |
| `Task.WhenAny` loops                             | `Task.WhenEach()`                                 | Modern pattern      |
| Manual SSE formatting                            | `SseItem<T>` + `IAsyncEnumerable<T>`              | Typed streaming     |

---

## Dependency Rules

### Allowed

```
instrumentation/* → core/*
receivers/*       → processing/*
processing/*      → storage/abstractions/*
api/*             → storage/abstractions/*
streaming/*       → processing/*
dashboard         → collector/api (HTTP), collector/streaming (SSE/WS)
cli               → collector/api (HTTP)
```

### Prohibited

```
core/*      ✗→ instrumentation/*, collector/*
collector/* ✗→ dashboard, cli
dashboard   ✗↔ cli
receivers/* ✗→ storage/*, api/*
streaming/* ✗→ storage/*
```

---

## Key Files

| Purpose             | Path                                            |
|---------------------|-------------------------------------------------|
| Entry Point         | `src/qyl.collector/Program.cs`                  |
| GenAI Extraction    | `src/qyl.collector/Ingestion/GenAiExtractor.cs` |
| Session Aggregation | `src/qyl.collector/Query/SessionAggregator.cs`  |
| SSE Streaming       | `src/qyl.collector/Realtime/SseHub.cs`          |
| Storage             | `src/qyl.collector/Storage/DuckDbStore.cs`      |
| TypeSpec Schema     | `src/qyl.dashboard/src/specs/main.tsp`          |

---

## TypeSpec Schema (54 files)

```
src/qyl.dashboard/src/specs/
├── main.tsp, tspconfig.yaml
├── common/     (3)  types, errors, pagination
├── otel/       (5)  enums, resource, span, logs, metrics
├── api/        (2)  routes, streaming
└── domains/
    ├── ai/         (3)  genai, code, cli
    ├── security/   (4)  network, dns, tls, security-rule
    ├── transport/  (7)  http, rpc, messaging, url, signalr, kestrel, user-agent
    ├── infra/      (7)  host, container, k8s, cloud, faas, os, webengine
    ├── runtime/    (5)  process, system, thread, dotnet, aspnetcore
    ├── data/       (5)  db, file, elasticsearch, vcs, artifact
    ├── observe/    (8)  session, browser, feature-flags, exceptions, otel, log, error, test
    ├── ops/        (2)  cicd, deployment
    └── identity/   (2)  user, geo
```

---

## API Endpoints

| Method | Endpoint           | Purpose              |
|--------|--------------------|----------------------|
| POST   | `/v1/traces`       | OTLP trace ingest    |
| POST   | `/v1/logs`         | OTLP log ingest      |
| POST   | `/v1/metrics`      | OTLP metric ingest   |
| GET    | `/api/v1/sessions` | List sessions        |
| GET    | `/api/v1/live`     | SSE real-time stream |
| GET    | `/health`          | Health check         |

---

## GenAI Provider Values (1.38)

```
openai, anthropic, gcp.gemini, gcp.vertex_ai, gcp.gen_ai,
azure.ai.openai, azure.ai.inference, aws.bedrock,
cohere, ibm.watsonx.ai, perplexity, x_ai, deepseek, groq, mistral_ai
```

---

## Operation Names (1.38)

```
chat, text_completion, embeddings, generate_content,
create_agent, invoke_agent, execute_tool
```

---

## Tech Stack

| Layer    | Technology                            |
|----------|---------------------------------------|
| Backend  | .NET 10 Native AOT                    |
| Frontend | React 19, TypeScript 5.9, Tailwind v4 |
| Storage  | DuckDB embedded, MemoryStore          |
| Schema   | TypeSpec 1.7.0 → OpenAPI 3.1          |
| Build    | NUKE 10.x                             |

---

## Processing Pipeline

```
OTLP Request
    │
    ▼
┌─────────────────┐
│ HTTP/gRPC       │  Task.WhenEach()
│ Receivers       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ BatchProcessor  │  Index(), Parallel.ForAsync
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Attribute       │  snake_case, drop deprecated
│ Normalizer      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ GenAiExtractor  │  StartsWith for prefix detection
│                 │  Validate: provider, model, tokens
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Session         │  Lock, CountBy(), AggregateBy()
│ Aggregator      │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌───────┐ ┌───────┐
│Storage│ │SSE Hub│  TypedResults.ServerSentEvents()
└───────┘ └───────┘
```

---

## Serialization Rules

- All JSON: `JsonNamingPolicy.SnakeCaseLower`
- All attributes: `snake_case`
- DTOs must match TypeSpec schema exactly
- No field renaming in code

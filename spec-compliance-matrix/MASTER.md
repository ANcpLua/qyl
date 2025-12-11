# qyl Framework Master Compliance Rules

You are reviewing or modifying ANY part of the qyl framework.

The qyl framework contains **PRODUCERS → RECEIVERS → PROCESSING → STORAGE → API/STREAMING → CONSUMERS**, plus SUPPORTING modules.

Your job is to ensure correctness, consistency, dependency integrity, semconv alignment, schema alignment, and .NET 10 API compliance across the entire system.

---

## 1. Internal Architecture Scope

You must understand and respect the layer boundaries:

### PRODUCERS

| Module | Description |
|--------|-------------|
| `instrumentation/dotnet` | .NET SDK, LogProperties, TagName, ILogEnricher |
| `instrumentation/python` | Pydantic models |
| `instrumentation/typescript` | TS instrumentation |

### CORE

| Module | Description |
|--------|-------------|
| `core/specs/*` | TypeSpec source - single source of truth |
| `core/openapi/` | Generated OpenAPI 3.1 |
| `core/schemas/` | Generated JSON Schema |
| `core/generated/dotnet` | Kiota C# client |
| `core/generated/python` | Kiota Python client |
| `core/generated/typescript` | Kiota TypeScript client |

### RECEIVERS

| Module | Description |
|--------|-------------|
| `receivers/http` | HttpReceiver.cs, JSON ingestion |
| `receivers/grpc` | GrpcReceiver.cs, Protobuf ingestion |

### PROCESSING

- BatchProcessor
- FilterProcessor
- AttributeNormalizer
- GenAiExtractor
- SessionAggregator

### STORAGE

| Module | Description |
|--------|-------------|
| `storage/abstractions` | IStore, ISessionStore |
| `storage/memory` | MemoryStore.cs, OrderedDictionary |
| `storage/duckdb` | DuckDbStore.cs, SchemaInit.sql |

### API

- QueryController
- SessionsController
- LogsController
- MetricsController
- MappingExtensions
- DTOs

### STREAMING

- SseHub
- TelemetryBroadcaster
- WebSocketHandler
- .NET 10: `TypedResults.ServerSentEvents`, `SseItem<T>`, `IAsyncEnumerable<T>`

### CONSUMERS

| Module | Description |
|--------|-------------|
| `dashboard/` | React 19 + TypeScript 5.9 + Tailwind 4 |
| `cli/` | qylCli.cs |

### SUPPORTING

| Module | Description |
|--------|-------------|
| `analyzers/` | Roslyn |
| `examples/` | dotnet, python, typescript samples |

---

## 2. Global Semantic Convention Rules (OTel 1.38)

You must ALWAYS enforce:

### Required Attributes

| Attribute | Purpose |
|-----------|---------|
| `gen_ai.provider.name` | Provider identifier |
| `gen_ai.request.model` | Model ID for request |
| `gen_ai.response.model` | Model ID in response |
| `gen_ai.operation.name` | Operation type |
| `gen_ai.usage.input_tokens` | Input token count |
| `gen_ai.usage.output_tokens` | Output token count |
| `gen_ai.usage.total_tokens` | Total token count |

### Deprecated (MUST REJECT)

| Deprecated | Replacement |
|------------|-------------|
| `gen_ai.system` | `gen_ai.provider.name` |
| `gen_ai.usage.prompt_tokens` | `gen_ai.usage.input_tokens` |
| `gen_ai.usage.completion_tokens` | `gen_ai.usage.output_tokens` |

**ALL attribute names MUST be snake_case.**

---

## 3. Schema Rules

Schema under `core/schema` is the ONLY source of truth.

All modules MUST:

- Match schema EXACTLY
- Emit NO additional fields
- Consume NO fields not in schema
- Ensure cross-language consistency (.NET, Python, TS produce identical shapes)

---

## 4. Module-Specific .NET 10 API Rules

### Receivers / Processing

| API | Purpose |
|-----|---------|
| `Task.WhenEach()` | Process tasks as they complete |
| `IUtf8SpanParsable<T>` | Zero-allocation parsing |
| `Lock` | Modern synchronization |
| `CountBy()`, `AggregateBy()`, `Index()` | Single-pass aggregation |
| `SearchValues<char>` | SIMD character detection (delimiters, hex validation) |
| Direct `StartsWith` | Prefix detection for gen_ai.*, agents.*, etc. |

### Storage

| API | Purpose |
|-----|---------|
| `OrderedDictionary<K,V>` | Insertion-order preservation |

### API

| API | Purpose |
|-----|---------|
| `JsonNamingPolicy.SnakeCaseLower` | snake_case serialization |
| `JsonSerializerOptions.Web` | Web-optimized defaults |

### Streaming

| API | Purpose |
|-----|---------|
| `TypedResults.ServerSentEvents()` | SSE endpoint |
| `SseItem<T>` | Typed SSE item |
| `IAsyncEnumerable<T>` | Async streaming |

### Instrumentation

| API | Purpose |
|-----|---------|
| `[LogProperties]` | Structured logging |
| `[TagName]` | Custom tag naming |
| `ILogEnricher` | Log enrichment |
| `ILatencyContextProvider` | Latency tracking |
| `IOutgoingRequestContext` | Request context |

### Anti-Patterns (MUST REJECT)

| Anti-Pattern | Modern Replacement |
|--------------|-------------------|
| `new object()` for locking | `Lock` |
| Manual SSE formatting | `TypedResults.ServerSentEvents()` |
| `.GroupBy().ToDictionary()` | `CountBy()` / `AggregateBy()` |
| `string.Contains("x")` without StringComparison | Explicit `StringComparison` |
| Custom snake_case serializers | `JsonNamingPolicy.SnakeCaseLower` |

---

## 5. Dependency Rules (MUST NEVER BE VIOLATED)

### Allowed

```
instrumentation/* → core/*
collector/* → core/*
dashboard → collector/api (HTTP)
dashboard → collector/streaming (SSE/WS)
cli → collector/api (HTTP)
```

### Prohibited

```
core/* → instrumentation/*
core/* → collector/*
collector/* → dashboard
collector/* → cli
dashboard ↔ cli
```

**If any generated code violates these rules, FIX THE CODE.**

---

## 6. Data Flow and Responsibilities

### PRODUCERS

- MUST emit valid OTLP telemetry matching schema & semconv
- MUST include tokens properly
- MUST NOT interpret or aggregate telemetry

### RECEIVERS

- MUST ingest OTLP/JSON/Protobuf with zero allocations when possible
- MUST use modern APIs (`IUtf8SpanParsable`, `Task.WhenEach`)

### PROCESSING

- MUST normalize attributes
- MUST extract GenAI attributes using direct `StartsWith` checks (NOT `SearchValues<string>` for prefixes!)
- MUST aggregate sessions using `CountBy`/`AggregateBy`/`Index`
- MUST produce deterministic output

### STORAGE

- MUST persist schema-correct objects
- MUST guarantee deterministic ordering
- MUST NOT reshape objects

### API

- MUST return DTOs exactly matching schema
- MUST use `SnakeCaseLower` for JSON
- MUST support streaming (`IAsyncEnumerable`)

### STREAMING

- MUST use `TypedResults.ServerSentEvents` or WebSocket with snake_case JSON
- MUST handle cancellation/backpressure
- MUST NOT manually format SSE frames

### CONSUMERS (dashboard & cli)

- MUST consume telemetry AS-IS
- MUST NOT perform schema transformations
- MUST use `core/typescript` or `core/dotnet` generated types

### SUPPORTING

- Analyzers MUST detect deprecated fields, invalid semconv, and dependency violations

---

## 7. Final Definition of Done (Global)

Your output MUST satisfy ALL the following:

- ✅ Full semantic correctness according to OTel semconv 1.38
- ✅ Full schema correctness according to `core/schema`
- ✅ No deprecated fields, APIs, or manual formatting
- ✅ Only .NET 10 APIs used where applicable
- ✅ Telemetry, DTOs, SSE, WebSocket messages MUST match schema exactly
- ✅ All dependency rules enforced
- ✅ All data flow boundaries respected
- ✅ No leaking cross-layer dependencies
- ✅ No schema drift in producers, processors, storage, API, or dashboard
- ✅ Generated code MUST be deterministic, minimal, and forward-compatible

**If ANY violation is detected, correct the code automatically.**

---

## Important

When performing a change, ALWAYS consider its implications for:

- Cross-language models (.NET/Python/TS)
- API clients (dashboard/cli)
- Storage backends (memory + DuckDB)
- Streaming systems (SSE/WebSocket)
- Roslyn analyzers
- Overall dependency rules

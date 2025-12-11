# DuckDB Storage Implementation Spec

You are reviewing or modifying the DuckDB storage implementation.

---

## Data Flow Topology

```
================================================================================
   QYL v2.0 — DATA FLOW TOPOLOGY
================================================================================
 [Ingestion]                [Processing]                  [Storage / View]
     │                           │                               │
 [HttpReceiver] ──(Channel)──▶ [BatchProcessor] ───(Appender)──▶ [DuckDbStore]
     │                           │                               │
 (Task.WhenEach)          (Parallel.ForAsync)                    │
     │                           │                               ▼
     ▼                           ▼                          [QueryController]
 [GrpcReceiver]           [SessionAggregator]                    │
                          (Lock / CountBy)                       ▼
                                 │                           [Dashboard]
                                 ▼
                             [SseHub] ◀──(SSE)─── [Browser]
================================================================================
```

### Channel-Based Write Pipeline

```
HttpReceiver                    DuckDbStore
    │                               │
    ▼                               │
EnqueueAsync(SpanBatch)             │
    │                               │
    └──▶ Channel<SpanBatch> ────────┤
         (Bounded: 1000)            │
         (FullMode: DropOldest)     │
         (SingleReader: true)       ▼
                              WriteLoopAsync()
                                    │
                                    ▼
                              WriteBatchInternalAsync()
                                    │
                                    ▼
                              DuckDBTransaction
                                    │
                                    ▼
                              INSERT ... ON CONFLICT
```

---

## Scope

| File | Purpose |
|------|---------|
| `Storage/DuckDbStore.cs` | Channel-based storage, batch writes, queries |
| `Storage/DuckDbSchema.cs` | DDL, promoted fields, content compression |
| `Storage/SpanRecord.cs` | Storage DTOs |

---

## .NET 10 API Requirements (MANDATORY)

### Must Use

| API | Location | Purpose |
|-----|----------|---------|
| `Channel<SpanBatch>` | `DuckDbStore._writeChannel` | Backpressure-aware write queue |
| `BoundedChannelOptions` | Constructor | Memory-bounded with `DropOldest` |
| `ReadAllAsync()` | `WriteLoopAsync` | Async enumeration of batches |
| `IAsyncDisposable` | `DuckDbStore` | Proper async cleanup |
| `CancelAsync()` | `DisposeAsync` | .NET 10 cancellation |
| `FrozenSet<string>` | `PromotedFields` | Immutable field registry |
| `FrozenDictionary<K,V>` | Schema lookups | Read-optimized mappings |

### Anti-Patterns (REJECT)

| Bad | Good | Why |
|-----|------|-----|
| `BlockingCollection<T>` | `Channel<T>` | Async-native |
| `lock` + sync writes | `Channel` + `WriteLoopAsync` | Non-blocking pipeline |
| `Task.Run` in hot path | Channel reader loop | Single background task |
| `StringBuilder` for SQL | Raw string literals `"""..."""` | Allocation-free |
| `Dictionary` for promoted fields | `FrozenSet` | SIMD-optimized lookup |

---

## Schema Requirements

### Tables

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `spans` | Hot storage | `(trace_id, span_id)` PK |
| `sessions` | Session aggregation | `session_id` PK |
| `feedback` | User feedback | `feedback_id` PK |
| `span_content` | Large content (ZSTD) | `content_id` PK |

### Promoted Fields (Columnar Performance)

```sql
-- GenAI Semantic Conventions (OTel 1.38)
"gen_ai.provider.name"      VARCHAR
"gen_ai.request.model"      VARCHAR
"gen_ai.response.model"     VARCHAR
"gen_ai.operation.name"     VARCHAR
"gen_ai.usage.input_tokens" BIGINT
"gen_ai.usage.output_tokens" BIGINT

-- Agent Tracking
"agents.agent.id"           VARCHAR
"agents.agent.name"         VARCHAR
"agents.tool.name"          VARCHAR

-- Session/User
"session.id"                VARCHAR
"user.id"                   VARCHAR

-- Error Tracking
"exception.type"            VARCHAR
"exception.message"         VARCHAR
```

### Non-Promoted Attributes

```sql
-- Flexible MAP storage for all other attributes
attributes MAP(VARCHAR, VARCHAR) NOT NULL DEFAULT MAP {}
```

---

## Write Semantics

### Requirements

- [x] **Batch writes via Channel** - `EnqueueAsync(SpanBatch)` is non-blocking
- [x] **Bounded backpressure** - `BoundedChannelOptions(1000)` with `DropOldest`
- [x] **Single writer task** - `WriteLoopAsync()` runs in background
- [x] **Transaction per batch** - `DuckDBTransaction` for atomicity
- [x] **Upsert semantics** - `ON CONFLICT DO UPDATE` for span updates
- [x] **Graceful shutdown** - `Writer.Complete()` then await `_writerTask`

### Current Implementation (Compliant)

```csharp
// Non-blocking enqueue
public ValueTask EnqueueAsync(SpanBatch batch, CancellationToken ct = default) =>
    _writeChannel.Writer.WriteAsync(batch, ct);

// Background writer with proper cancellation
private async Task WriteLoopAsync()
{
    await foreach (SpanBatch batch in _writeChannel.Reader.ReadAllAsync(_cts.Token))
    {
        await WriteBatchInternalAsync(batch, _cts.Token);
    }
}
```

---

## Read Semantics

### Requirements

- [x] **Deterministic ordering** - `ORDER BY start_time ASC/DESC`
- [x] **Partial range queries** - `WHERE start_time >= $start_after AND start_time <= $start_before`
- [x] **Parameterized queries** - `DuckDBParameter` for SQL injection prevention
- [x] **Session-scoped queries** - `GetSpansBySessionAsync(sessionId)`
- [x] **Trace reconstruction** - `GetTraceAsync(traceId)`

### Query Methods

| Method | Filter Support | Order |
|--------|----------------|-------|
| `GetSpansBySessionAsync` | session_id | start_time ASC |
| `GetTraceAsync` | trace_id | start_time ASC |
| `GetSpansAsync` | session, provider, time range | start_time DESC |
| `QueryParquetAsync` | session, trace, time | (unordered) |

---

## Analytical Capabilities

### Token Aggregation (MANDATORY)

```sql
SELECT
    COALESCE(SUM(tokens_in), 0) as total_input_tokens,
    COALESCE(SUM(tokens_out), 0) as total_output_tokens,
    COALESCE(SUM(cost_usd), 0) as total_cost_usd,
    AVG(CASE WHEN eval_score IS NOT NULL THEN eval_score END) as avg_eval_score
FROM spans
WHERE provider_name IS NOT NULL
```

### Current Implementation (`GetGenAiStatsAsync`)

- [x] `SUM(tokens_in)` / `SUM(tokens_out)`
- [x] `SUM(cost_usd)`
- [x] `AVG(eval_score)` with NULL handling
- [x] Filter by session_id, time range
- [x] Filter for GenAI spans only (`provider_name IS NOT NULL`)

### Filter Support

| Filter | Column | Index |
|--------|--------|-------|
| Provider | `provider_name` | `idx_spans_provider` |
| Model | `request_model` | (covered by provider index) |
| Time Range | `start_time` | `idx_spans_time` |
| Session | `session_id` | `idx_spans_session` |

---

## Archival (Parquet Export)

### Requirements

- [x] **ZSTD compression** - `COMPRESSION ZSTD`
- [x] **Row group sizing** - `ROW_GROUP_SIZE 100000`
- [x] **Time-based cutoff** - Archive spans older than `olderThan`
- [x] **Atomic delete** - Delete archived spans after successful export
- [x] **Query support** - `read_parquet()` for archived data

### Implementation

```csharp
await exportCmd.ExecuteNonQueryAsync(ct);  // COPY TO parquet
await deleteCmd.ExecuteNonQueryAsync(ct);  // DELETE archived
```

---

## Dependency Rules

### Allowed

```
storage/duckdb/* → storage/abstractions/*
storage/duckdb/* → collector/Models/*
storage/duckdb/* → collector/Primitives/*
```

### Prohibited

```
storage/duckdb/* → api/*
storage/duckdb/* → streaming/*
storage/duckdb/* → dashboard/*
storage/duckdb/* → instrumentation/*
storage/duckdb/* → receivers/*
```

---

## Definition of Done

- [x] SQL schema uses snake_case column names
- [x] Promoted GenAI fields for columnar performance
- [x] Channel-based non-blocking writes
- [x] BoundedChannel with backpressure (DropOldest)
- [x] Deterministic query ordering
- [x] Token aggregation queries
- [x] Parquet archival with ZSTD compression
- [x] IAsyncDisposable with graceful shutdown
- [x] No dependency rule violations
- [ ] **TODO:** Use `Lock` instead of `SemaphoreSlim` for write synchronization
- [ ] **TODO:** Add `FrozenSet<string>` for promoted field detection

---

## Compliance Issues Found

### 1. SemaphoreSlim vs Lock (SHOULD FIX)

**Current:**
```csharp
private readonly SemaphoreSlim _writeLock = new(1, 1);
```

**Should be:**
```csharp
private readonly Lock _writeLock = new();
```

### 2. Missing Promoted Field Registry (SHOULD ADD)

The schema in `DuckDbSchema.cs` has promoted fields, but `DuckDbStore.cs` doesn't reference them for field routing. Consider:

```csharp
private static readonly FrozenSet<string> PromotedFields = new[]
{
    "gen_ai.provider.name",
    "gen_ai.request.model",
    "gen_ai.usage.input_tokens",
    // ...
}.ToFrozenSet(StringComparer.Ordinal);
```

### 3. Schema Version Mismatch

- `DuckDbStore.cs` has inline schema (sessions, spans, feedback)
- `DuckDbSchema.cs` has v2.0.0 schema with more promoted fields

**Recommendation:** Consolidate to single schema source.

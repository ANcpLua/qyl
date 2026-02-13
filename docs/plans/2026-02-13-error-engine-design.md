# Error Engine Design — Double Sentry's Furniture

## Goal

Build a real-time error engine in qyl.collector that matches and exceeds Sentry's error tracking for .NET and GenAI workloads. Backend engine first — dashboard and MCP shells light up in later workflows.

## Approach: Inline Ingestion Pipeline (Approach B)

Wire error extraction into the existing ingestion path. When spans arrive with `status_code = 2`, extract, fingerprint, categorize, and aggregate in real-time. No polling, no separate projection service.

## 3 Workflows

| Workflow | Scope | Depends on |
|----------|-------|------------|
| **1: Error Engine Core** | Auto-capture + fingerprinting + grouping + API | Nothing |
| **2: Deploy Correlation + SLO** | Release tracking + auto-resolve + burn rate | Workflow 1 |
| **3: Breadcrumbs + AI Triage** | Passive event trail + MCP error tools | Workflow 1 |

## Workflow 1: Error Engine Core (This Document)

### Components

#### 1. Auto-Capture Middleware (`qyl.servicedefaults`)

Add to `AddServiceDefaults()`:
- ASP.NET Core exception-capturing middleware (early in pipeline)
- `AppDomain.CurrentDomain.UnhandledException` hook
- `TaskScheduler.UnobservedTaskException` hook
- Each captured exception becomes an OTel log record with `exception.type`, `exception.message`, `exception.stacktrace`, `exception.escaped`
- Uses `Activity.Current` to correlate with active trace/span

This is the "zero-config crash capture" that Sentry does. Adding `AddServiceDefaults()` to your app gives you automatic exception telemetry.

#### 2. Error Fingerprinting (`ErrorFingerprinter`)

Deterministic fingerprint from exception data:
- Input: `exception.type` + `exception.stacktrace` + `exception.message`
- Algorithm:
  1. Normalize stack trace: strip line numbers, strip file paths, keep method signatures
  2. Normalize message: replace GUIDs, numbers, URLs with placeholders
  3. SHA256(`type + normalized_stack + normalized_message`) → fingerprint
- GenAI-aware: if span has `gen_ai.operation.name`, include it in the fingerprint. A `rate_limit_exceeded` from `chat` is a different error than `rate_limit_exceeded` from `embeddings`.

#### 3. Error Categorization (`ErrorCategorizer`)

Map exception type + attributes to `ErrorCategory` enum:
- `HttpRequestException` → `network`
- `TimeoutException` → `timeout`
- `UnauthorizedAccessException` → `auth`
- `DbException` / `DuckDBException` → `database`
- GenAI-specific: `gen_ai.error.type` attribute → direct mapping
  - `rate_limit_exceeded` → `rate_limit`
  - `context_length_exceeded` → `validation`
  - `authentication_error` → `auth`
  - `model_overloaded` → `external`
- Fallback: `unknown`

#### 4. Error Aggregator (`ErrorAggregator`)

Receives error events from ingestion pipeline, writes to `errors` table:
- Channel<ErrorEvent> for backpressure (like existing span writer)
- Batch processing (50 errors/batch)
- UPSERT by fingerprint:
  - New fingerprint → INSERT with `status = 'new'`, `occurrence_count = 1`
  - Existing fingerprint → UPDATE `last_seen`, `occurrence_count += 1`, merge `affected_services`, append `sample_traces` (max 10)
- Track `affected_users` via `enduser.id` or `user.id` span attribute

#### 5. Error REST API Endpoints

Implement the stubbed endpoints:

| Endpoint | Method | Behavior |
|----------|--------|----------|
| `GET /api/v1/errors` | GET | List errors with filtering (category, status, service, time range), pagination, sorting |
| `GET /api/v1/errors/{errorId}` | GET | Error detail with sample traces, affected services, occurrence timeline |
| `GET /api/v1/errors/stats` | GET | Stats: by category, by service, by type, trend direction |
| `PATCH /api/v1/errors/{errorId}` | PATCH | Update status (acknowledge, resolve, ignore), assign |
| `GET /api/v1/errors/groups` | GET | GenAI-grouped errors: by operation, by provider, by finish_reason |

#### 6. GenAI-Aware Error Grouping

Beyond Sentry — group errors by AI operation semantics:
- By `gen_ai.operation.name` (chat vs embeddings vs tool_calls)
- By `gen_ai.provider.name` (anthropic vs openai vs azure)
- By `gen_ai.response.finish_reasons` (stop vs max_tokens vs content_filter)
- By `gen_ai.error.type` (rate_limit vs context_length vs model_overloaded)
- Surface: "Your OpenAI chat completions have a 12% error rate, 80% are rate_limit_exceeded"

#### 7. Real-time Error SSE

Add `/api/v1/live/errors` SSE endpoint:
- Filters the existing SSE broadcast for `status_code = 2` spans
- Includes fingerprint and category in the event payload
- Dashboard can subscribe for live error feed

### Data Flow

```
OTLP Span arrives (status_code = 2)
    │
    ├─ OtlpConverter extracts exception attributes (existing)
    │
    ├─ ErrorFingerprinter computes fingerprint + normalizations
    │
    ├─ ErrorCategorizer maps to ErrorCategory enum
    │
    ├─ ErrorAggregator.EnqueueAsync(errorEvent)
    │     │
    │     └─ Background: batch UPSERT to errors table
    │
    ├─ Span written to spans table (existing)
    │
    └─ SSE broadcast (existing + error-specific channel)
```

### Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/qyl.servicedefaults/ErrorCapture/ExceptionCaptureMiddleware.cs` | CREATE | ASP.NET exception middleware |
| `src/qyl.servicedefaults/ErrorCapture/GlobalExceptionHooks.cs` | CREATE | AppDomain + TaskScheduler hooks |
| `src/qyl.collector/Errors/ErrorFingerprinter.cs` | CREATE | Fingerprint computation |
| `src/qyl.collector/Errors/ErrorCategorizer.cs` | CREATE | Exception → category mapping |
| `src/qyl.collector/Errors/ErrorAggregator.cs` | CREATE | Channel-buffered error writer |
| `src/qyl.collector/Errors/ErrorEndpoints.cs` | CREATE | REST API (replaces stubs) |
| `src/qyl.collector/Errors/ErrorQueryService.cs` | CREATE | DuckDB error queries |
| `src/qyl.collector/Storage/DuckDbStore.cs` | MODIFY | Add error UPSERT methods |
| `src/qyl.collector/Ingestion/OtlpConverter.cs` | MODIFY | Wire fingerprinter + categorizer |
| `src/qyl.collector/Program.cs` | MODIFY | Register error services, replace stub endpoints |
| `src/qyl.collector/Realtime/SseEndpoints.cs` | MODIFY | Add `/api/v1/live/errors` |
| `tests/qyl.collector.tests/` | CREATE | Error engine tests |

### What "Double Sentry" Means Here

| Feature | Sentry | qyl (after Workflow 1) |
|---------|--------|------------------------|
| Auto crash capture | AppDomain + TaskScheduler + middleware | Same + GenAI error type detection |
| Fingerprinting | Stack trace + type + message | Same + `gen_ai.operation.name` in fingerprint |
| Categorization | ~5 categories | 13 categories + GenAI-specific |
| Error grouping | By stack trace | By stack trace + by AI operation + by provider + by finish_reason |
| Real-time | Polling-based UI | SSE streaming |
| API | Pull-based issue queries | Push ingestion + pull queries + SSE |
| GenAI awareness | None | Native first-class columns |

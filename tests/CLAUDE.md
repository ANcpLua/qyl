# tests

@import "../CLAUDE.md"

Test infrastructure for qyl. Uses xUnit v3 with Microsoft Testing Platform (MTP).

## Test Projects

| Project                  | Namespace                | Target                   |
|--------------------------|--------------------------|--------------------------|
| `qyl.collector.tests`    | `*.Storage.*`            | DuckDbStore, queries     |
| `qyl.collector.tests`    | `*.Ingestion.*`          | OTLP parsing             |
| `qyl.collector.tests`    | `*.Integration.*`        | End-to-end API tests     |
| `qyl.mcp.server.tests`   | `*.Unit.*`               | MCP server (EMPTY)       |

## Test Execution

```bash
# Direct MTP execution (fast)
./tests/qyl.collector.tests/bin/Debug/net10.0/qyl.collector.tests

# Filter by namespace
./tests/.../qyl.collector.tests -filter "/*/qyl.collector.tests.Storage/*"

# Filter by method pattern
./tests/.../qyl.collector.tests -filter "/*/*/*/Insert*"
```

## qyl Test Infrastructure

### SpanBuilder (Fluent Builder)

```csharp
// GenAI span with defaults
var span = SpanBuilder.GenAi(TestConstants.TraceDefault, TestConstants.SpanDefault)
    .WithSessionId(TestConstants.SessionDefault)
    .Build();

// Minimal span
var minimal = SpanBuilder.Minimal("trace-001", "span-001").Build();

// Custom timing
var timed = SpanBuilder.Create("trace-001", "span-001")
    .AtTime(baseTime, offsetMs: 100, durationMs: 50)
    .Build();
```

### SpanFactory (Batch Creation)

```csharp
// Sequential batch
var batch = SpanFactory.CreateBatch(traceId, sessionId, count: 5, baseTime);

// Trace hierarchy (root + children)
var hierarchy = SpanFactory.CreateHierarchy(traceId, baseTime, childCount: 2);

// GenAI stats test data
var stats = SpanFactory.CreateGenAiStats(sessionId, baseTime);
```

### DuckDbTestHelpers

```csharp
// In-memory store
var store = DuckDbTestHelpers.CreateInMemoryStore();
await DuckDbTestHelpers.WaitForSchemaInit();

// Write and wait
await DuckDbTestHelpers.EnqueueAndWaitAsync(store, span);

// Query extensions
var columns = await connection.GetTableColumnsAsync("spans");
var count = await connection.CountSpansAsync(traceId, spanId);
```

### TestConstants

```csharp
TestConstants.InMemoryDb              // ":memory:"
TestConstants.SessionDefault          // "session-001"
TestConstants.TraceDefault            // "trace-001"
TestConstants.ProviderOpenAi          // "openai"
TestConstants.TokensInDefault         // 50L
TestConstants.SchemaInitDelayMs       // 200
```

## Coverage Gaps (P0/P1)

### CRITICAL: Untested Code Paths

| Gap ID   | Component              | Path                        | Priority |
|----------|------------------------|-----------------------------|----------|
| TEST-001 | qyl.mcp                | Entire server (0% coverage) | P0       |
| TEST-002 | OtlpConverter          | Proto/gRPC path             | P1       |
| TEST-003 | Realtime/SSE           | SseExtensions, SseEndpoints | P1       |
| TEST-004 | TraceServiceImpl       | gRPC TraceService           | P1       |
| TEST-005 | SpanBroadcaster        | Channel pub/sub             | P2       |

### Banned API Violations (24+ instances)

Test code directly uses banned time APIs instead of `TimeProvider.System.GetUtcNow()`.

**Files requiring TimeProvider injection:**

- `Helpers/SpanBuilder.cs`
- `Storage/DuckDbStoreTests.cs`
- `Query/SessionQueryServiceTests.cs`
- `Integration/*.cs`

**Required fix:** Inject `TimeProvider` into `SpanBuilder` constructor and test fixtures.

## xUnit v3 Pattern

```csharp
public sealed class MyTests : IAsyncLifetime
{
    private DuckDbStore _store = null!;

    public async ValueTask InitializeAsync()  // ValueTask, not Task
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
    }
}
```

## MTP Package Note

Use `xunit.v3.mtp-v2` (NOT plain `xunit.v3`) for .NET 10 SDK compatibility.
Plain `xunit.v3` bundles MTP v1 which causes loader errors.

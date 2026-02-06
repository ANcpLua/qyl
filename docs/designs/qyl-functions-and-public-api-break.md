# qyl Functions: The Final Public API Break

**Status**: Proposal
**Author**: ancplua + Claude Opus 4.6
**Date**: 2026-02-06

---

## Problem

Three things are stuck behind access modifiers that shouldn't be:

1. **DuckDbStore** — internal to collector. Nobody else can query telemetry.
2. **SpanRingBuffer + SSE broadcaster** — internal. Nobody can react to spans in real-time.
3. **WorkflowEngine execution** — locked inside copilot. Nobody can trigger workflows from outside.

Meanwhile, the analyzer diagnostics (QYL0001-1001) are all `Info` severity with no canonical suppression path. Users can't opt out of interceptor guidance cleanly.

And qyl has no serverless story. Workflows exist but require Copilot. There's no way to say: "when a span with `status_code=500` and `gen_ai.system=openai` arrives, run this code."

**This spec proposes one change that solves all three.**

---

## The Change: `IQylRuntime`

One public interface. Exposed from `qyl.hosting`. Gives full read access to the collector pipeline and write access to register functions.

```csharp
namespace Qyl.Hosting;

/// <summary>
/// The runtime surface of a running qyl instance.
/// Injected via DI. This is the only interface users need.
/// </summary>
public interface IQylRuntime
{
    // === Query ===
    IQylStore Store { get; }

    // === React ===
    IQylStream Stream { get; }

    // === Execute ===
    IQylFunctions Functions { get; }

    // === Suppress ===
    IQylDiagnostics Diagnostics { get; }
}
```

### `IQylStore` — Query anything

Currently `DuckDbStore` is internal to collector. Extract the read interface:

```csharp
public interface IQylStore
{
    ValueTask<IReadOnlyList<SpanRecord>> QuerySpansAsync(
        SpanQuery query, CancellationToken ct = default);

    ValueTask<IReadOnlyList<LogRecord>> QueryLogsAsync(
        LogQuery query, CancellationToken ct = default);

    ValueTask<TraceTree?> GetTraceAsync(
        string traceId, CancellationToken ct = default);

    ValueTask<GenAiStats> GetGenAiStatsAsync(
        TimeSpan window, CancellationToken ct = default);

    ValueTask<StorageStats> GetStorageStatsAsync(
        CancellationToken ct = default);
}
```

Query types live in `qyl.protocol` (BCL-only). Implementation stays in collector.

### `IQylStream` — React to telemetry in real-time

Currently `SpanRingBuffer` + `ITelemetrySseBroadcaster` are internal. Extract:

```csharp
public interface IQylStream
{
    /// <summary>
    /// Subscribe to spans matching a predicate. Evaluated on the hot path
    /// against the ring buffer — keep predicates fast.
    /// </summary>
    IAsyncEnumerable<SpanRecord> SubscribeAsync(
        Func<SpanRecord, bool> predicate,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe to all spans (no filtering).
    /// </summary>
    IAsyncEnumerable<SpanRecord> SubscribeAllAsync(
        CancellationToken ct = default);
}
```

This is the trigger mechanism for serverless functions.

### `IQylFunctions` — Register and manage functions

The new concept. A "function" is: **predicate + handler + metadata**.

```csharp
public interface IQylFunctions
{
    /// <summary>
    /// Register a function that runs when matching spans arrive.
    /// Returns a handle to unregister.
    /// </summary>
    IQylFunctionHandle Register(QylFunctionDefinition definition);

    /// <summary>
    /// List all registered functions.
    /// </summary>
    IReadOnlyList<QylFunctionInfo> List();
}

public sealed record QylFunctionDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// When to trigger. Evaluated against every incoming span.
    /// Return true to invoke the handler.
    /// </summary>
    public required Func<SpanRecord, bool> Trigger { get; init; }

    /// <summary>
    /// What to do. Receives the matched span + runtime for chaining.
    /// Return value is persisted as execution result.
    /// </summary>
    public required Func<SpanRecord, IQylRuntime, CancellationToken, ValueTask<string?>> Handler { get; init; }

    /// <summary>
    /// Max concurrent invocations. Default: 1 (serial).
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>
    /// Timeout per invocation. Default: 30s.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

public interface IQylFunctionHandle : IAsyncDisposable
{
    string FunctionId { get; }
    QylFunctionInfo Info { get; }
}

public sealed record QylFunctionInfo(
    string Id,
    string Name,
    string? Description,
    long InvocationCount,
    long ErrorCount,
    DateTimeOffset? LastInvokedAt);
```

### `IQylDiagnostics` — One way to suppress

Currently: no canonical suppression mechanism for QYL diagnostics.

```csharp
/// <summary>
/// Applied to any symbol to suppress one or more QYL diagnostics.
/// This is the ONE AND ONLY way to suppress qyl analyzer output.
/// </summary>
[AttributeUsage(
    AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method |
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Struct,
    AllowMultiple = true)]
public sealed class QylSuppressAttribute : Attribute
{
    public QylSuppressAttribute(string diagnosticId) => DiagnosticId = diagnosticId;
    public QylSuppressAttribute(params string[] diagnosticIds) => DiagnosticIds = diagnosticIds;

    public string? DiagnosticId { get; }
    public string[]? DiagnosticIds { get; }
    public string? Justification { get; init; }
}
```

Usage:

```csharp
// Suppress a single diagnostic
[QylSuppress("QYL0001", Justification = "Interface dispatch is intentional here")]
public void CallViaInterface() { ... }

// Suppress at assembly level
[assembly: QylSuppress("QYL0003")]

// Suppress multiple
[QylSuppress("QYL0001", "QYL0002")]
public class MyClass { ... }
```

The analyzer checks for `[QylSuppress]` before reporting. No `#pragma`. No `.editorconfig`. No ruleset files. One attribute. One mechanism.

---

## Serverless Functions: How It Works

### Registration (at startup)

```csharp
var app = Qyl.CreateApp(args);

app.MapQylFunction("alert-high-latency", fn =>
{
    fn.Description = "Alert when GenAI calls exceed 5s";
    fn.Trigger = span =>
        span.DurationNs > 5_000_000_000 &&
        span.Attributes.ContainsKey("gen_ai.system");
    fn.Handler = async (span, runtime, ct) =>
    {
        var trace = await runtime.Store.GetTraceAsync(span.TraceId, ct);
        return $"Slow GenAI call: {span.Name} ({span.DurationNs / 1_000_000}ms), trace: {trace?.RootSpan?.Name}";
    };
});

app.MapQylFunction("error-pattern-detector", fn =>
{
    fn.Description = "Detect recurring error patterns";
    fn.Trigger = span => span.StatusCode >= 2; // ERROR
    fn.Handler = async (span, runtime, ct) =>
    {
        var recent = await runtime.Store.QuerySpansAsync(new SpanQuery
        {
            StatusCode = 2,
            Hours = 1,
            Limit = 100
        }, ct);

        var grouped = recent.GroupBy(s => s.StatusMessage).OrderByDescending(g => g.Count());
        var top = grouped.First();
        return top.Count() > 10
            ? $"Recurring error: '{top.Key}' ({top.Count()} times in last hour)"
            : null; // null = no result to persist
    };
});

app.Run();
```

### Runtime execution

```
Span arrives at POST /v1/traces
    ↓
SpanRingBuffer.Push(span)
    ↓ (parallel, non-blocking)
TelemetrySseBroadcaster.PublishSpans(batch)
FunctionTriggerEvaluator.EvaluateAsync(batch)
    ↓
For each span in batch:
    For each registered function:
        if function.Trigger(span) == true:
            enqueue (function, span) to bounded channel
    ↓
FunctionExecutor (background service):
    dequeue (function, span)
    start OTel span: gen_ai.execute_tool / function.Name
    invoke function.Handler(span, runtime, ct)
    persist to function_invocations table
    broadcast result via SSE
    record metrics: qyl.function.invocations, qyl.function.duration
```

### Storage (new DuckDB tables)

```sql
CREATE TABLE IF NOT EXISTS qyl_functions (
    function_id VARCHAR PRIMARY KEY,
    name VARCHAR NOT NULL UNIQUE,
    description VARCHAR,
    max_concurrency INTEGER NOT NULL DEFAULT 1,
    timeout_ms INTEGER NOT NULL DEFAULT 30000,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS function_invocations (
    invocation_id VARCHAR PRIMARY KEY,
    function_id VARCHAR NOT NULL,
    triggered_by_span_id VARCHAR,
    triggered_by_trace_id VARCHAR,
    status VARCHAR NOT NULL,  -- running | completed | failed | skipped
    result VARCHAR,
    error VARCHAR,
    duration_ms DOUBLE,
    started_at TIMESTAMP NOT NULL,
    completed_at TIMESTAMP
);

CREATE INDEX idx_fi_function_id ON function_invocations(function_id);
CREATE INDEX idx_fi_status ON function_invocations(status);
CREATE INDEX idx_fi_started_at ON function_invocations(started_at);
```

---

## The Public API Break

### What becomes public

| Type | Currently | Becomes | Lives In |
|------|-----------|---------|----------|
| `IQylRuntime` | NEW | public | qyl.hosting |
| `IQylStore` | NEW (extracts from DuckDbStore) | public | qyl.hosting |
| `IQylStream` | NEW (extracts from ring buffer) | public | qyl.hosting |
| `IQylFunctions` | NEW | public | qyl.hosting |
| `QylFunctionDefinition` | NEW | public | qyl.hosting |
| `QylSuppressAttribute` | NEW | public | qyl.protocol |
| `SpanQuery`, `LogQuery` | NEW | public | qyl.protocol |
| `SpanRecord`, `LogRecord` | internal to collector | public | qyl.protocol |
| `TraceTree` | internal to collector | public | qyl.protocol |
| `GenAiStats`, `StorageStats` | internal to collector | public | qyl.protocol |

### What stays internal

| Type | Why |
|------|-----|
| `DuckDbStore` | Implementation detail behind `IQylStore` |
| `SpanRingBuffer` | Implementation detail behind `IQylStream` |
| `TelemetrySseBroadcaster` | Implementation detail |
| `QylRunner` | Orchestration internals |
| `FunctionTriggerEvaluator` | Internal machinery |
| `FunctionExecutor` | Background service |

### Dependency flow

```
qyl.protocol (BCL-only)
    ↑ defines: SpanRecord, SpanQuery, QylSuppressAttribute
    |
qyl.hosting (interfaces)
    ↑ defines: IQylRuntime, IQylStore, IQylStream, IQylFunctions
    |
qyl.collector (implementations)
    ↑ implements: DuckDbStore → IQylStore, RingBuffer → IQylStream
    |
User code
    uses: IQylRuntime (injected via DI)
```

No circular dependencies. Protocol stays BCL-only. Hosting defines contracts. Collector implements them.

---

## Analyzer Changes

### New diagnostics

| ID | Severity | Message |
|----|----------|---------|
| QYL2001 | Warning | Function trigger predicate should not perform I/O |
| QYL2002 | Warning | Function handler exceeds recommended timeout |
| QYL2003 | Error | Function name must be unique |
| QYL2004 | Info | Function registered: '{name}' with trigger on {attribute} |

### Suppression via `[QylSuppress]`

The analyzer generator reads `[QylSuppress]` on the containing symbol chain (method → class → assembly) before reporting any QYL diagnostic. One attribute. One mechanism. Works for:

- Interceptor diagnostics (QYL0001-0003, QYL1001)
- Function diagnostics (QYL2001-2004)
- Any future QYL diagnostic

---

## What This Is NOT

- **Not Vercel Functions.** No HTTP routing, no edge runtime, no CDN. qyl functions react to telemetry events, not HTTP requests.
- **Not AWS Lambda.** No container isolation, no cold starts, no per-invocation billing. Functions run in-process with the collector.
- **Not a plugin system.** Functions are C# code registered at startup via `MapQylFunction`. No dynamic loading, no sandboxing.

This is **event-driven compute scoped to observability**. A span arrives, a predicate matches, a handler runs, a result is persisted. That's it.

---

## Implementation Order

1. **Extract `SpanRecord`/`LogRecord` to qyl.protocol** — the public API break
2. **Define interfaces in qyl.hosting** — `IQylRuntime`, `IQylStore`, `IQylStream`, `IQylFunctions`
3. **Implement in collector** — adapter classes around existing `DuckDbStore` and `SpanRingBuffer`
4. **Add `QylSuppressAttribute`** to qyl.protocol + wire into analyzer generator
5. **Add `MapQylFunction` extension** — registration + trigger evaluator + executor
6. **Add DuckDB tables** — `qyl_functions`, `function_invocations`
7. **Add OTel instrumentation** — spans and metrics for function execution

# Zero-Cost-Until-Observed: Compiled Observability Contracts

## Summary

Observability today is always-on or always-off. qyl already has the foundation for a third option — instrumentation that exists at zero cost until someone asks for it, with compile-time contracts that guarantee schema agreement between producer and consumer before the first byte flows.

This is not a greenfield proposal. The subscription manager (`POST /api/v1/observe`) already ships. The ActivitySource/Meter infrastructure is registered. The interceptor generator produces compile-time instrumentation, and the storage layer is moving to `DuckDBAppenderMap<T>` for direct DuckDB writes. What's missing is the connective tissue: a catalog that makes subscriptions discoverable, contracts that keep the interceptor attributes and storage columns in sync, and an observability mode that makes zero-cost the default.

---

## What Exists Today

Before proposing anything new, here is what the codebase already provides:

### Dynamic Subscription Manager

**Files:** `src/qyl.collector/Observe/SubscriptionManager.cs`, `ObservationSubscription.cs`, `ObserveEndpoints.cs`

The collector exposes three endpoints:

| Endpoint | Purpose |
|----------|---------|
| `POST /api/v1/observe` | Activate a subscription (filter + OTLP endpoint) |
| `DELETE /api/v1/observe/{id}` | Tear down a subscription |
| `GET /api/v1/observe` | List active subscriptions |

Each subscription wires an `ActivityListener` with glob-style filter matching (`gen_ai.*`, `*`, exact match), backed by a `BatchActivityExportProcessor` + `OtlpTraceExporter`. Subscriptions are idempotent (same filter + endpoint = reuse). Teardown disposes listener first (stops new spans), then drains the processor buffer.

### Dormant ActivitySources and Meters

**File:** `src/qyl.servicedefaults/Instrumentation/ActivitySources.cs`

Four ActivitySources and two Meters, all lazy-initialized:

| Source | Name | Signal |
|--------|------|--------|
| `GenAiSource` | `qyl.genai` | traces |
| `DbSource` | `qyl.db` | traces |
| `AgentSource` | `qyl.agent` | traces + metrics |
| `TracedSource` | `qyl.traced` | traces |
| `GenAiMeter` | `qyl.genai` | metrics |
| `AgentMeter` | `qyl.agent` | metrics |

These are created via `??=` — they exist as static fields but the `ActivitySource`/`Meter` objects are only allocated on first access.

### Interceptor Generator + Storage Appender

| Component | File | Type | Consumed by | Produces |
|-----------|------|------|-------------|----------|
| `TracedInterceptorEmitter` | `servicedefaults.generator/Emitters/` | Roslyn IIncrementalGenerator | Apps via NuGet | Before/after method hooks, tag setters, exception events |
| `SpanStorageRowMap` | `collector/Storage/SpanAppender.cs` | Runtime `DuckDBAppenderMap<T>` | Collector only | Direct DuckDB column mapping for bulk insert |

These two components consume the same semantic convention vocabulary — the interceptor sets `gen_ai.system` as a span tag, the appender writes `GenAiProviderName` to DuckDB. But the link between them is convention, not construction. Renaming a property on `SpanStorageRow` without updating the appender map is a silent data loss bug. This is the gap that subscription contracts are designed to close.

> **Note:** The previous `DuckDbInsertGenerator` Roslyn source generator is being replaced by `DuckDBAppenderMap<T>` (see `docs/plans/hades-storage-purge.md`). The appender approach is simpler (27 lines vs a full generator project), faster (direct DuckDB writes, no parameterized SQL), and removes the `(decimal)ulong` UBIGINT workaround. The cost is that column mapping is no longer enforced at compile time — making the contract model more valuable, not less.

### SSE Streaming Infrastructure

**Files:** `src/qyl.collector/Realtime/SseEndpoints.cs`, `SpanRingBuffer.cs`

The collector already streams live telemetry via SSE at `/api/v1/live` and `/api/v1/live/spans`. Each client gets a bounded channel (backpressure via DropOldest). The connection lifecycle is managed via `RequestAborted` — disconnect = unsubscribe.

### Current Observability Mode: Always-On

**File:** `src/qyl.servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs`, line 255

```csharp
tracing.SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
```

`UseQyl()` registers `TracerProvider` with `AlwaysOnSampler` at startup. This means:

- `HasListeners()` is **always true** for all registered sources
- Every `StartActivity()` creates a full `Activity` object
- All spans are exported to the OTLP endpoint (if configured or auto-discovered)

The subscription manager adds **additional** listeners on top of this. Currently, it's an additive layer over an always-on pipeline — not a zero-cost activation mechanism.

---

## The Insight

### .NET already has the zero-cost primitive

`ActivitySource.StartActivity()` checks `HasListeners()` before allocating anything. If no `ActivityListener` is registered for that source, it returns `null`. No object creation. No attribute evaluation. No processor callbacks. Exactly zero allocation.

```csharp
// Cost when nobody is listening: one boolean check. That's it.
using var activity = source.StartActivity("agent.curate");
// activity is null → the using statement disposes nothing → zero cost
```

The generated interceptors already handle this correctly. `TracedInterceptorEmitter` generates:

```csharp
using var activity = TracedActivitySources.qyl_traced.StartActivity("MyMethod", ActivityKind.Internal);
if (activity is not null)
{
    activity.SetTag("my.param", param);
}
```

If `StartActivity()` returns null, no tags are set, no events are added, no exception data is captured. The entire instrumentation block is a null check.

### Correcting a common misconception about AlwaysOffSampler

An earlier version of this document stated: *"Even with `AlwaysOffSampler`, the .NET SDK still creates Activity objects."* This is wrong.

Here is what actually happens with `AlwaysOffSampler`:

| Scenario | Sampler result | StartActivity returns | Overhead |
|----------|---------------|----------------------|----------|
| Root span, no parent context | `Drop` → `None` | `null` | Zero |
| Child span with propagated parent | `Drop` → `PropagationData` | Minimal Activity (no attributes, no export) | One allocation for context propagation, no recording |

**Root spans with `AlwaysOffSampler` are truly zero-cost** — `StartActivity()` returns null. Child spans create a minimal Activity solely to propagate the W3C trace context (trace ID, span ID), but no attributes are evaluated, no processors fire, and nothing is exported.

This distinction matters for the architecture: if the sampler is `AlwaysOff`, the `TracerProvider` listener is registered but causes zero allocation for root spans and minimal allocation for propagated contexts.

### What's missing is the mode switch

The runtime primitive exists. The subscription manager exists. The generators exist. What's missing is a way to say: *"Start with everything dormant. Activate specific domains on demand."*

Currently, `UseQyl()` forces `AlwaysOnSampler`. To enable zero-cost-until-observed, the system needs an alternative configuration mode where:

1. `TracerProvider` uses `AlwaysOffSampler` as default (or no provider at all)
2. Subscriptions are the mechanism that activates specific domains
3. Teardown returns the system to dormancy

---

## Three Observability Modes

The proposal adds two new modes alongside the existing one:

### Mode 1: Always-On (current default)

```
UseQyl()  →  AlwaysOnSampler  →  all spans created and exported
```

This is what ships today. Every ActivitySource produces spans, every span is exported. Simple, comprehensive, and always costs CPU/memory/network.

**Use when:** Development, staging, small-scale production where full visibility outweighs overhead.

### Mode 2: Zero-Cost-Until-Observed (proposed)

```
UseQyl(o => o.ObservabilityMode = ObservabilityMode.OnDemand)
  →  AlwaysOffSampler (or no TracerProvider)
  →  all sources dormant, zero allocation
  →  POST /api/v1/observe { filter: "gen_ai.*" }
  →  subscription activates listener → spans flow
  →  DELETE /api/v1/observe/{id}
  →  back to zero
```

`HasListeners()` is false until a subscription activates. The generated interceptors short-circuit entirely.

**Use when:** Production with hundreds of services, cost-sensitive environments, services that are instrumented comprehensively but observed selectively.

### Mode 3: Warm (proposed, builds on Mode 2)

```
UseQyl(o => o.ObservabilityMode = ObservabilityMode.Warm)
  →  AlwaysOffSampler → root spans return null, but child spans propagate context
  →  trace IDs chain correctly across services
  →  POST /api/v1/observe { filter: "gen_ai.*" }
  →  subscription activates full recording
  →  spans include valid parent trace IDs from warm propagation
```

Warm mode solves a real problem: in pure zero-cost mode, if Service A has a subscription but Service B doesn't, the trace chain breaks at B because B never creates Activities. In warm mode, B creates minimal Activities for context propagation (one allocation, no attributes, no export), preserving the trace ID chain.

**Use when:** Distributed systems where trace continuity matters, but most services shouldn't actively export until asked.

---

## Signal-by-Signal Analysis

### Traces: Full zero-cost support

| Primitive | Guard | Zero-cost path |
|-----------|-------|---------------|
| `ActivitySource.StartActivity()` | `HasListeners()` → `null` | No allocation, no processing |
| Generated interceptor | `activity is not null` check | Tags, events, exception recording all skip |
| `BatchActivityExportProcessor` | Only created by subscription | No buffer, no timer, no export overhead |

Traces are the ideal signal for zero-cost-until-observed. The runtime, the generators, and the subscription manager all support this fully.

### Metrics: Partial zero-cost support

.NET's `Meter`/`Instrument` API has a listener pattern similar to ActivitySource, but with a critical distinction:

| Instrument type | Mechanism | Zero-cost when idle |
|----------------|-----------|-------------------|
| **Counter, Histogram** (push) | `MeterListener.SetMeasurementEventCallback<T>()` receives values when recorded | Yes — if no MeterListener is registered, `Add()`/`Record()` effectively no-ops |
| **ObservableGauge, ObservableCounter** (pull) | Callback only fires when `MeterListener.RecordObservableInstruments()` is called | Yes — nobody polls, callback never runs |

Both types are zero-cost when idle. The difference is operational:

- **Push instruments** (Counter, Histogram): The subscription wires a `MeterListener` that receives measurements via callback. Same model as traces — subscribe and measurements start flowing.
- **Pull instruments** (ObservableGauge): The subscription must periodically call `RecordObservableInstruments()` to poll values. This requires a timer per subscription, adding a small coordination cost that traces don't have.

The existing `GenAiMeter` and `AgentMeter` in `ActivitySources.cs` would need to be included in the subscription model. A subscription to `gen_ai.*` should activate both the `ActivityListener` (traces) and a `MeterListener` (metrics) for the `qyl.genai` source.

### Logs: No zero-cost equivalent

.NET logging (`ILoggerProvider`/`ILogger`) does not have a listener pattern comparable to `ActivitySource.HasListeners()`. The guards available are:

| Guard | What it does | Limitation |
|-------|-------------|-----------|
| `ILogger.IsEnabled(LogLevel)` | Checks minimum log level | Level-based, not listener-based; always evaluates if level >= minimum |
| `[LoggerMessage]` source gen | Checks `IsEnabled` before string interpolation | Avoids allocation for below-threshold logs, but the check itself always runs |

There is no way to make logging truly zero-cost. `IsEnabled()` returns true whenever the configured minimum level matches, regardless of whether any exporter is connected.

**Recommendation:** Logs should not be part of the zero-cost subscription model. They operate on a different cost model (level-based filtering, not listener-based activation). The subscription API should support `"signals": ["traces"]` and `"signals": ["traces", "metrics"]` but not `"signals": ["logs"]`.

---

## The Contract Model

### Why contracts matter

The subscription manager today accepts a string filter: `"gen_ai.*"`. It doesn't know:

- What attributes spans matching that filter will carry
- Whether the DuckDB schema has columns for those attributes
- What semconv version the producer is using

The collector discovers the schema at ingestion time — whatever attributes arrive get mapped to DuckDB columns by the `SpanStorageRowMap` appender mapping. If an attribute arrives that has no column mapping, it's silently dropped. If a column mapping exists that no attribute provides, it's null.

This works, but it's the same runtime-schema-discovery model that every other observability platform uses. With the storage layer moving from a Roslyn generator to a hand-written `DuckDBAppenderMap<T>`, the link between interceptor attributes and storage columns is now maintained by convention alone. The contract formalizes that convention into a verifiable artifact.

### What a contract contains

```
SubscriptionContract {
    domain:          "gen_ai"
    source_pattern:  "qyl.genai"
    schema_version:  "semconv-1.40.0"
    signals:         ["traces", "metrics"]

    trace_attributes: [
        { name: "gen_ai.system",              type: "string",  required: true  },
        { name: "gen_ai.request.model",       type: "string",  required: true  },
        { name: "gen_ai.usage.input_tokens",  type: "int",     required: false },
        { name: "gen_ai.usage.output_tokens", type: "int",     required: false },
        ...
    ]

    metric_instruments: [
        { name: "gen_ai.client.token.usage",        instrument: "histogram", unit: "token" },
        { name: "gen_ai.client.operation.duration",  instrument: "histogram", unit: "s"     },
    ]

    storage: {
        table:       "spans"
        column_map:  { "gen_ai.system" → "gen_ai_system", ... }
        schema_hash: "a1b2c3d4"
    }
}
```

### The gRPC analogy

A `.proto` file generates both the client stub and the server handler, guaranteeing type compatibility across the wire. Here, the semconv schema generates the interceptor (what attributes a span will have), and the contract verifies that the hand-written appender map (what columns DuckDB stores) agrees with those attributes. The contract bridges compile-time generation and runtime mapping.

Without it: the collector hopes the incoming spans match its schema. With it: the system knows at subscription time whether the producer and consumer agree.

---

## Catalog Endpoint

### `GET /api/v1/observe/catalog`

A new endpoint that surfaces available subscription domains, their contracts, and current state:

```json
{
  "schema_version": "semconv-1.40.0",
  "modes": {
    "current": "always-on",
    "available": ["always-on", "on-demand", "warm"]
  },
  "domains": [
    {
      "name": "gen_ai",
      "source": "qyl.genai",
      "signals": ["traces", "metrics"],
      "trace_attributes": [
        { "name": "gen_ai.system", "type": "string", "required": true },
        { "name": "gen_ai.request.model", "type": "string", "required": true },
        { "name": "gen_ai.usage.input_tokens", "type": "int", "required": false },
        { "name": "gen_ai.usage.output_tokens", "type": "int", "required": false }
      ],
      "metric_instruments": [
        { "name": "gen_ai.client.token.usage", "instrument": "histogram", "unit": "token" },
        { "name": "gen_ai.client.operation.duration", "instrument": "histogram", "unit": "s" }
      ],
      "storage_table": "spans",
      "contract_hash": "a1b2c3d4"
    },
    {
      "name": "db",
      "source": "qyl.db",
      "signals": ["traces"],
      "trace_attributes": [
        { "name": "db.system.name", "type": "string", "required": true },
        { "name": "db.query.text", "type": "string", "required": false }
      ],
      "storage_table": "spans",
      "contract_hash": "e5f6g7h8"
    },
    {
      "name": "traced",
      "source": "qyl.traced",
      "signals": ["traces"],
      "trace_attributes": [],
      "storage_table": "spans",
      "contract_hash": "i9j0k1l2"
    },
    {
      "name": "agent",
      "source": "qyl.agent",
      "signals": ["traces", "metrics"],
      "trace_attributes": [],
      "metric_instruments": [],
      "storage_table": "spans",
      "contract_hash": "m3n4o5p6"
    }
  ],
  "active_subscriptions": [
    {
      "id": "abc123def456",
      "filter": "gen_ai.*",
      "endpoint": "otlp://qyl:4318",
      "created_at": "2026-02-28T10:00:00Z",
      "contract_hash": "a1b2c3d4"
    }
  ]
}
```

The catalog is the UI-facing feature that gives immediate value. A dashboard can display which domains are available, which are active, and what data each one produces — before any spans flow.

### SSE Convergence: Connection-Scoped Subscriptions

The existing SSE infrastructure and the subscription manager can converge. Opening an SSE connection with an observe filter simultaneously activates the subscription and streams the results:

```
GET /api/v1/live?observe=gen_ai.*

→ Creates subscription (activates ActivityListener for gen_ai.*)
→ Streams resulting spans as SSE events
→ Client disconnects → subscription torn down automatically
→ Back to zero
```

This means: **the SSE connection IS the subscription.** No explicit POST/DELETE needed for interactive use cases (dashboard, debugging tools). The existing `RequestAborted` handler in `SseEndpoints.cs` already manages connection lifecycle.

Explicit `POST /api/v1/observe` endpoints remain for headless subscriptions (where the export target is a different collector, not the SSE stream).

---

## Implementation Path

### Phase 0: Subscription Manager (done)

`SubscriptionManager`, `ObservationSubscription`, `ObserveEndpoints` — already shipping.

**Exists at:** `src/qyl.collector/Observe/`

### Phase 1: Observability Modes

Add `ObservabilityMode` enum to `QylOptions`. In `ConfigureOpenTelemetry()`, switch the sampler:

```csharp
tracing.SetSampler(options.ObservabilityMode switch
{
    ObservabilityMode.AlwaysOn => new ParentBasedSampler(new AlwaysOnSampler()),
    ObservabilityMode.OnDemand => new AlwaysOffSampler(),
    ObservabilityMode.Warm    => new ParentBasedSampler(new AlwaysOffSampler()),
    _ => new ParentBasedSampler(new AlwaysOnSampler())
});
```

- `AlwaysOn`: Current behavior (unchanged)
- `OnDemand` with `AlwaysOffSampler`: Root spans return null, all spans zero-cost. Context propagation requires explicit parent, otherwise breaks.
- `Warm` with `ParentBasedSampler(AlwaysOffSampler)`: Root spans return null, child spans with propagated parent create minimal Activity for trace ID continuity.

**Validation metrics:**

| Metric | Measurement | Target |
|--------|-------------|--------|
| Idle allocation rate | `dotnet-counters` → `gc-heap-size` delta over 60s with no subscriptions | 0 bytes growth |
| Activation latency | Time from `POST /observe` to first span arriving at exporter | < 50ms |
| Teardown completeness | Span count: created vs exported after `DELETE` | 100% (no span loss) |
| Memory reclamation | Heap delta before subscription vs after teardown + one GC cycle | < 1KB residual |
| Idempotency | Duplicate subscription requests → verify single pipeline via exporter span count | No double export |

### Phase 2: Catalog Endpoint

Add `GET /api/v1/observe/catalog` returning available domains, their attribute manifests, and active subscriptions. Initially hardcoded from the four known domains in `ActivitySources.cs`.

**Validation:** A dashboard or MCP tool can query the catalog and display available domains without any prior knowledge of the schema.

### Phase 3: Subscription Contracts (from the interceptor generator)

Extend `servicedefaults.generator` to emit a second output alongside interceptors: typed subscription contracts. Each contract is a static manifest describing the attributes that a domain's interceptors will set on spans.

The collector loads these contracts at startup and validates them against `SpanStorageRowMap` — does every contract attribute have a corresponding property in `SpanStorageRow` with a mapping in the appender? Mismatches are logged as warnings at startup, not discovered via silent data loss during ingestion.

The catalog endpoint (`Phase 2`) resolves contracts from the generated code instead of hardcoded definitions.

**Validation:** Add a `gen_ai.new_attribute` to the interceptor generator's input schema. The contract now includes it. If `SpanStorageRow` doesn't have the property and `SpanStorageRowMap` doesn't map it, the collector logs a mismatch at startup.

### Phase 4: Schema Negotiation

Subscriptions include a schema version. If the app's semconv pin (e.g., 1.40) differs from the collector's (e.g., 1.39), the contract surfaces the delta at subscription time. The system can:

- Accept (versions are compatible, no breaking changes)
- Transform (map renamed attributes to their old/new names)
- Reject (incompatible schema, explicit error rather than silent data loss)

**Validation:** Multi-version environments (e.g., during a rolling semconv upgrade) work without silent data loss or missing columns.

---

## Risks and Open Questions

### Contract-to-appender validation gap

`servicedefaults.generator` is a public NuGet package (compile-time). `SpanStorageRowMap` is internal to the collector (runtime). Contracts are generated at compile time from the interceptor's attribute definitions, but they validate against a hand-written appender map at collector startup. If a developer adds a property to `SpanStorageRow` and maps it in the appender but the interceptor generator doesn't know about that attribute, the contract won't flag it — it only catches the reverse direction (generator attribute with no storage mapping). A future enhancement could generate the appender map itself from the contract, closing the loop entirely.

### Subscription security

`POST /api/v1/observe` is a control plane endpoint. It activates data pipelines in remote applications. In the current codebase, `ObserveEndpoints.cs` has no authentication. This needs:

- Authentication (at minimum, the same workspace token that `GitHubEndpoints` uses)
- Authorization (which users/services can create subscriptions for which domains)
- Rate limiting (prevent subscription storms)
- Subscription limits (maximum concurrent subscriptions per workspace)

### Multi-process activation

If the app runs in multiple pods, a single subscription needs to activate listeners across all instances. Two models:

| Model | Mechanism | Tradeoff |
|-------|-----------|----------|
| **Push** | Collector sends subscription activation via gRPC/HTTP to each pod | Requires service discovery, fan-out complexity |
| **Pull** | Each pod polls the collector for active subscriptions at startup and periodically | Simpler, but activation latency = poll interval |

The pull model is simpler and works well with Kubernetes service discovery. Each pod calls `GET /api/v1/observe` at startup and subscribes to the active set. New subscriptions are picked up on the next poll.

### Warm mode and context propagation cost

Warm mode creates minimal Activities for child spans with propagated context. In a deep call chain (10+ services), each service creates one Activity per request for propagation — even when no data is recorded. This is far less than full instrumentation, but not truly zero.

Measure: Is the propagation cost acceptable for your deployment? For most services, one minimal Activity per request is negligible. For ultra-high-throughput services (>100K req/s), even this may matter.

### Metrics subscription lifecycle

Observable instruments (gauges, observable counters) require periodic polling via `MeterListener.RecordObservableInstruments()`. The subscription manager needs a timer per metrics subscription — and that timer must be properly disposed on teardown to avoid leaks. The traces subscription model (purely event-driven) doesn't have this complexity.

---

## Conclusion

The components for zero-cost-until-observed already exist across the .NET runtime and the qyl codebase:

| Component | Where it lives |
|-----------|---------------|
| `HasListeners()` → null return path | .NET runtime (`System.Diagnostics.Activity`) |
| Dynamic subscription wiring | `src/qyl.collector/Observe/SubscriptionManager.cs` |
| HTTP subscription endpoints | `src/qyl.collector/Observe/ObserveEndpoints.cs` |
| Lazy ActivitySources + Meters | `src/qyl.servicedefaults/Instrumentation/ActivitySources.cs` |
| Generated interceptors with null-guard | `src/qyl.servicedefaults.generator/Emitters/TracedInterceptorEmitter.cs` |
| DuckDB storage via Appender | `src/qyl.collector/Storage/SpanAppender.cs` (post-purge) |
| SSE streaming with connection lifecycle | `src/qyl.collector/Realtime/SseEndpoints.cs` |
| TypeSpec schemas with semconv 1.40 | `core/specs/main.tsp` |

What's missing is the mode switch (Phase 1), the catalog (Phase 2), and the contracts (Phases 3-4). Phase 1 is a single `enum` and a sampler swap in `ConfigureOpenTelemetry()`. Phase 2 is a new endpoint. Phases 3-4 extend the existing interceptor generator and add startup validation against the hand-written appender map.

The end state is something the observability industry hasn't built: **telemetry that costs nothing until the moment you need it, with compile-time proof that the data will arrive in the shape you expect, activated by a single HTTP request and torn down when you disconnect.**

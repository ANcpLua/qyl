# Zero-Cost-Until-Observed: Compiled Observability Contracts

> Merged from: `zero-cost-observability-proposal.md` (vision) + `plans/phase-3-4-5-implementation-spec.md` (
> implementation)

## Implementation Status

| Phase                           | Status      | Notes                                                               |
|---------------------------------|-------------|---------------------------------------------------------------------|
| Phase 0: Subscription Manager   | **DONE**    | `SubscriptionManager.cs`, `ObserveEndpoints.cs`                     |
| Phase 1: Observability Modes    | **DONE**    | Partially — `ObserveCatalog.cs`, `SchemaVersionNegotiator.cs` exist |
| Phase 2: Catalog Endpoint       | **DONE**    | `GET /api/v1/observe/catalog` exists                                |
| Phase 3: Subscription Contracts | NOT STARTED | Roslyn generator extension                                          |
| Phase 4: Schema Negotiation     | NOT STARTED | Multi-version semconv support                                       |
| Phase 5: Contract validation    | NOT STARTED | Startup appender-map verification                                   |

---

## Part 1: Vision

# Zero-Cost-Until-Observed: Compiled Observability Contracts

## Summary

Observability today is always-on or always-off. qyl already has the foundation for a third option — instrumentation that
exists at zero cost until someone asks for it, with compile-time contracts that guarantee schema agreement between
producer and consumer before the first byte flows.

This is not a greenfield proposal. The subscription manager (`POST /api/v1/observe`) already ships. The
ActivitySource/Meter infrastructure is registered. The interceptor generator produces compile-time instrumentation, and
the storage layer is moving to `DuckDBAppenderMap<T>` for direct DuckDB writes. What's missing is the connective tissue:
a catalog that makes subscriptions discoverable, contracts that keep the interceptor attributes and storage columns in
sync, and an observability mode that makes zero-cost the default.

---

## What Exists Today

Before proposing anything new, here is what the codebase already provides:

### Dynamic Subscription Manager

**Files:** `src/qyl.collector/Observe/SubscriptionManager.cs`, `ObservationSubscription.cs`, `ObserveEndpoints.cs`

The collector exposes three endpoints:

| Endpoint                      | Purpose                                          |
|-------------------------------|--------------------------------------------------|
| `POST /api/v1/observe`        | Activate a subscription (filter + OTLP endpoint) |
| `DELETE /api/v1/observe/{id}` | Tear down a subscription                         |
| `GET /api/v1/observe`         | List active subscriptions                        |

Each subscription wires an `ActivityListener` with glob-style filter matching (`gen_ai.*`, `*`, exact match), backed by
a `BatchActivityExportProcessor` + `OtlpTraceExporter`. Subscriptions are idempotent (same filter + endpoint = reuse).
Teardown disposes listener first (stops new spans), then drains the processor buffer.

### Dormant ActivitySources and Meters

**File:** `src/qyl.servicedefaults/Instrumentation/ActivitySources.cs`

Four ActivitySources and two Meters, all lazy-initialized:

| Source         | Name         | Signal           |
|----------------|--------------|------------------|
| `GenAiSource`  | `qyl.genai`  | traces           |
| `DbSource`     | `qyl.db`     | traces           |
| `AgentSource`  | `qyl.agent`  | traces + metrics |
| `TracedSource` | `qyl.traced` | traces           |
| `GenAiMeter`   | `qyl.genai`  | metrics          |
| `AgentMeter`   | `qyl.agent`  | metrics          |

These are created via `??=` — they exist as static fields but the `ActivitySource`/`Meter` objects are only allocated on
first access.

### Interceptor Generator + Storage Appender

| Component                  | File                                  | Type                           | Consumed by    | Produces                                                 |
|----------------------------|---------------------------------------|--------------------------------|----------------|----------------------------------------------------------|
| `TracedInterceptorEmitter` | `servicedefaults.generator/Emitters/` | Roslyn IIncrementalGenerator   | Apps via NuGet | Before/after method hooks, tag setters, exception events |
| `SpanStorageRowMap`        | `collector/Storage/SpanAppender.cs`   | Runtime `DuckDBAppenderMap<T>` | Collector only | Direct DuckDB column mapping for bulk insert             |

These two components consume the same semantic convention vocabulary — the interceptor sets `gen_ai.system` as a span
tag, the appender writes `GenAiProviderName` to DuckDB. But the link between them is convention, not construction.
Renaming a property on `SpanStorageRow` without updating the appender map is a silent data loss bug. This is the gap
that subscription contracts are designed to close.

> **Note:** The previous `DuckDbInsertGenerator` Roslyn source generator is being replaced by `DuckDBAppenderMap<T>` (
> see `hades-storage-purge.md` in this directory). The appender approach is simpler (27 lines vs a full generator
> project), faster (direct DuckDB writes, no parameterized SQL), and removes the `(decimal)ulong` UBIGINT workaround. The
> cost is that column mapping is no longer enforced at compile time — making the contract model more valuable, not less.

### SSE Streaming Infrastructure

**Files:** `src/qyl.collector/Realtime/SseEndpoints.cs`, `SpanRingBuffer.cs`

The collector already streams live telemetry via SSE at `/api/v1/live` and `/api/v1/live/spans`. Each client gets a
bounded channel (backpressure via DropOldest). The connection lifecycle is managed via `RequestAborted` — disconnect =
unsubscribe.

### Current Observability Mode: Always-On

**File:** `src/qyl.servicedefaults/Instrumentation/QylServiceDefaultsExtensions.cs`, line 255

```csharp
tracing.SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
```

`UseQyl()` registers `TracerProvider` with `AlwaysOnSampler` at startup. This means:

- `HasListeners()` is **always true** for all registered sources
- Every `StartActivity()` creates a full `Activity` object
- All spans are exported to the OTLP endpoint (if configured or auto-discovered)

The subscription manager adds **additional** listeners on top of this. Currently, it's an additive layer over an
always-on pipeline — not a zero-cost activation mechanism.

---

## The Insight

### .NET already has the zero-cost primitive

`ActivitySource.StartActivity()` checks `HasListeners()` before allocating anything. If no `ActivityListener` is
registered for that source, it returns `null`. No object creation. No attribute evaluation. No processor callbacks.
Exactly zero allocation.

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

If `StartActivity()` returns null, no tags are set, no events are added, no exception data is captured. The entire
instrumentation block is a null check.

### Correcting a common misconception about AlwaysOffSampler

An earlier version of this document stated: *"Even with `AlwaysOffSampler`, the .NET SDK still creates Activity
objects."* This is wrong.

Here is what actually happens with `AlwaysOffSampler`:

| Scenario                          | Sampler result             | StartActivity returns                       | Overhead                                             |
|-----------------------------------|----------------------------|---------------------------------------------|------------------------------------------------------|
| Root span, no parent context      | `Drop` → `None`            | `null`                                      | Zero                                                 |
| Child span with propagated parent | `Drop` → `PropagationData` | Minimal Activity (no attributes, no export) | One allocation for context propagation, no recording |

**Root spans with `AlwaysOffSampler` are truly zero-cost** — `StartActivity()` returns null. Child spans create a
minimal Activity solely to propagate the W3C trace context (trace ID, span ID), but no attributes are evaluated, no
processors fire, and nothing is exported.

This distinction matters for the architecture: if the sampler is `AlwaysOff`, the `TracerProvider` listener is
registered but causes zero allocation for root spans and minimal allocation for propagated contexts.

### What's missing is the mode switch

The runtime primitive exists. The subscription manager exists. The generators exist. What's missing is a way to say: *"
Start with everything dormant. Activate specific domains on demand."*

Currently, `UseQyl()` forces `AlwaysOnSampler`. To enable zero-cost-until-observed, the system needs an alternative
configuration mode where:

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

This is what ships today. Every ActivitySource produces spans, every span is exported. Simple, comprehensive, and always
costs CPU/memory/network.

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

**Use when:** Production with hundreds of services, cost-sensitive environments, services that are instrumented
comprehensively but observed selectively.

### Mode 3: Warm (proposed, builds on Mode 2)

```
UseQyl(o => o.ObservabilityMode = ObservabilityMode.Warm)
  →  AlwaysOffSampler → root spans return null, but child spans propagate context
  →  trace IDs chain correctly across services
  →  POST /api/v1/observe { filter: "gen_ai.*" }
  →  subscription activates full recording
  →  spans include valid parent trace IDs from warm propagation
```

Warm mode solves a real problem: in pure zero-cost mode, if Service A has a subscription but Service B doesn't, the
trace chain breaks at B because B never creates Activities. In warm mode, B creates minimal Activities for context
propagation (one allocation, no attributes, no export), preserving the trace ID chain.

**Use when:** Distributed systems where trace continuity matters, but most services shouldn't actively export until
asked.

---

## Signal-by-Signal Analysis

### Traces: Full zero-cost support

| Primitive                        | Guard                        | Zero-cost path                             |
|----------------------------------|------------------------------|--------------------------------------------|
| `ActivitySource.StartActivity()` | `HasListeners()` → `null`    | No allocation, no processing               |
| Generated interceptor            | `activity is not null` check | Tags, events, exception recording all skip |
| `BatchActivityExportProcessor`   | Only created by subscription | No buffer, no timer, no export overhead    |

Traces are the ideal signal for zero-cost-until-observed. The runtime, the generators, and the subscription manager all
support this fully.

### Metrics: Partial zero-cost support

.NET's `Meter`/`Instrument` API has a listener pattern similar to ActivitySource, but with a critical distinction:

| Instrument type                               | Mechanism                                                                        | Zero-cost when idle                                                            |
|-----------------------------------------------|----------------------------------------------------------------------------------|--------------------------------------------------------------------------------|
| **Counter, Histogram** (push)                 | `MeterListener.SetMeasurementEventCallback<T>()` receives values when recorded   | Yes — if no MeterListener is registered, `Add()`/`Record()` effectively no-ops |
| **ObservableGauge, ObservableCounter** (pull) | Callback only fires when `MeterListener.RecordObservableInstruments()` is called | Yes — nobody polls, callback never runs                                        |

Both types are zero-cost when idle. The difference is operational:

- **Push instruments** (Counter, Histogram): The subscription wires a `MeterListener` that receives measurements via
  callback. Same model as traces — subscribe and measurements start flowing.
- **Pull instruments** (ObservableGauge): The subscription must periodically call `RecordObservableInstruments()` to
  poll values. This requires a timer per subscription, adding a small coordination cost that traces don't have.

The existing `GenAiMeter` and `AgentMeter` in `ActivitySources.cs` would need to be included in the subscription model.
A subscription to `gen_ai.*` should activate both the `ActivityListener` (traces) and a `MeterListener` (metrics) for
the `qyl.genai` source.

### Logs: No zero-cost equivalent

.NET logging (`ILoggerProvider`/`ILogger`) does not have a listener pattern comparable to
`ActivitySource.HasListeners()`. The guards available are:

| Guard                         | What it does                                   | Limitation                                                                   |
|-------------------------------|------------------------------------------------|------------------------------------------------------------------------------|
| `ILogger.IsEnabled(LogLevel)` | Checks minimum log level                       | Level-based, not listener-based; always evaluates if level >= minimum        |
| `[LoggerMessage]` source gen  | Checks `IsEnabled` before string interpolation | Avoids allocation for below-threshold logs, but the check itself always runs |

There is no way to make logging truly zero-cost. `IsEnabled()` returns true whenever the configured minimum level
matches, regardless of whether any exporter is connected.

**Recommendation:** Logs should not be part of the zero-cost subscription model. They operate on a different cost
model (level-based filtering, not listener-based activation). The subscription API should support
`"signals": ["traces"]` and `"signals": ["traces", "metrics"]` but not `"signals": ["logs"]`.

---

## The Contract Model

### Why contracts matter

The subscription manager today accepts a string filter: `"gen_ai.*"`. It doesn't know:

- What attributes spans matching that filter will carry
- Whether the DuckDB schema has columns for those attributes
- What semconv version the producer is using

The collector discovers the schema at ingestion time — whatever attributes arrive get mapped to DuckDB columns by the
`SpanStorageRowMap` appender mapping. If an attribute arrives that has no column mapping, it's silently dropped. If a
column mapping exists that no attribute provides, it's null.

This works, but it's the same runtime-schema-discovery model that every other observability platform uses. With the
storage layer moving from a Roslyn generator to a hand-written `DuckDBAppenderMap<T>`, the link between interceptor
attributes and storage columns is now maintained by convention alone. The contract formalizes that convention into a
verifiable artifact.

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

A `.proto` file generates both the client stub and the server handler, guaranteeing type compatibility across the wire.
Here, the semconv schema generates the interceptor (what attributes a span will have), and the contract verifies that
the hand-written appender map (what columns DuckDB stores) agrees with those attributes. The contract bridges
compile-time generation and runtime mapping.

Without it: the collector hopes the incoming spans match its schema. With it: the system knows at subscription time
whether the producer and consumer agree.

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

The catalog is the UI-facing feature that gives immediate value. A dashboard can display which domains are available,
which are active, and what data each one produces — before any spans flow.

### SSE Convergence: Connection-Scoped Subscriptions

The existing SSE infrastructure and the subscription manager can converge. Opening an SSE connection with an observe
filter simultaneously activates the subscription and streams the results:

```
GET /api/v1/live?observe=gen_ai.*

→ Creates subscription (activates ActivityListener for gen_ai.*)
→ Streams resulting spans as SSE events
→ Client disconnects → subscription torn down automatically
→ Back to zero
```

This means: **the SSE connection IS the subscription.** No explicit POST/DELETE needed for interactive use cases (
dashboard, debugging tools). The existing `RequestAborted` handler in `SseEndpoints.cs` already manages connection
lifecycle.

Explicit `POST /api/v1/observe` endpoints remain for headless subscriptions (where the export target is a different
collector, not the SSE stream).

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
- `OnDemand` with `AlwaysOffSampler`: Root spans return null, all spans zero-cost. Context propagation requires explicit
  parent, otherwise breaks.
- `Warm` with `ParentBasedSampler(AlwaysOffSampler)`: Root spans return null, child spans with propagated parent create
  minimal Activity for trace ID continuity.

**Validation metrics:**

| Metric                | Measurement                                                                      | Target              |
|-----------------------|----------------------------------------------------------------------------------|---------------------|
| Idle allocation rate  | `dotnet-counters` → `gc-heap-size` delta over 60s with no subscriptions          | 0 bytes growth      |
| Activation latency    | Time from `POST /observe` to first span arriving at exporter                     | < 50ms              |
| Teardown completeness | Span count: created vs exported after `DELETE`                                   | 100% (no span loss) |
| Memory reclamation    | Heap delta before subscription vs after teardown + one GC cycle                  | < 1KB residual      |
| Idempotency           | Duplicate subscription requests → verify single pipeline via exporter span count | No double export    |

### Phase 2: Catalog Endpoint

Add `GET /api/v1/observe/catalog` returning available domains, their attribute manifests, and active subscriptions.
Initially hardcoded from the four known domains in `ActivitySources.cs`.

**Validation:** A dashboard or MCP tool can query the catalog and display available domains without any prior knowledge
of the schema.

### Phase 3: Subscription Contracts (from the interceptor generator)

Extend `servicedefaults.generator` to emit a second output alongside interceptors: typed subscription contracts. Each
contract is a static manifest describing the attributes that a domain's interceptors will set on spans.

The collector loads these contracts at startup and validates them against `SpanStorageRowMap` — does every contract
attribute have a corresponding property in `SpanStorageRow` with a mapping in the appender? Mismatches are logged as
warnings at startup, not discovered via silent data loss during ingestion.

The catalog endpoint (`Phase 2`) resolves contracts from the generated code instead of hardcoded definitions.

**Validation:** Add a `gen_ai.new_attribute` to the interceptor generator's input schema. The contract now includes it.
If `SpanStorageRow` doesn't have the property and `SpanStorageRowMap` doesn't map it, the collector logs a mismatch at
startup.

### Phase 4: Schema Negotiation

Subscriptions include a schema version. If the app's semconv pin (e.g., 1.40) differs from the collector's (e.g., 1.39),
the contract surfaces the delta at subscription time. The system can:

- Accept (versions are compatible, no breaking changes)
- Transform (map renamed attributes to their old/new names)
- Reject (incompatible schema, explicit error rather than silent data loss)

**Validation:** Multi-version environments (e.g., during a rolling semconv upgrade) work without silent data loss or
missing columns.

---

## Risks and Open Questions

### Contract-to-appender validation gap

`servicedefaults.generator` is a public NuGet package (compile-time). `SpanStorageRowMap` is internal to the collector (
runtime). Contracts are generated at compile time from the interceptor's attribute definitions, but they validate
against a hand-written appender map at collector startup. If a developer adds a property to `SpanStorageRow` and maps it
in the appender but the interceptor generator doesn't know about that attribute, the contract won't flag it — it only
catches the reverse direction (generator attribute with no storage mapping). A future enhancement could generate the
appender map itself from the contract, closing the loop entirely.

### Subscription security

`POST /api/v1/observe` is a control plane endpoint. It activates data pipelines in remote applications. In the current
codebase, `ObserveEndpoints.cs` has no authentication. This needs:

- Authentication (at minimum, the same workspace token that `GitHubEndpoints` uses)
- Authorization (which users/services can create subscriptions for which domains)
- Rate limiting (prevent subscription storms)
- Subscription limits (maximum concurrent subscriptions per workspace)

### Multi-process activation

If the app runs in multiple pods, a single subscription needs to activate listeners across all instances. Two models:

| Model    | Mechanism                                                                         | Tradeoff                                        |
|----------|-----------------------------------------------------------------------------------|-------------------------------------------------|
| **Push** | Collector sends subscription activation via gRPC/HTTP to each pod                 | Requires service discovery, fan-out complexity  |
| **Pull** | Each pod polls the collector for active subscriptions at startup and periodically | Simpler, but activation latency = poll interval |

The pull model is simpler and works well with Kubernetes service discovery. Each pod calls `GET /api/v1/observe` at
startup and subscribes to the active set. New subscriptions are picked up on the next poll.

### Warm mode and context propagation cost

Warm mode creates minimal Activities for child spans with propagated context. In a deep call chain (10+ services), each
service creates one Activity per request for propagation — even when no data is recorded. This is far less than full
instrumentation, but not truly zero.

Measure: Is the propagation cost acceptable for your deployment? For most services, one minimal Activity per request is
negligible. For ultra-high-throughput services (>100K req/s), even this may matter.

### Metrics subscription lifecycle

Observable instruments (gauges, observable counters) require periodic polling via
`MeterListener.RecordObservableInstruments()`. The subscription manager needs a timer per metrics subscription — and
that timer must be properly disposed on teardown to avoid leaks. The traces subscription model (purely event-driven)
doesn't have this complexity.

---

## Conclusion

The components for zero-cost-until-observed already exist across the .NET runtime and the qyl codebase:

| Component                               | Where it lives                                                           |
|-----------------------------------------|--------------------------------------------------------------------------|
| `HasListeners()` → null return path     | .NET runtime (`System.Diagnostics.Activity`)                             |
| Dynamic subscription wiring             | `src/qyl.collector/Observe/SubscriptionManager.cs`                       |
| HTTP subscription endpoints             | `src/qyl.collector/Observe/ObserveEndpoints.cs`                          |
| Lazy ActivitySources + Meters           | `src/qyl.servicedefaults/Instrumentation/ActivitySources.cs`             |
| Generated interceptors with null-guard  | `src/qyl.servicedefaults.generator/Emitters/TracedInterceptorEmitter.cs` |
| DuckDB storage via Appender             | `src/qyl.collector/Storage/SpanAppender.cs` (post-purge)                 |
| SSE streaming with connection lifecycle | `src/qyl.collector/Realtime/SseEndpoints.cs`                             |
| TypeSpec schemas with semconv 1.40      | `core/specs/main.tsp`                                                    |

What's missing is the mode switch (Phase 1), the catalog (Phase 2), and the contracts (Phases 3-4). Phase 1 is a single
`enum` and a sampler swap in `ConfigureOpenTelemetry()`. Phase 2 is a new endpoint. Phases 3-4 extend the existing
interceptor generator and add startup validation against the hand-written appender map.

The end state is something the observability industry hasn't built: **telemetry that costs nothing until the moment you
need it, with compile-time proof that the data will arrive in the shape you expect, activated by a single HTTP request
and torn down when you disconnect.**


---

## Part 2: Implementation Specification (Phases 3-5)

# Zero-Cost-Until-Observed: Phases 3, 4, 5 — Full Implementation Specification

## Scope

This document is the single authoritative implementation reference for Phases 3, 4, and 5 of the
Zero-Cost-Until-Observed proposal (`docs/zero-cost-observability-proposal.md`). Phases 0, 1, and 2 are
already shipped. Do not touch them.

Each phase is self-contained and must be implemented independently in the order specified. No phase
depends on the next being started. Each phase ends with a mandatory validation gate before the next begins.

The work touches **four** areas of the repository.

| Area                                  | Projects                              |
|---------------------------------------|---------------------------------------|
| Nuke build system                     | `eng/build/`                          |
| Roslyn generator (public NuGet)       | `src/qyl.servicedefaults.generator/`  |
| Roslyn generator (collector-internal) | `src/qyl.instrumentation.generators/` |
| Collector observe subsystem           | `src/qyl.collector/Observe/`          |

**Public API is out of scope.** All work is `internal`. Do not add public surface.
**Documentation is out of scope.** No XML docs, no README updates.
**Breaking changes are permitted** for `internal` types across all four areas.

## Current State — Read Before Starting

### What exists in `src/qyl.collector/Observe/`

```
ObservationSubscription.cs   — holds Id, Filter, Endpoint, CreatedAt, ActivityListener, IDisposable pipeline
SubscriptionManager.cs       — ConcurrentDictionary<string, ObservationSubscription>, Subscribe/Unsubscribe/GetAll
ObserveEndpoints.cs          — GET /, GET /catalog, POST /, DELETE /{id}
ObserveCatalog.cs            — static Build(SubscriptionManager) → CatalogResponse
                               hardcoded Domains[] array (gen_ai, db, traced, agent)
                               DTOs: CatalogResponse, CatalogDomain, CatalogAttribute, CatalogMetricInstrument
```

`ObservationSubscription` constructor signature.

```csharp
internal ObservationSubscription(
    string id, string filter, string endpoint,
    ActivityListener listener, IDisposable pipeline)
```

`ObservationSubscription` public properties: `Id`, `Filter`, `Endpoint`, `CreatedAt`.

`SubscriptionManager.Subscribe(string filter, string endpoint)` returns `ObservationSubscription`.

### Naming Conventions — Two Namespaces

The system uses two naming conventions that must not be conflated.

| Convention                | Example                                          | Where used                                                                  |
|---------------------------|--------------------------------------------------|-----------------------------------------------------------------------------|
| **Domain name** (semconv) | `gen_ai`, `db`, `traced`, `agent`                | `CatalogDomain.Name`, `qyl-extensions.json` facades                         |
| **ActivitySource name**   | `qyl.genai`, `qyl.db`, `qyl.traced`, `qyl.agent` | `CatalogDomain.Source`, `ActivitySource` registration, subscription filters |

Subscription filters match against **ActivitySource names**, not domain names. A filter of `"gen_ai.*"`
will **not** match source `"qyl.genai"`. Use `"qyl.genai"` for exact match or `"qyl.*"` for wildcard.

The `ObserveCatalog.Domains[]` array maps between the two: each `CatalogDomain` has both `Name` (domain)
and `Source` (ActivitySource). Phase 4's `ResolveContractHash` uses this mapping.

### What exists in `eng/build/`

`BuildPipeline.cs` (interface `IPipeline`) has a `Generate` target that calls.

```csharp
SchemaGenerator.Generate(openApiPath, paths.Protocol, paths.Collector, guard)
```

`CodegenPaths` record has.

```csharp
public AbsolutePath Protocol => Root / "src" / "qyl.protocol";
public AbsolutePath Collector => Root / "src" / "qyl.collector";
```

`GenerationGuard.WriteIfAllowed(AbsolutePath, string content, string name)` is the single write method
used by `SchemaGenerator`. Use it for all new file writes. It respects `--force`, `--dry-run`, and CI
change-detection.

### What exists in `eng/semconv/qyl-extensions.json`

The canonical domain attribute lists live here as `facades[].attributes` arrays.

- `gen_ai` facade: `upstreamPrefix = "gen_ai"`, `attributes` = 40+ attribute names from semconv 1.40
- `db` facade: `upstreamPrefix = "db"`, `attributes` = 12 attribute names
- Metric names in `customClasses.Metrics.values`

This JSON is the **source of truth** for Phase 3. Do not invent attribute lists.

### What exists in `src/qyl.servicedefaults.generator/`

No `Generated/` directory yet. The generator already has.

- `Emitters/TracedInterceptorEmitter.cs` — emits interceptors; hardcodes no attribute names
- `Models/Models.cs` — `TracedCallSite`, `TracedTagParameter`, `TracedReturnInfo`
- `Analyzers/TracedCallSiteAnalyzer.cs` — extracts call sites from Roslyn syntax

### What exists in `src/qyl.instrumentation.generators/`

No `Generated/` directory yet. The generator.

- `DuckDb/DuckDbInsertGenerator.cs` — reads `[DuckDbTable]`/`[DuckDbColumn]` attributes to generate
  `AddParameters`, `MapFromReader`, `BuildMultiRowInsertSql`
- Does **not** use any domain attribute lists; schema comes entirely from `[DuckDbColumn]` annotations

## Phase 3 — Schema as Data: ContractGenerator

### Goal

Eliminate duplicated schema knowledge. Today the attribute lists for `gen_ai`, `db`, `traced`, and `agent`
domains exist in two places: `ObserveCatalog.cs` (hardcoded `Domains[]`) and `qyl-extensions.json`
(the source of truth). Phase 3 makes `ObserveCatalog` read from a **generated** data file that is itself
derived from `qyl-extensions.json`. Both Roslyn generators gain access to the same file as a future
compile-time validation source.

### Files to Create

```
eng/build/ContractGenerator.cs                                        NEW
src/qyl.servicedefaults.generator/Generated/DomainContracts.g.cs     GENERATED (written by Nuke)
src/qyl.instrumentation.generators/Generated/DomainContracts.g.cs    GENERATED (written by Nuke, identical content)
```

### Files to Modify

```
eng/build/BuildPipeline.cs          add GenerateContracts target, wire into Generate
eng/build/BuildPaths.cs             add ServiceDefaultsGenerator and InstrumentationGenerators paths to CodegenPaths
src/qyl.collector/Observe/ObserveCatalog.cs    replace hardcoded Domains[] with DomainContracts.All
```

### Step 1 — Extend `CodegenPaths`

In `eng/build/BuildPaths.cs`, add to `CodegenPaths`.

```csharp
public AbsolutePath ServiceDefaultsGenerator => Root / "src" / "qyl.servicedefaults.generator";
public AbsolutePath InstrumentationGenerators => Root / "src" / "qyl.instrumentation.generators";
```

No other changes to `BuildPaths.cs`.

### Step 2 — Create `eng/build/ContractGenerator.cs`

This file follows the same structural discipline as `SchemaGenerator.cs`: pure functions with IO only at
the edges. `ContractGenerator` is a `public static class` with a single public entry point.

```csharp
// eng/build/ContractGenerator.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Nuke.Common.IO;
using Serilog;

/// <summary>
///     Generates DomainContracts.g.cs from qyl-extensions.json into both generator projects.
///     Single entry point: <see cref="Generate"/>.
/// </summary>
public static class ContractGenerator
{
    private const string SchemaVersion = "semconv-1.40.0";

    /// <summary>
    ///     Reads qyl-extensions.json, emits DomainContracts.g.cs to both generator destinations.
    /// </summary>
    public static void Generate(
        AbsolutePath extensionsJsonPath,
        AbsolutePath serviceDefaultsGeneratorDir,
        AbsolutePath instrumentationGeneratorsDir,
        GenerationGuard guard)
    {
        if (!extensionsJsonPath.FileExists())
        {
            Log.Error("qyl-extensions.json not found at {Path}", extensionsJsonPath);
            throw new FileNotFoundException("qyl-extensions.json not found", extensionsJsonPath);
        }

        var domains = LoadDomains(extensionsJsonPath);
        Log.Information("Loaded {Count} domain(s) from qyl-extensions.json", domains.Count);

        var content = EmitDomainContracts(domains);

        var dest1 = serviceDefaultsGeneratorDir / "Generated" / "DomainContracts.g.cs";
        var dest2 = instrumentationGeneratorsDir / "Generated" / "DomainContracts.g.cs";

        guard.WriteIfAllowed(dest1, content, "DomainContracts.g.cs → servicedefaults.generator");
        guard.WriteIfAllowed(dest2, content, "DomainContracts.g.cs → instrumentation.generators");

        Log.Information("DomainContracts.g.cs written to 2 destination(s)");
    }

    // ── Domain loading ────────────────────────────────────────────────────────

    private static List<DomainSpec> LoadDomains(AbsolutePath extensionsJsonPath)
    {
        using var stream = File.OpenRead(extensionsJsonPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var domains = new List<DomainSpec>();

        // gen_ai domain — lookup by upstreamPrefix, not positional index
        var genAiFacade = FindFacade(root, "gen_ai");
        domains.Add(new DomainSpec(
            Name:     "gen_ai",
            Source:   "qyl.genai",
            Signals:  ["traces", "metrics"],
            TraceAttributes: ExtractAttributes(genAiFacade, requiredNames: ["gen_ai.operation.name", "gen_ai.provider.name", "gen_ai.request.model"]),
            MetricInstruments:
            [
                new MetricSpec("gen_ai.client.token.usage",       "histogram", "token"),
                new MetricSpec("gen_ai.client.operation.duration", "histogram", "s"),
            ]));

        // db domain — lookup by upstreamPrefix, not positional index
        var dbFacade = FindFacade(root, "db");
        domains.Add(new DomainSpec(
            Name:    "db",
            Source:  "qyl.db",
            Signals: ["traces"],
            TraceAttributes: ExtractAttributes(dbFacade, requiredNames: ["db.system.name", "db.operation.name"]),
            MetricInstruments: []));

        // traced domain — open schema, no fixed attributes
        domains.Add(new DomainSpec(
            Name:    "traced",
            Source:  "qyl.traced",
            Signals: ["traces"],
            TraceAttributes: [],
            MetricInstruments: []));

        // agent domain — subset of gen_ai attributes
        domains.Add(new DomainSpec(
            Name:    "agent",
            Source:  "qyl.agent",
            Signals: ["traces", "metrics"],
            TraceAttributes:
            [
                new AttributeSpec("gen_ai.agent.name",      "string", Required: false),
                new AttributeSpec("gen_ai.operation.name",  "string", Required: true),
            ],
            MetricInstruments: []));

        return domains;
    }

    private static JsonElement FindFacade(JsonElement root, string upstreamPrefix)
    {
        foreach (var facade in root.GetProperty("facades").EnumerateArray())
        {
            if (string.Equals(facade.GetProperty("upstreamPrefix").GetString(),
                    upstreamPrefix, StringComparison.Ordinal))
                return facade;
        }
        throw new InvalidOperationException(
            $"Facade with upstreamPrefix '{upstreamPrefix}' not found in qyl-extensions.json");
    }

    private static List<AttributeSpec> ExtractAttributes(
        JsonElement facade,
        string[] requiredNames)
    {
        // Derive type from attribute name heuristic:
        // *_tokens, *_count, *_size, max_tokens → int
        // *_temperature, *_top_p, *_top_k, *_penalty, *_score* → double
        // *_reasons, *_sequences, *_formats, *_messages → string[]
        // everything else → string

        var attrs = new List<AttributeSpec>();

        foreach (var nameElem in facade.GetProperty("attributes").EnumerateArray())
        {
            var name = nameElem.GetString()!;
            var type = InferType(name);
            var required = requiredNames.Contains(name);
            attrs.Add(new AttributeSpec(name, type, required));
        }

        return attrs;
    }

    private static string InferType(string attributeName)
    {
        var suffix = attributeName.Split('.')[^1];

        if (suffix is "tokens" || attributeName.EndsWith("_tokens", StringComparison.Ordinal)
                                || attributeName.EndsWith("_count", StringComparison.Ordinal)
                                || attributeName.EndsWith("_size", StringComparison.Ordinal)
                                || attributeName.EndsWith("max_tokens", StringComparison.Ordinal)
                                || attributeName.EndsWith("returned_rows", StringComparison.Ordinal)
                                || attributeName.EndsWith("batch.size", StringComparison.Ordinal))
            return "int";

        if (attributeName.EndsWith("_temperature", StringComparison.Ordinal)
         || attributeName.EndsWith("_top_p", StringComparison.Ordinal)
         || attributeName.EndsWith("_top_k", StringComparison.Ordinal)
         || attributeName.EndsWith("_penalty", StringComparison.Ordinal)
         || attributeName.EndsWith("score.value", StringComparison.Ordinal))
            return "double";

        if (attributeName.EndsWith("_reasons", StringComparison.Ordinal)
         || attributeName.EndsWith("_sequences", StringComparison.Ordinal)
         || attributeName.EndsWith("_formats", StringComparison.Ordinal)
         || attributeName.EndsWith("input.messages", StringComparison.Ordinal)
         || attributeName.EndsWith("output.messages", StringComparison.Ordinal))
            return "string[]";

        return "string";
    }

    // ── C# source emission ────────────────────────────────────────────────────

    private static string EmitDomainContracts(List<DomainSpec> domains)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by eng/build/ContractGenerator.cs — do not edit manually.");
        sb.AppendLine($"// Source: eng/semconv/qyl-extensions.json");
        sb.AppendLine($"// Schema: {SchemaVersion}");
        sb.AppendLine();
        sb.AppendLine("namespace qyl.Contracts;");
        sb.AppendLine();

        // AttributeDef
        sb.AppendLine("internal readonly record struct AttributeDef(");
        sb.AppendLine("    string Name,");
        sb.AppendLine("    string Type,");
        sb.AppendLine("    bool Required);");
        sb.AppendLine();

        // MetricDef
        sb.AppendLine("internal readonly record struct MetricDef(");
        sb.AppendLine("    string Name,");
        sb.AppendLine("    string Instrument,");
        sb.AppendLine("    string Unit);");
        sb.AppendLine();

        // DomainDef
        sb.AppendLine("internal readonly record struct DomainDef(");
        sb.AppendLine("    string Name,");
        sb.AppendLine("    string Source,");
        sb.AppendLine("    string[] Signals,");
        sb.AppendLine("    AttributeDef[] TraceAttributes,");
        sb.AppendLine("    MetricDef[] MetricInstruments,");
        sb.AppendLine("    string SchemaVersion);");
        sb.AppendLine();

        // DomainContracts static class
        sb.AppendLine("internal static class DomainContracts");
        sb.AppendLine("{");
        sb.AppendLine($"    internal const string SchemaVersion = \"{SchemaVersion}\";");
        sb.AppendLine();

        foreach (var domain in domains)
        {
            var fieldName = ToPascalCase(domain.Name);
            sb.AppendLine($"    internal static readonly DomainDef {fieldName} = new(");
            sb.AppendLine($"        Name:     \"{domain.Name}\",");
            sb.AppendLine($"        Source:   \"{domain.Source}\",");
            sb.Append(    $"        Signals:  [");
            sb.Append(string.Join(", ", domain.Signals.Select(static s => $"\"{s}\"")));
            sb.AppendLine("],");

            // TraceAttributes
            if (domain.TraceAttributes.Count == 0)
            {
                sb.AppendLine("        TraceAttributes: [],");
            }
            else
            {
                sb.AppendLine("        TraceAttributes:");
                sb.AppendLine("        [");
                foreach (var attr in domain.TraceAttributes)
                {
                    var req = attr.Required ? "Required: true" : "Required: false";
                    sb.AppendLine($"            new(\"{attr.Name}\", \"{attr.Type}\", {req}),");
                }
                sb.AppendLine("        ],");
            }

            // MetricInstruments
            if (domain.MetricInstruments.Count == 0)
            {
                sb.AppendLine("        MetricInstruments: [],");
            }
            else
            {
                sb.AppendLine("        MetricInstruments:");
                sb.AppendLine("        [");
                foreach (var metric in domain.MetricInstruments)
                    sb.AppendLine($"            new(\"{metric.Name}\", \"{metric.Instrument}\", \"{metric.Unit}\"),");
                sb.AppendLine("        ],");
            }

            sb.AppendLine($"        SchemaVersion: \"{SchemaVersion}\");");
            sb.AppendLine();
        }

        // All array
        sb.Append("    internal static readonly DomainDef[] All = [");
        sb.Append(string.Join(", ", domains.Select(static d => ToPascalCase(d.Name))));
        sb.AppendLine("];");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string ToPascalCase(string name)
    {
        // "gen_ai" → "GenAi", "db" → "Db", "traced" → "Traced", "agent" → "Agent"
        var sb = new StringBuilder();
        var nextUpper = true;
        foreach (var ch in name)
        {
            if (ch == '_' || ch == '.')
            {
                nextUpper = true;
            }
            else
            {
                sb.Append(nextUpper ? char.ToUpperInvariant(ch) : ch);
                nextUpper = false;
            }
        }
        return sb.ToString();
    }

    // ── Internal data model ───────────────────────────────────────────────────

    private sealed record DomainSpec(
        string Name,
        string Source,
        string[] Signals,
        List<AttributeSpec> TraceAttributes,
        List<MetricSpec> MetricInstruments);

    private sealed record AttributeSpec(string Name, string Type, bool Required);
    private sealed record MetricSpec(string Name, string Instrument, string Unit);
}
```

### Step 3 — Wire `GenerateContracts` into `BuildPipeline.cs`

In `eng/build/BuildPipeline.cs`, inside `IPipeline`, add after `GenerateSemconv`.

```csharp
Target GenerateContracts => d => d
    .Description("Generate DomainContracts.g.cs into both Roslyn generator projects")
    .DependsOn(GenerateSemconv)
    .Executes(() =>
    {
        var paths = CodegenPaths.From(this);
        var extensionsJson = SemconvDirectory / "qyl-extensions.json";
        var guard = IsServerBuild
            ? GenerationGuard.ForCi()
            : (DryRunGenerate ?? false)
                ? new GenerationGuard(dryRun: true)
                : GenerationGuard.ForLocal(ForceGenerate ?? false);

        ContractGenerator.Generate(
            extensionsJson,
            paths.ServiceDefaultsGenerator,
            paths.InstrumentationGenerators,
            guard);
    });
```

Then in the existing `Generate` target, add `.DependsOn(GenerateContracts)`.

```csharp
Target Generate => d => d
    .Description("Generate ALL code from TypeSpec God Schema (C# + DuckDB + TypeScript + Semconv + Contracts)")
    .DependsOn(TypeSpecCompile)
    .DependsOn(GenerateTypeScript)
    .DependsOn(GenerateSemconv)
    .DependsOn(GenerateContracts)   // ← ADD THIS LINE
    .Executes(() => { ... });       // body unchanged
```

And update the final log summary inside `Generate.Executes()`.

```csharp
Log.Information("  Contracts:   servicedefaults.generator/Generated/DomainContracts.g.cs");
Log.Information("  Contracts:   instrumentation.generators/Generated/DomainContracts.g.cs");
```

### Step 4 — Run Nuke to generate the files

```bash
nuke GenerateContracts
```

This writes identical `DomainContracts.g.cs` files to both destinations.
Verify both files exist and compile.

```bash
dotnet build src/qyl.servicedefaults.generator/qyl.servicedefaults.generator.csproj --no-restore -v q
dotnet build src/qyl.instrumentation.generators/qyl.instrumentation.generators.csproj --no-restore -v q
```

### Step 5 — Replace hardcoded `Domains[]` in `ObserveCatalog.cs`

`ObserveCatalog.cs` lives in `src/qyl.collector/Observe/`. It does **not** reference
`DomainContracts.g.cs` (that file is in the generator projects, not the collector). Instead,
`ObserveCatalog` keeps its own domain data — but it now mirrors the shape from `DomainContracts`.
This is intentional: the collector is a separate deployment unit from the generators.

The change in this step is narrower: make the `Domains[]` array construction use the same namespace
and type shape that Phase 4 will extend with `contract_hash`. **No behavioral change yet.**

In `ObserveCatalog.cs`, add a `ContractHash` placeholder field to `CatalogDomain`.

```csharp
internal sealed record CatalogDomain(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("source")]  string Source,
    [property: JsonPropertyName("signals")] string[] Signals,
    [property: JsonPropertyName("trace_attributes"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogAttribute>? TraceAttributes = null,
    [property: JsonPropertyName("metric_instruments"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<CatalogMetricInstrument>? MetricInstruments = null,
    [property: JsonPropertyName("contract_hash"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null);   // ← ADD THIS; populated in Phase 4
```

No other changes to `ObserveCatalog.cs` in this step.

### Phase 3 Validation Checklist

1. `nuke GenerateContracts` exits with code 0.
2. File `src/qyl.servicedefaults.generator/Generated/DomainContracts.g.cs` exists.
3. File `src/qyl.instrumentation.generators/Generated/DomainContracts.g.cs` exists.
4. Both files have identical content (byte-for-byte).
5. Both files declare `namespace qyl.Contracts`.
6. Both files contain `internal static class DomainContracts` with fields `GenAi`, `Db`, `Traced`, `Agent`, `All`.
7. `DomainContracts.All.Length == 4`.
8. `dotnet build src/qyl.servicedefaults.generator/...csproj --no-restore -v q` → 0 errors.
9. `dotnet build src/qyl.instrumentation.generators/...csproj --no-restore -v q` → 0 errors.
10. `CatalogDomain` in `ObserveCatalog.cs` has a nullable `ContractHash` property with `JsonIgnore(WhenWritingNull)`.
11. Running `nuke Generate` runs `GenerateContracts` as a dependency step.

## Phase 4 — Contracts as Values: Hash + Catalog + Subscription

### Goal

Make schema drift visible. Each domain in the catalog gets a deterministic `contract_hash` derived from
its attribute manifest. Each active subscription records the hash at the moment it was created.
If the collector is redeployed with a new semconv and a subscription was activated against the old schema,
the hashes differ — surfaced in the catalog response without any polling.

### Files to Create

None.

### Files to Modify

```
src/qyl.collector/Observe/ObserveCatalog.cs       add hash computation, wire to CatalogDomain
src/qyl.collector/Observe/ObservationSubscription.cs  add ContractHash property
src/qyl.collector/Observe/SubscriptionManager.cs  resolve domain hash on Subscribe
src/qyl.collector/Observe/ObserveEndpoints.cs     pass schema_version to Subscribe (prep for Phase 5)
```

### Step 1 — Add hash computation to `ObserveCatalog.cs`

Add a `using System.Security.Cryptography;` import.

Add a private static method `ComputeHash` inside `ObserveCatalog`.

```csharp
/// <summary>
/// Deterministic 8-hex-char contract hash.
/// Input: "{schema_version}:{domain_name}:{sorted_attribute_name:type:required,...}"
/// Stable across restarts and deployments for the same semconv pin.
/// </summary>
private static string ComputeHash(CatalogDomain domain)
{
    var input = string.Concat(
        SchemaVersion, ":",
        domain.Name, ":",
        domain.TraceAttributes is null
            ? string.Empty
            : string.Join(",", domain.TraceAttributes
                .OrderBy(static a => a.Name, StringComparer.Ordinal)
                .Select(static a => $"{a.Name}:{a.Type}:{(a.Required ? "1" : "0")}")));

    var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hash)[..8].ToLowerInvariant();
}
```

Modify the static `Domains` initializer: replace the `new(...)` calls with calls through a helper that
attaches the hash after construction. Because `CatalogDomain` is a record (immutable), compute the hash
and use `with`.

```csharp
private static readonly CatalogDomain[] Domains =
    BuildDomains().ToArray();

private static IEnumerable<CatalogDomain> BuildDomains()
{
    var raw = new CatalogDomain[]
    {
        new("gen_ai", "qyl.genai", ["traces", "metrics"],
            [ /* existing attributes unchanged */ ],
            [ /* existing metrics unchanged */ ]),

        new("db", "qyl.db", ["traces"],
            [ /* existing attributes unchanged */ ]),

        new("traced", "qyl.traced", ["traces"]),

        new("agent", "qyl.agent", ["traces", "metrics"],
            [ /* existing attributes unchanged */ ]),
    };

    foreach (var domain in raw)
        yield return domain with { ContractHash = ComputeHash(domain) };
}
```

The `Build` method is unchanged — it still returns `new CatalogResponse(SchemaVersion, Domains, active)`.

### Step 2 — Add `ContractHash` to `ObservationSubscription`

In `ObservationSubscription.cs`.

Add field and property.

```csharp
public string? ContractHash { get; }
```

Extend the constructor to accept the optional hash.

```csharp
internal ObservationSubscription(
    string id,
    string filter,
    string endpoint,
    ActivityListener listener,
    IDisposable pipeline,
    string? contractHash = null)   // ← ADD, default null for backward compat
{
    Id = id;
    Filter = filter;
    Endpoint = endpoint;
    ContractHash = contractHash;   // ← ADD
    CreatedAt = TimeProvider.System.GetUtcNow();
    _listener = listener;
    _pipeline = pipeline;
}
```

### Step 3 — Resolve domain hash in `SubscriptionManager.Subscribe`

`SubscriptionManager` resolves the contract hash by delegating to `ObserveCatalog.GetDomainHash`.
Since filters match **ActivitySource names** (e.g., `qyl.genai`) not domain names (e.g., `gen_ai`),
the resolution uses `MatchesFilter` against the known source names — the same logic the
`ActivityListener` uses.

Add a private static helper to `SubscriptionManager`.

```csharp
/// <summary>
/// Resolves the contract hash for the given filter by matching against known domain sources.
/// Returns null if the filter matches zero or multiple domains (wildcard = no single contract).
/// </summary>
private static string? ResolveContractHash(string filter)
{
    // All known domain source names (must match ObserveCatalog.Domains[].Source)
    ReadOnlySpan<string> sources = ["qyl.genai", "qyl.db", "qyl.traced", "qyl.agent"];

    string? matchedSource = null;
    foreach (var source in sources)
    {
        if (!MatchesFilter(source, filter))
            continue;

        if (matchedSource is not null)
            return null; // Multiple matches (e.g., wildcard "qyl.*") → no single contract

        matchedSource = source;
    }

    return matchedSource is not null
        ? ObserveCatalog.GetDomainHash(matchedSource)
        : null;
}
```

Add a new internal method to `ObserveCatalog`.

```csharp
/// <summary>Returns the contract hash for a given domain source name, or null if not found.</summary>
internal static string? GetDomainHash(string sourceName)
    => Array.Find(Domains, d => string.Equals(d.Source, sourceName, StringComparison.Ordinal))
            ?.ContractHash;
```

In `SubscriptionManager.Subscribe`, after computing the `id`, resolve the hash and pass it.

```csharp
var id = Guid.NewGuid().ToString("N");
var contractHash = ResolveContractHash(filter);   // ← ADD
var pipeline = new ExportPipeline(endpoint);

// ... listener creation unchanged ...

var subscription = new ObservationSubscription(id, filter, endpoint, listener, pipeline, contractHash);
```

### Step 4 — Expose `ContractHash` in catalog active subscriptions

`SubscriptionDto` currently has: `Id`, `Filter`, `Endpoint`, `CreatedAt`.
Add `ContractHash`.

```csharp
internal sealed record SubscriptionDto(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("filter")]      string Filter,
    [property: JsonPropertyName("endpoint")]    string Endpoint,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("contract_hash"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null);
```

In `ObserveCatalog.Build`, project the subscription dto including the hash.

```csharp
var active = subscriptions.GetAll()
    .Select(static s => new SubscriptionDto(s.Id, s.Filter, s.Endpoint, s.CreatedAt, s.ContractHash))
    .ToArray();
```

In `ObserveEndpoints.Subscribe`, the return path also creates a `SubscriptionDto`.

```csharp
var dto = new SubscriptionDto(
    subscription.Id, subscription.Filter, subscription.Endpoint,
    subscription.CreatedAt, subscription.ContractHash);   // ← ADD hash
```

### Phase 4 Validation Checklist

1. `dotnet build src/qyl.collector/qyl.collector.csproj --no-restore -v q` → same pre-existing errors, no new errors.
2. `GET /api/v1/observe/catalog` response: every domain object has a `"contract_hash"` field (8 hex chars).
3. All four domain hashes are distinct (no collision).
4. Same `nuke GenerateContracts` run → same hashes (determinism: run twice, compare output).
5. `POST /api/v1/observe` with `{"filter":"qyl.genai","endpoint":"http://localhost:4318"}`:
   response includes `"contract_hash"` field matching the value in the catalog for `gen_ai` domain.
6. `POST /api/v1/observe` with `{"filter":"qyl.*","endpoint":"..."}` (wildcard):
   response `contract_hash` is null (wildcard matches multiple domains, no single contract).
7. `GET /api/v1/observe/catalog` → `active_subscriptions[0].contract_hash` matches the value from step 5.
8. `SubscriptionDto` in `ObserveEndpoints.cs` has 5 parameters including `ContractHash`.
9. `ObservationSubscription.ContractHash` is nullable `string?`.
10. `ResolveContractHash("qyl.genai")` returns non-null hash matching catalog's gen_ai domain hash.
11. `ResolveContractHash("qyl.*")` returns null (multiple matches).
12. `ResolveContractHash("unknown.source")` returns null.

## Phase 5 — Schema Negotiation: Discriminated Union + Version Check

### Goal

Subscriptions declare their semconv version. If the collector's deployed schema version matches, the
subscription is accepted. If the delta is incompatible (major semconv bump with renamed required
attributes), the subscription is rejected with an explicit error instead of silently accepting data
that will be dropped at ingestion. `Transform` (attribute rename mapping) is defined in the type
hierarchy but not implemented — the union shape is final so call sites require no future changes.

### Files to Create

```
src/qyl.collector/Observe/SchemaVersionNegotiator.cs   NEW
```

### Files to Modify

```
src/qyl.collector/Observe/ObserveEndpoints.cs      add schema_version to SubscribeRequest, pattern match
src/qyl.collector/Observe/SubscriptionManager.cs   add overload Subscribe(filter, endpoint, schemaVersion)
src/qyl.collector/Observe/ObservationSubscription.cs   add SchemaVersion property
```

### Step 1 — Create `SchemaVersionNegotiator.cs`

```csharp
// src/qyl.collector/Observe/SchemaVersionNegotiator.cs
namespace qyl.collector.Observe;

/// <summary>
/// Negotiates schema version compatibility between a subscriber's declared semconv pin
/// and the collector's deployed semconv version.
///
/// Result hierarchy:
///   Accept  — versions are compatible; subscription proceeds as normal.
///   Reject  — versions are incompatible; subscription is refused with a reason.
///   Transform (declared, not yet implemented) — reserved for attribute rename mapping.
///
/// This is a pure function: no IO, no side effects, fully deterministic on inputs.
/// </summary>
internal static class SchemaVersionNegotiator
{
    /// <summary>Collector's deployed semconv version (matches DomainContracts.SchemaVersion).</summary>
    internal const string CollectorVersion = "semconv-1.40.0";

    // ── Result DU ─────────────────────────────────────────────────────────────

    internal abstract record NegotiationResult
    {
        /// <summary>Versions are compatible. Subscription proceeds.</summary>
        internal sealed record Accept(string CollectorVersion, string? RequestedVersion)
            : NegotiationResult;

        /// <summary>Versions are incompatible. Subscription is refused.</summary>
        internal sealed record Reject(string Reason, string CollectorVersion, string? RequestedVersion)
            : NegotiationResult;

        /// <summary>
        /// Reserved: versions differ but a known attribute rename mapping exists.
        /// Not implemented — declared here so future call sites require no changes.
        /// </summary>
        internal sealed record Transform(string CollectorVersion, string RequestedVersion)
            : NegotiationResult;
    }

    // ── Negotiation logic ─────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates compatibility between <paramref name="requestedVersion"/> (from the subscriber)
    /// and <see cref="CollectorVersion"/> (deployed).
    ///
    /// Rules:
    /// - null/empty requested version → Accept (subscriber does not declare a version; permissive).
    /// - Same version → Accept.
    /// - Minor/patch delta (same major semconv series) → Accept with warning surfaced in response.
    /// - Major or unknown delta → Reject.
    /// </summary>
    internal static NegotiationResult Negotiate(string? requestedVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
            return new NegotiationResult.Accept(CollectorVersion, null);

        if (string.Equals(requestedVersion, CollectorVersion, StringComparison.OrdinalIgnoreCase))
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);

        // Parse both versions. Expected format: "semconv-{major}.{minor}.{patch}"
        if (!TryParseSemconv(CollectorVersion, out var collectorParsed) ||
            !TryParseSemconv(requestedVersion, out var requestedParsed))
        {
            // Unrecognised format: accept permissively (do not block unknown version strings)
            return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
        }

        // Major version mismatch → Reject
        if (collectorParsed.Major != requestedParsed.Major)
        {
            return new NegotiationResult.Reject(
                Reason: $"Incompatible semconv major version: collector={CollectorVersion}, requested={requestedVersion}",
                CollectorVersion: CollectorVersion,
                RequestedVersion: requestedVersion);
        }

        // Minor/patch delta → Accept (potentially with schema drift, but not blocking)
        return new NegotiationResult.Accept(CollectorVersion, requestedVersion);
    }

    // ── Version parsing ───────────────────────────────────────────────────────

    private static bool TryParseSemconv(string version, out (int Major, int Minor, int Patch) parsed)
    {
        // Expected: "semconv-1.40.0"
        const string prefix = "semconv-";
        parsed = default;

        if (!version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = version[prefix.Length..].Split('.');
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor))
            return false;

        var patch = parts.Length >= 3 && int.TryParse(parts[2], out var p) ? p : 0;
        parsed = (major, minor, patch);
        return true;
    }
}
```

### Step 2 — Add `SchemaVersion` to `SubscribeRequest` and `ObservationSubscription`

In `ObserveEndpoints.cs`, extend `SubscribeRequest`.

```csharp
internal sealed record SubscribeRequest(
    [property: JsonPropertyName("filter")]         string Filter,
    [property: JsonPropertyName("endpoint")]       string Endpoint,
    [property: JsonPropertyName("schema_version")] string? SchemaVersion = null);
```

In `ObservationSubscription.cs`, add property (alongside `ContractHash`).

```csharp
public string? SchemaVersion { get; }
```

Extend the constructor.

```csharp
internal ObservationSubscription(
    string id,
    string filter,
    string endpoint,
    ActivityListener listener,
    IDisposable pipeline,
    string? contractHash = null,
    string? schemaVersion = null)
{
    Id = id;
    Filter = filter;
    Endpoint = endpoint;
    ContractHash = contractHash;
    SchemaVersion = schemaVersion;
    CreatedAt = TimeProvider.System.GetUtcNow();
    _listener = listener;
    _pipeline = pipeline;
}
```

### Step 3 — Add `Subscribe` overload to `SubscriptionManager`

Keep the existing `Subscribe(string filter, string endpoint)` untouched (backward compat for internal
callers). Add an overload with the schema version parameter.

```csharp
/// <summary>
/// Creates a new subscription with optional schema version declaration.
/// If <paramref name="schemaVersion"/> is provided, it is stored on the subscription
/// for drift detection in catalog responses.
///
/// Idempotency: filter + endpoint uniquely identifies a subscription pipeline.
/// SchemaVersion is metadata — it does not create a separate pipeline.
/// </summary>
public ObservationSubscription Subscribe(string filter, string endpoint, string? schemaVersion)
{
    // Idempotency: reuse if same filter+endpoint already active (schemaVersion is metadata, not identity)
    foreach (var existing in _subscriptions.Values)
    {
        if (string.Equals(existing.Filter, filter, StringComparison.Ordinal) &&
            string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
            return existing;
    }

    var id = Guid.NewGuid().ToString("N");
    var contractHash = ResolveContractHash(filter);
    var pipeline = new ExportPipeline(endpoint);

    var listener = new ActivityListener
    {
        ShouldListenTo = source => MatchesFilter(source.Name, filter),
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStopped = pipeline.OnEnd
    };

    ActivitySource.AddActivityListener(listener);

    var subscription = new ObservationSubscription(
        id, filter, endpoint, listener, pipeline, contractHash, schemaVersion);
    _subscriptions[id] = subscription;
    return subscription;
}

// Keep existing overload delegating to the new one
public ObservationSubscription Subscribe(string filter, string endpoint)
    => Subscribe(filter, endpoint, schemaVersion: null);
```

### Step 4 — Pattern match on `NegotiationResult` in `ObserveEndpoints.Subscribe`

Replace the entire `Subscribe` handler body.

```csharp
private static IResult Subscribe(SubscribeRequest req, SubscriptionManager manager)
{
    if (string.IsNullOrWhiteSpace(req.Filter))
        return Results.BadRequest(new { error = "filter is required" });

    if (string.IsNullOrWhiteSpace(req.Endpoint))
        return Results.BadRequest(new { error = "endpoint is required" });

    if (!Uri.TryCreate(req.Endpoint, UriKind.Absolute, out _))
        return Results.BadRequest(new { error = "endpoint must be an absolute URI" });

    var negotiation = SchemaVersionNegotiator.Negotiate(req.SchemaVersion);

    return negotiation switch
    {
        SchemaVersionNegotiator.NegotiationResult.Reject r =>
            Results.Conflict(new
            {
                error            = r.Reason,
                collector_version = r.CollectorVersion,
                requested_version = r.RequestedVersion
            }),

        SchemaVersionNegotiator.NegotiationResult.Accept a =>
            CreateSubscription(req, manager, a),

        // Transform: not yet implemented — treat as Accept
        SchemaVersionNegotiator.NegotiationResult.Transform t =>
            CreateSubscription(req, manager,
                new SchemaVersionNegotiator.NegotiationResult.Accept(
                    t.CollectorVersion, t.RequestedVersion)),

        _ => Results.StatusCode(500)
    };
}

private static IResult CreateSubscription(
    SubscribeRequest req,
    SubscriptionManager manager,
    SchemaVersionNegotiator.NegotiationResult.Accept negotiation)
{
    var subscription = manager.Subscribe(req.Filter, req.Endpoint, req.SchemaVersion);
    var dto = new SubscriptionDto(
        subscription.Id, subscription.Filter, subscription.Endpoint,
        subscription.CreatedAt, subscription.ContractHash);

    // Surface version delta as a warning field if versions differ
    if (negotiation.RequestedVersion is not null &&
        !string.Equals(negotiation.RequestedVersion, negotiation.CollectorVersion,
            StringComparison.OrdinalIgnoreCase))
    {
        return Results.Ok(new
        {
            subscription      = dto,
            schema_warning    = $"Semconv version mismatch: collector={negotiation.CollectorVersion}, requested={negotiation.RequestedVersion}. Schema attributes may differ.",
            collector_version = negotiation.CollectorVersion,
            requested_version = negotiation.RequestedVersion
        });
    }

    return Results.Ok(dto);
}
```

### Step 5 — Expose `schema_version` on the subscription in catalog active_subscriptions

In `SubscriptionDto`, add.

```csharp
internal sealed record SubscriptionDto(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("filter")]         string Filter,
    [property: JsonPropertyName("endpoint")]       string Endpoint,
    [property: JsonPropertyName("created_at")]     DateTimeOffset CreatedAt,
    [property: JsonPropertyName("contract_hash"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null,
    [property: JsonPropertyName("schema_version"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SchemaVersion = null);
```

In `ObserveCatalog.Build`, project the schema version.

```csharp
var active = subscriptions.GetAll()
    .Select(static s => new SubscriptionDto(
        s.Id, s.Filter, s.Endpoint, s.CreatedAt,
        s.ContractHash, s.SchemaVersion))
    .ToArray();
```

Same in `ObserveEndpoints.Subscribe` (`CreateSubscription` helper).

```csharp
var dto = new SubscriptionDto(
    subscription.Id, subscription.Filter, subscription.Endpoint,
    subscription.CreatedAt, subscription.ContractHash, subscription.SchemaVersion);
```

### Phase 5 Validation Checklist

1. `dotnet build src/qyl.collector/qyl.collector.csproj --no-restore -v q` → same pre-existing errors only, zero new
   errors.

2. `POST /api/v1/observe` with `{"filter":"qyl.genai","endpoint":"http://localhost:4318"}` (no schema_version).
    - Response is 200 with subscription DTO.
    - No `schema_warning` field in response.

3. `POST /api/v1/observe` with
   `{"filter":"qyl.genai","endpoint":"http://localhost:4318","schema_version":"semconv-1.40.0"}`.
    - Response is 200.
    - No `schema_warning` field (exact version match).

4. `POST /api/v1/observe` with
   `{"filter":"qyl.genai","endpoint":"http://localhost:4318","schema_version":"semconv-1.39.0"}`.
    - Response is 200 (minor delta → Accept).
    - Response body has `"schema_warning"` field mentioning version mismatch.
    - Response body has `"collector_version": "semconv-1.40.0"` and `"requested_version": "semconv-1.39.0"`.

5. `POST /api/v1/observe` with
   `{"filter":"qyl.genai","endpoint":"http://localhost:4318","schema_version":"semconv-2.0.0"}`.
    - Response is 409 Conflict.
    - Response body has `"error"` field describing incompatible major version.
    - Response body has `"collector_version"` and `"requested_version"` fields.

6. `POST /api/v1/observe` with
   `{"filter":"qyl.genai","endpoint":"http://localhost:4318","schema_version":"unknown-format"}`.
    - Response is 200 (unknown format → permissive Accept).
    - Subscription is created normally.

7. `GET /api/v1/observe/catalog` after step 3.
    - `active_subscriptions[0].schema_version == "semconv-1.40.0"`.
    - `active_subscriptions[0].contract_hash` matches the gen_ai domain hash from `domains[]`.

8. `SchemaVersionNegotiator.Negotiate(null)` returns `Accept`.
9. `SchemaVersionNegotiator.Negotiate("semconv-1.40.0")` returns `Accept`.
10. `SchemaVersionNegotiator.Negotiate("semconv-1.39.0")` returns `Accept`.
11. `SchemaVersionNegotiator.Negotiate("semconv-2.0.0")` returns `Reject`.
12. `NegotiationResult.Transform` is declared but never instantiated in production code paths.

## Cross-Phase Invariants

These must hold at every phase boundary and in the final state.

1. **No public API additions.** All new types, methods, and properties are `internal`. No public surface
   is added to any assembly.

2. **`ObservationSubscription` disposal is unchanged.** `ContractHash` and `SchemaVersion` are
   read-only metadata — they have no disposal behavior and cannot cause resource leaks.

3. **Idempotency is preserved.** Two identical `POST /observe` requests with the same
   `filter + endpoint` return the same subscription (not two separate pipelines).
   `SchemaVersion` is metadata on the subscription, not part of pipeline identity.
   The idempotency check in `SubscriptionManager.Subscribe` compares `filter + endpoint` only.

4. **`GenerationGuard.WriteIfAllowed` is the only write path.** `ContractGenerator` must not call
   `File.WriteAllText` directly. All writes go through `guard.WriteIfAllowed` to respect dry-run and
   CI constraints.

5. **Hash stability.** `ComputeHash` must produce identical output across restarts, OS, and .NET patch
   versions for the same attribute manifest. The input string is fully deterministic: sorted attribute
   names with ordinal sort, fixed format string, UTF-8 encoding, SHA-256. Do not use `GetHashCode()`,
   `string.GetHashCode()`, or any non-deterministic primitive.

6. **`DomainContracts.g.cs` is never edited manually.** Both files are in `Generated/` subdirectories.
   The `.gitattributes` or `.gitignore` for those directories should mark them as generated if the
   project uses such conventions. If not, the `<auto-generated />` header is sufficient.

7. **Backward compatibility within the Observe subsystem.** Any caller that previously called
   `manager.Subscribe(filter, endpoint)` continues to work without modification.
   `SubscriptionManager.Subscribe(string, string)` remains and delegates to the three-parameter overload.

8. **`NegotiationResult.Transform` is never returned.** The type is declared in the DU to make the
   `switch` expression exhaustive-by-intent. The default arm in the switch (`Transform → Accept`)
   ensures no 500s if it is somehow reached. Real `Transform` logic is a Phase 5.5 concern.

9. **Filters use ActivitySource names, not domain names.** Subscription filters like `"qyl.genai"` or
   `"qyl.*"` match against ActivitySource names registered in the process. Domain names (`gen_ai`, `db`)
   appear only in `CatalogDomain.Name` and `qyl-extensions.json`. The `ResolveContractHash` method
   uses `MatchesFilter` (same logic as `ActivityListener.ShouldListenTo`) to resolve hashes.

10. **`qyl-extensions.json` facade lookup is by `upstreamPrefix`, not positional index.** The
    `FindFacade` helper searches by `upstreamPrefix` field. Adding, removing, or reordering facades
    in the JSON does not break `ContractGenerator.LoadDomains`.

## File Change Summary

| File                                                                | Phase | Operation                                                                                          |
|---------------------------------------------------------------------|-------|----------------------------------------------------------------------------------------------------|
| `eng/build/BuildPaths.cs`                                           | 3     | Modify — add 2 path properties to `CodegenPaths`                                                   |
| `eng/build/ContractGenerator.cs`                                    | 3     | **Create**                                                                                         |
| `eng/build/BuildPipeline.cs`                                        | 3     | Modify — add `GenerateContracts` target, wire into `Generate`                                      |
| `src/qyl.servicedefaults.generator/Generated/DomainContracts.g.cs`  | 3     | **Generated by Nuke**                                                                              |
| `src/qyl.instrumentation.generators/Generated/DomainContracts.g.cs` | 3     | **Generated by Nuke**                                                                              |
| `src/qyl.collector/Observe/ObserveCatalog.cs`                       | 3 + 4 | Modify — add `ContractHash` to `CatalogDomain`; add `ComputeHash`, `BuildDomains`, `GetDomainHash` |
| `src/qyl.collector/Observe/ObservationSubscription.cs`              | 4 + 5 | Modify — add `ContractHash`, `SchemaVersion` properties; extend constructor                        |
| `src/qyl.collector/Observe/SubscriptionManager.cs`                  | 4 + 5 | Modify — add `ResolveContractHash`; add 3-param `Subscribe` overload                               |
| `src/qyl.collector/Observe/ObserveEndpoints.cs`                     | 4 + 5 | Modify — extend `SubscribeRequest`; extend `SubscriptionDto`; pattern-match on `NegotiationResult` |
| `src/qyl.collector/Observe/SchemaVersionNegotiator.cs`              | 5     | **Create**                                                                                         |

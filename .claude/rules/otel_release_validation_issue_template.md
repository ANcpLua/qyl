---
name: OTel platform release validation
about: Definition of Done and release validation matrix for OpenTelemetry platform completeness
title: "[Release Validation] OTel platform <version>"
labels: ["release-validation", "otel", "observability"]
assignees: []
---

# OpenTelemetry Platform Release Validation Matrix

## 0. Release metadata
- Release: `<version>`
- Target date: `<yyyy-mm-dd>`
- Owner: `@owner`
- Spec baseline: `OTel spec/API/SDK version(s) supported by this release`
- Scope: `backend / browser / agents / .NET / collector / UI / storage`
- Environments validated: `dev / staging / prod-like`

## 1. Claim scope
Mark only what this release explicitly claims.

- [ ] Full tracing support
- [ ] Full metrics support
- [ ] Full logs support
- [ ] Profile signal support
- [ ] OTLP/gRPC ingest
- [ ] OTLP/HTTP protobuf ingest
- [ ] OTLP/HTTP JSON ingest
- [ ] Browser/Web SDK support
- [ ] Auto-instrumentation support
- [ ] Zero-code auto-instrumentation for .NET
- [ ] Advanced semantic conventions coverage
- [ ] Multi-tenant production readiness

## 2. Global DoD gates
These must all be true before the release can be called done.

- [ ] Every claimed capability below has an exact source-of-truth path.
- [ ] Every claimed capability below has at least one reproducible validation method.
- [ ] Every claimed capability below has at least one screenshot, trace export, log excerpt, or artifact path.
- [ ] All screenshots/artifacts are stored under `artifacts/release-validation/<release>/`.
- [ ] All validations run in CI or in a documented local command sequence.
- [ ] Any claim-gated optional item left unsupported is explicitly marked `Out of scope`.
- [ ] No claimed capability depends on undocumented manual steps.
- [ ] Failure modes and known gaps are documented in the release notes.

## 3. Architecture truth model
This is the target shape being validated by this issue.

```text
applications / browsers / agents
   ↓
OpenTelemetry API + SDK + auto-instrumentation
   ↓
context propagation
(traceparent / tracestate / baggage)
   ↓
OTLP
   ├─ gRPC
   └─ HTTP
      (protobuf required in practice; HTTP/JSON optional if claimed)
   ↓
ingestion service / collector
   ↓
decode + normalize into internal model
   ↓
processing pipeline
   ↓
storage
   ├─ traces
   ├─ metrics
   ├─ logs
   └─ profiles
   ↓
query services
   ↓
dashboards / APIs / alerts
```

## 4. Interactive release validation checklist

### A. Producer-side contract

- [ ] **RVM-01 API**
  - DoD: Public telemetry API exists, is versioned, documented, and usable without internal knowledge.
  - Source of truth: `<path>`
  - Validation: `<unit/integration test, doc example, sample app>`
  - Screenshot / artifact: `<path>`
  - Notes: `<optional>`

- [ ] **RVM-02 SDK**
  - DoD: SDK can initialize providers, resources, processors, and exporters for all claimed signals.
  - Source of truth: `<path>`
  - Validation: `<path or command>`
  - Screenshot / artifact: `<path>`

- [ ] **RVM-03 Auto-instrumentation**
  - DoD: Claimed frameworks/libraries emit telemetry without manual span creation.
  - Source of truth: `<path>`
  - Validation: `<integration test / demo app>`
  - Screenshot / artifact: `<trace screenshot>`

- [ ] **RVM-04 Manual instrumentation**
  - DoD: Developers can create custom spans/metrics/logs and enrich them with attributes/events safely.
  - Source of truth: `<path>`
  - Validation: `<sample code + test>`
  - Screenshot / artifact: `<trace/log screenshot>`

- [ ] **RVM-05 Context propagation**
  - DoD: `traceparent`, `tracestate`, and `baggage` propagate across all claimed boundaries and preserve parent/child relationships.
  - Source of truth: `<path>`
  - Validation: `<distributed integration test>`
  - Screenshot / artifact: `<end-to-end trace screenshot>`

- [ ] **RVM-06 Resource attribution**
  - DoD: `service.name` and other claimed resource attributes are attached consistently and survive export/ingest.
  - Source of truth: `<path>`
  - Validation: `<test/query>`
  - Screenshot / artifact: `<resource attribute screenshot>`

- [ ] **RVM-07 Semantic conventions coverage**
  - DoD: Core semantic conventions are emitted correctly for all claimed domains; unsupported conventions are documented.
  - Source of truth: `<path>`
  - Validation: `<schema/assertion test>`
  - Screenshot / artifact: `<attribute validation evidence>`

### B. Signal support

- [ ] **RVM-08 Tracing**
  - DoD: Spans, events, links, status, attributes, parent/child structure, and trace lookup work end to end.
  - Source of truth: `<path>`
  - Validation: `<e2e trace test>`
  - Screenshot / artifact: `<trace UI screenshot>`

- [ ] **RVM-09 Metrics**
  - DoD: Claimed metric types and temporality behave correctly end to end, including labels/attributes and aggregation.
  - Source of truth: `<path>`
  - Validation: `<metrics test/query>`
  - Screenshot / artifact: `<dashboard screenshot>`

- [ ] **RVM-10 Logs**
  - DoD: Structured logs ingest, index, correlate to traces when claimed, and are queryable.
  - Source of truth: `<path>`
  - Validation: `<log ingestion test/query>`
  - Screenshot / artifact: `<log explorer screenshot>`

- [ ] **RVM-11 Profiles** *(claim-gated / emerging)*
  - DoD: Profiles can be ingested, stored, queried, and linked to services/traces when this release claims profile support.
  - Source of truth: `<path>`
  - Validation: `<test or benchmark>`
  - Screenshot / artifact: `<profile UI screenshot>`

### C. OTLP transport and ingestion

- [ ] **RVM-12 OTLP/gRPC exporter**
  - DoD: Producers can export all claimed signals over OTLP/gRPC successfully.
  - Source of truth: `<path>`
  - Validation: `<interop test / collector test>`
  - Screenshot / artifact: `<collector receive evidence>`

- [ ] **RVM-13 OTLP/HTTP protobuf exporter**
  - DoD: Producers can export all claimed signals over OTLP/HTTP protobuf successfully.
  - Source of truth: `<path>`
  - Validation: `<interop test / curl replay>`
  - Screenshot / artifact: `<collector receive evidence>`

- [ ] **RVM-14 OTLP/HTTP JSON** *(claim-gated)*
  - DoD: JSON payloads are accepted and normalized correctly when this release claims support.
  - Source of truth: `<path>`
  - Validation: `<json fixture test>`
  - Screenshot / artifact: `<request/response capture>`

- [ ] **RVM-15 OTLP/gRPC receiver**
  - DoD: Ingestion service accepts, authenticates, validates, and decodes gRPC OTLP traffic correctly.
  - Source of truth: `<path>`
  - Validation: `<receiver integration test>`
  - Screenshot / artifact: `<packet / server log / UI evidence>`

- [ ] **RVM-16 OTLP/HTTP receiver**
  - DoD: Ingestion service accepts, authenticates, validates, and decodes HTTP OTLP traffic correctly.
  - Source of truth: `<path>`
  - Validation: `<receiver integration test>`
  - Screenshot / artifact: `<request/response evidence>`

- [ ] **RVM-17 Decode + normalize**
  - DoD: gRPC and HTTP inputs normalize into the same internal model with no signal-specific loss beyond documented behavior.
  - Source of truth: `<path>`
  - Validation: `<golden fixture comparison>`
  - Screenshot / artifact: `<normalized output diff>`

### D. Pipeline, storage, and retrieval

- [ ] **RVM-18 Processing pipeline**
  - DoD: Batching, sampling, enrichment, retry, ordering expectations, and backpressure behavior are correct and documented.
  - Source of truth: `<path>`
  - Validation: `<stress/integration test>`
  - Screenshot / artifact: `<pipeline metrics / logs>`

- [ ] **RVM-19 Trace storage**
  - DoD: Trace writes, retention, retrieval by trace ID, and span search work for claimed scale and cardinality.
  - Source of truth: `<path>`
  - Validation: `<storage test/query>`
  - Screenshot / artifact: `<trace query screenshot>`

- [ ] **RVM-20 Metrics storage**
  - DoD: Metric writes, aggregation correctness, retention, and time-window queries work end to end.
  - Source of truth: `<path>`
  - Validation: `<query test>`
  - Screenshot / artifact: `<metrics graph screenshot>`

- [ ] **RVM-21 Log storage**
  - DoD: Structured log writes, filtering, correlation, retention, and search work end to end.
  - Source of truth: `<path>`
  - Validation: `<query test>`
  - Screenshot / artifact: `<log search screenshot>`

- [ ] **RVM-22 Profile storage** *(claim-gated)*
  - DoD: Profile writes, retention, retrieval, and UI/API lookup work when profile support is claimed.
  - Source of truth: `<path>`
  - Validation: `<query test>`
  - Screenshot / artifact: `<profile retrieval screenshot>`

- [ ] **RVM-23 Query services**
  - DoD: Programmatic query APIs return correct results and error shapes for traces, metrics, logs, and profiles as claimed.
  - Source of truth: `<path>`
  - Validation: `<API test suite>`
  - Screenshot / artifact: `<API response capture>`

- [ ] **RVM-24 Dashboards / APIs / alerts**
  - DoD: UI dashboards, public APIs, and alert rules work on top of stored telemetry and reflect current data correctly.
  - Source of truth: `<path>`
  - Validation: `<UI/API/alert integration test>`
  - Screenshot / artifact: `<dashboard + alert evidence>`

### E. Production readiness gates commonly missed

- [ ] **RVM-25 Security / auth / TLS**
  - DoD: Claimed endpoints enforce authN/authZ/TLS correctly and reject invalid requests safely.
  - Source of truth: `<path>`
  - Validation: `<security test>`
  - Screenshot / artifact: `<test output>`

- [ ] **RVM-26 Multi-tenancy / isolation** *(claim-gated)*
  - DoD: Tenant boundaries are enforced for ingest, storage, query, and UI access.
  - Source of truth: `<path>`
  - Validation: `<tenant isolation test>`
  - Screenshot / artifact: `<evidence>`

- [ ] **RVM-27 Self-observability**
  - DoD: The platform emits its own health metrics/traces/logs so dropped telemetry, queue pressure, and exporter errors are observable.
  - Source of truth: `<path>`
  - Validation: `<chaos/failure test>`
  - Screenshot / artifact: `<internal dashboard screenshot>`

- [ ] **RVM-28 Performance / scale / regression**
  - DoD: Claimed throughput, latency, memory, and storage cost envelopes are validated against production-like load.
  - Source of truth: `<path>`
  - Validation: `<benchmark / load test>`
  - Screenshot / artifact: `<benchmark report>`

- [ ] **RVM-29 Compatibility matrix**
  - DoD: Supported languages, runtimes, SDK versions, browsers, and agents are explicitly listed and validated.
  - Source of truth: `<path>`
  - Validation: `<matrix test report>`
  - Screenshot / artifact: `<matrix artifact>`

- [ ] **RVM-30 Documentation / examples / release notes**
  - DoD: Docs, setup guides, examples, and release notes match actual behavior and do not claim unsupported features.
  - Source of truth: `<path>`
  - Validation: `<docs review / sample app run>`
  - Screenshot / artifact: `<docs screenshot>`

## 5. .NET-specific validation matrix
Use this only if the release claims .NET support.

### A. .NET runtime + OTel bridge

- [ ] **DOTNET-01 Activity bridge**
  - DoD: `System.Diagnostics.Activity` is bridged into OTel spans correctly.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `<trace screenshot>`

- [ ] **DOTNET-02 DiagnosticSource integration**
  - DoD: `DiagnosticSource`-emitted operations are captured where claimed.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `<evidence>`

- [ ] **DOTNET-03 W3C trace context**
  - DoD: `traceparent` / `tracestate` propagation works across ASP.NET Core, HttpClient, gRPC, and messaging boundaries as claimed.
  - Source of truth: `<path>`
  - Validation: `<distributed test>`
  - Screenshot / artifact: `<end-to-end trace screenshot>`

### B. Automatic instrumentation coverage

- [ ] **DOTNET-04 ASP.NET Core**
  - Captures: incoming requests
  - DoD: Request spans include route/template/status/resource attributes and parent context.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-aspnetcore-trace.png`

- [ ] **DOTNET-05 HttpClient**
  - Captures: outgoing requests
  - DoD: Outbound spans include method, URL/route, status, timing, and propagated context.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-httpclient-trace.png`

- [ ] **DOTNET-06 SQL Client**
  - Captures: database queries
  - DoD: DB spans include system, operation, timing, and safe statement metadata per policy.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-sqlclient-trace.png`

- [ ] **DOTNET-07 Entity Framework**
  - Captures: ORM operations
  - DoD: EF-generated operations emit spans with correct parent linkage and duration.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-ef-trace.png`

- [ ] **DOTNET-08 Redis**
  - Captures: cache calls
  - DoD: Cache spans include command/action metadata, timing, and correlation.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-redis-trace.png`

- [ ] **DOTNET-09 gRPC**
  - Captures: RPC calls
  - DoD: Client/server spans and propagated trace context are correct for claimed gRPC scenarios.
  - Source of truth: `<path>`
  - Validation: `<integration test>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-grpc-trace.png`

### C. Zero-code auto-instrumentation

- [ ] **DOTNET-10 Agent bootstrap**
  - DoD: Zero-code auto-instrumentation can be enabled without application code changes.
  - Source of truth: `<path>`
  - Validation: `OTEL_DOTNET_AUTO_HOME=/otel` + `DOTNET_STARTUP_HOOKS=/otel/OpenTelemetry.AutoInstrumentation.StartupHook.dll`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-auto-bootstrap.png`

- [ ] **DOTNET-11 Agent-generated traces**
  - DoD: A baseline ASP.NET Core request automatically produces spans such as:

    ```text
    HTTP GET /users
       ├─ controller execution
       ├─ database query
       └─ outgoing HTTP call
    ```
  - Source of truth: `<path>`
  - Validation: `<sample app + collector + trace query>`
  - Screenshot / artifact: `artifacts/release-validation/<release>/dotnet-auto-request-trace.png`

### D. Minimal proof snippet
Record the exact snippet used for manual validation when applicable.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
    });
```

## 6. Evidence index
List every linked artifact in one place so release reviewers can scan quickly.

- [ ] `artifacts/release-validation/<release>/...`
- [ ] CI run: `<url or identifier>`
- [ ] Benchmark report: `<path>`
- [ ] Interop matrix: `<path>`
- [ ] Security test output: `<path>`
- [ ] Dashboard screenshots: `<path>`
- [ ] Trace screenshots: `<path>`
- [ ] Log screenshots: `<path>`
- [ ] Metrics screenshots: `<path>`
- [ ] Profile screenshots: `<path>`

## 7. Final sign-off
- [ ] Engineering owner approved
- [ ] Observability owner approved
- [ ] QA / release manager approved
- [ ] Docs owner approved
- [ ] Security owner approved *(if applicable)*
- [ ] All out-of-scope items documented
- [ ] Release is approved for shipment

## 8. Out of scope / known gaps
- `<gap>`
- `<gap>`
- `<gap>`

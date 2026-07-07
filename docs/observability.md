l# Observability & Instrumentation (OpenTelemetry)

> **Status of this document.** A vocabulary + role map that pins qyl's instrumentation to the
> official OpenTelemetry guidelines. It describes **current, honestly-tracked state** — not
> aspirational targets. The per-item source of truth is the *generated* coverage matrix in the
> `Qyl.OpenTelemetry.AutoInstrumentation` repo (derived from a contract whose only statuses are
> `implemented` / `option_bound` / `control_bound` / `unsupported_nativeaot` — there is no
> "planned" state, by design: "missing values stay missing; never synthesize").
https://github.com/ANcpLua/qyl.mobile/blob/main/ios/TelemetryObserver/ViewModels/TelemetryDashboardViewModel.swift
qyl is an OpenTelemetry-native observability platform. This document pins the vocabulary to
the [OpenTelemetry glossary](https://opentelemetry.io/docs/concepts/glossary/) and maps each
qyl component to its OTel role, so contributors (human or agent) share one precise model.

## OpenTelemetry terminology (canonical)

| Term | OpenTelemetry definition | In qyl |
|---|---|---|
| **Instrumented Library** | The library for which telemetry (traces, metrics, logs) is gathered. Example: `org.mongodb.client`. | `HttpClient`, ASP.NET Core, EF Core, `Microsoft.Data.SqlClient`, gRPC, GraphQL, Oracle MDA, plus qyl's own services. |
| **Instrumentation Library** (a.k.a. *Instrumenting Library*) | The library that provides the instrumentation for a given Instrumented Library. May be the same library if instrumentation is built in. Example: `io.opentelemetry.contrib.mongodb`. | **`Qyl.OpenTelemetry.AutoInstrumentation`** (generic libraries) and **`qyl.instrumentation`** (qyl's own GenAI/agent domain) and **`ANcpLua.Agents.Instrumentation`** (Microsoft Agent Framework). |
| **Instrumentation Scope** | The `name` + optional `version` given when obtaining a Tracer / Meter / Logger. The name/version pair identifies the scope in which telemetry is emitted. | `QylActivitySource` / `QylActivityNames` (name + package version). See [naming guidelines](#instrumentation-scope-naming). |
| **Automatic Instrumentation** | Telemetry collection that does **not** require the end user to modify application source code — via compile-time/runtime code manipulation, monkey-patching, or eBPF. | qyl's method is **AOT-safe compile-time source interceptors + `DiagnosticListener`** (see [Automatic instrumentation under .NET AOT](#automatic-instrumentation-under-net-aot)). |
| **Semantic Conventions** | The shared attribute/metric/span vocabulary telemetry must follow. | `Qyl.OpenTelemetry.SemanticConventions` (+ `.Incubating`), tracking OTel semconv **v1.41.0**. |

## qyl components in OTel terms

- **Instrumentation Library — generic:** [`Qyl.OpenTelemetry.AutoInstrumentation`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation) (`4.0.3`). A vendor-neutral, NativeAOT-ready **Automatic Instrumentation** library. Zero source changes: a `PackageReference` is the supported path. Packages: `.Hosting`, `.EntityFrameworkCore`, `.SqlClient`, `.DiagnosticListeners`, `.SourceGenerators`. Public surface: `QylActivitySource`, `QylActivityNames`, `QylAutoInstrumentationIds`, `QylAutoInstrumentationOptions` (per-signal enablement + header/URL redaction), `AddQylAutoInstrumentation(...)` (zero-config via `.Hosting`), and `AddQylAspNetCoreInstrumentation(...)` (opt-in server-span middleware). A single-owner-per-signal registry keeps the interceptor, middleware, and DiagnosticListener lanes from double-instrumenting the same operation.
- **Instrumentation Library — qyl domain:** [`internal/qyl.instrumentation`](../internal/qyl.instrumentation) (namespace `Qyl.Instrumentation`) + its generator. Provides ServiceDefaults (DI/health/endpoints/discovery/resilience) **and** instrumentation for domains the generic library cannot know about: **GenAI** (`Microsoft.Extensions.AI` `IChatClient`) and **agents** (`QylAgentActivityProcessor`, `AddQylAgentInventory`). Its `WebApplicationBuilder.Build()` interceptor wires the ServiceDefaults at the build call site.
- **Instrumentation Library — Agent Framework:** `ANcpLua.Agents.Instrumentation` — MAF-native, wraps agents in an `OpenTelemetryAgent` emitting `invoke_agent` / `execute_tool` spans (sensitive data off by default).
- **Semantic Conventions:** `Qyl.OpenTelemetry.SemanticConventions` — the vocabulary the instrumentation libraries emit against. Not itself an instrumentation library.
- **Backend (not an instrumentation library):** `qyl.collector` — OTLP ingest + DuckDB storage + REST API + dashboard. It *receives* signals; it instruments only itself (via `qyl.instrumentation`).

## Automatic instrumentation under .NET AOT

OpenTelemetry defines Automatic Instrumentation as collecting telemetry **without modifying
application source code**, by "code manipulation (during compilation or at runtime),
monkey patching, or running eBPF programs." The classic .NET implementation — a **CLR
profiler that rewrites IL at runtime** — is **not compatible with Native AOT** (no JIT, no
runtime IL emission).

**qyl's method is the AOT-safe equivalent — all at compile time, zero source changes:**

1. **Build-time Roslyn source interceptors** (`[InterceptsLocation]`) — the source generator
   discovers supported call-sites in the consumer and emits ordinary C# interceptors that
   wrap them. (OTel's "code manipulation during compilation.")
2. **`DiagnosticListener` subscribers** — subscribe to framework/library-emitted diagnostic
   events and translate them into the OpenTelemetry model. (OTel's "subscribing to
   library-specific callbacks.")
3. **`[ModuleInitializer]` bootstrap** — registers `ActivitySource` / `Meter` before
   `Main`, so the consumer writes no wiring code.

Explicitly **out of scope** because they break AOT: CLR-profiler / IL-rewriting, runtime
monkey-patching, and reflection-heavy dynamic proxies.

## Signal coverage

The generic Instrumentation Library — [`Qyl.OpenTelemetry.AutoInstrumentation`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation) — implements **all three OpenTelemetry signals** across the full 60-item OTel .NET auto-instrumentation contract, verified under NativeAOT:

- **Traces** — HttpClient, ASP.NET Core, EF Core, SqlClient, gRPC, Kafka, RabbitMQ, MongoDB, Redis, Quartz, MassTransit, NServiceBus, MySQL, Npgsql, SQLite, Oracle MDA, Elasticsearch, GraphQL, Azure SDK.
- **Metrics** — ASP.NET Core, HttpClient, .NET runtime, process, Npgsql, NServiceBus, SqlClient.
- **Logs** — `ILogger`, log4net, NLog.

> The **authoritative, per-item coverage matrix is generated** in that repo (`docs/coverage-matrix.md`, from the contract) — qyl does not keep a second copy. Each row carries lane, status, call-site visibility, payload access, and evidence, anchored to the official OpenTelemetry source. That generated matrix is the source of truth; this page only summarizes it.

The only items **not** implemented are the ones that are **structurally impossible** under the NativeAOT / source-generator substrate (reflection-required): classic ASP.NET (Framework), WCF (Core/Service), and ASP.NET Framework metrics — marked `unsupported_nativeaot`. These are boundaries, not a backlog.

The **domain layer** (`internal/qyl.instrumentation`) adds what the generic library cannot know: **GenAI** (`IChatClient`) and **agent inventory** telemetry for qyl's own workload. It targets the ASP.NET Core minimal-API surface (no upstream AOT annotations), so it deliberately does not set `IsAotCompatible` — the NativeAOT-verified claim on this page scopes to the generic library, not to this layer or the collector.

## Guarantees the instrumentation library holds

Grounded in its contract (do not overstate these; they are current, not aspirational):

- **All three signals** shipped and NativeAOT-verified in that library's own repo — the contract has no "planned" state; every item is `implemented` / `option_bound` / `control_bound` / `unsupported_nativeaot`.
- **Semantic conventions** — emitted against `Qyl.OpenTelemetry.SemanticConventions` (stable; incubating opt-in), pinned to OTel semconv **v1.41.0**.
- **Never synthesize** — "missing values stay missing"; qyl never invents a value the instrumented library did not expose.
- **Privacy by default** — URLs/query values redacted per key; `db.query.text` behind upstream flags; agent sensitive-data off by default.
- **Zero source changes** — `PackageReference`-only.
- **AOT-clean** — no CLR profiler / IL rewrite / runtime monkey-patch; compile-time `[InterceptsLocation]` interceptors + `DiagnosticListener`.

### Instrumentation Scope naming

Per OTel, the scope `name` identifies the *instrumentation*, not the app. In this stack:

- Generic library scopes → the `Qyl.OpenTelemetry.AutoInstrumentation.*` identifiers
  (`QylActivityNames`), each with the package version.
- Domain scopes → `Qyl.Instrumentation.*` (GenAI, agents), with version.
- Every `ActivitySource` / `Meter` (and future `ILogger` scope) is created **with a version
  argument** so the Instrumentation Scope is fully qualified.

## See also

- Instrumentation library internals: [`Qyl.OpenTelemetry.AutoInstrumentation` docs](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation/tree/main/docs) — `coverage-matrix.md`, `RUNTIME_SEMANTICS.md`, `TELEMETRY_CAPABILITY_GRAPH.md`, `qyl-aot-autoinstrumentation.conformance-plan.json`.
- Package/version pins: [`docs/package-api-update-matrix.md`](package-api-update-matrix.md).
- OpenTelemetry: [glossary](https://opentelemetry.io/docs/concepts/glossary/) · [instrumentation](https://opentelemetry.io/docs/concepts/instrumentation/) · [semantic conventions](https://opentelemetry.io/docs/specs/semconv/).

# Observability & Instrumentation (OpenTelemetry)

> **Status of this document.** This is the **OpenTelemetry-conformant target** for qyl's
> instrumentation, written to the ideal OTel guidelines so implementation can catch up to
> it. Cells and checklist items are marked: **✅ shipped/verified · 🟡 partial · 🎯 not yet
> (implement or verify)**. When a cell is uncertain it is marked 🎯 on purpose — the gap is
> the next task. Do not "downgrade" a 🎯 to ✅ without evidence in the code; do not claim a
> capability the source does not have.

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

- **Instrumentation Library — generic:** [`Qyl.OpenTelemetry.AutoInstrumentation`](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation) (`3.1.2`). A vendor-neutral, NativeAOT-ready **Automatic Instrumentation** library. Zero source changes: a `PackageReference` is the supported path. Packages: `.Hosting`, `.EntityFrameworkCore`, `.SqlClient`, `.DiagnosticListeners`, `.SourceGenerators`. Public surface: `QylActivitySource`, `QylActivityNames`, `QylAutoInstrumentationIds`, `QylAutoInstrumentationOptions` (per-signal enablement + header/URL redaction), `AddQylAutoInstrumentation(...)`.
- **Instrumentation Library — qyl domain:** [`internal/qyl.instrumentation`](../internal/qyl.instrumentation) (namespace `Qyl.Instrumentation`) + its generator. Provides ServiceDefaults (DI/health/endpoints/discovery/resilience) **and** instrumentation for domains the generic library cannot know about: **GenAI** (`Microsoft.Extensions.AI` `IChatClient`) and **agents** (`QylAgentActivityProcessor`, `AddQylAgentInventory`). Its `WebApplicationBuilder.Build()` interceptor composes the generic library in (diagnostic `QYL0138` errors if `Qyl.OpenTelemetry.AutoInstrumentation` is missing).
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

## Conformance matrix (target)

Rows are **Instrumented Libraries**; columns are the three OTel signals. This is the
OTel-conformant target — the ideal is **all three signals** for every instrumented library.

| Instrumented Library | Traces | Metrics | Logs | Notes |
|---|:--:|:--:|:--:|---|
| HttpClient | ✅ | 🎯 | 🎯 | BCL `HttpHandlerDiagnosticListener`. |
| ASP.NET Core | ✅ | ✅ | 🎯 | Kestrel/framework listener; metrics demo shipped. |
| EF Core | ✅ | 🎯 | 🎯 | `EntityFrameworkCoreDiagnosticListener`. |
| SqlClient (`Microsoft.Data.SqlClient`) | ✅ | 🎯 | 🎯 | Source interceptor. |
| gRPC client | ✅ | 🎯 | 🎯 | `GrpcClientDiagnosticListener`. |
| GraphQL | 🟡 | 🎯 | 🎯 | Option toggle present (`QylAutoInstrumentationOptions`); verify coverage. |
| Oracle MDA | 🎯 | 🎯 | 🎯 | Option toggle present; implement/verify. |
| GenAI (`IChatClient`) | 🟡 | 🎯 | 🎯 | `qyl.instrumentation` GenAI; align to OTel **GenAI** semconv. |
| Agents (MAF) | ✅ | 🎯 | 🎯 | `invoke_agent` / `execute_tool` spans. |

> **Primary gap for the next contributor: the Logs signal.** No instrumented library above
> emits the OpenTelemetry **logs** signal yet — bridging `ILogger`/library logs into OTLP
> logs (with trace correlation) is the highest-leverage OTel-conformance work. After that:
> fill the Metrics column and confirm GraphQL/Oracle traces. Verify every cell against the
> code before promoting it to ✅.

## Ideal OTel-conformance checklist

Treat each unchecked item as a target for the instrumentation libraries:

- 🎯 **All three signals** (traces, metrics, logs) per instrumented library — see matrix.
- 🎯 **Instrumentation Scope naming** — every Tracer/Meter/Logger is created with a `name`
  = the instrumentation library identifier and a `version` = its package version, so each
  scope is unambiguous. See below.
- ✅ **Semantic conventions** — emit stable conventions by default; incubating opt-in via the
  `.Incubating` package. Keep pinned to a known semconv version (currently v1.41.0).
- 🎯 **Resource attributes** — `service.name` / `service.version` / `service.instance.id`
  and `telemetry.sdk.*` set on every signal.
- 🎯 **Context propagation** — W3C Trace Context / Baggage across HttpClient/gRPC boundaries.
- ✅ **Opt-in / no surprise telemetry** — nothing emitted unless enabled; per-signal toggles.
- ✅ **Privacy by default** — no raw prompts, headers, query strings, or PII unless explicitly
  opted in (`QylAutoInstrumentationOptions` redaction; agent sensitive-data off by default).
- ✅ **Zero source changes** — `PackageReference`-only automatic instrumentation.
- ✅ **AOT-clean** — no CLR profiler / IL rewrite / runtime monkey-patch; interceptor-based.

### Instrumentation Scope naming

Per OTel, the scope `name` should identify the *instrumentation*, not the app. Target:

- Generic library scopes → the `Qyl.OpenTelemetry.AutoInstrumentation.*` identifiers
  (`QylActivityNames`), each with the package version.
- Domain scopes → `Qyl.Instrumentation.*` (GenAI, agents), with version.
- Every `ActivitySource` / `Meter` (and future `ILogger` scope) is created **with a version
  argument** so the Instrumentation Scope is fully qualified.

## See also

- Instrumentation library internals: [`Qyl.OpenTelemetry.AutoInstrumentation` docs](https://github.com/ANcpLua/Qyl.OpenTelemetry.AutoInstrumentation/tree/main/docs) — `coverage-matrix.md`, `RUNTIME_SEMANTICS.md`, `TELEMETRY_CAPABILITY_GRAPH.md`, `qyl-aot-autoinstrumentation.conformance-plan.json`.
- Package/version pins: [`docs/package-api-update-matrix.md`](package-api-update-matrix.md).
- OpenTelemetry: [glossary](https://opentelemetry.io/docs/concepts/glossary/) · [instrumentation](https://opentelemetry.io/docs/concepts/instrumentation/) · [semantic conventions](https://opentelemetry.io/docs/specs/semconv/).

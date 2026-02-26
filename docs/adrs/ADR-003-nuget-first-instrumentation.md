# ADR-003: .NET Premium SDK (qyl.servicedefaults)

Status: Accepted
Date: 2026-02-26
Depends-On: ADR-001

## Context

qyl accepts OTLP from any language (ADR-001). But for .NET projects, we can offer a significantly better experience: compile-time source generators that auto-detect the project's stack and emit interceptors with zero configuration.

This is an **optional premium layer** — qyl works without it. But for .NET users, it's the difference between "add OTLP env var" and "add one NuGet package, everything is auto-instrumented."

## Decision

`qyl.servicedefaults` is an optional NuGet package that provides compile-time auto-instrumentation for .NET projects. It's the premium SDK, not the core product.

### Install (Two Lines)

```bash
dotnet add package qyl.servicedefaults
```

```csharp
builder.AddQylServiceDefaults();
```

Build → Source generators auto-detect dependencies → emit interceptors → telemetry flows.

### What Source Generators Auto-Detect

| Dependency Found | Auto-Enables | OTel Convention |
|------------------|-------------|-----------------|
| `Microsoft.Extensions.AI` | GenAI interceptors | gen_ai.* |
| `Microsoft.EntityFrameworkCore` | DB interceptors | db.* |
| `Npgsql` / `Microsoft.Data.SqlClient` | DB interceptors | db.* |
| `HttpClient` (always present) | HTTP interceptors | http.* |

No user action needed — if the dependency exists, the interceptor is emitted.

### What "Core" Means (Always On)

| Feature | Source Generator | OTel Convention |
|---------|-----------------|-----------------|
| HTTP traces | TracedInterceptorEmitter | http.* |
| Error capture | ErrorInterceptorEmitter | exception.* |
| Health checks | (runtime) | — |
| Basic metrics | MeterEmitter | process.*, http.server.* |

### Dashboard as Dimmer (Post-Install)

After telemetry flows, the dashboard shows what's active and what's available:

```
Active (auto-detected):
  ✅ HTTP traces (http.*)
  ✅ GenAI calls (gen_ai.*) — detected Microsoft.Extensions.AI
  ✅ Error capture (exception.*)

Available (enable via checkbox → rebuild):
  ☐ Database calls (db.*)
  ☐ Custom methods ([Traced] attribute)
  ☐ Metrics ([Counter], [Histogram], [Gauge])
  ☐ Kubernetes metadata (k8s.*)
  ☐ Deploy correlation (deployment.*)
```

### Configuration Storage

```xml
<PropertyGroup>
  <QylGenAi>true</QylGenAi>
  <QylDatabase>true</QylDatabase>
  <QylHttp>true</QylHttp>
  <QylKubernetes>false</QylKubernetes>
</PropertyGroup>
```

Source generators read these at compile time. Disabled = no interceptor emitted = zero runtime overhead.

### Without the NuGet Package

.NET apps still work with qyl — just like Python or Node:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run
```

Standard OTel SDK traces flow. No source generators, no auto-detection, but telemetry works.

## Constraints

- Optional — qyl works without this package
- .NET only (source generators are a Roslyn feature)
- Configuration changes require rebuild (compile-time is the whole point)
- No Aspire-style AppHost required

## Acceptance Criteria

```gherkin
GIVEN a .NET project with Microsoft.Extensions.AI
WHEN  user adds qyl.servicedefaults and builder.AddQylServiceDefaults()
AND   builds the project
THEN  source generators emit GenAI + HTTP + Error interceptors
AND   telemetry flows to qyl without further configuration

GIVEN a .NET project WITHOUT qyl.servicedefaults
WHEN  user sets OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
AND   uses standard OTel SDK
THEN  telemetry still flows to qyl (standard OTLP path)

GIVEN telemetry flowing to qyl dashboard
WHEN  user opens localhost:5100
THEN  dashboard shows active instrumentation (auto-detected from .NET SDK)
AND   dashboard shows available instrumentation (checkboxes)

GIVEN user disables GenAI checkbox in dashboard
WHEN  project is rebuilt
THEN  gen_ai.* interceptors are NOT emitted
AND   no GenAI overhead in the application
```

## Verification Steps (Agent-Executable)

1. Create test project: `dotnet new webapi`
2. `dotnet add package qyl.servicedefaults`
3. Add `builder.AddQylServiceDefaults()` to Program.cs
4. `dotnet build` → assert source generators emit interceptors
5. Run project + send HTTP request → assert span in collector
6. Remove package → rebuild → assert clean build, zero qyl artifacts
7. Set `OTEL_EXPORTER_OTLP_ENDPOINT` → run without package → assert spans still flow via standard OTel

## Consequences

- qyl.servicedefaults is a .NET-specific value-add, not a requirement
- Polyglot users get standard OTLP experience
- .NET users get premium compile-time auto-instrumentation
- Source generators must handle incremental compilation (already do)
- Dashboard dimmer works regardless of SDK — it reads OTel attributes from spans

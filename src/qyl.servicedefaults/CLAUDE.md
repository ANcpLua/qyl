# qyl.servicedefaults - .NET Premium SDK (ADR-003)

Optional NuGet package for .NET projects. Compile-time auto-instrumentation via source generators.

qyl works WITHOUT this package (standard OTLP env var). This is the premium .NET experience.

## Identity

| Property  | Value           |
|-----------|-----------------|
| SDK       | ANcpLua.NET.Sdk |
| Framework | net10.0         |

## Install (Two Lines)

```bash
dotnet add package qyl.servicedefaults
```

```csharp
builder.AddQylServiceDefaults();  // That's it — source generators handle the rest
```

## Auto-Detection (Compile-Time)

| Dependency Found | Auto-Enables | OTel Convention |
|------------------|-------------|-----------------|
| `Microsoft.Extensions.AI` | GenAI interceptors | gen_ai.* |
| `Microsoft.EntityFrameworkCore` | DB interceptors | db.* |
| `Npgsql` / `Microsoft.Data.SqlClient` | DB interceptors | db.* |
| `HttpClient` (always present) | HTTP interceptors | http.* |

No user action needed — if the dependency exists, the interceptor is emitted.

## Core (Always On)

| Feature | Source Generator | OTel Convention |
|---------|-----------------|-----------------|
| HTTP traces | TracedInterceptorEmitter | http.* |
| Error capture | ErrorInterceptorEmitter | exception.* |
| Health checks | (runtime) | — |
| Basic metrics | MeterEmitter | process.*, http.server.* |

## Dashboard Dimmer (Post-Install)

MSBuild properties control what gets generated:

```xml
<PropertyGroup>
  <QylGenAi>true</QylGenAi>
  <QylDatabase>true</QylDatabase>
  <QylHttp>true</QylHttp>
  <QylKubernetes>false</QylKubernetes>
</PropertyGroup>
```

Disabled = no interceptor emitted = zero runtime overhead. Changes require rebuild.

## Without This Package

.NET apps still work with qyl — just like Python or Node:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run
```

## What Gets Configured

- **OTel**: OTLP exporter, service name, HTTP/gRPC instrumentation
- **Health**: `/health` (full), `/alive` (liveness)
- **Resilience**: retry, circuit breaker, timeout

## Key Packages

OpenTelemetry.Extensions.Hosting | Microsoft.Extensions.Http.Resilience | Microsoft.Extensions.Diagnostics.HealthChecks

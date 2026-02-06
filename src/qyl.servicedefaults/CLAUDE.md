# qyl.servicedefaults - Service Defaults

Aspire-style service defaults for consistent OTel, health checks, and resilience.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |

## Usage

```csharp
builder.AddServiceDefaults();   // OTel + health + resilience
app.MapDefaultEndpoints();      // /health, /alive
```

## What Gets Configured

- **OTel**: OTLP exporter, service name, HTTP/gRPC instrumentation
- **Health**: `/health` (full), `/alive` (liveness)
- **Resilience**: retry, circuit breaker, timeout

## Key Packages

OpenTelemetry.Extensions.Hosting | Microsoft.Extensions.Http.Resilience | Microsoft.Extensions.Diagnostics.HealthChecks

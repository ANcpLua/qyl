# qyl.servicedefaults - Aspire Defaults

Aspire-style service defaults for consistent telemetry configuration.

## Identity

| Property | Value |
|----------|-------|
| SDK | ANcpLua.NET.Sdk |
| Framework | net10.0 |
| Pattern | aspire-service-defaults |

## Purpose

Provides opinionated defaults for:

- OpenTelemetry configuration
- Health checks (Aspire standard endpoints)
- Resilience patterns
- HTTP client configuration

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

// Apply all service defaults
builder.AddServiceDefaults();

var app = builder.Build();

// Map health endpoints
app.MapDefaultEndpoints();

app.Run();
```

## What Gets Configured

### OpenTelemetry

```csharp
// Automatic configuration:
// - OTLP exporter (from OTEL_EXPORTER_OTLP_ENDPOINT)
// - Service name from assembly
// - Environment enrichment
// - HTTP/gRPC instrumentation
```

### Health Checks

| Endpoint | Purpose |
|----------|---------|
| `/health` | Full health check |
| `/alive` | Liveness probe (Kubernetes) |

### Resilience

- Standard retry policies for transient failures
- Circuit breaker defaults
- Timeout policies

## Dependencies

| Package | Purpose |
|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | OTel host integration |
| `Microsoft.Extensions.Http.Resilience` | HTTP resilience |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | Health checks |

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint |
| `OTEL_SERVICE_NAME` | Override service name |
| `ASPNETCORE_ENVIRONMENT` | Environment for enrichment |

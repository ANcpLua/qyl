# qyl.servicedefaults

Aspire-style service defaults for consistent telemetry configuration.

## identity

```yaml
sdk: ANcpLua.NET.Sdk
role: shared-configuration
pattern: aspire-service-defaults
```

## purpose

Provides opinionated defaults for:
- OpenTelemetry configuration
- Health checks (Aspire standard)
- Resilience patterns
- HTTP client configuration

## usage

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // Apply all defaults

var app = builder.Build();
app.MapDefaultEndpoints(); // /health, /alive
```

## features

```yaml
telemetry:
  - OTLP exporter configuration
  - Service name from assembly
  - Environment enrichment

health:
  - /health (full check)
  - /alive (liveness probe)

resilience:
  - Standard retry policies
  - Circuit breaker defaults
```

## dependencies

```yaml
packages:
  - OpenTelemetry.Extensions.Hosting
  - Microsoft.Extensions.Http.Resilience
  - Microsoft.Extensions.Diagnostics.HealthChecks
```

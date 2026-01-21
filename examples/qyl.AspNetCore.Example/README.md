# qyl OpenTelemetry ASP.NET Core Example

This example demonstrates modern OpenTelemetry patterns for instrumenting an ASP.NET Core application with the qyl
observability platform.

## Features

- **Tracing**: Automatic ASP.NET Core and HttpClient instrumentation + manual span creation
- **Metrics**: Runtime metrics, HTTP metrics, and custom counters
- **Logging**: OpenTelemetry-integrated structured logging with source generators
- **OTLP Export**: Sends telemetry to qyl.collector
- **Advanced .NET 10 Telemetry**: Demonstrates new .NET 10 features like `ActivitySourceOptions`, `MeterOptions` with
  `TelemetrySchemaUrl`, and advanced source-generated logging with `[TagProvider]`.

## Quick Start

### 1. Run qyl.collector

```bash
# From qyl root directory
docker compose up -d
```

Or run the collector directly:

```bash
cd src/qyl.collector
dotnet run
```

### 2. Run the Example

```bash
cd examples/qyl.AspNetCore.Example
dotnet run
```

### 3. Generate Telemetry

```bash
# Create an order
curl -X POST http://localhost:5050/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"cust-123","customerEmail":"cust@example.com","customerPhone":"555-0100","items":[{"quantity":2,"unitPrice":12.5}]}'

# Retrieve an order
curl http://localhost:5050/orders/123

# Emit a Gen AI span
curl -X POST http://localhost:5050/genai \
  -H "Content-Type: application/json" \
  -d '{"operationName":"chat","providerName":"openai","requestModel":"gpt-4o-mini","responseModel":"gpt-4o-mini","inputTokens":120,"outputTokens":42}'
```

### 4. View in qyl Dashboard

Open http://localhost:5100 to see:

- Traces in the Traces page
- Live streaming in the Live page
- Metrics (if OTLP metrics enabled)

## Configuration

Edit `appsettings.json` to configure exporters:

| Setting              | Options           | Default                 |
|----------------------|-------------------|-------------------------|
| `UseTracingExporter` | `OTLP`, `CONSOLE` | `OTLP`                  |
| `UseMetricsExporter` | `OTLP`, `CONSOLE` | `CONSOLE`               |
| `UseLogExporter`     | `OTLP`, `CONSOLE` | `CONSOLE`               |
| `Otlp:Endpoint`      | URL               | `http://localhost:5100` |

### Environment Variable Overrides

```bash
export UseTracingExporter=OTLP
export UseMetricsExporter=OTLP
export Otlp__Endpoint=http://localhost:5100
dotnet run
```

## Docker Compose

Run the full stack:

```bash
docker compose up --build
```

Services:

- **qyl-collector**: Port 5100 - Telemetry receiver + dashboard
- **example-app**: Port 5050 - This example app
- **grafana**: Port 3000 - Optional visualization

## Project Structure

```
examples/qyl.AspNetCore.Example/
├── Program.cs                 # OpenTelemetry setup
├── Telemetry/
│   ├── AppTelemetry.cs        # ActivitySource + Meter
│   ├── Log.cs                 # Source-generated logging + TagProviders
│   └── OTelSemconv.cs          # Gen AI semantic conventions
├── Controllers/
│   ├── OrdersController.cs    # Order spans + metrics
│   └── GenAiController.cs     # Gen AI spans + tags
├── Models/
│   └── Telemetry/
│       └── TelemetryModels.cs # Order + Gen AI DTOs
├── appsettings.json           # Configuration
├── docker-compose.yml         # Full stack
└── Dockerfile                 # Container build
```

## OpenTelemetry Patterns

### Unified API

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("qyl-example"))
    .WithTracing(b => b.AddAspNetCoreInstrumentation())
    .WithMetrics(b => b.AddRuntimeInstrumentation())
    .WithLogging(b => b.AddConsoleExporter());
```

### Manual Span Creation

```csharp
using var activity = AppTelemetry.Source.StartActivity("GenAI.Process");
activity?.SetTag(OTelSemconv.OperationName, "chat");
activity?.SetTag(OTelSemconv.ProviderName, "openai");
```

### Custom Metrics

```csharp
AppTelemetry.OrdersCreated.Add(1);
AppTelemetry.OrderProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
```

### Source-Generated Logging

```csharp
[LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Gen AI span processed")]
public static partial void GenAiSpanProcessed(
    ILogger logger,
    [TagProvider(typeof(GenAiTagProvider), nameof(GenAiTagProvider.RecordTags))]
    GenAiSpanData data);
```

### Advanced .NET 10 Patterns

#### Schema-Aware Telemetry

```csharp
public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
{
    Version = ServiceVersion,
    TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.39.0"
});
```

#### Complex Tag Extraction ([TagProvider])

```csharp
[LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Gen AI span processed")]
public static partial void GenAiSpanProcessed(
    ILogger logger,
    [TagProvider(typeof(GenAiTagProvider), nameof(GenAiTagProvider.RecordTags))]
    GenAiSpanData data);
```

## Requirements

- .NET 10 SDK
- Docker (for qyl.collector)

## References

- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [qyl Observability Platform](../../README.md)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)

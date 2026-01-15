# qyl OpenTelemetry ASP.NET Core Example

This example demonstrates modern OpenTelemetry patterns for instrumenting an ASP.NET Core application with the qyl
observability platform.

## Features

- **Tracing**: Automatic ASP.NET Core and HttpClient instrumentation + manual span creation
- **Metrics**: Runtime metrics, HTTP metrics, and custom counters
- **Logging**: OpenTelemetry-integrated structured logging with source generators
- **OTLP Export**: Sends telemetry to qyl.collector
- **Advanced .NET 10 Telemetry**: Demonstrates new .NET 10 features like `ActivitySourceOptions`, `MeterOptions` with `TelemetrySchemaUrl`, and advanced source-generated logging with `[TagProvider]`.

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
cd examples/AspNetCore
dotnet run
```

### 3. Generate Telemetry

```bash
# Hit the API endpoint
curl http://localhost:5050/WeatherForecast

# Request custom number of days
curl http://localhost:5050/WeatherForecast/10
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
examples/AspNetCore/
├── Program.cs                 # OpenTelemetry setup
├── InstrumentationSource.cs   # ActivitySource + Meter holder
├── Controllers/
│   └── WeatherForecastController.cs  # Manual spans + metrics
├── Models/
│   └── WeatherForecast.cs     # DTO
├── Logging/
│   └── LoggerExtensions.cs    # Source-generated logging
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
using var activity = _activitySource.StartActivity("CalculateForecast");
activity?.SetTag("forecast.days", 5);
```

### Custom Metrics

```csharp
_requestCounter.Add(1);
_freezingDaysCounter.Add(forecast.Count(f => f.TemperatureC < 0));
```

### Source-Generated Logging

```csharp
[LoggerMessage(EventId = 1, Message = "Generated {Count} forecasts")]
public static partial void ForecastGenerated(this ILogger logger, LogLevel level, int count);
```

### Advanced .NET 10 Patterns

#### Schema-Aware Telemetry

```csharp
public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
{
    Version = ServiceVersion,
    TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.38.0"
});
```

#### Complex Tag Extraction ([TagProvider])

```csharp
[LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Order created")]
public static partial void OrderCreated(
    ILogger logger,
    [TagProvider(typeof(OrderTagProvider), nameof(OrderTagProvider.RecordTags))]
    Order order);
```

## Requirements

- .NET 10 SDK
- Docker (for qyl.collector)

## References

- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [qyl Observability Platform](../../README.md)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)

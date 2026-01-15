# Exporters

Exporters allow you to send telemetry data from your .NET application to various
backends for analysis and visualization.

## OTLP Dependencies

If you want to send telemetry data to an OTLP endpoint (like the
OpenTelemetry Collector, Jaeger or Prometheus), you can choose between two
different protocols to transport your data:

- HTTP/protobuf
- gRPC

Start by installing the
[`OpenTelemetry.Exporter.OpenTelemetryProtocol`](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/)
package as a dependency for your project:

```sh
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

If you're using ASP.NET Core install the
[`OpenTelemetry.Extensions.Hosting`](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
package as well:

```sh
dotnet add package OpenTelemetry.Extensions.Hosting
```

## OTLP Usage

### ASP.NET Core

Configure the exporters in your ASP.NET Core services:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        // The rest of your setup code goes here
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        // The rest of your setup code goes here
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging => {
    // The rest of your setup code goes here
    logging.AddOtlpExporter();
});
```

This will, by default, send telemetry using gRPC to <http://localhost:4317>, to
customize this to use HTTP and the protobuf format, you can add options like
this:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        // The rest of your setup code goes here
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("your-endpoint-here/v1/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        // The rest of your setup code goes here
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("your-endpoint-here/v1/metrics");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

builder.Logging.AddOpenTelemetry(logging => {
    // The rest of your setup code goes here
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("your-endpoint-here/v1/logs");
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
});
```

### Non-ASP.NET Core

Configure the exporter when creating a `TracerProvider`, `MeterProvider` or
`LoggerFactory`:

```csharp
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // Other setup code, like setting a resource goes here too
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("your-endpoint-here/v1/traces");
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
    })
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    // Other setup code, like setting a resource goes here too
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("your-endpoint-here/v1/metrics");
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
    })
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("your-endpoint-here/v1/logs");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        })
    });
});
```

Use environment variables to set values like headers and an endpoint URL for
production.

## Console Exporter

The console exporter is useful for development and debugging tasks, and is the
simplest to set up. Start by installing the
[`OpenTelemetry.Exporter.Console`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console/)
package as a dependency for your project:

```sh
dotnet add package OpenTelemetry.Exporter.Console
```

If you're using ASP.NET Core install the
[`OpenTelemetry.Extensions.Hosting`](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
package as well:

```sh
dotnet add package OpenTelemetry.Extensions.Hosting
```

### ASP.NET Core Console Usage

Configure the exporter in your ASP.NET Core services:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        // The rest of your setup code goes here
        .AddConsoleExporter()
    )
    .WithMetrics(metrics => metrics
        // The rest of your setup code goes here
        .AddConsoleExporter()
    );

builder.Logging.AddOpenTelemetry(logging => {
    // The rest of your setup code goes here
    logging.AddConsoleExporter();
});
```

### Non-ASP.NET Core Console Usage

Configure the exporter when creating a `TracerProvider`, `MeterProvider` or
`LoggerFactory`:

```csharp
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // The rest of your setup code goes here
    .AddConsoleExporter()
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    // The rest of your setup code goes here
    .AddConsoleExporter()
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddConsoleExporter();
    });
});
```

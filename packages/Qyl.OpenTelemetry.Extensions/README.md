# Qyl.OpenTelemetry.Extensions

Zero-boilerplate [OpenTelemetry](https://opentelemetry.io/) wiring for .NET services that export
telemetry to a [qyl](https://github.com/ancplua/qyl) collector over OTLP.

## Install

```sh
dotnet add package Qyl.OpenTelemetry.Extensions
```

## Usage

```csharp
using Qyl.OpenTelemetry.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddQylOpenTelemetry(o =>
{
    o.Endpoint    = new Uri(builder.Configuration["Qyl:Endpoint"]!);
    o.ServiceName = "my-service";
    o.ApiKey      = builder.Configuration["Qyl:ApiKey"]; // optional
    o.SampleRate  = 1.0;                                  // 0.0 – 1.0
});
```

The call registers an `OpenTelemetry` tracer pipeline with:

- A `TraceIdRatioBasedSampler` at the configured sample rate
- `service.name` resource attribute set from `ServiceName`
- An `OtlpExporter` pointing at `Endpoint`, with `Authorization: Bearer <ApiKey>` header
  when `ApiKey` is non-null

Pair with [`Qyl.Client`](https://www.nuget.org/packages/Qyl.Client) for the REST surface.

## License

MIT © 2025-2026 ancplua

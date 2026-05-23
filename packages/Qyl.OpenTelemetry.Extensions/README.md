# Qyl.OpenTelemetry.Extensions

Zero-boilerplate [OpenTelemetry](https://opentelemetry.io/) tracing wiring for .NET services that
export traces to a [qyl](https://github.com/ancplua/qyl) collector over OTLP, with optional metrics
pipeline registration.

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
    o.ServiceName = "my-service";
    o.ApiKey      = builder.Configuration["Qyl:ApiKey"]; // optional
    o.SampleRate  = 1.0;                                  // 0.0 – 1.0
    o.MeterNames.Add("my-service");                        // optional metrics pipeline

    if (builder.Configuration["Qyl:Endpoint"] is { Length: > 0 } endpoint)
        o.Endpoint = new Uri(endpoint);
});
```

The call registers an `OpenTelemetry` tracer pipeline with:

- A `TraceIdRatioBasedSampler` at the configured sample rate
- `service.name` resource attribute set from `ServiceName`
- An `OtlpExporter` pointing at `Endpoint`, with `Authorization: Bearer <ApiKey>` header
  when `ApiKey` is non-null

Metrics are registration-only in this package: set `EnableMetrics`, add meter
names through `MeterNames`, or customize the `MeterProviderBuilder` through
`ConfigureMetrics`. Add a metrics exporter in `ConfigureMetrics` only for a
target that actually accepts OTLP metrics. The qyl collector in this repository
does not currently map OTLP `/v1/metrics`, so this package does not add a qyl
metric exporter automatically.

Pair with [`Qyl.Client`](https://www.nuget.org/packages/Qyl.Client) for the REST surface.

## License

MIT © 2025-2026 ancplua

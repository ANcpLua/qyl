using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Qyl.Run.Workload;

// Synthetic demo workload (#510 ④): continuously emits realistic gen_ai + http + db spans,
// metrics and logs through the SemConv source-generated surface, exported over OTLP.
//
// Standalone: `dotnet run --project packages/Qyl.Run.Workload` against a collector on :4318.
// Under `qyl run --demo`, Qyl.Run injects OTEL_EXPORTER_OTLP_ENDPOINT/_PROTOCOL,
// OTEL_SERVICE_NAME and ASPNETCORE_URLS; the env defaults below only fill the standalone gap
// (composition-declared env always wins — it is set before this process starts).
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
{
    Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4318");
    Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
}

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")))
{
    Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", WorkloadTelemetry.DefaultServiceName);
}

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(static tracing => tracing
        .AddSource(WorkloadTelemetry.SourceName)
        .AddOtlpExporter())
    // The collector has no metrics signal today (traces/logs/profiles only) — it 404s
    // /v1/metrics and the exporter drops the batch quietly. Shipping them anyway means the
    // demo lights up the day the collector grows one, and the generated instrument
    // factories are exercised end-to-end regardless.
    .WithMetrics(static metrics => metrics
        .AddMeter(WorkloadTelemetry.SourceName)
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(static logging => logging.AddOtlpExporter());

builder.Services.AddHostedService<WorkloadEmitter>();

var app = builder.Build();

// Qyl.Run AddProject readiness = 2xx on /health at the injected ASPNETCORE_URLS port.
app.MapGet("/health", static () => Results.Text("healthy"));

await app.RunAsync().ConfigureAwait(false);

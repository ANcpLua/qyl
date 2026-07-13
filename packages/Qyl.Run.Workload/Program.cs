using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Qyl.Run.Workload;

// Synthetic conformance workload: continuously emits realistic gen_ai + http + db spans
// and logs through the SemConv source-generated surface, exported over OTLP.
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
var oneShot = string.Equals(Environment.GetEnvironmentVariable("QYL_WORKLOAD_ONESHOT"), "1",
    StringComparison.Ordinal);
var logsEndpoint = new Uri(
    $"{Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")!.TrimEnd('/')}/v1/logs");
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(static level => level >= LogLevel.Trace);

builder.Services.AddOpenTelemetry()
    .WithTracing(static tracing => tracing
        .AddSource(WorkloadTelemetry.SourceName)
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter((exporter, processor) =>
    {
        exporter.Endpoint = logsEndpoint;
        exporter.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        // The acceptance process exits immediately after one record. Export synchronously so its
        // proof cannot depend on a batch timer; the long-running demo keeps the normal batch processor.
        if (oneShot) processor.ExportProcessorType = ExportProcessorType.Simple;
    });
});

builder.Services.AddHostedService<WorkloadEmitter>();

var app = builder.Build();

// Qyl.Run AddProject readiness = 2xx on /health at the injected ASPNETCORE_URLS port.
app.MapGet("/health", static () => Results.Text("healthy"));

await app.RunAsync().ConfigureAwait(false);

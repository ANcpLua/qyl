using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using qyl.AspNetCore.Example;

var builder = WebApplication.CreateBuilder(args);

var tracingExporter = builder.Configuration.GetValue("UseTracingExporter", "CONSOLE").ToUpperInvariant();
var metricsExporter = builder.Configuration.GetValue("UseMetricsExporter", "CONSOLE").ToUpperInvariant();
var logExporter = builder.Configuration.GetValue("UseLogExporter", "CONSOLE").ToUpperInvariant();
var histogramAggregation = builder.Configuration.GetValue("HistogramAggregation", "EXPLICIT").ToUpperInvariant();

builder.Services.AddSingleton<InstrumentationSource>();

builder.Logging.ClearProviders();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration.GetValue("ServiceName", "qyl-aspnetcore-example"),
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(InstrumentationSource.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(
            builder.Configuration.GetSection("AspNetCoreInstrumentation"));

        switch (tracingExporter)
        {
            case "OTLP":
                tracing.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint =
                        new Uri(builder.Configuration.GetValue("Otlp:Endpoint", "http://localhost:5100"));
                });
                break;
            default:
                tracing.AddConsoleExporter();
                break;
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(InstrumentationSource.MeterName)
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        if (histogramAggregation == "EXPONENTIAL")
            metrics.AddView(instrument => instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                ? new Base2ExponentialBucketHistogramConfiguration()
                : null);

        switch (metricsExporter)
        {
            case "OTLP":
                metrics.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint =
                        new Uri(builder.Configuration.GetValue("Otlp:Endpoint", "http://localhost:5100"));
                });
                break;
            default:
                metrics.AddConsoleExporter();
                break;
        }
    })
    .WithLogging(logging =>
    {
        switch (logExporter)
        {
            case "OTLP":
                logging.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint =
                        new Uri(builder.Configuration.GetValue("Otlp:Endpoint", "http://localhost:5100"));
                });
                break;
            default:
                logging.AddConsoleExporter();
                break;
        }
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();

await app.RunAsync().ConfigureAwait(false);
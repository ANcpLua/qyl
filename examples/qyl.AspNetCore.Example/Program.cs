// qyl OpenTelemetry ASP.NET Core Example
// Demonstrates modern OpenTelemetry patterns for the qyl observability platform

using System.Diagnostics.Metrics;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using qyl.AspNetCore.Example;

var builder = WebApplication.CreateBuilder(args);

// Read exporter configuration from appsettings.json
var tracingExporter = builder.Configuration.GetValue("UseTracingExporter", defaultValue: "CONSOLE")!.ToUpperInvariant();
var metricsExporter = builder.Configuration.GetValue("UseMetricsExporter", defaultValue: "CONSOLE")!.ToUpperInvariant();
var logExporter = builder.Configuration.GetValue("UseLogExporter", defaultValue: "CONSOLE")!.ToUpperInvariant();
var histogramAggregation = builder.Configuration.GetValue("HistogramAggregation", defaultValue: "EXPLICIT")!.ToUpperInvariant();

// Register InstrumentationSource for manual instrumentation (ActivitySource + Meters)
builder.Services.AddSingleton<InstrumentationSource>();

// Clear default logging providers
builder.Logging.ClearProviders();

// Configure OpenTelemetry with Tracing, Metrics, and Logging
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: builder.Configuration.GetValue("ServiceName", defaultValue: "qyl-aspnetcore-example")!,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing =>
    {
        // Subscribe to custom ActivitySource and add auto-instrumentation
        tracing
            .AddSource(InstrumentationSource.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        // Bind AspNetCore instrumentation options from config
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(
            builder.Configuration.GetSection("AspNetCoreInstrumentation"));

        // Configure exporter based on settings
        switch (tracingExporter)
        {
            case "OTLP":
                tracing.AddOtlpExporter(otlp =>
                {
                    // qyl.collector endpoint - default to localhost:5100
                    otlp.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:5100")!);
                });
                break;
            default:
                tracing.AddConsoleExporter();
                break;
        }
    })
    .WithMetrics(metrics =>
    {
        // Subscribe to custom Meter and add auto-instrumentation
        metrics
            .AddMeter(InstrumentationSource.MeterName)
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        // Configure histogram aggregation
        if (histogramAggregation == "EXPONENTIAL")
        {
            metrics.AddView(instrument =>
            {
                return instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                    ? new Base2ExponentialBucketHistogramConfiguration()
                    : null;
            });
        }

        // Configure exporter based on settings
        switch (metricsExporter)
        {
            case "OTLP":
                metrics.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:5100")!);
                });
                break;
            default:
                metrics.AddConsoleExporter();
                break;
        }
    })
    .WithLogging(logging =>
    {
        // Configure exporter based on settings
        switch (logExporter)
        {
            case "OTLP":
                logging.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:5100")!);
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

app.Run();

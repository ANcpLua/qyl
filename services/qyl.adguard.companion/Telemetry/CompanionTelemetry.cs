using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Qyl.AdGuard.Companion.Telemetry;

internal sealed class CompanionTelemetry : IAsyncDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly TracerProvider? _provider;
    private bool _disposed;

    private CompanionTelemetry(ActivitySource activitySource, TracerProvider? provider)
    {
        _activitySource = activitySource;
        _provider = provider;
    }

    public bool Enabled => _provider is not null;

    public static async Task<CompanionTelemetry> CreateAsync(TextWriter diagnostics)
    {
        var activitySource = new ActivitySource("qyl.adguard.companion");
        var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
            return new CompanionTelemetry(activitySource, provider: null);

        try
        {
            var endpointUri = new Uri(endpoint);
            var provider = Sdk.CreateTracerProviderBuilder()
                .ConfigureResource(static resource => resource.AddService("qyl-adguard-companion"))
                .AddSource("qyl.adguard.companion")
                .AddOtlpExporter(options => options.Endpoint = endpointUri)
                .Build();

            await Task.CompletedTask.ConfigureAwait(false);
            return new CompanionTelemetry(activitySource, provider);
        }
        catch (Exception ex)
        {
            diagnostics.WriteLine($"qyl-adguard-companion telemetry disabled: {ex.Message}");
            return new CompanionTelemetry(activitySource, provider: null);
        }
    }

    public Activity? StartActivity(string name) => _activitySource.StartActivity(name);

    public QylFlushResult Flush()
    {
        if (_provider is null)
            return new QylFlushResult(Enabled: false, Flushed: false, Message: "OTEL_EXPORTER_OTLP_ENDPOINT is not set.");

        var flushed = _provider.ForceFlush(timeoutMilliseconds: 5_000);
        return new QylFlushResult(
            Enabled: true,
            Flushed: flushed,
            Message: flushed ? "Telemetry flushed." : "Telemetry flush timed out.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _provider?.Dispose();
        _disposed = true;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

internal sealed record QylFlushResult(bool Enabled, bool Flushed, string Message);

using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Qyl.OpenTelemetry.Extensions;

/// <summary>
/// Registers qyl-oriented OpenTelemetry defaults for an application service collection.
/// </summary>
public static class QylOpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenTelemetry resource, tracing, and optional metrics collection configured by <see cref="QylOtelOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures qyl OpenTelemetry options.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddQylOpenTelemetry(
        this IServiceCollection services,
        Action<QylOtelOptions> configure)
    {
        Guard.NotNull(services);
        Guard.NotNull(configure);

        var options = new QylOtelOptions();
        configure(options);

        var serviceName = RequireServiceName(options.ServiceName);
        ValidateSampleRate(options.SampleRate);
        var meterNames = ResolveMeterNames(options);
        var shouldConfigureMetrics = ShouldConfigureMetrics(options, meterNames);

        var builder = services
            .AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(serviceName));

        if (options.EnableTracing)
        {
            var traceEndpoint = RequireEndpoint(options.Endpoint);
            builder.WithTracing(tp =>
            {
                tp.SetSampler(new TraceIdRatioBasedSampler(options.SampleRate));
                tp.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = traceEndpoint;
                    if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    {
                        otlp.Headers = $"Authorization=Bearer {options.ApiKey}";
                    }
                });
            });
        }

        if (shouldConfigureMetrics)
        {
            builder.WithMetrics(mp =>
            {
                foreach (var meterName in meterNames)
                    mp.AddMeter(meterName);

                options.ConfigureMetrics?.Invoke(mp);
            });
        }

        return services;
    }

    private static bool ShouldConfigureMetrics(QylOtelOptions options, IReadOnlyCollection<string> meterNames) =>
        options.EnableMetrics || meterNames.Count > 0 || options.ConfigureMetrics is not null;

    private static Uri RequireEndpoint(Uri? endpoint) =>
        endpoint ?? throw new InvalidOperationException($"{nameof(QylOtelOptions.Endpoint)} is required.");

    private static string RequireServiceName(string? serviceName)
    {
        if (!string.IsNullOrWhiteSpace(serviceName))
            return serviceName.Trim();

        throw new InvalidOperationException($"{nameof(QylOtelOptions.ServiceName)} is required.");
    }

    private static void ValidateSampleRate(double sampleRate)
    {
        if (double.IsFinite(sampleRate) && sampleRate is >= 0 and <= 1)
            return;

        throw new InvalidOperationException($"{nameof(QylOtelOptions.SampleRate)} must be between 0.0 and 1.0.");
    }

    private static IReadOnlyCollection<string> ResolveMeterNames(QylOtelOptions options)
    {
        List<string> meterNames = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var meterName in options.MeterNames)
        {
            if (string.IsNullOrWhiteSpace(meterName))
                throw new InvalidOperationException($"{nameof(QylOtelOptions.MeterNames)} cannot contain empty values.");

            var normalized = meterName.Trim();
            if (seen.Add(normalized))
                meterNames.Add(normalized);
        }

        return meterNames;
    }
}

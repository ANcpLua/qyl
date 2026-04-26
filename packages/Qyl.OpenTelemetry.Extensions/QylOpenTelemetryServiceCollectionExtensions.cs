// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Qyl.OpenTelemetry.Extensions;

/// <summary>
///     Extension methods that register an OpenTelemetry tracer pipeline exporting to a qyl collector
///     via OTLP.
/// </summary>
public static class QylOpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    ///     Registers an OpenTelemetry tracer pipeline that exports spans over OTLP to the configured
    ///     qyl collector endpoint.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Callback that populates <see cref="QylOtelOptions" />.</param>
    /// <returns>The original <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddQylOpenTelemetry(
        this IServiceCollection services,
        Action<QylOtelOptions> configure)
    {
        Guard.NotNull(services, nameof(services));
        Guard.NotNull(configure, nameof(configure));

        var options = new QylOtelOptions();
        configure(options);

        var endpoint = options.Endpoint
                       ?? throw new InvalidOperationException($"{nameof(QylOtelOptions.Endpoint)} is required.");
        var serviceName = options.ServiceName is { Length: > 0 } s
            ? s
            : throw new InvalidOperationException($"{nameof(QylOtelOptions.ServiceName)} is required.");

        services
            .AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(serviceName))
            .WithTracing(tp =>
            {
                tp.SetSampler(new TraceIdRatioBasedSampler(options.SampleRate));
                tp.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = endpoint;
                    if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    {
                        otlp.Headers = $"Authorization=Bearer {options.ApiKey}";
                    }
                });
            });

        return services;
    }
}

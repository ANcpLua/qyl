
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Qyl.OpenTelemetry.Extensions;

public static class QylOpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddQylOpenTelemetry(
        this IServiceCollection services,
        Action<QylOtelOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

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

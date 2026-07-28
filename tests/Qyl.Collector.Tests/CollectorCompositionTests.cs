using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Instrumentation.Instrumentation;

namespace Qyl.Collector.Tests;

/// <summary>
/// G8 companion for the layer the startup guards cannot see: the composition's
/// <c>RequireConfiguredEndpoint</c>. The self-export guard is rightly silent when no endpoint is
/// configured — it checks a value, and there is none — so nothing at guard level stops the OTLP
/// exporter's <c>http://localhost:4318</c> fallback, which is this process's own ingest port.
/// "No endpoint means do not export" is proven here as behavior: composing with no endpoint must
/// register no OTLP exporter at all. The foreign-endpoint case is the positive control that keeps
/// the probe honest — if the OTel SDK ever stops registering Otlp-named services, that test goes
/// red instead of the negative one passing vacuously.
/// </summary>
public sealed class CollectorCompositionTests
{
    private static bool HasOtlpRegistration(IServiceCollection services) => services.Any(static d =>
        Mentions(d.ServiceType)
        || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Any(Mentions))
        || (d.ImplementationType is not null && Mentions(d.ImplementationType))
        || (d.ImplementationInstance is not null && Mentions(d.ImplementationInstance.GetType())));

    private static bool Mentions(Type type) =>
        (type.FullName ?? string.Empty).Contains("Otlp", StringComparison.Ordinal);

    [Fact]
    public void No_configured_endpoint_composes_no_otlp_exporter()
    {
        var builder = WebApplication.CreateSlimBuilder();

        QylServiceDefaultsExtensions.ConfigureQylTelemetry(builder, new QylOptions());

        Assert.False(HasOtlpRegistration(builder.Services),
            "With no OTEL_EXPORTER_OTLP_ENDPOINT configured, the composition registered an OTLP " +
            "exporter — the localhost fallback would be this collector's own ingest port. " +
            "RequireConfiguredEndpoint must stay set in ConfigureQylTelemetry.");
    }

    [Fact]
    public void A_configured_foreign_endpoint_composes_the_otlp_exporter()
    {
        // The composition reads the endpoint from the process environment, so the
        // positive control sets it there (TEST-NET-3: never routable, never resolved).
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://203.0.113.9:4318");
        try
        {
            var builder = WebApplication.CreateSlimBuilder();

            QylServiceDefaultsExtensions.ConfigureQylTelemetry(builder, new QylOptions());

            Assert.True(HasOtlpRegistration(builder.Services),
                "A configured endpoint composed no OTLP exporter — the negative assertion above " +
                "would now pass for the wrong reason. The composition or this probe changed shape.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        }
    }
}

using System.Collections.ObjectModel;
using OpenTelemetry.Metrics;

namespace Qyl.OpenTelemetry.Extensions;

/// <summary>
/// Options for <see cref="QylOpenTelemetryServiceCollectionExtensions.AddQylOpenTelemetry"/>.
/// </summary>
public sealed class QylOtelOptions
{
    /// <summary>
    /// Gets or sets the OTLP endpoint for qyl tracing export.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry service name resource attribute. This value is required.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the optional API key sent as a bearer token on the qyl trace exporter.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the trace sampling ratio, where 1.0 samples every trace and 0.0 samples none.
    /// </summary>
    public double SampleRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets whether qyl tracing export is registered. The default is <see langword="true"/>.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an OpenTelemetry metrics pipeline is registered even without explicit meter names.
    /// </summary>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Gets meter names registered with the OpenTelemetry metrics pipeline.
    /// </summary>
    public Collection<string> MeterNames { get; } = [];

    /// <summary>
    /// Gets or sets additional metrics pipeline configuration, including any exporter the application owns.
    /// </summary>
    public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }
}

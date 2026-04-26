// Copyright (c) 2025-2026 ancplua

namespace Qyl.OpenTelemetry.Extensions;

/// <summary>
///     Configuration for the qyl OpenTelemetry exporter.
/// </summary>
/// <remarks>
///     Validated by <see cref="QylOpenTelemetryServiceCollectionExtensions.AddQylOpenTelemetry" /> —
///     setting <see cref="Endpoint" /> and <see cref="ServiceName" /> is mandatory; the registration
///     call throws if either is left at its default.
/// </remarks>
public sealed class QylOtelOptions
{
    /// <summary>Base OTLP endpoint (gRPC or HTTP).</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Service name surfaced on every resource.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Optional bearer token sent as <c>Authorization: Bearer &lt;key&gt;</c> on every OTLP export.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Fraction of traces to sample (0.0–1.0). Defaults to 1.0 (sample all).</summary>
    public double SampleRate { get; set; } = 1.0;
}

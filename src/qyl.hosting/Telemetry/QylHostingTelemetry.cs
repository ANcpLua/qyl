namespace Qyl.Hosting.Telemetry;

/// <summary>
///     ActivitySource for qyl.hosting distributed tracing.
///     Meter is auto-generated via [Meter] attribute on QylHostingMetrics.
/// </summary>
internal static class QylHostingTelemetry
{
    public const string ServiceName = "qyl.hosting";
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    ///     ActivitySource for distributed tracing with OTel 1.39 schema URL.
    /// </summary>
    public static readonly ActivitySource Source = new(new ActivitySourceOptions(ServiceName)
    {
        Version = ServiceVersion, TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.39.0"
    });
}

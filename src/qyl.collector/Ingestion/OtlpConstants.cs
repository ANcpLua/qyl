namespace qyl.collector.Ingestion;

/// <summary>
///     Shared constants for OTLP endpoints.
/// </summary>
public static class OtlpConstants
{
    /// <summary>OTLP HTTP endpoint paths.</summary>
    public static readonly string[] Paths = ["/v1/traces", "/v1/logs", "/v1/metrics"];

    /// <summary>Check if a path is an OTLP endpoint.</summary>
    public static bool IsOtlpPath(string path) =>
        Paths.Any(path.StartsWithIgnoreCase);
}

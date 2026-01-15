namespace qyl.collector.Ingestion;

/// <summary>
///     Configuration for OTLP endpoint CORS.
/// </summary>
public sealed class OtlpCorsOptions
{
    /// <summary>
    ///     Allowed origins. Comma-separated list or "*" for all.
    ///     Default: empty (CORS disabled).
    /// </summary>
    public string? AllowedOrigins { get; set; }

    /// <summary>
    ///     Additional allowed headers beyond defaults.
    ///     Default: content-type, x-otlp-api-key.
    /// </summary>
    public string? AllowedHeaders { get; set; }

    /// <summary>
    ///     Max age for preflight cache in seconds.
    /// </summary>
    public int MaxAge { get; set; } = 86400; // 24 hours

    public bool IsEnabled => !string.IsNullOrWhiteSpace(AllowedOrigins);

    public IEnumerable<string> GetOrigins() =>
        AllowedOrigins?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? [];

    public IEnumerable<string> GetHeaders()
    {
        var defaults = new[]
        {
            "content-type", "x-otlp-api-key"
        };
        var custom = AllowedHeaders?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     ?? [];
        return defaults.Concat(custom).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

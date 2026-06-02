namespace Qyl.Collector.Ingestion;

internal sealed class OtlpCorsOptions
{
    public string? AllowedOrigins { get; set; }

    public string? AllowedHeaders { get; set; }

    public int MaxAge
    {
        get;
        set => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "MaxAge must be positive");
    } = 86400;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(AllowedOrigins);

    public IEnumerable<string> GetOrigins() =>
        AllowedOrigins?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? [];

    public IEnumerable<string> GetHeaders()
    {
        var defaults = new[] { "content-type", "x-otlp-api-key" };
        var custom = AllowedHeaders?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     ?? [];
        return defaults.Concat(custom).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

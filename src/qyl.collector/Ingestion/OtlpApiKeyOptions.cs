namespace qyl.collector.Ingestion;

/// <summary>
///     Configuration for OTLP endpoint authentication.
/// </summary>
public sealed class OtlpApiKeyOptions
{
    /// <summary>
    ///     Auth mode: "ApiKey" or "Unsecured".
    /// </summary>
    public string AuthMode { get; set; } = "Unsecured";

    /// <summary>
    ///     Primary API key for validation.
    /// </summary>
    public string? PrimaryApiKey { get; set; }

    /// <summary>
    ///     Secondary API key for rotation.
    /// </summary>
    public string? SecondaryApiKey { get; set; }

    /// <summary>
    ///     Header name for API key.
    /// </summary>
    public string HeaderName { get; set; } = "x-otlp-api-key";

    public bool IsApiKeyMode =>
        AuthMode.Equals("ApiKey", StringComparison.OrdinalIgnoreCase);
}

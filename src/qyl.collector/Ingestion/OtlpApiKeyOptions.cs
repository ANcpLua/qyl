namespace qyl.collector.Ingestion;

/// <summary>
///     Configuration for OTLP endpoint authentication.
/// </summary>
public sealed class OtlpApiKeyOptions
{
    private static readonly string[] ValidAuthModes = ["ApiKey", "Unsecured"];

    /// <summary>
    ///     Auth mode: "ApiKey" or "Unsecured".
    /// </summary>
    public string AuthMode
    {
        get;
        set => field = ValidAuthModes.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value
            : throw new ArgumentException($"AuthMode must be one of: {string.Join(", ", ValidAuthModes)}",
                nameof(value));
    } = "Unsecured";

    /// <summary>
    ///     Primary API key for validation.
    /// </summary>
    public string? PrimaryApiKey { get; set; }

    /// <summary>
    ///     Secondary API key for rotation.
    /// </summary>
    public string? SecondaryApiKey { get; set; }

    /// <summary>
    ///     Header name for API key. Cannot be empty.
    /// </summary>
    public string HeaderName
    {
        get;
        set => field = !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("HeaderName cannot be empty", nameof(value));
    } = "x-otlp-api-key";

    public bool IsApiKeyMode =>
        AuthMode.EqualsIgnoreCase("ApiKey");
}

namespace Qyl.Collector.Ingestion;

public sealed class OtlpApiKeyOptions
{
    private static readonly string[] s_validAuthModes = ["ApiKey", "Unsecured"];

    public string AuthMode
    {
        get;
        set => field = s_validAuthModes.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value
            : throw new ArgumentException($"AuthMode must be one of: {string.Join(", ", s_validAuthModes)}",
                nameof(value));
    } = "Unsecured";

    public string? PrimaryApiKey { get; set; }

    public string? SecondaryApiKey { get; set; }

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

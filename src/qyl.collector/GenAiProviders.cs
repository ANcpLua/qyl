namespace qyl.collector;

/// <summary>
///     Provider identifiers per OTel 1.39 gen_ai.provider.name values.
///     Supports host-based provider detection for automatic attribution.
/// </summary>
public static class GenAiProviders
{
    // OTel 1.39 provider name constants
    public const string OpenAi = "openai";
    public const string Anthropic = "anthropic";
    public const string GcpGemini = "gcp.gemini";
    public const string AwsBedrock = "aws.bedrock";
    public const string AzureOpenAi = "azure.openai";
    public const string Cohere = "cohere";
    public const string Mistral = "mistral";

    private static readonly FrozenDictionary<string, string> SHostToProvider =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["api.openai.com"] = OpenAi,
            ["api.anthropic.com"] = Anthropic,
            ["generativelanguage.googleapis.com"] = GcpGemini,
            ["openai.azure.com"] = AzureOpenAi,
            ["api.cohere.ai"] = Cohere,
            ["api.mistral.ai"] = Mistral
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Attempts to detect the GenAI provider from an API host.
    /// </summary>
    /// <param name="host">The API host (e.g., "api.openai.com")</param>
    /// <param name="provider">The detected provider name if successful</param>
    /// <returns>True if provider was detected, false otherwise</returns>
    public static bool TryDetectFromHost(ReadOnlySpan<char> host, [NotNullWhen(true)] out string? provider)
    {
        // Check frozen dict first for exact matches
        var hostStr = host.ToString();
        if (SHostToProvider.TryGetValue(hostStr, out provider))
            return true;

        // Partial match for AWS Bedrock (contains region in hostname)
        if (host.Contains("bedrock-runtime", StringComparison.OrdinalIgnoreCase))
        {
            provider = AwsBedrock;
            return true;
        }

        provider = null;
        return false;
    }

    /// <summary>
    ///     Attempts to detect the GenAI provider from a full URL.
    /// </summary>
    public static bool TryDetectFromUrl(Uri url, [NotNullWhen(true)] out string? provider) =>
        TryDetectFromHost(url.Host.AsSpan(), out provider);
}
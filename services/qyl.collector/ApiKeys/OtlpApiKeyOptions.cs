namespace Qyl.Collector.ApiKeys;

internal sealed class OtlpApiKeyOptions
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

    // Each credential belongs to exactly one project. Multiple credentials can rotate within a project.
    public IReadOnlyList<ProjectApiKey> Keys { get; set; } = [];

    public bool IsApiKeyMode =>
        string.Equals(AuthMode, "ApiKey", StringComparison.OrdinalIgnoreCase);
}

internal sealed record ProjectApiKey(string ProjectId, string ApiKey);

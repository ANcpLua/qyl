namespace Qyl.Collector.Ingestion;

/// <summary>
/// Resolve the project bound to a credential for both transports.
/// </summary>
internal static class OtlpApiKeyValidator
{
    public static string? ResolveProject(string? candidate, OtlpApiKeyOptions options)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;

        string? projectId = null;
        foreach (var entry in options.Keys)
        {
            if (FixedTimeEquals(candidate, entry.ApiKey))
                projectId = entry.ProjectId;
        }

        return projectId;
    }

    private static bool FixedTimeEquals(string candidate, string expected)
    {
        if (candidate.Length != expected.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < candidate.Length; i++)
            diff |= candidate[i] ^ expected[i];

        return diff is 0;
    }
}

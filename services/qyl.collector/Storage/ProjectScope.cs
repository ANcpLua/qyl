namespace Qyl.Collector.Storage;

internal static class ProjectScope
{
    public const string DefaultProjectId = "default";

    public static string Normalize(string? projectId) =>
        string.IsNullOrWhiteSpace(projectId) ? DefaultProjectId : projectId.Trim();

    public static string ForIngest(string? projectIdHint, string? authenticatedProjectId)
    {
        if (authenticatedProjectId is null) return Normalize(projectIdHint);
        if (!string.IsNullOrWhiteSpace(projectIdHint) &&
            !string.Equals(Normalize(projectIdHint), authenticatedProjectId, StringComparison.Ordinal))
            throw new InvalidDataException("OTLP project does not match the authenticated project.");

        return authenticatedProjectId;
    }
}

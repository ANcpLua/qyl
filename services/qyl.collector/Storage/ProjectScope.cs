namespace Qyl.Collector.Storage;

internal static class ProjectScope
{
    public const string DefaultProjectId = "default";

    public static string Normalize(string? projectId) =>
        string.IsNullOrWhiteSpace(projectId) ? DefaultProjectId : projectId.Trim();
}

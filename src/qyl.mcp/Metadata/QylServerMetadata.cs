using System.Reflection;

namespace qyl.mcp.Metadata;

internal static class QylServerMetadata
{
    public const string Name = "qyl";
    public const string DisplayName = "qyl MCP";

    public const string Summary =
        "qyl exposes observability tools for traces, logs, errors, builds, analytics, RCA, and AI workflows.";

    public const string Instructions =
        "Use qyl tools to inspect telemetry, traces, logs, errors, builds, and AI workflow health.";

    public const string DocumentationUrl = "https://github.com/ANcpLua/qyl.mcp#readme";

    public static string Version =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.1.0-beta";
}

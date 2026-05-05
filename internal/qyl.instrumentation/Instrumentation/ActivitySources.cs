// =============================================================================
// Activity Sources - OTel Enterprise Pattern
// Single source of truth for qyl ActivitySource and Meter instances
// Based on: opentelemetry-dotnet-contrib patterns
// =============================================================================

using System.Reflection;

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     Single source of truth for qyl activity sources and meters.
///     Uses assembly-based versioning per OTel enterprise pattern.
/// </summary>
public static class ActivitySources
{
    /// <summary>GenAI operations source name.</summary>
    public const string GenAi = GenAiConstants.SourceName;

    /// <summary>Database operations source name.</summary>
    public const string Db = "qyl.db";

    /// <summary>Traced method operations source name.</summary>
    public const string Traced = "qyl.traced";

    /// <summary>Agent operations source name.</summary>
    public const string Agent = "qyl.agent";

    /// <summary>MCP protocol operations source name.</summary>
    public const string Mcp = "qyl.mcp";

    /// <summary>Assembly containing the instrumentation.</summary>
    internal static readonly Assembly s_assembly = typeof(ActivitySources).Assembly;

    /// <summary>Package version extracted from assembly metadata, with git SHA stripped for stable span tags.</summary>
    internal static readonly string s_version = GetVersion(s_assembly);

    /// <summary>ActivitySource for GenAI instrumentation.</summary>
    public static ActivitySource GenAiSource => field ??= new ActivitySource(GenAi, s_version);

    /// <summary>ActivitySource for database instrumentation.</summary>
    public static ActivitySource DbSource => field ??= new ActivitySource(Db, s_version);

    /// <summary>ActivitySource for agent instrumentation.</summary>
    public static ActivitySource AgentSource => field ??= new ActivitySource(Agent, s_version);

    /// <summary>ActivitySource for MCP protocol instrumentation.</summary>
    public static ActivitySource McpSource => field ??= new ActivitySource(Mcp, s_version);

    /// <summary>Meter for GenAI metrics.</summary>
    public static Meter GenAiMeter => field ??= new Meter(GenAi, s_version);

    /// <summary>Meter for agent metrics.</summary>
    public static Meter AgentMeter => field ??= new Meter(Agent, s_version);

    private static string GetVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? assembly.GetName().Version?.ToString()
                            ?? "0.0.0";
        var plus = informational.IndexOfOrdinal('+');
        return plus > 0 ? informational[..plus] : informational;
    }
}

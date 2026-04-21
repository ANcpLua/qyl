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
    internal static readonly Assembly Assembly = typeof(ActivitySources).Assembly;

    /// <summary>Package version extracted from assembly metadata, with git SHA stripped for stable span tags.</summary>
    internal static readonly string Version = GetVersion(Assembly);

    private static string GetVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? assembly.GetName().Version?.ToString()
                            ?? "0.0.0";
        var plus = informational.IndexOf('+', System.StringComparison.Ordinal);
        return plus > 0 ? informational.Substring(0, plus) : informational;
    }

    // Lazy-initialized ActivitySource instances
    private static ActivitySource? s_genAi;

    private static ActivitySource? s_db;
    private static ActivitySource? s_agent;
    private static ActivitySource? s_mcp;

    // Lazy-initialized Meter instances
    private static Meter? s_genAiMeter;
    private static Meter? s_agentMeter;

    /// <summary>ActivitySource for GenAI instrumentation.</summary>
    public static ActivitySource GenAiSource => s_genAi ??= new ActivitySource(GenAi, Version);

    /// <summary>ActivitySource for database instrumentation.</summary>
    public static ActivitySource DbSource => s_db ??= new ActivitySource(Db, Version);

    /// <summary>ActivitySource for agent instrumentation.</summary>
    public static ActivitySource AgentSource => s_agent ??= new ActivitySource(Agent, Version);

    /// <summary>ActivitySource for MCP protocol instrumentation.</summary>
    public static ActivitySource McpSource => s_mcp ??= new ActivitySource(Mcp, Version);

    /// <summary>Meter for GenAI metrics.</summary>
    public static Meter GenAiMeter => s_genAiMeter ??= new Meter(GenAi, Version);

    /// <summary>Meter for agent metrics.</summary>
    public static Meter AgentMeter => s_agentMeter ??= new Meter(Agent, Version);
}

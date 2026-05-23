
using System.Reflection;

namespace Qyl.Instrumentation.Instrumentation;

public static class ActivitySources
{
    public const string GenAi = GenAiConstants.SourceName;

    public const string Db = "qyl.db";

    public const string Traced = "qyl.traced";

    public const string Agent = "qyl.agent";

    public const string Mcp = "qyl.mcp";

    public const string ErrorCapture = "Qyl.Instrumentation.ErrorCapture";

    internal static readonly Assembly s_assembly = typeof(ActivitySources).Assembly;

    internal static readonly string s_version = GetVersion(s_assembly);

    public static ActivitySource GenAiSource => field ??= new ActivitySource(GenAi, s_version);

    public static ActivitySource DbSource => field ??= new ActivitySource(Db, s_version);

    public static ActivitySource AgentSource => field ??= new ActivitySource(Agent, s_version);

    public static ActivitySource McpSource => field ??= new ActivitySource(Mcp, s_version);

    public static Meter GenAiMeter => field ??= new Meter(GenAi, s_version);

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

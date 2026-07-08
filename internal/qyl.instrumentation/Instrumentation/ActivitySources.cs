
using System.Reflection;

namespace Qyl.Instrumentation.Instrumentation;

public static class ActivitySources
{
    public const string GenAi = GenAiConstants.SourceName;

    public const string Db = "qyl.db";

    public const string Agent = "qyl.agent";

    public const string ErrorCapture = "Qyl.Instrumentation.ErrorCapture";

    internal static readonly Assembly s_assembly = typeof(ActivitySources).Assembly;

    internal static readonly string s_version = GetVersion(s_assembly);

    private static readonly ActivitySource s_genAiSource = new(GenAi, s_version);

    private static readonly ActivitySource s_agentSource = new(Agent, s_version);

    private static readonly Meter s_genAiMeter = new(GenAi, s_version);

    private static readonly Meter s_agentMeter = new(Agent, s_version);

    public static ActivitySource GenAiSource => s_genAiSource;

    public static ActivitySource AgentSource => s_agentSource;

    public static Meter GenAiMeter => s_genAiMeter;

    public static Meter AgentMeter => s_agentMeter;

    private static string GetVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? assembly.GetName().Version?.ToString()
                            ?? "0.0.0";
        var plus = informational.IndexOfOrdinal('+');
        return plus > 0 ? informational[..plus] : informational;
    }
}

// =============================================================================
// Activity Sources - OTel Enterprise Pattern
// Single source of truth for qyl ActivitySource and Meter instances
// Based on: opentelemetry-dotnet-contrib patterns
// =============================================================================

using System.Reflection;
using Qyl.ServiceDefaults.Internal;

namespace Qyl.ServiceDefaults.Instrumentation;

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

    /// <summary>Assembly containing the instrumentation.</summary>
    internal static readonly Assembly Assembly = typeof(ActivitySources).Assembly;

    /// <summary>Package version extracted from assembly metadata.</summary>
    internal static readonly string Version = Assembly.GetPackageVersion();

    // Lazy-initialized ActivitySource instances
    private static ActivitySource? s_genAi;

    private static ActivitySource? s_db;

    // Lazy-initialized Meter instances
    private static Meter? s_genAiMeter;

    /// <summary>ActivitySource for GenAI instrumentation.</summary>
    public static ActivitySource GenAiSource => s_genAi ??= new ActivitySource(GenAi, Version);

    /// <summary>ActivitySource for database instrumentation.</summary>
    public static ActivitySource DbSource => s_db ??= new ActivitySource(Db, Version);

    /// <summary>Meter for GenAI metrics.</summary>
    public static Meter GenAiMeter => s_genAiMeter ??= new Meter(GenAi, Version);
}

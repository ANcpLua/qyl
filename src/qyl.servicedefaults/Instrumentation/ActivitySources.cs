using System.Diagnostics;

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
/// Centralized ActivitySource definitions for instrumentation.
/// </summary>
/// <remarks>
/// These sources are automatically registered via <c>.AddSource("ANcpSdk.*")</c>
/// in the service defaults configuration.
/// </remarks>
internal static class ActivitySources
{
    /// <summary>
    /// ActivitySource for GenAI SDK instrumentation (OpenAI, Anthropic, Ollama).
    /// </summary>
    public static readonly ActivitySource GenAi = new("ANcpSdk.GenAi");

    /// <summary>
    /// ActivitySource for ADO.NET database instrumentation (DuckDB, SQLite, Npgsql, etc.).
    /// </summary>
    public static readonly ActivitySource Db = new("ANcpSdk.Db");
}

namespace Qyl.Hosting;

/// <summary>
/// Entry point for qyl-orchestrated distributed applications.
/// GenAI-native, zero-config observability, just works.
/// </summary>
/// <example>
/// <code>
/// var app = Qyl.CreateApp(args);
///
/// var api = app.AddProject&lt;Projects.MyApi&gt;("api")
///     .WithGenAI();
///
/// var frontend = app.AddVite("web", "../frontend")
///     .WithBrowserTelemetry()
///     .WaitFor(api);
///
/// app.Run();
/// </code>
/// </example>
public static class Qyl
{
    /// <summary>
    /// Creates a new qyl application with sensible defaults.
    /// Dashboard and GenAI instrumentation enabled automatically.
    /// </summary>
    public static QylAppBuilder CreateApp(string[] args)
        => new(args);

    /// <summary>
    /// Creates a new qyl application with custom options.
    /// </summary>
    public static QylAppBuilder CreateApp(string[] args, Action<QylOptions> configure)
    {
        var builder = new QylAppBuilder(args);
        configure(builder.Options);
        return builder;
    }
}

/// <summary>
/// Configuration for qyl orchestration.
/// </summary>
public sealed class QylOptions
{
    /// <summary>
    /// Dashboard port. Default: 5100
    /// </summary>
    public int DashboardPort { get; set; } = 5100;

    /// <summary>
    /// OTLP gRPC port. Default: 4317
    /// </summary>
    public int OtlpPort { get; set; } = 4317;

    /// <summary>
    /// Authentication token. Auto-generated if null.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Enable GenAI instrumentation for all projects. Default: true
    /// </summary>
    public bool GenAI { get; set; } = true;

    /// <summary>
    /// Enable cost tracking. Default: true
    /// </summary>
    public bool CostTracking { get; set; } = true;

    /// <summary>
    /// Persist telemetry data. Default: true
    /// </summary>
    public bool PersistData { get; set; } = true;

    /// <summary>
    /// Data directory for DuckDB. Default: .qyl/data
    /// </summary>
    public string DataPath { get; set; } = ".qyl/data";
}

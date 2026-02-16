namespace Qyl.Hosting.Resources;

/// <summary>
///     A Vite/React/Vue/Svelte frontend application.
/// </summary>
public sealed class ViteResource : QylResourceBase<ViteResource>
{
    internal ViteResource(string name, string workingDirectory, QylAppBuilder builder)
        : base(name, builder)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);

        // Auto-assign port for Vite dev server
        var port = PortAllocator.Next();
        AddPort(new PortBinding(port, port, "http", true));
        SetEnvironment("PORT", port.ToString());
        SetEnvironment("VITE_PORT", port.ToString());
    }

    /// <summary>
    ///     Path to the frontend directory (contains package.json).
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    ///     npm script to run. Default: "dev"
    /// </summary>
    public string RunScript { get; private set; } = "dev";

    /// <summary>
    ///     Whether browser telemetry is enabled.
    /// </summary>
    public bool BrowserTelemetryEnabled { get; private set; }

    public override string Type => "vite";

    /// <summary>
    ///     Sets the npm script to run.
    /// </summary>
    public ViteResource WithRunScript(string script)
    {
        RunScript = script;
        return this;
    }

    /// <summary>
    ///     Enables browser telemetry - console.log, errors, and performance sent to qyl.
    ///     Injects a small script that bridges browser console to the collector.
    /// </summary>
    public ViteResource WithBrowserTelemetry()
    {
        BrowserTelemetryEnabled = true;

        // Inject the qyl dashboard URL for the browser bridge
        SetEnvironment("VITE_QYL_ENDPOINT", $"http://localhost:{Builder.Options.DashboardPort}");
        SetEnvironment("VITE_QYL_BROWSER_TELEMETRY", "true");

        return this;
    }

    /// <summary>
    ///     Sets the API backend this frontend talks to.
    ///     Configures proxy and injects VITE_API_URL.
    /// </summary>
    public ViteResource WithApi(IQylResource api)
    {
        WithReference(api);

        // Set up Vite proxy configuration
        var apiPort = api.Ports.Count > 0 ? api.Ports[0].HostPort : 5000;
        SetEnvironment("VITE_API_URL", $"http://localhost:{apiPort}");

        return this;
    }
}

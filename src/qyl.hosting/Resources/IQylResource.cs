namespace Qyl.Hosting.Resources;

/// <summary>
/// Base interface for all qyl resources.
/// </summary>
public interface IQylResource
{
    /// <summary>
    /// Resource name (unique identifier).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Resource type for display.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Resources this depends on.
    /// </summary>
    IEnumerable<IQylResource> Dependencies { get; }

    /// <summary>
    /// Environment variables to inject.
    /// </summary>
    IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>
    /// Port bindings.
    /// </summary>
    IReadOnlyList<PortBinding> Ports { get; }

    /// <summary>
    /// Whether GenAI instrumentation is enabled.
    /// </summary>
    bool GenAiEnabled { get; }

    /// <summary>
    /// Whether cost tracking is enabled.
    /// </summary>
    bool CostTrackingEnabled { get; }

    /// <summary>
    /// HTTP endpoint for health checks (if any).
    /// </summary>
    string? HealthEndpoint { get; }
}

/// <summary>
/// Port binding configuration.
/// </summary>
public sealed record PortBinding(int HostPort, int ContainerPort, string? Name = null, bool External = false);

/// <summary>
/// Base class for qyl resources with fluent API.
/// </summary>
public abstract class QylResourceBase<TSelf>(string name, QylAppBuilder builder) : IQylResource
    where TSelf : QylResourceBase<TSelf>
{
    private readonly List<IQylResource> _dependencies = [];
    private readonly Dictionary<string, string> _environment = [];
    private readonly List<PortBinding> _ports = [];

    public string Name { get; } = name;
    public abstract string Type { get; }
    public IEnumerable<IQylResource> Dependencies => _dependencies;
    public IReadOnlyDictionary<string, string> Environment => _environment;
    public IReadOnlyList<PortBinding> Ports => _ports;
    public bool GenAiEnabled { get; private set; }
    public bool CostTrackingEnabled { get; private set; }
    public string? HealthEndpoint { get; private set; }

    protected QylAppBuilder Builder { get; } = builder;

    /// <summary>
    /// Declares a dependency - this resource waits for the other to be healthy.
    /// </summary>
    public TSelf WaitFor(IQylResource dependency)
    {
        _dependencies.Add(dependency);
        return (TSelf)this;
    }

    /// <summary>
    /// Adds a reference to another resource - injects connection info as environment variables.
    /// </summary>
    protected TSelf WithReference(IQylResource reference)
    {
        _dependencies.Add(reference);

        // Inject connection environment variables
        var prefix = reference.Name.ToUpperInvariant().Replace('-', '_');
        foreach (var port in reference.Ports)
        {
            var portName = port.Name?.ToUpperInvariant() ?? "PORT";
            _environment[$"{prefix}_{portName}"] = port.HostPort.ToString();
            _environment[$"{prefix}_HOST"] = reference.Name;
        }

        return (TSelf)this;
    }

    /// <summary>
    /// Sets an environment variable.
    /// </summary>
    public TSelf WithEnvironment(string key, string value)
    {
        _environment[key] = value;
        return (TSelf)this;
    }

    /// <summary>
    /// Enables GenAI instrumentation (traces, metrics, cost tracking).
    /// </summary>
    public TSelf WithGenAi()
    {
        GenAiEnabled = true;
        CostTrackingEnabled = true;

        // Inject OpenTelemetry configuration
        _environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = $"http://qyl:{Builder.Options.OtlpPort}";
        _environment["OTEL_SERVICE_NAME"] = Name;
        _environment["OTEL_TRACES_SAMPLER"] = "always_on";

        // qyl-specific
        _environment["QYL_GENAI_ENABLED"] = "true";
        _environment["QYL_COST_TRACKING"] = "true";

        return (TSelf)this;
    }

    /// <summary>
    /// Enables cost tracking for LLM API calls.
    /// </summary>
    public TSelf WithCostTracking()
    {
        CostTrackingEnabled = true;
        _environment["QYL_COST_TRACKING"] = "true";
        return (TSelf)this;
    }

    /// <summary>
    /// Sets the HTTP health check endpoint.
    /// </summary>
    public TSelf WithHealthCheck(string endpoint = "/health")
    {
        HealthEndpoint = endpoint;
        return (TSelf)this;
    }

    /// <summary>
    /// Exposes the endpoint externally (outside the orchestration network).
    /// </summary>
    public TSelf WithExternalEndpoint()
    {
        if (_ports.Count > 0)
        {
            var port = _ports[0];
            _ports[0] = port with { External = true };
        }

        return (TSelf)this;
    }

    /// <summary>
    /// Adds a port binding.
    /// </summary>
    public TSelf WithPort(int port, string? name = null)
    {
        _ports.Add(new PortBinding(port, port, name));
        return (TSelf)this;
    }

    /// <summary>
    /// Adds a port mapping.
    /// </summary>
    public TSelf WithPort(int hostPort, int containerPort, string? name = null)
    {
        _ports.Add(new PortBinding(hostPort, containerPort, name));
        return (TSelf)this;
    }

    protected void AddPort(PortBinding binding) => _ports.Add(binding);
    protected void SetEnvironment(string key, string value) => _environment[key] = value;
}

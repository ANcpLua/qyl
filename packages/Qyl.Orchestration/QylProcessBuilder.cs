using Qyl.Orchestration.Internal;

namespace Qyl.Orchestration;

/// <summary>Configures one service process before its application is built.</summary>
public sealed class QylProcessBuilder
{
    private readonly QylApplicationBuilder _owner;
    private readonly HashSet<string> _references = new(StringComparer.Ordinal);

    internal QylProcessBuilder(QylApplicationBuilder owner, string name, QylLaunchSpec launch)
    {
        _owner = owner;
        Resource = new QylResource { Name = name, Kind = QylResourceKind.Project, Port = 0, Launch = launch };
    }

    internal QylResource Resource { get; private set; }
    internal IEnumerable<string> References => _references;

    /// <summary>The service's unique name.</summary>
    public string Name => Resource.Name;

    /// <summary>Sets a child environment variable, overriding the inherited value.</summary>
    public QylProcessBuilder WithEnvironment(string name, string value)
    {
        _owner.EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        Resource = Resource with
        {
            Launch = Resource.Launch with
            {
                Env = new Dictionary<string, string>(Resource.Launch.Env, StringComparer.Ordinal) { [name] = value }
            }
        };
        return this;
    }

    /// <summary>Removes inherited variables with this prefix before applying explicit environment values.</summary>
    public QylProcessBuilder WithoutEnvironmentPrefix(string prefix)
    {
        _owner.EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Resource = Resource with
        {
            Launch = Resource.Launch with
            {
                RemovedEnvironmentPrefixes = [.. Resource.Launch.RemovedEnvironmentPrefixes, prefix]
            }
        };
        return this;
    }

    /// <summary>Passes another service's HTTP endpoint as services__name__http__0.</summary>
    public QylProcessBuilder WithReference(QylProcessBuilder service)
    {
        CheckOwner(service);
        _references.Add(service.Name);
        return this;
    }

    /// <summary>Starts after the named services are ready and stops before them. Cycles fail at Build.</summary>
    public QylProcessBuilder WaitFor(params QylProcessBuilder[] services)
    {
        _owner.EnsureMutable();
        ArgumentNullException.ThrowIfNull(services);
        foreach (var service in services)
            CheckOwner(service);
        Resource = Resource with
        {
            WaitsFor = [.. Resource.WaitsFor.Concat(services.Select(static service => service.Name)).Distinct(StringComparer.Ordinal)]
        };
        return this;
    }

    /// <summary>Requires a successful response at the path; optionally requires the response body to be the child's process ID.</summary>
    public QylProcessBuilder WithHealthCheck(string path = "/health", bool requireProcessId = false)
    {
        _owner.EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("A health path must start with a single '/'.", nameof(path));
        Resource = Resource with
        {
            Launch = Resource.Launch with { HealthPath = path, RequireProcessId = requireProcessId }
        };
        return this;
    }

    private void CheckOwner(QylProcessBuilder service)
    {
        _owner.EnsureMutable();
        ArgumentNullException.ThrowIfNull(service);
        if (!ReferenceEquals(_owner, service._owner))
            throw new ArgumentException("Services must belong to the same application.", nameof(service));
    }
}

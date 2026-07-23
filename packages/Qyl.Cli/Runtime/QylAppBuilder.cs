using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Qyl.Cli.Runtime;

internal sealed class QylAppBuilder
{
    private readonly HostApplicationBuilder _host;
    private readonly QylAppOptions _options;
    private readonly List<QylResource> _resources = [];

    private QylAppBuilder(HostApplicationBuilder host, QylAppOptions options)
    {
        _host = host;
        _options = options;
    }

    internal IReadOnlyList<QylResource> Resources => _resources;

    internal static QylAppBuilder Create(QylAppOptions? options = null)
    {
        return new QylAppBuilder(
            Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder([]),
            options ?? new QylAppOptions());
    }

    internal QylResourceBuilder AddCollector(string name, string executable, IReadOnlyList<string> arguments,
        int? port = null, string? workingDirectory = null,
        Action<QylSelfTelemetryBuilder>? selfTelemetry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Any(static argument => argument is null))
            throw new ArgumentException("Arguments cannot contain null values.", nameof(arguments));

        return AddCollectorCore(name, new QylProcessCommand
        {
            Executable = executable,
            Args = new ReadOnlyCollection<string>(arguments.ToArray()),
            WorkingDirectory = workingDirectory
        }, port, selfTelemetry);
    }

    internal QylResourceBuilder AddCollectorCore(string name, QylProcessCommand command, int? port,
        Action<QylSelfTelemetryBuilder>? selfTelemetry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Collector port must be in the range 1..65535.");

        // Collectors resolve to a concrete port at composition time: the child only learns its API
        // port through QYL_PORT, so deferring to launch-time DynamicAllocation would health-probe a
        // port the collector never binds (it would fall back to its built-in 5100 default).
        var apiPort = port ?? PortAllocator.ClaimFreePort(QylConstants.Network.Loopback);

        // OTLP receiver ports must be unique across the whole composition: two children pinned to
        // the same receiver port either lose a bind race or, worse, receive each other's telemetry.
        // The first collector gets the well-known defaults; every later one gets freshly claimed ports.
        var otlpHttpPort = NextFreePort(QylConstants.Collector.DefaultOtlpHttpPort, apiPort);
        var grpcPort = NextFreePort(QylConstants.Collector.DefaultGrpcPort, apiPort, otlpHttpPort);

        var env = new Dictionary<string, string>
        {
            [QylConstants.Env.QylPort] = apiPort.ToString(CultureInfo.InvariantCulture),
            // Pin the ports the resource record advertises, so ambient QYL_OTLP_PORT/QYL_GRPC_PORT
            // in the runner's environment cannot desync child reality from composition.
            [QylConstants.Env.QylOtlpPort] = otlpHttpPort.ToString(CultureInfo.InvariantCulture),
            [QylConstants.Env.QylGrpcPort] = grpcPort.ToString(CultureInfo.InvariantCulture),
            // Host-run collectors deliberately disable ingest auth for local development. Pin every
            // listener to loopback so that choice can never expose an unauthenticated LAN endpoint.
            [QylConstants.Env.QylBindAddress] = QylConstants.Network.Loopback
        };

        // The runner launches children with ASPNETCORE_ENVIRONMENT=dev, which is not "Development",
        // so the collector's auth fallback is ApiKey and it refuses to start without a key. Default
        // the loopback dev-runner children to Unsecured unless the operator's environment already
        // decided the auth mode (the child inherits that decision). Whitespace counts as undecided:
        // `export QYL_OTLP_AUTH_MODE=` yields "" (not null), and "" is not a decision.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode)))
            env[QylConstants.Env.QylOtlpAuthMode] = QylConstants.Collector.UnsecuredAuthMode;

        var builder = Register(new QylResource
        {
            Name = name,
            Kind = QylResourceKind.Collector,
            Port = apiPort,
            OtlpHttpPort = otlpHttpPort,
            GrpcPort = grpcPort,
            Launch = BuildLaunchSpec(command, name, env)
        }).WithIsolatedStorage();

        if (selfTelemetry is not null)
        {
            var telemetry = new QylSelfTelemetryBuilder(this, builder, command);
            selfTelemetry(telemetry);
            telemetry.Apply();
        }

        return builder;
    }

    internal QylApp Build()
    {
        ValidateWaitForGraph();
        _host.Services.AddSingleton(_options);
        _host.Services.AddSingleton(TimeProvider.System);
        _host.Services.AddSingleton<IReadOnlyList<QylResource>>(new ReadOnlyCollection<QylResource>([.. _resources]));
#pragma warning disable AL1105 // health probing IS the retry loop; a resilience pipeline here would double-retry and drags non-AOT config binding back in
        _host.Services.AddHttpClient(QylConstants.HttpClients.HealthProbe, static client =>
                client.Timeout = TimeSpan.FromSeconds(QylConstants.Orchestrator.HealthProbeAttemptTimeoutSeconds))
            // Per-attempt HTTP logging is noise by the same argument: connection-refused during
            // startup is the probe working, not an error, and the recurring poll chatter shreds
            // the interactive console table. The orchestrator reports the state transitions.
            .RemoveAllLoggers();
#pragma warning restore AL1105
        _host.Services.AddSingleton<QylResourceRegistry>();
        _host.Services.AddSingleton<QylResourceActions>();
        _host.Services.AddSingleton<QylLogStore>();
        _host.Services.AddSingleton<QylProcessLauncher>();
        _host.Services.AddHostedService<QylOrchestrator>();
        _host.Services.AddHostedService<QylConsoleUi>();
        _host.Services.AddHostedService<QylRunnerApi>();
        return new QylApp(_host.Build());
    }

    // A WaitFor edge naming an unknown resource would wait forever; a cycle would deadlock the whole
    // composition. Both are composition bugs, so both fail Build() loudly.
    private void ValidateWaitForGraph()
    {
        var byName = _resources.ToDictionary(static r => r.Name, StringComparer.Ordinal);

        foreach (var resource in _resources)
            foreach (var dep in resource.WaitsFor.Where(dep => !byName.ContainsKey(dep)))
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' waits for unknown resource '{dep}'.");
            }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string name)
        {
            if (done.Contains(name))
                return;
            if (!visiting.Add(name))
            {
                throw new InvalidOperationException(
                    $"WaitFor cycle detected involving resource '{name}' — a cycle can never become ready.");
            }

            foreach (var dep in byName[name].WaitsFor)
                Visit(dep);
            visiting.Remove(name);
            done.Add(name);
        }

        foreach (var resource in _resources)
            Visit(resource.Name);
    }

    // First caller of a well-known port keeps it; later collectors get freshly claimed ports so the
    // composition can never pin two children to the same receiver. alsoAvoid covers ports assigned
    // earlier in the same AddCollector call that are not yet visible in _resources.
    private int NextFreePort(int preferred, params int[] alsoAvoid)
    {
        var taken = _resources
            .SelectMany(static r => new[] { r.Port, r.OtlpHttpPort, r.GrpcPort })
            .Concat(alsoAvoid)
            .Where(static p => p > 0)
            .ToHashSet();

        return taken.Contains(preferred)
            ? PortAllocator.ClaimFreePort(QylConstants.Network.Loopback)
            : preferred;
    }

    private QylResourceBuilder Register(QylResource resource)
    {
        if (_resources.Any(r => string.Equals(r.Name, resource.Name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Resource '{resource.Name}' was already added; names must be unique.");
        }

        // Port 0 is not a claim: it means DynamicAllocation (launch-time claim) or a connection-only
        // resource with no listening port at all — two zeros never collide.
        var declaredPorts = DeclaredPorts(resource).ToHashSet();
        if (declaredPorts.Count != DeclaredPorts(resource).Count())
        {
            throw new InvalidOperationException(
                $"Resource '{resource.Name}' assigns the same port to more than one endpoint.");
        }

        var portClash = _resources.FirstOrDefault(existing =>
            DeclaredPorts(existing).Any(declaredPorts.Contains));
        if (portClash is not null)
        {
            throw new InvalidOperationException(
                $"Resource '{resource.Name}' overlaps a port already used by '{portClash.Name}'; every api/otlp/grpc port must be unique.");
        }

        _resources.Add(resource);
        return new QylResourceBuilder(this, resource, (oldResource, newResource) =>
        {
            var index = _resources.IndexOf(oldResource);
            if (index >= 0)
                _resources[index] = newResource;
        });
    }

    private static IEnumerable<int> DeclaredPorts(QylResource resource)
    {
        if (resource.Port > 0)
            yield return resource.Port;
        if (resource.OtlpHttpPort > 0)
            yield return resource.OtlpHttpPort;
        if (resource.GrpcPort > 0)
            yield return resource.GrpcPort;
    }

    private static QylLaunchSpec BuildLaunchSpec(QylProcessCommand command, string serviceName,
        IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        var env = new Dictionary<string, string>
        {
            [QylConstants.Env.AspNetCoreEnvironment] = QylConstants.Environments.Dev,
            [QylConstants.Env.DotnetEnvironment] = QylConstants.Environments.Dev,
            [QylConstants.Env.OtelServiceName] = serviceName
        };

        if (extraEnv is not null)
        {
            foreach (var kv in extraEnv)
                env[kv.Key] = kv.Value;
        }

        return new QylLaunchSpec
        {
            Executable = command.Executable,
            Args = command.Args,
            WorkingDirectory = command.WorkingDirectory,
            Env = env
        };
    }
}

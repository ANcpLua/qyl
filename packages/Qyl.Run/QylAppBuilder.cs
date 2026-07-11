using System.Collections.ObjectModel;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qyl.Run.Internal;

namespace Qyl.Run;

public sealed class QylAppBuilder
{
    private readonly List<QylResource> _resources = [];

    private QylAppBuilder(HostApplicationBuilder host)
    {
        Host = host;
    }

    public HostApplicationBuilder Host { get; }

    public IReadOnlyList<QylResource> Resources => _resources;

    public static QylAppBuilder Create(string[]? args = null)
    {
        return new QylAppBuilder(Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? []));
    }

    public IQylResourceBuilder AddCollector(string name, int? port = null, string? project = null,
        Action<QylSelfTelemetryBuilder>? selfTelemetry = null)
    {
        Guard.NotNullOrWhiteSpace(name);

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
            [QylConstants.Env.QylGrpcPort] = grpcPort.ToString(CultureInfo.InvariantCulture)
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
            Kind = QylConstants.ResourceKinds.Collector,
            Port = apiPort,
            OtlpHttpPort = otlpHttpPort,
            GrpcPort = grpcPort,
            Launch = BuildLaunchSpec(project, name, env)
        });

        if (selfTelemetry is not null)
        {
            var telemetry = new QylSelfTelemetryBuilder(this, builder, project);
            selfTelemetry(telemetry);
            telemetry.Apply();
        }

        return builder;
    }

    public IQylResourceBuilder AddProject(string name, string project, int? port = null)
    {
        return AddCore(name, QylConstants.ResourceKinds.Project, port ?? QylConstants.Ports.DynamicAllocation,
            project);
    }

    public QylApp Build()
    {
        // Bound here, not in the ctor, so configuration sources added after Create() are still seen;
        // FromConfiguration validates imperatively, keeping the fail-fast of the old ValidateOnStart
        // without reflection-based binding. Retry policy for health probes lives in the orchestrator's
        // poll loop; the client timeout only bounds a single attempt so a hung connect cannot eat the
        // whole startup deadline.
        Host.Services.AddSingleton(QylAppOptions.FromConfiguration(Host.Configuration));
        Host.Services.AddSingleton(TimeProvider.System);
        Host.Services.AddSingleton<IReadOnlyList<QylResource>>(new ReadOnlyCollection<QylResource>([.. _resources]));
#pragma warning disable AL1105 // health probing IS the retry loop; a resilience pipeline here would double-retry and drags non-AOT config binding back in
        Host.Services.AddHttpClient(QylConstants.HttpClients.HealthProbe, static client =>
            client.Timeout = TimeSpan.FromSeconds(QylConstants.Orchestrator.HealthProbeAttemptTimeoutSeconds));
#pragma warning restore AL1105
        Host.Services.AddSingleton<QylResourceRegistry>();
        Host.Services.AddSingleton<QylRestartRequests>();
        Host.Services.AddSingleton<QylLogStore>();
        Host.Services.AddSingleton<QylProcessLauncher>();
        Host.Services.AddHostedService<QylOrchestrator>();
        Host.Services.AddHostedService<QylConsoleUi>();
        Host.Services.AddHostedService<QylRunnerApi>();
        return new QylApp(Host.Build());
    }

    private QylResourceBuilder AddCore(string name, string kind, int port, string? project)
    {
        Guard.NotNullOrWhiteSpace(name);
        return Register(new QylResource
        {
            Name = name,
            Kind = kind,
            Port = port,
            Launch = BuildLaunchSpec(project, name)
        });
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

        var portClash = _resources.FirstOrDefault(r =>
            r.Port == resource.Port ||
            (resource.OtlpHttpPort > 0 && (r.Port == resource.OtlpHttpPort || r.OtlpHttpPort == resource.OtlpHttpPort)) ||
            (resource.GrpcPort > 0 && (r.Port == resource.GrpcPort || r.GrpcPort == resource.GrpcPort)));
        if (portClash is not null)
        {
            throw new InvalidOperationException(
                $"Resource '{resource.Name}' overlaps a port already used by '{portClash.Name}'; every api/otlp/grpc port must be unique.");
        }

        _resources.Add(resource);
        return new QylResourceBuilder(this, resource, (oldResource, newResource) =>
        {
            var index = _resources.IndexOf(oldResource);
            if (index >= 0) _resources[index] = newResource;
        });
    }

    private static QylLaunchSpec BuildLaunchSpec(string? project, string serviceName,
        IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        if (project is null)
        {
            throw new InvalidOperationException($"Resource '{serviceName}' needs a project path.");
        }

        var env = new Dictionary<string, string>
        {
            [QylConstants.Env.AspNetCoreEnvironment] = QylConstants.Environments.Dev,
            [QylConstants.Env.DotnetEnvironment] = QylConstants.Environments.Dev,
            [QylConstants.Env.OtelServiceName] = serviceName
        };

        if (extraEnv is not null)
        {
            foreach (var kv in extraEnv) env[kv.Key] = kv.Value;
        }

        return new QylLaunchSpec
        {
            Executable = QylConstants.Orchestrator.DotnetExecutable,
            // --no-launch-profile: a Properties/launchSettings.json in the child project would
            // otherwise silently override every env var injected here, including the ports and the
            // self-telemetry loop-breaker. Composition-declared env must always win.
            Args = new ReadOnlyCollection<string>([
                QylConstants.Orchestrator.RunCommand, QylConstants.Orchestrator.NoLaunchProfileFlag,
                QylConstants.Orchestrator.ProjectFlag, project
            ]),
            Env = env
        };
    }
}

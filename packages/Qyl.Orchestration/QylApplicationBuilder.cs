using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qyl.Orchestration.Internal;

namespace Qyl.Orchestration;

/// <summary>Composes local service processes without starting them or opening a control server.</summary>
public sealed class QylApplicationBuilder
{
    private readonly List<QylProcessBuilder> _processes = [];
    private bool _built;

    /// <summary>The deadline for all selected processes to become ready. Defaults to 60 seconds.</summary>
    public int StartupTimeoutSeconds { get; set; } = OrchestrationDefaults.Orchestrator.StartupTimeoutSeconds;

    /// <summary>Crash restarts allowed after initial readiness. Set zero for deterministic tests.</summary>
    public int MaxRestarts { get; set; } = OrchestrationDefaults.Orchestrator.MaxRestarts;

    /// <summary>Adds a .NET executable already built with its runtimeconfig.json beside it.</summary>
    /// <param name="name">Unique service name, also used for discovery and telemetry.</param>
    /// <param name="assemblyPath">Path to the service's compiled assembly.</param>
    /// <returns>The process configuration.</returns>
    public QylProcessBuilder AddDotnetService(string name, string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        var path = Path.GetFullPath(assemblyPath);
        if (!File.Exists(path) || !File.Exists(Path.ChangeExtension(path, ".runtimeconfig.json")))
            throw new FileNotFoundException("A .NET service needs its assembly and runtimeconfig.json.", path);
        return AddExecutable(name, Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [path], Path.GetDirectoryName(path));
    }

    /// <summary>Adds an executable. Arguments are passed directly without shell parsing.</summary>
    /// <param name="name">Unique service name.</param>
    /// <param name="executable">Executable name or path.</param>
    /// <param name="arguments">Individual arguments, copied at registration.</param>
    /// <param name="workingDirectory">Optional process working directory.</param>
    /// <returns>The process configuration.</returns>
    public QylProcessBuilder AddExecutable(string name, string executable, IReadOnlyList<string> arguments,
        string? workingDirectory = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        if (name.Contains(':', StringComparison.Ordinal) || name.Contains("__", StringComparison.Ordinal))
            throw new ArgumentException("Service names cannot contain configuration separators ':' or '__'.", nameof(name));
        if (_processes.Any(process => string.Equals(process.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Service '{name}' was already added.", nameof(name));
        if (arguments.Any(static argument => argument is null))
            throw new ArgumentException("Arguments cannot contain null.", nameof(arguments));
        var process = new QylProcessBuilder(this, name, new QylLaunchSpec
        {
            Executable = executable,
            Args = new ReadOnlyCollection<string>(arguments.ToArray()),
            WorkingDirectory = workingDirectory,
            Env = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["OTEL_SERVICE_NAME"] = name
            }
        });
        _processes.Add(process);
        return process;
    }

    /// <summary>Freezes the composition and reserves distinct loopback endpoints. Dispose the returned application.</summary>
    /// <returns>An application whose processes start with <see cref="QylApplication.StartAsync"/>.</returns>
    public QylApplication Build()
    {
        EnsureMutable();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(StartupTimeoutSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRestarts);
        var resources = _processes.Select(process => process.Resource).ToArray();
        QylResourceGraph.Validate(resources);
        var reservations = new List<PortReservation>();
        IHost? host = null;
        try
        {
            for (var index = 0; index < resources.Length; index++)
            {
                var reservation = new PortReservation();
                reservations.Add(reservation);
                resources[index] = resources[index] with { Port = reservation.Port, Reservation = reservation };
            }
            var endpoints = resources.ToDictionary(resource => resource.Name,
                resource => new Uri($"http://127.0.0.1:{resource.Port}"), StringComparer.Ordinal);
            for (var index = 0; index < resources.Length; index++)
            {
                var environment = new Dictionary<string, string>(resources[index].Launch.Env, StringComparer.Ordinal);
                foreach (var reference in _processes[index].References)
                    environment[$"services__{reference}__http__0"] = endpoints[reference].ToString();
                resources[index] = resources[index] with
                {
                    Launch = resources[index].Launch with { Env = new ReadOnlyDictionary<string, string>(environment) }
                };
            }

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                DisableDefaults = true
            });
            // Embedded applications leave telemetry and console ownership to their caller.
            builder.Logging.ClearProviders();
            OrchestrationServices.Register(builder.Services, resources, new OrchestratorOptions
            {
                StartupTimeoutSeconds = StartupTimeoutSeconds,
                MaxRestarts = MaxRestarts
            });
            builder.Services.AddHostedService<QylOrchestrator>();
            host = builder.Build();
            var application = new QylApplication(host, resources, StartupTimeoutSeconds);
            _built = true;
            return application;
        }
        catch
        {
            host?.Dispose();
            foreach (var reservation in reservations)
                reservation.Dispose();
            throw;
        }
    }

    internal void EnsureMutable()
    {
        if (_built)
            throw new InvalidOperationException("This application was already built; create a new builder for another run.");
    }
}

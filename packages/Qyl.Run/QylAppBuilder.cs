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
        Host.Services
            .AddOptionsWithValidateOnStart<QylAppOptions>()
            .BindConfiguration(QylAppOptions.SectionName)
            .ValidateDataAnnotations();
    }

    public HostApplicationBuilder Host { get; }

    public IReadOnlyList<QylResource> Resources => _resources;

    public static QylAppBuilder Create(string[]? args = null)
    {
        return new QylAppBuilder(Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? []));
    }

    public IQylResourceBuilder AddCollector(string name, int? port = null,
        string environment = QylConstants.Environments.Dev, string? project = null, Uri? externalEndpoint = null,
        string? description = null)
    {
        return AddCore(name, QylConstants.ResourceKinds.Collector, port ?? QylConstants.Ports.DynamicAllocation,
            environment, project, externalEndpoint, description);
    }

    public IQylResourceBuilder AddDashboard(string name, int port = QylConstants.Ports.Dashboard,
        string environment = QylConstants.Environments.Dev, string? project = null, string? description = null)
    {
        return AddCore(name, QylConstants.ResourceKinds.Dashboard, port, environment, project, null,
            description);
    }

    public IQylResourceBuilder AddProject(string name, string project, int? port = null,
        string environment = QylConstants.Environments.Dev, string? description = null)
    {
        return AddCore(name, QylConstants.ResourceKinds.Project, port ?? QylConstants.Ports.DynamicAllocation,
            environment, project, null, description);
    }

    public QylApp Build()
    {
        Host.Services.AddSingleton(TimeProvider.System);
        Host.Services.AddSingleton<IReadOnlyList<QylResource>>(new ReadOnlyCollection<QylResource>([.. _resources]));
        Host.Services.AddHttpClient(QylConstants.HttpClients.HealthProbe)
            .AddStandardResilienceHandler();
        Host.Services.AddSingleton<QylResourceRegistry>();
        Host.Services.AddSingleton<QylProcessLauncher>();
        Host.Services.AddHostedService<QylOrchestrator>();
        Host.Services.AddHostedService<QylConsoleUi>();
        return new QylApp(Host.Build());
    }

    private QylResourceBuilder AddCore(string name, string kind, int port, string environment, string? project,
        Uri? externalEndpoint, string? description)
    {
        Guard.NotNullOrWhiteSpace(name);
        if (_resources.Any(r => string.Equals(r.Name, name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Resource '{name}' was already added; names must be unique.");
        }

        var resource = new QylResource
        {
            Name = name,
            Kind = kind,
            Environment = environment,
            Port = port,
            Launch = BuildLaunchSpec(project, externalEndpoint, environment, name),
            Description = description
        };
        _resources.Add(resource);
        return new QylResourceBuilder(this, resource, (oldR, newR) =>
        {
            var idx = _resources.IndexOf(oldR);
            if (idx >= 0) _resources[idx] = newR;
        });
    }

    private static QylLaunchSpec BuildLaunchSpec(string? project, Uri? externalEndpoint, string environment,
        string serviceName)
    {
        var env = new Dictionary<string, string>
        {
            [QylConstants.Env.AspNetCoreEnvironment] = environment,
            [QylConstants.Env.DotnetEnvironment] = environment,
            [QylConstants.Env.OtelServiceName] = serviceName
        };

        if (externalEndpoint is not null)
        {
            return new QylLaunchSpec { Executable = string.Empty, Env = env };
        }

        if (project is null)
        {
            throw new InvalidOperationException(
                $"Resource '{serviceName}' needs either a project path or an external endpoint.");
        }

        return new QylLaunchSpec
        {
            Executable = QylConstants.Orchestrator.DotnetExecutable,
            Args = new ReadOnlyCollection<string>([
                QylConstants.Orchestrator.RunCommand, QylConstants.Orchestrator.ProjectFlag, project
            ]),
            Env = env
        };
    }
}

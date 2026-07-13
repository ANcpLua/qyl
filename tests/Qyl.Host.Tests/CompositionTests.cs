using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Host.Internal;

namespace Qyl.Host.Tests;

public sealed class CompositionTests
{
    [Fact]
    public async Task Connection_only_readiness_does_not_invent_a_listener_endpoint()
    {
        var resource = new QylResource
        {
            Name = "remote-mcp",
            Kind = QylResourceKind.McpHttp,
            Port = QylConstants.Ports.DynamicAllocation,
            Launch = null,
            ReadinessProbe = new AlwaysReadyProbe()
        };
        QylResource[] resources = [resource];
        var registry = new QylResourceRegistry(resources, TimeProvider.System);
        var logs = new QylLogStore();
        var launcher = new QylProcessLauncher(logs, NullLogger<QylProcessLauncher>.Instance);
        await using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var orchestrator = new QylOrchestrator(
            resources,
            registry,
            new QylAppOptions { StartupTimeoutSeconds = 2 },
            services.GetRequiredService<IHttpClientFactory>(),
            launcher,
            new QylResourceActions(),
            TimeProvider.System,
            NullLogger<QylOrchestrator>.Instance);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(5));

        await orchestrator.StartAsync(lifetime.Token);
        try
        {
            while (!registry.Snapshot.TryGetValue(resource.Name, out var state) ||
                   state.Lifecycle is not ResourceLifecycle.Ready)
            {
                await Task.Delay(10, lifetime.Token);
            }

            var ready = registry.Snapshot[resource.Name];
            Assert.Null(ready.AllocatedPort);
            Assert.Null(ready.Endpoint);
        }
        finally
        {
            lifetime.Cancel();
            await orchestrator.StopAsync(TestContext.Current.CancellationToken);
            orchestrator.Dispose();
        }
    }

    [Fact]
    public async Task Command_arguments_reach_a_real_process_without_shell_parsing()
    {
        if (OperatingSystem.IsWindows()) return;

        const string executable = "/bin/echo";
        Assert.True(File.Exists(executable), $"Expected the platform executable {executable}.");

        var builder = QylAppBuilder.Create();
        builder.AddCommand("echo", executable, 54321, ["hello world"]);

        var resource = Assert.Single(builder.Resources);
        Assert.Equal(executable, resource.Launch?.Executable);
        Assert.Equal(["hello world"], resource.Launch?.Args);

        var logs = new QylLogStore();
        var launcher = new QylProcessLauncher(logs, NullLogger<QylProcessLauncher>.Instance);
        using var process = launcher.Launch(resource, new Uri("http://127.0.0.1:54321"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        await process.WaitForExitAsync(timeout.Token);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (logs.Snapshot(resource.Name).Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, timeout.Token);
        }

        var line = Assert.Single(logs.Snapshot(resource.Name));
        Assert.Equal("hello world", line.Line);
        Assert.Equal(QylLogStream.Stdout, line.Stream);
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public void Collector_composition_pins_every_unauthenticated_listener_to_loopback()
    {
        var builder = QylAppBuilder.Create();
        builder.AddCollector(
            "collector",
            "/work/qyl.collector.csproj",
            55100,
            static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", 55101));

        var owner = builder.Resources.Single(static resource => resource.Name == "collector");
        var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");

        Assert.Equal(QylResourceKind.Collector, owner.Kind);
        Assert.Equal(QylResourceKind.Collector, diagnostics.Kind);
        Assert.Equal("127.0.0.1", owner.Launch?.Env[QylConstants.Env.QylBindAddress]);
        Assert.Equal("127.0.0.1", diagnostics.Launch?.Env[QylConstants.Env.QylBindAddress]);
        Assert.Equal(string.Empty, diagnostics.Launch?.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
        Assert.Equal("Unsecured", diagnostics.Launch?.Env[QylConstants.Env.QylOtlpAuthMode]);
        Assert.Equal("qyl.diagnostics.duckdb", diagnostics.Launch?.Env[QylConstants.Env.QylDataPath]);
        Assert.Equal([diagnostics.Name], owner.WaitsFor);
        Assert.Equal(
            $"http://127.0.0.1:{diagnostics.OtlpHttpPort}",
            owner.Launch?.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
        Assert.Equal("http/protobuf", owner.Launch?.Env[QylConstants.Env.OtelExporterOtlpProtocol]);
    }

    [Fact]
    public void Composition_rejects_invalid_and_cross_kind_port_collisions()
    {
        var invalid = QylAppBuilder.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invalid.AddCommand("command", "dotnet", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invalid.AddCollector("collector", "/work/qyl.collector.csproj", 0));

        var builder = QylAppBuilder.Create();
        builder.AddCollector("collector", "/work/qyl.collector.csproj", 55100);
        var otlpPort = Assert.Single(builder.Resources).OtlpHttpPort;

        var error = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCommand("overlap", "dotnet", otlpPort));
        Assert.Contains("every api/otlp/grpc port must be unique", error.Message, StringComparison.Ordinal);
    }

    private sealed class AlwaysReadyProbe : IReadinessProbe
    {
        public Task<bool> IsReadyAsync(QylResourceState state, CancellationToken cancellationToken)
        {
            Assert.Null(state.AllocatedPort);
            Assert.Null(state.Endpoint);
            return Task.FromResult(true);
        }
    }
}

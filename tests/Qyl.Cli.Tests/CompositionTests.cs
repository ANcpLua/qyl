using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[TestClass]
public sealed class CompositionTests(TestContext testContext)
{

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task Command_arguments_reach_a_real_process_without_shell_parsing()
    {
        if (OperatingSystem.IsWindows()) return;

        const string Executable = "/bin/echo";
        Assert.IsTrue(File.Exists(Executable), $"Expected the platform executable {Executable}.");

        var resource = new QylResource
        {
            Name = "echo",
            Kind = QylResourceKind.Command,
            Port = 54321,
            Launch = new QylLaunchSpec
            {
                Executable = Executable,
                Args = new System.Collections.ObjectModel.ReadOnlyCollection<string>(["hello world"])
            }
        };

        var logs = new QylLogStore();
        using var output = logs.Subscribe(resource.Name);
        var launcher = new QylProcessLauncher(logs, NullLogger<QylProcessLauncher>.Instance);
        using var process = launcher.Launch(resource, new Uri("http://127.0.0.1:54321"));
        try
        {
            await process.WaitForExitAsync(testContext.CancellationToken);
            var line = await output.Events.ReadAsync(testContext.CancellationToken);

            Assert.ContainsSingle(logs.Snapshot(resource.Name));
            Assert.AreEqual("hello world", line.Line);
            Assert.AreEqual(QylLogStream.Stdout, line.Stream);
            Assert.AreEqual(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task A_failed_dependency_prevents_launch_without_waiting_for_a_clock_tick()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"qyl-missing-{Guid.NewGuid():N}");
        var dependency = new QylResource { Name = "dependency", Kind = QylResourceKind.Command, Port = 0,
            Launch = new QylLaunchSpec { Executable = missing } };
        // Put the dependent first so it is already waiting when its prerequisite fails.
        var dependent = new QylResource { Name = "dependent", Kind = QylResourceKind.Command, Port = 0,
            WaitsFor = [dependency.Name], Launch = new QylLaunchSpec { Executable = missing } };
        QylResource[] resources = [dependent, dependency];
        var clock = new FakeTimeProvider();
        var registry = new QylResourceRegistry(resources, clock);
        using var changes = registry.Subscribe();
        var logs = new QylLogStore();
        await using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        using var orchestrator = new QylOrchestrator(resources, registry, new OrchestratorOptions(),
            services.GetRequiredService<IHttpClientFactory>(),
            new QylProcessLauncher(logs, NullLogger<QylProcessLauncher>.Instance), new QylResourceActions(),
            clock, NullLogger<QylOrchestrator>.Instance);
        try
        {
            await orchestrator.StartAsync(testContext.CancellationToken);
            while (registry.Snapshot.GetValueOrDefault(dependent.Name)?.Lifecycle != ResourceLifecycle.Failed)
                await changes.Events.ReadAsync(testContext.CancellationToken);

            Assert.AreEqual(ResourceLifecycle.Failed, registry.Snapshot[dependency.Name].Lifecycle);
            Assert.AreEqual("Dependency 'dependency' reached state Failed before becoming ready",
                registry.Snapshot[dependent.Name].LastError);
            Assert.IsEmpty(logs.Snapshot(dependent.Name));
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await orchestrator.StopAsync(cleanup.Token);
            if (orchestrator.ExecuteTask is { } execution) await execution.WaitAsync(cleanup.Token);
        }
    }

    [TestMethod]
    public void Runner_self_telemetry_targets_the_dedicated_diagnostics_collector_only()
    {
        var builder = QylAppBuilder.Create();
        Assert.IsNull(builder.SelfTelemetryEndpoint);

        builder.AddCollector(
            "collector",
            "dotnet",
            ["/work/qyl.collector.dll"],
            port: 55110,
            selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", 55111));

        var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");
        Assert.AreEqual(new Uri($"http://127.0.0.1:{diagnostics.OtlpHttpPort}"), builder.SelfTelemetryEndpoint);
        // The same dead end the collector exports to: its own exporter is blanked, so no loop can form.
        Assert.AreEqual(string.Empty, diagnostics.Launch.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
    }

    [TestMethod]
    public async Task Runner_host_builds_with_its_telemetry_pipeline_and_without_a_sink_exports_nothing()
    {
        var builder = QylAppBuilder.Create();
        builder.AddCollector("collector", "dotnet", ["/work/qyl.collector.dll"], port: 55120);
        Assert.IsNull(builder.SelfTelemetryEndpoint);

        await using var app = builder.Build();
        Assert.IsNotNull(app);
    }

    [TestMethod]
    public void Collector_composition_pins_every_unauthenticated_listener_to_loopback()
    {
        var builder = QylAppBuilder.Create();
        builder.AddCollector(
            "collector",
            "dotnet",
            ["/work/qyl.collector.dll"],
            port: 55100,
            selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", 55101));

        var owner = builder.Resources.Single(static resource => resource.Name == "collector");
        var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");

        Assert.AreEqual(QylResourceKind.Collector, owner.Kind);
        Assert.AreEqual(QylResourceKind.Collector, diagnostics.Kind);
        Assert.AreEqual("127.0.0.1", owner.Launch.Env[QylConstants.Env.QylBindAddress]);
        Assert.AreEqual("127.0.0.1", diagnostics.Launch.Env[QylConstants.Env.QylBindAddress]);
        Assert.AreEqual(string.Empty, diagnostics.Launch.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
        Assert.AreEqual("Unsecured", diagnostics.Launch.Env[QylConstants.Env.QylOtlpAuthMode]);
        Assert.AreEqual(
            Path.Combine(QylConstants.Collector.DefaultDataHome, "qyl.diagnostics.duckdb"),
            diagnostics.Launch.Env[QylConstants.Env.QylDataPath]);
        Assert.AreEqual(
            Path.Combine(QylConstants.Collector.DefaultDataHome, "qyl.collector.duckdb"),
            owner.Launch.Env[QylConstants.Env.QylDataPath]);
        Assert.AreSequenceEqual([diagnostics.Name], owner.WaitsFor);
        Assert.AreEqual(
            $"http://127.0.0.1:{diagnostics.OtlpHttpPort}",
            owner.Launch.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
        Assert.AreEqual("http/protobuf", owner.Launch.Env[QylConstants.Env.OtelExporterOtlpProtocol]);
    }

    [TestMethod]
    public void Composition_rejects_invalid_and_cross_kind_port_collisions()
    {
        var invalid = QylAppBuilder.Create();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            invalid.AddCollector("collector", "dotnet", ["/work/qyl.collector.dll"], port: 0));

        var builder = QylAppBuilder.Create();
        builder.AddCollector("collector", "dotnet", ["/work/qyl.collector.dll"], port: 55100);
        var otlpPort = Assert.ContainsSingle(builder.Resources).OtlpHttpPort;

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.AddCollector("overlap", "dotnet", ["/work/qyl.collector.dll"], port: otlpPort));
        Assert.Contains("every api/otlp/grpc port must be unique", error.Message, StringComparison.Ordinal);
    }

}

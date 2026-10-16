using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[TestClass]
public sealed class CompositionTests(TestContext testContext)
{

    [TestMethod]
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
        var launcher = new QylProcessLauncher(logs, NullLogger<QylProcessLauncher>.Instance);
        using var process = launcher.Launch(resource, new Uri("http://127.0.0.1:54321"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        await process.WaitForExitAsync(timeout.Token);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (logs.Snapshot(resource.Name).Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, timeout.Token);
        }

        var line = Assert.ContainsSingle(logs.Snapshot(resource.Name));
        Assert.AreEqual("hello world", line.Line);
        Assert.AreEqual(QylLogStream.Stdout, line.Stream);
        Assert.AreEqual(0, process.ExitCode);
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

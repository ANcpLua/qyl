using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Host.Internal;

namespace Qyl.Host.Tests;

public sealed class CompositionTests
{
    [Fact]
    public async Task Command_arguments_reach_a_real_process_without_shell_parsing()
    {
        if (OperatingSystem.IsWindows()) return;

        const string executable = "/bin/echo";
        Assert.True(File.Exists(executable), $"Expected the platform executable {executable}.");

        var builder = QylAppBuilder.Create();
        builder.AddCommand("echo", executable, 54321, ["hello world"]);

        var resource = Assert.Single(builder.Resources);
        Assert.Equal(executable, resource.Launch.Executable);
        Assert.Equal(["hello world"], resource.Launch.Args);

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
        Assert.Equal("127.0.0.1", owner.Launch.Env[QylConstants.Env.QylBindAddress]);
        Assert.Equal("127.0.0.1", diagnostics.Launch.Env[QylConstants.Env.QylBindAddress]);
        Assert.Equal(string.Empty, diagnostics.Launch.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
        Assert.Equal("Unsecured", diagnostics.Launch.Env[QylConstants.Env.QylOtlpAuthMode]);
        Assert.Equal(
            Path.Combine(QylConstants.Collector.DefaultDataHome, "qyl.diagnostics.duckdb"),
            diagnostics.Launch.Env[QylConstants.Env.QylDataPath]);
        Assert.Equal(
            Path.Combine(QylConstants.Collector.DefaultDataHome, "qyl.collector.duckdb"),
            owner.Launch.Env[QylConstants.Env.QylDataPath]);
        Assert.Equal([diagnostics.Name], owner.WaitsFor);
        Assert.Equal(
            $"http://127.0.0.1:{diagnostics.OtlpHttpPort}",
            owner.Launch.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
        Assert.Equal("http/protobuf", owner.Launch.Env[QylConstants.Env.OtelExporterOtlpProtocol]);
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

}

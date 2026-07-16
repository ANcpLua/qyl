using System.Net;
using System.Net.Sockets;
using Qyl.Run.Host;

namespace Qyl.Host.Tests;

[Collection(ProcessEnvironmentTestGroup.Name)]
public sealed class QylCliTests
{
    [Theory]
    [InlineData()]
    [InlineData("--help")]
    [InlineData("help")]
    [InlineData("help", "up")]
    [InlineData("up", "--help")]
    public void Help_forms_parse_without_starting_the_product(params string[] args)
    {
        Assert.Equal(QylCliAction.Help, QylCli.Parse(args).Action);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Version_forms_parse_without_starting_the_product(params string[] args)
    {
        Assert.Equal(QylCliAction.Version, QylCli.Parse(args).Action);
    }

    [Fact]
    public void Up_is_the_only_launch_command()
    {
        Assert.Equal(QylCliAction.Up, QylCli.Parse(["up"]).Action);

        var oldFlag = QylCli.Parse(["up", "--dev"]);
        Assert.Equal(QylCliAction.Invalid, oldFlag.Action);
        Assert.Contains("Unknown qyl command: up --dev", oldFlag.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Packaged_collector_path_is_resolved_from_the_tool_installation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qyl-cli-{Guid.NewGuid():N}");
        var collectorDirectory = Path.Combine(root, "collector");
        Directory.CreateDirectory(collectorDirectory);
        var expected = Path.Combine(collectorDirectory, "qyl.collector.dll");
        File.WriteAllBytes(expected, []);

        try
        {
            Assert.Equal(Path.GetFullPath(expected), QylCli.ResolveCollectorAssembly(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Missing_packaged_collector_fails_with_a_reinstall_instruction()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qyl-cli-missing-{Guid.NewGuid():N}");
        var error = Assert.Throws<FileNotFoundException>(() => QylCli.ResolveCollectorAssembly(root));

        Assert.Contains("installation is incomplete", error.Message, StringComparison.Ordinal);
        Assert.Contains("Reinstall the qyl dotnet tool", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Port_preflight_reports_the_exact_collision()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var port = ((IPEndPoint)occupied.LocalEndpoint).Port;

        Assert.False(QylCli.TryFindUnavailablePort([port], out var unavailable));
        Assert.Equal(port, unavailable);
    }

    [Fact]
    public void Up_composes_both_collectors_from_the_same_packaged_runtime()
    {
        var collectorAssembly = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(), "installed", "qyl", "collector", "qyl.collector.dll"));
        var builder = QylCli.CreateApp(collectorAssembly);

        var collector = builder.Resources.Single(static resource => resource.Name == "collector");
        var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");

        Assert.Equal(QylResourceKind.Collector, collector.Kind);
        Assert.Equal(5100, collector.Port);
        Assert.Equal("dotnet", collector.Launch?.Executable);
        Assert.Equal([collectorAssembly], collector.Launch?.Args);
        Assert.Equal([diagnostics.Name], collector.WaitsFor);

        Assert.Equal(QylResourceKind.Collector, diagnostics.Kind);
        Assert.Equal(5200, diagnostics.Port);
        Assert.Equal("dotnet", diagnostics.Launch?.Executable);
        Assert.Equal([collectorAssembly], diagnostics.Launch?.Args);
        Assert.Equal(string.Empty, diagnostics.Launch?.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
    }

    [Fact]
    public void Up_pins_its_zero_configuration_contract_over_ambient_settings()
    {
        const string runnerPortEnvironment = "Qyl__Host__RunnerPort";
        var originalRunnerPort = Environment.GetEnvironmentVariable(runnerPortEnvironment);
        var originalAuthMode = Environment.GetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode);
        try
        {
            Environment.SetEnvironmentVariable(runnerPortEnvironment, "19999");
            Environment.SetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode, "ApiKey");

            var builder = QylCli.CreateApp(Path.Combine(Path.GetTempPath(), "qyl.collector.dll"));
            var options = QylAppOptions.FromConfiguration(builder.Host.Configuration);
            var collector = builder.Resources.Single(static resource => resource.Name == "collector");
            var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");

            Assert.Equal(QylConstants.Ports.RunnerApi, options.RunnerPort);
            Assert.Equal(QylConstants.Collector.UnsecuredAuthMode,
                collector.Launch?.Env[QylConstants.Env.QylOtlpAuthMode]);
            Assert.Equal(QylConstants.Collector.UnsecuredAuthMode,
                diagnostics.Launch?.Env[QylConstants.Env.QylOtlpAuthMode]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(runnerPortEnvironment, originalRunnerPort);
            Environment.SetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode, originalAuthMode);
        }
    }
}

using System.Net;
using System.Net.Sockets;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[DoNotParallelize]
[TestClass]
public sealed class QylCliTests
{
    [TestMethod]
    [DataRow]
    [DataRow("--help")]
    [DataRow("help")]
    [DataRow("help", "up")]
    [DataRow("up", "--help")]
    public void Help_forms_parse_without_starting_the_product(params string[] args)
    {
        Assert.AreEqual(QylCliAction.Help, QylCli.Parse(args).Action);
    }

    [TestMethod]
    [DataRow("--version")]
    [DataRow("-v")]
    public void Version_forms_parse_without_starting_the_product(params string[] args)
    {
        Assert.AreEqual(QylCliAction.Version, QylCli.Parse(args).Action);
    }

    [TestMethod]
    public void Up_is_the_only_launch_command()
    {
        var bare = QylCli.Parse(["up"]);
        Assert.AreEqual(QylCliAction.Up, bare.Action);

        var oldFlag = QylCli.Parse(["up", "--dev"]);
        Assert.AreEqual(QylCliAction.Invalid, oldFlag.Action);
        Assert.IsNotNull(oldFlag.Error);
        Assert.Contains("Unknown qyl command: up --dev", oldFlag.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("--mcp-stdio", "node", "server.js")]
    [DataRow("--mcp-http", "http://127.0.0.1:3001/mcp")]
    public void Removed_mcp_attachment_flags_are_rejected(params string[] flags)
    {
        var invocation = QylCli.Parse(["up", .. flags]);

        Assert.AreEqual(QylCliAction.Invalid, invocation.Action);
        Assert.IsNotNull(invocation.Error);
        Assert.Contains("Unknown qyl command", invocation.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Up_keeps_the_two_collector_composition()
    {
        var builder = QylCli.CreateApp(Path.Combine(Path.GetTempPath(), "qyl.collector.dll"));

        Assert.AreSequenceEqual(["collector", "diagnostics"],
            builder.Resources.Select(static resource => resource.Name).Order(StringComparer.Ordinal));
    }

    [TestMethod]
    public void Packaged_collector_path_is_resolved_from_the_tool_installation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qyl-cli-{Guid.NewGuid():N}");
        var collectorDirectory = Path.Combine(root, "collector");
        Directory.CreateDirectory(collectorDirectory);
        var expected = Path.Combine(collectorDirectory, "qyl.collector.dll");
        File.WriteAllBytes(expected, []);

        try
        {
            Assert.AreEqual(Path.GetFullPath(expected), QylCli.ResolveCollectorAssembly(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Missing_packaged_collector_fails_with_a_reinstall_instruction()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qyl-cli-missing-{Guid.NewGuid():N}");
        var error = Assert.ThrowsExactly<FileNotFoundException>(() => QylCli.ResolveCollectorAssembly(root));

        Assert.Contains("installation is incomplete", error.Message, StringComparison.Ordinal);
        Assert.Contains("Reinstall the qyl dotnet tool", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Port_preflight_reports_the_exact_collision()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var port = ((IPEndPoint)occupied.LocalEndpoint).Port;

        Assert.IsFalse(QylCli.TryValidatePortsAvailable([port], out var unavailable));
        Assert.AreEqual(port, unavailable);
    }

    [TestMethod]
    public void Up_composes_both_collectors_from_the_same_packaged_runtime()
    {
        var collectorAssembly = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(), "installed", "qyl", "collector", "qyl.collector.dll"));
        var builder = QylCli.CreateApp(collectorAssembly);

        var collector = builder.Resources.Single(static resource => resource.Name == "collector");
        var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");

        Assert.AreEqual(QylResourceKind.Collector, collector.Kind);
        Assert.AreEqual(5100, collector.Port);
        Assert.AreEqual("dotnet", collector.Launch.Executable);
        Assert.AreSequenceEqual([collectorAssembly], collector.Launch.Args);
        Assert.AreSequenceEqual([diagnostics.Name], collector.WaitsFor);

        Assert.AreEqual(QylResourceKind.Collector, diagnostics.Kind);
        Assert.AreEqual(5200, diagnostics.Port);
        Assert.AreEqual("dotnet", diagnostics.Launch.Executable);
        Assert.AreSequenceEqual([collectorAssembly], diagnostics.Launch.Args);
        Assert.AreEqual(string.Empty, diagnostics.Launch.Env[QylConstants.Env.OtelExporterOtlpEndpoint]);
    }

    [TestMethod]
    public void Up_pins_its_zero_configuration_contract()
    {
        var originalAuthMode = Environment.GetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode);
        try
        {
            Environment.SetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode, "ApiKey");

            var builder = QylCli.CreateApp(Path.Combine(Path.GetTempPath(), "qyl.collector.dll"));
            var collector = builder.Resources.Single(static resource => resource.Name == "collector");
            var diagnostics = builder.Resources.Single(static resource => resource.Name == "diagnostics");

            Assert.AreEqual(QylConstants.Collector.UnsecuredAuthMode,
                collector.Launch.Env[QylConstants.Env.QylOtlpAuthMode]);
            Assert.AreEqual(QylConstants.Collector.UnsecuredAuthMode,
                diagnostics.Launch.Env[QylConstants.Env.QylOtlpAuthMode]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(QylConstants.Env.QylOtlpAuthMode, originalAuthMode);
        }
    }
}

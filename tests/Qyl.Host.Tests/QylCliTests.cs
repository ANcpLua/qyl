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
        var bare = QylCli.Parse(["up"]);
        Assert.Equal(QylCliAction.Up, bare.Action);
        Assert.Null(bare.Mcp);

        var oldFlag = QylCli.Parse(["up", "--dev"]);
        Assert.Equal(QylCliAction.Invalid, oldFlag.Action);
        Assert.Contains("Unknown qyl command: up --dev", oldFlag.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Mcp_stdio_consumes_the_rest_of_the_command_line_as_the_child_command()
    {
        var invocation = QylCli.Parse(["up", "--mcp-stdio", "node", "dist/main.js", "--stdio"]);

        Assert.Equal(QylCliAction.Up, invocation.Action);
        Assert.Equal("node", invocation.Mcp?.Command);
        Assert.Equal(["dist/main.js", "--stdio"], invocation.Mcp?.Arguments);
        Assert.Null(invocation.Mcp?.Endpoint);
    }

    [Fact]
    public void Mcp_stdio_without_a_command_explains_the_expected_shape()
    {
        var invocation = QylCli.Parse(["up", "--mcp-stdio"]);

        Assert.Equal(QylCliAction.Invalid, invocation.Action);
        Assert.Contains("--mcp-stdio requires the MCP server command", invocation.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://127.0.0.1:3001/mcp")]
    [InlineData("https://mcp.qyl.at/mcp")]
    public void Mcp_http_accepts_one_absolute_http_url(string url)
    {
        var invocation = QylCli.Parse(["up", "--mcp-http", url]);

        Assert.Equal(QylCliAction.Up, invocation.Action);
        Assert.Equal(new Uri(url), invocation.Mcp?.Endpoint);
        Assert.Null(invocation.Mcp?.Command);
    }

    [Theory]
    [InlineData("up", "--mcp-http")]
    [InlineData("up", "--mcp-http", "not-a-url")]
    [InlineData("up", "--mcp-http", "ftp://host/mcp")]
    [InlineData("up", "--mcp-http", "http://a/mcp", "http://b/mcp")]
    public void Mcp_http_rejects_missing_relative_non_http_and_multiple_urls(params string[] args)
    {
        var invocation = QylCli.Parse(args);

        Assert.Equal(QylCliAction.Invalid, invocation.Action);
        Assert.Contains("--mcp-http requires exactly one absolute http(s) URL", invocation.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_documents_both_mcp_attachment_forms()
    {
        Assert.Contains("--mcp-stdio", QylCli.HelpText, StringComparison.Ordinal);
        Assert.Contains("--mcp-http", QylCli.HelpText, StringComparison.Ordinal);
    }

    [Fact]
    public void Mcp_child_environment_targets_the_fixed_local_stack()
    {
        var environment = QylCli.BuildMcpChildEnvironment();

        // Literal names: this is qyl.mcp's env contract, so a renamed constant must fail here.
        Assert.Equal("http://127.0.0.1:5100", environment["QYL_COLLECTOR_URL"]);
        Assert.Equal("http://127.0.0.1:4318", environment["QYL_OTLP_ENDPOINT"]);
        Assert.Equal(2, environment.Count);
    }

    [Fact]
    public void Up_with_mcp_stdio_composes_a_probed_launchless_mcp_resource()
    {
        var builder = QylCli.CreateApp(
            Path.Combine(Path.GetTempPath(), "qyl.collector.dll"),
            QylMcpAttachment.Stdio("node", ["dist/main.js", "--stdio"]));

        var mcp = builder.Resources.Single(static resource => resource.Name == QylCli.McpResourceName);

        Assert.Equal(QylResourceKind.McpStdio, mcp.Kind);
        Assert.Null(mcp.Launch);
        Assert.NotNull(mcp.ReadinessProbe);
        Assert.Equal(["collector"], mcp.WaitsFor);
    }

    [Fact]
    public void Up_with_mcp_http_composes_a_probed_connection_resource()
    {
        var builder = QylCli.CreateApp(
            Path.Combine(Path.GetTempPath(), "qyl.collector.dll"),
            QylMcpAttachment.Http(new Uri("http://127.0.0.1:3001/mcp")));

        var mcp = builder.Resources.Single(static resource => resource.Name == QylCli.McpResourceName);

        Assert.Equal(QylResourceKind.McpHttp, mcp.Kind);
        Assert.Null(mcp.Launch);
        Assert.NotNull(mcp.ReadinessProbe);
        Assert.Equal(["collector"], mcp.WaitsFor);
    }

    [Fact]
    public void Up_without_mcp_keeps_the_two_collector_composition()
    {
        var builder = QylCli.CreateApp(Path.Combine(Path.GetTempPath(), "qyl.collector.dll"));

        Assert.DoesNotContain(builder.Resources, static resource => resource.Name == QylCli.McpResourceName);
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

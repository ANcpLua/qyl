using System.Net;
using System.Net.Sockets;
using Qyl.Host;
using Qyl.Host.Mcp;

namespace Qyl.Run.Host;

internal static class QylCli
{
    private const string CollectorDirectoryName = "collector";
    private const string CollectorAssemblyName = "qyl.collector.dll";
    private const int ProductPort = 5100;
    private const int DiagnosticsPort = 5200;
    internal const string McpResourceName = "mcp";
    private static readonly int[] s_requiredPorts =
    [
        ProductPort, DiagnosticsPort, QylConstants.Collector.DefaultGrpcPort,
        QylConstants.Collector.DefaultOtlpHttpPort, QylConstants.Ports.RunnerApi
    ];

    internal const string HelpText = """
        qyl - local OpenTelemetry investigation stack

        Usage:
          qyl up        Start the collector, embedded dashboard, and diagnostics collector.
                        Telemetry is stored under ~/.qyl/, never in the working directory.
          qyl up --mcp-stdio <command> [args...]
                        Also launch an MCP server as a supervised child over stdio and
                        project it on the runner API: GET /runner/mcp/mcp/tools,
                        POST /runner/mcp/mcp/tools/call, POST /runner/mcp/mcp/resources/read.
                        The child inherits QYL_COLLECTOR_URL and QYL_OTLP_ENDPOINT
                        pointed at this stack.
          qyl up --mcp-http <url>
                        Also attach to an already-running MCP server over Streamable HTTP
          qyl --version Show the installed qyl version
          qyl --help    Show this help
        """;

    internal static async Task<int> RunAsync(string[] args)
    {
        var invocation = Parse(args);
        switch (invocation.Action)
        {
            case QylCliAction.Help:
                Console.Out.WriteLine(HelpText);
                return 0;
            case QylCliAction.Version:
                Console.Out.WriteLine(GetVersion());
                return 0;
            case QylCliAction.Invalid:
                Console.Error.WriteLine(invocation.Error);
                Console.Error.WriteLine();
                Console.Error.WriteLine(HelpText);
                return 2;
            case QylCliAction.Up:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invocation), invocation.Action, "Unknown CLI action.");
        }

        string collectorAssembly;
        try
        {
            collectorAssembly = ResolveCollectorAssembly(AppContext.BaseDirectory);
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }

        if (!TryFindUnavailablePort(s_requiredPorts, out var unavailablePort))
        {
            Console.Error.WriteLine(
                $"Cannot start qyl: 127.0.0.1:{unavailablePort} is already in use. Stop the process using that port and run `qyl up` again.");
            return 1;
        }

        await CreateApp(collectorAssembly, invocation.Mcp).Build().RunAsync().ConfigureAwait(false);
        return 0;
    }

    internal static QylCliInvocation Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args is ["--help"] or ["-h"] or ["help"] or ["help", "up"] or ["up", "--help"] or ["up", "-h"])
            return new QylCliInvocation(QylCliAction.Help);

        if (args is ["--version"] or ["-v"])
            return new QylCliInvocation(QylCliAction.Version);

        if (args is ["up"])
            return new QylCliInvocation(QylCliAction.Up);

        // Slice patterns below need a sliceable receiver, which IReadOnlyList<string> is not.
        var argv = args as string[] ?? [.. args];

        // --mcp-stdio consumes the remainder of the command line as the child command,
        // so it admits arbitrary server arguments without an escaping convention.
        if (argv is ["up", "--mcp-stdio", .. var stdio])
        {
            return stdio is [var command, .. var commandArguments]
                ? new QylCliInvocation(QylCliAction.Up, Mcp: QylMcpAttachment.Stdio(command, [.. commandArguments]))
                : new QylCliInvocation(
                    QylCliAction.Invalid,
                    "--mcp-stdio requires the MCP server command to launch, e.g. `qyl up --mcp-stdio node server.js --stdio`.");
        }

        if (argv is ["up", "--mcp-http", .. var http])
        {
            return http is [var url]
                   && Uri.TryCreate(url, UriKind.Absolute, out var endpoint)
                   && (string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                ? new QylCliInvocation(QylCliAction.Up, Mcp: QylMcpAttachment.Http(endpoint))
                : new QylCliInvocation(
                    QylCliAction.Invalid,
                    "--mcp-http requires exactly one absolute http(s) URL of a running MCP server.");
        }

        return new QylCliInvocation(
            QylCliAction.Invalid,
            $"Unknown qyl command: {string.Join(' ', args.Select(QuoteIfNeeded))}");
    }

    internal static string ResolveCollectorAssembly(string baseDirectory)
    {
        var path = Path.GetFullPath(Path.Combine(baseDirectory, CollectorDirectoryName, CollectorAssemblyName));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The qyl installation is incomplete: packaged collector '{path}' was not found. Reinstall the qyl dotnet tool.",
                path);
        }

        return path;
    }

    internal static bool TryFindUnavailablePort(IEnumerable<int> ports, out int unavailablePort)
    {
        var listeners = new List<TcpListener>();
        try
        {
            foreach (var port in ports)
            {
                TcpListener? listener = null;
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listeners.Add(listener);
                    listener = null;
                }
                catch (SocketException)
                {
                    unavailablePort = port;
                    return false;
                }
                finally
                {
                    listener?.Dispose();
                }
            }

            unavailablePort = 0;
            return true;
        }
        finally
        {
            foreach (var listener in listeners)
                listener.Stop();
        }
    }

    internal static QylAppBuilder CreateApp(string collectorAssembly, QylMcpAttachment? mcp = null)
    {
        var app = QylAppBuilder.Create();
        // qyl up is a fixed zero-configuration product command. Library callers may move the
        // runner API, but this CLI must keep preflight, documentation, and the actual listener on
        // one port even when an ambient appsettings/environment provider says otherwise.
        app.Host.Configuration[$"{QylAppOptions.SectionName}:{nameof(QylAppOptions.RunnerPort)}"] =
            QylConstants.Ports.RunnerApi.ToString(CultureInfo.InvariantCulture);

        var collector = app
            .AddCollector("collector", QylConstants.Orchestrator.DotnetExecutable, [collectorAssembly], port: ProductPort,
                selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", port: DiagnosticsPort))
            // The packaged command is intentionally a loopback-only local product. Do not inherit
            // a deployment's ApiKey mode and turn the advertised one-command launch into a missing-
            // key startup failure. QylAppBuilder itself continues to honor ambient operator policy.
            .WithEnvironment(QylConstants.Env.QylOtlpAuthMode, QylConstants.Collector.UnsecuredAuthMode);

        // The attachment exists to serve this stack's telemetry, so its handshake (and for
        // stdio, the child spawn) waits for the collector: a tool call in the collector's
        // startup window would answer "collector unreachable" for a stack that is coming up.
        if (mcp?.Endpoint is { } endpoint)
        {
            app.AddMcpHttp(McpResourceName, endpoint).WaitFor(collector);
        }
        else if (mcp?.Command is { } command)
        {
            app.AddMcpStdio(McpResourceName, command, mcp.Arguments,
                environment: BuildMcpChildEnvironment()).WaitFor(collector);
        }

        return app;
    }

    /// <summary>
    /// The fixed local stack a stdio MCP child should target: telemetry reads on the
    /// product port and OTLP export on the collector's OTLP HTTP port. Without the
    /// explicit OTLP entry, qyl-aware servers fall back to the read-API base for export.
    /// </summary>
    internal static Dictionary<string, string?> BuildMcpChildEnvironment() => new(StringComparer.Ordinal)
    {
        [QylConstants.Env.QylCollectorUrl] = string.Format(
            CultureInfo.InvariantCulture, QylConstants.Network.LocalhostUrlTemplate,
            QylConstants.Network.Loopback, ProductPort),
        [QylConstants.Env.QylOtlpEndpoint] = string.Format(
            CultureInfo.InvariantCulture, QylConstants.Network.LocalhostUrlTemplate,
            QylConstants.Network.Loopback, QylConstants.Collector.DefaultOtlpHttpPort)
    };

    internal static string GetVersion() => BuildVersion.ProductVersion;

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? $"\"{value}\"" : value;
}

internal readonly record struct QylCliInvocation(
    QylCliAction Action,
    string? Error = null,
    QylMcpAttachment? Mcp = null);

/// <summary>An optional MCP server attachment for <c>qyl up</c>: launched over stdio or joined over HTTP.</summary>
internal sealed record QylMcpAttachment
{
    public string? Command { get; private init; }
    public IReadOnlyList<string> Arguments { get; private init; } = [];
    public Uri? Endpoint { get; private init; }

    public static QylMcpAttachment Stdio(string command, IReadOnlyList<string> arguments) =>
        new() { Command = command, Arguments = arguments };

    public static QylMcpAttachment Http(Uri endpoint) => new() { Endpoint = endpoint };
}

internal enum QylCliAction
{
    Help,
    Version,
    Up,
    Invalid
}

using System.Net;
using System.Net.Sockets;
using Qyl.Host;

namespace Qyl.Run.Host;

internal static class QylCli
{
    private const string CollectorDirectoryName = "collector";
    private const string CollectorAssemblyName = "qyl.collector.dll";
    private const int ProductPort = 5100;
    private const int DiagnosticsPort = 5200;
    private static readonly int[] s_requiredPorts =
    [
        ProductPort, DiagnosticsPort, QylConstants.Collector.DefaultGrpcPort,
        QylConstants.Collector.DefaultOtlpHttpPort, QylConstants.Ports.RunnerApi
    ];

    internal const string HelpText = """
        qyl - local OpenTelemetry investigation stack

        Usage:
          qyl up        Start the collector, embedded dashboard, and diagnostics collector
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

        await CreateApp(collectorAssembly).Build().RunAsync().ConfigureAwait(false);
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

    internal static QylAppBuilder CreateApp(string collectorAssembly)
    {
        var app = QylAppBuilder.Create();
        // qyl up is a fixed zero-configuration product command. Library callers may move the
        // runner API, but this CLI must keep preflight, documentation, and the actual listener on
        // one port even when an ambient appsettings/environment provider says otherwise.
        app.Host.Configuration[$"{QylAppOptions.SectionName}:{nameof(QylAppOptions.RunnerPort)}"] =
            QylConstants.Ports.RunnerApi.ToString(CultureInfo.InvariantCulture);

        app.AddCollector("collector", QylConstants.Orchestrator.DotnetExecutable, [collectorAssembly], port: ProductPort,
                selfTelemetry: static telemetry => telemetry.ExportToDedicatedCollector("diagnostics", port: DiagnosticsPort))
            // The packaged command is intentionally a loopback-only local product. Do not inherit
            // a deployment's ApiKey mode and turn the advertised one-command launch into a missing-
            // key startup failure. QylAppBuilder itself continues to honor ambient operator policy.
            .WithEnvironment(QylConstants.Env.QylOtlpAuthMode, QylConstants.Collector.UnsecuredAuthMode);
        return app;
    }

    internal static string GetVersion() => BuildVersion.ProductVersion;

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? $"\"{value}\"" : value;
}

internal readonly record struct QylCliInvocation(QylCliAction Action, string? Error = null);

internal enum QylCliAction
{
    Help,
    Version,
    Up,
    Invalid
}

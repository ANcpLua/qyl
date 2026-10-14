using System.Net;
using System.Net.Sockets;
using Qyl.Cli.Runtime;

namespace Qyl.Cli;

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
          qyl up        Start the collector, embedded dashboard, and diagnostics collector.
                        Telemetry is stored under ~/.qyl/, never in the working directory.
          qyl --version Show the installed qyl version
          qyl --help    Show this help
        """;

    internal static async Task<int> RunAsync(string[] args)
    {
        var invocation = Parse(args);
        switch (invocation.Action)
        {
            case QylCliAction.Help:
                await Console.Out.WriteLineAsync(HelpText).ConfigureAwait(false);
                return 0;
            case QylCliAction.Version:
                await Console.Out.WriteLineAsync(GetVersion()).ConfigureAwait(false);
                return 0;
            case QylCliAction.Invalid:
                await Console.Error.WriteLineAsync(invocation.Error).ConfigureAwait(false);
                await Console.Error.WriteLineAsync().ConfigureAwait(false);
                await Console.Error.WriteLineAsync(HelpText).ConfigureAwait(false);
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
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }

        if (!TryValidatePortsAvailable(s_requiredPorts, out var unavailablePort))
        {
            await Console.Error.WriteLineAsync(
                $"Cannot start qyl: 127.0.0.1:{unavailablePort} is unavailable. Free the port or grant permission to bind it, then run `qyl up` again.")
                .ConfigureAwait(false);
            return 1;
        }

        await CreateApp(collectorAssembly).Build().RunAsync().ConfigureAwait(false);
        return 0;
    }

    internal static QylCliInvocation Parse(IReadOnlyList<string> args)
    {
        if (args.Count is 0 ||
            args is ["--help"] or ["-h"] or ["help"] or
            ["help", "up"] or ["up", "--help"] or ["up", "-h"])
        {
            return new QylCliInvocation(QylCliAction.Help);
        }

        return args switch
        {
            ["--version"] or ["-v"] => new QylCliInvocation(QylCliAction.Version),
            ["up"] => new QylCliInvocation(QylCliAction.Up),
            _ => new QylCliInvocation(QylCliAction.Invalid,
                $"Unknown qyl command: {string.Join(' ', args.Select(QuoteIfNeeded))}")
        };
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

    internal static bool TryValidatePortsAvailable(IEnumerable<int> ports, out int unavailablePort)
    {
        var listeners = new List<TcpListener>();
        try
        {
            foreach (var port in ports)
            {
                TcpListener? listener = null;
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port)
                    {
                        ExclusiveAddressUse = true
                    };
                    listener.Start();
                    listeners.Add(listener);
                    listener = null;
                }
                catch (SocketException exception) when (
                    exception.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
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
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}

internal readonly record struct QylCliInvocation(
    QylCliAction Action,
    string? Error = null);

internal enum QylCliAction
{
    Help,
    Version,
    Up,
    Invalid
}

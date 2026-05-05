using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Qyl.Instrumentation.Discovery;

internal static partial class CollectorDiscovery
{
    private static readonly Lazy<Uri?> s_cachedEndpoint =
        new(ProbeForCollector, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly (string Host, int Port)[] s_probeTargets =
    [
        ("localhost", 4317),
        ("localhost", 4318),
        ("qyl", 4317),
        ("qyl", 4318)
    ];

    internal static Uri? DiscoverEndpoint() => s_cachedEndpoint.Value;

    private static Uri? ProbeForCollector()
    {
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var uri) ? uri : null;
        }

        var qylEndpoint = Environment.GetEnvironmentVariable("QYL_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(qylEndpoint))
        {
            return Uri.TryCreate(qylEndpoint, UriKind.Absolute, out var uri) ? uri : null;
        }

        foreach (var (host, port) in s_probeTargets)
        {
            if (TcpProbe(host, port))
            {
                return new Uri($"http://{host}:{port}");
            }
        }

        return null;
    }

    private static bool TcpProbe(string host, int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var result = socket.BeginConnect(host, port, null, null);
            var connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));

            if (connected && socket.Connected)
            {
                socket.EndConnect(result);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    internal static void LogResult(ILogger logger)
    {
        var endpoint = s_cachedEndpoint.Value;
        if (endpoint is not null)
        {
            LogCollectorDiscovered(logger, endpoint);
        }
        else
        {
            LogNoCollectorFound(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "qyl collector discovered at {Endpoint}")]
    private static partial void LogCollectorDiscovered(ILogger logger, Uri endpoint);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "No qyl collector found. Set OTEL_EXPORTER_OTLP_ENDPOINT or run qyl collector on localhost:4318 (HTTP) or localhost:4317 (gRPC)")]
    private static partial void LogNoCollectorFound(ILogger logger);
}

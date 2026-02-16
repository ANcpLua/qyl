using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Qyl.ServiceDefaults.Discovery;

/// <summary>
///     Discovers a qyl collector endpoint at startup using environment variables
///     and network probes. Results are cached for the lifetime of the process.
/// </summary>
internal static partial class CollectorDiscovery
{
    private static readonly Lazy<Uri?> SCachedEndpoint =
        new(ProbeForCollector, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly (string Host, int Port)[] SProbeTargets =
    [
        ("localhost", 4317),
        ("qyl", 4317)
    ];

    /// <summary>
    ///     Returns the discovered collector endpoint, or null if none was found.
    ///     The result is computed once and cached for the process lifetime.
    /// </summary>
    internal static Uri? DiscoverEndpoint() => SCachedEndpoint.Value;

    /// <summary>
    ///     Runs the full discovery sequence: env vars first, then network probes.
    /// </summary>
    private static Uri? ProbeForCollector()
    {
        // Priority 1: Explicit OTLP endpoint (standard OTel env var)
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var uri) ? uri : null;
        }

        // Priority 2: qyl-specific shorthand
        var qylEndpoint = Environment.GetEnvironmentVariable("QYL_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(qylEndpoint))
        {
            return Uri.TryCreate(qylEndpoint, UriKind.Absolute, out var uri) ? uri : null;
        }

        // Priority 3-4: Network probes (TCP socket connect, 100ms timeout each)
        foreach (var (host, port) in SProbeTargets)
        {
            if (TcpProbe(host, port))
            {
                return new Uri($"http://{host}:{port}");
            }
        }

        // Priority 5: Not found
        return null;
    }

    /// <summary>
    ///     Attempts a TCP connection to the specified host and port with a 100ms timeout.
    ///     Returns true if the port is accepting connections.
    /// </summary>
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

    /// <summary>
    ///     Logs the discovery result. Called once after discovery completes.
    /// </summary>
    internal static void LogResult(ILogger logger)
    {
        var endpoint = SCachedEndpoint.Value;
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
        Message = "No qyl collector found. Set OTEL_EXPORTER_OTLP_ENDPOINT or run qyl collector on localhost:4317")]
    private static partial void LogNoCollectorFound(ILogger logger);
}

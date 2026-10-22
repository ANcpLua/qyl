using System.Net;
using System.Net.Sockets;

namespace Qyl.Orchestration.Internal;

internal static class PortAllocator
{
    public static int ClaimFreePort(string host)
    {
        var ip = IPAddress.Parse(host);
        using var listener = new TcpListener(ip, OrchestrationDefaults.Ports.DynamicAllocation);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

// The application retains every reservation until its child is about to bind, or startup fails.
internal sealed class PortReservation : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    internal PortReservation()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    internal int Port { get; }

    public void Dispose() => _listener.Dispose();
}

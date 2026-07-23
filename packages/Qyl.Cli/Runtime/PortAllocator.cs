
using System.Net;
using System.Net.Sockets;

namespace Qyl.Cli.Runtime;

internal static class PortAllocator
{
    public static int ClaimFreePort(string host)
    {
        var ip = IPAddress.Parse(host);
        using var listener = new TcpListener(ip, QylConstants.Ports.DynamicAllocation);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

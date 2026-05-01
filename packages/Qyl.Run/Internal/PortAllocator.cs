// Copyright (c) 2025-2026 ancplua

using System.Net;
using System.Net.Sockets;

namespace Qyl.Run.Internal;

/// <summary>Claims a free TCP port by binding, reading the OS-assigned port, and releasing.</summary>
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



#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Net;

public static class NetAttributes
{
    [global::System.Obsolete("Replaced by network.local.address.", false)]
    public const string HostIp = "net.host.ip";

    [global::System.Obsolete("Replaced by server.address.", false)]
    public const string HostName = "net.host.name";

    [global::System.Obsolete("Replaced by server.port.", false)]
    public const string HostPort = "net.host.port";

    [global::System.Obsolete("Replaced by network.peer.address.", false)]
    public const string PeerIp = "net.peer.ip";

    [global::System.Obsolete("Replaced by `server.address` on client spans and `client.address` on server spans.", false)]
    public const string PeerName = "net.peer.name";

    [global::System.Obsolete("Replaced by `server.port` on client spans and `client.port` on server spans.", false)]
    public const string PeerPort = "net.peer.port";

    [global::System.Obsolete("Replaced by network.protocol.name.", false)]
    public const string ProtocolName = "net.protocol.name";

    [global::System.Obsolete("Replaced by network.protocol.version.", false)]
    public const string ProtocolVersion = "net.protocol.version";

    [global::System.Obsolete("Split to `network.transport` and `network.type`.", false)]
    public const string SockFamily = "net.sock.family";

    public static class SockFamilyValues
    {
        public const string Inet = "inet";

        public const string Inet6 = "inet6";

        public const string Unix = "unix";
    }

    [global::System.Obsolete("Replaced by network.local.address.", false)]
    public const string SockHostAddr = "net.sock.host.addr";

    [global::System.Obsolete("Replaced by network.local.port.", false)]
    public const string SockHostPort = "net.sock.host.port";

    [global::System.Obsolete("Replaced by network.peer.address.", false)]
    public const string SockPeerAddr = "net.sock.peer.addr";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string SockPeerName = "net.sock.peer.name";

    [global::System.Obsolete("Replaced by network.peer.port.", false)]
    public const string SockPeerPort = "net.sock.peer.port";

    [global::System.Obsolete("Replaced by network.transport.", false)]
    public const string Transport = "net.transport";

    public static class TransportValues
    {
        public const string Inproc = "inproc";

        public const string IpTcp = "ip_tcp";

        public const string IpUdp = "ip_udp";

        public const string Other = "other";

        public const string Pipe = "pipe";
    }
}

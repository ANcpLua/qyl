

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Network;

public static class NetworkAttributes
{
    public const string LocalAddress = "network.local.address";

    public const string LocalPort = "network.local.port";

    public const string PeerAddress = "network.peer.address";

    public const string PeerPort = "network.peer.port";

    public const string ProtocolName = "network.protocol.name";

    public const string ProtocolVersion = "network.protocol.version";

    public const string Transport = "network.transport";

    public static class TransportValues
    {
        public const string Pipe = "pipe";

        public const string Quic = "quic";

        public const string Tcp = "tcp";

        public const string Udp = "udp";

        public const string Unix = "unix";
    }

    public const string Type = "network.type";

    public static class TypeValues
    {
        public const string Ipv4 = "ipv4";

        public const string Ipv6 = "ipv6";
    }
}

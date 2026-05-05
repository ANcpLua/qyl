

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Network;

public static class NetworkAttributes
{
    public const string CarrierIcc = "network.carrier.icc";

    public const string CarrierMcc = "network.carrier.mcc";

    public const string CarrierMnc = "network.carrier.mnc";

    public const string CarrierName = "network.carrier.name";

    public const string ConnectionState = "network.connection.state";

    public static class ConnectionStateValues
    {
        public const string CloseWait = "close_wait";

        public const string Closed = "closed";

        public const string Closing = "closing";

        public const string Established = "established";

        public const string FinWait1 = "fin_wait_1";

        public const string FinWait2 = "fin_wait_2";

        public const string LastAck = "last_ack";

        public const string Listen = "listen";

        public const string SynReceived = "syn_received";

        public const string SynSent = "syn_sent";

        public const string TimeWait = "time_wait";
    }

    public const string ConnectionSubtype = "network.connection.subtype";

    public static class ConnectionSubtypeValues
    {
        public const string Cdma = "cdma";

        public const string Cdma20001xrtt = "cdma2000_1xrtt";

        public const string Edge = "edge";

        public const string Ehrpd = "ehrpd";

        public const string Evdo0 = "evdo_0";

        public const string EvdoA = "evdo_a";

        public const string EvdoB = "evdo_b";

        public const string Gprs = "gprs";

        public const string Gsm = "gsm";

        public const string Hsdpa = "hsdpa";

        public const string Hspa = "hspa";

        public const string Hspap = "hspap";

        public const string Hsupa = "hsupa";

        public const string Iden = "iden";

        public const string Iwlan = "iwlan";

        public const string Lte = "lte";

        public const string LteCa = "lte_ca";

        public const string Nr = "nr";

        public const string Nrnsa = "nrnsa";

        public const string TdScdma = "td_scdma";

        public const string Umts = "umts";
    }

    public const string ConnectionType = "network.connection.type";

    public static class ConnectionTypeValues
    {
        public const string Cell = "cell";

        public const string Unavailable = "unavailable";

        public const string Unknown = "unknown";

        public const string Wifi = "wifi";

        public const string Wired = "wired";
    }

    public const string InterfaceName = "network.interface.name";

    public const string IoDirection = "network.io.direction";

    public static class IoDirectionValues
    {
        public const string Receive = "receive";

        public const string Transmit = "transmit";
    }
}

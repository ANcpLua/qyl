

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Signalr;

public static class SignalrAttributes
{
    public const string ConnectionStatus = "signalr.connection.status";

    public static class ConnectionStatusValues
    {
        public const string AppShutdown = "app_shutdown";

        public const string NormalClosure = "normal_closure";

        public const string Timeout = "timeout";
    }

    public const string Transport = "signalr.transport";

    public static class TransportValues
    {
        public const string LongPolling = "long_polling";

        public const string ServerSentEvents = "server_sent_events";

        public const string WebSockets = "web_sockets";
    }
}

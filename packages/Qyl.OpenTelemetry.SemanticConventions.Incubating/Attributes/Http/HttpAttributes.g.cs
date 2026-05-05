

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Http;

public static class HttpAttributes
{
    [global::System.Obsolete("Replaced by client.address.", false)]
    public const string ClientIp = "http.client_ip";

    public const string ConnectionState = "http.connection.state";

    public static class ConnectionStateValues
    {
        public const string Active = "active";

        public const string Idle = "idle";
    }

    [global::System.Obsolete("Split into `network.protocol.name` and `network.protocol.version`", false)]
    public const string Flavor = "http.flavor";

    public static class FlavorValues
    {
        public const string Http10 = "1.0";

        public const string Http11 = "1.1";

        public const string Http20 = "2.0";

        public const string Http30 = "3.0";

        public const string Quic = "QUIC";

        public const string Spdy = "SPDY";
    }

    [global::System.Obsolete("Replaced by one of `server.address`, `client.address` or `http.request.header.host`, depending on the usage.", false)]
    public const string Host = "http.host";

    [global::System.Obsolete("Replaced by http.request.method.", false)]
    public const string Method = "http.method";

    public const string RequestBodySize = "http.request.body.size";

    public const string RequestSize = "http.request.size";

    [global::System.Obsolete("Replaced by `http.request.header.content-length`.", false)]
    public const string RequestContentLength = "http.request_content_length";

    [global::System.Obsolete("Replaced by http.request.body.size.", false)]
    public const string RequestContentLengthUncompressed = "http.request_content_length_uncompressed";

    public const string ResponseBodySize = "http.response.body.size";

    public const string ResponseSize = "http.response.size";

    [global::System.Obsolete("Replaced by `http.response.header.content-length`.", false)]
    public const string ResponseContentLength = "http.response_content_length";

    [global::System.Obsolete("Replaced by http.response.body.size.", false)]
    public const string ResponseContentLengthUncompressed = "http.response_content_length_uncompressed";

    [global::System.Obsolete("Replaced by url.scheme.", false)]
    public const string Scheme = "http.scheme";

    [global::System.Obsolete("Replaced by server.address.", false)]
    public const string ServerName = "http.server_name";

    [global::System.Obsolete("Replaced by http.response.status_code.", false)]
    public const string StatusCode = "http.status_code";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Target = "http.target";

    [global::System.Obsolete("Replaced by url.full.", false)]
    public const string Url = "http.url";

    [global::System.Obsolete("Replaced by user_agent.original.", false)]
    public const string UserAgent = "http.user_agent";
}

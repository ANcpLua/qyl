

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Http;

public static class HttpAttributes
{
    public const string RequestHeader = "http.request.header";

    public const string RequestMethod = "http.request.method";

    public static class RequestMethodValues
    {
        public const string Other = "_OTHER";

        public const string Connect = "CONNECT";

        public const string Delete = "DELETE";

        public const string Get = "GET";

        public const string Head = "HEAD";

        public const string Options = "OPTIONS";

        public const string Patch = "PATCH";

        public const string Post = "POST";

        public const string Put = "PUT";

        public const string Query = "QUERY";

        public const string Trace = "TRACE";
    }

    public const string RequestMethodOriginal = "http.request.method_original";

    public const string RequestResendCount = "http.request.resend_count";

    public const string ResponseHeader = "http.response.header";

    public const string ResponseStatusCode = "http.response.status_code";

    public const string Route = "http.route";
}

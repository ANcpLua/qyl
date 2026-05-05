

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Aspnetcore;

public static class AspnetcoreAttributes
{
    public const string DiagnosticsExceptionResult = "aspnetcore.diagnostics.exception.result";

    public static class DiagnosticsExceptionResultValues
    {
        public const string Aborted = "aborted";

        public const string Handled = "handled";

        public const string Skipped = "skipped";

        public const string Unhandled = "unhandled";
    }

    public const string DiagnosticsHandlerType = "aspnetcore.diagnostics.handler.type";

    public const string RateLimitingPolicy = "aspnetcore.rate_limiting.policy";

    public const string RateLimitingResult = "aspnetcore.rate_limiting.result";

    public static class RateLimitingResultValues
    {
        public const string Acquired = "acquired";

        public const string EndpointLimiter = "endpoint_limiter";

        public const string GlobalLimiter = "global_limiter";

        public const string RequestCanceled = "request_canceled";
    }

    public const string RequestIsUnhandled = "aspnetcore.request.is_unhandled";

    public const string RoutingIsFallback = "aspnetcore.routing.is_fallback";

    public const string RoutingMatchStatus = "aspnetcore.routing.match_status";

    public static class RoutingMatchStatusValues
    {
        public const string Failure = "failure";

        public const string Success = "success";
    }

    public const string UserIsAuthenticated = "aspnetcore.user.is_authenticated";
}

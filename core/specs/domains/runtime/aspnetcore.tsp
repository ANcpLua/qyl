// =============================================================================
// QYL v2.0 - ASP.NET Core Semantic Conventions (OTel v1.39)
// =============================================================================
// ASP.NET Core web framework telemetry attributes and metrics.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Runtime.AspNetCore;

// =============================================================================
// ASP.NET Core Request Attributes
// =============================================================================

@doc("ASP.NET Core request attributes")
model AspNetCoreAttributes {
  @doc("Route handler matched")
  @encodedName("application/json", "aspnetcore.routing.match_status")
  routingMatchStatus?: AspNetCoreRoutingMatchStatus;

  @doc("Whether the request had an exception")
  @encodedName("application/json", "aspnetcore.request.is_unhandled")
  requestIsUnhandled?: boolean;

  @doc("Rate limiting policy")
  @encodedName("application/json", "aspnetcore.rate_limiting.policy")
  rateLimitingPolicy?: string;

  @doc("Rate limiting result")
  @encodedName("application/json", "aspnetcore.rate_limiting.result")
  rateLimitingResult?: AspNetCoreRateLimitingResult;

  @doc("Diagnostics handler type")
  @encodedName("application/json", "aspnetcore.diagnostics.handler.type")
  diagnosticsHandlerType?: string;

  @doc("Exception handler type")
  @encodedName("application/json", "aspnetcore.diagnostics.exception.result")
  exceptionResult?: AspNetCoreExceptionResult;
}

// =============================================================================
// ASP.NET Core Routing Metrics
// =============================================================================

@doc("ASP.NET Core routing match attempts metric")
model AspNetCoreRoutingMatchAttemptsMetric {
  @doc("Metric name")
  name: "aspnetcore.routing.match_attempts";

  @doc("Metric unit")
  unit: "{match_attempt}";

  @doc("Match status")
  @encodedName("application/json", "aspnetcore.routing.match_status")
  matchStatus: AspNetCoreRoutingMatchStatus;

  @doc("Is fallback route")
  @encodedName("application/json", "aspnetcore.routing.is_fallback")
  isFallback?: boolean;
}

// =============================================================================
// ASP.NET Core Rate Limiting Metrics
// =============================================================================

@doc("ASP.NET Core rate limiting requests metric")
model AspNetCoreRateLimitingRequestsMetric {
  @doc("Metric name")
  name: "aspnetcore.rate_limiting.requests";

  @doc("Metric unit")
  unit: "{request}";

  @doc("Rate limiting policy")
  @encodedName("application/json", "aspnetcore.rate_limiting.policy")
  policy: string;

  @doc("Rate limiting result")
  @encodedName("application/json", "aspnetcore.rate_limiting.result")
  result: AspNetCoreRateLimitingResult;
}

@doc("ASP.NET Core rate limiting queue length metric")
model AspNetCoreRateLimitingQueueLengthMetric {
  @doc("Metric name")
  name: "aspnetcore.rate_limiting.queued_requests";

  @doc("Metric unit")
  unit: "{request}";

  @doc("Rate limiting policy")
  @encodedName("application/json", "aspnetcore.rate_limiting.policy")
  policy: string;
}

@doc("ASP.NET Core rate limiting active lease count metric")
model AspNetCoreRateLimitingActiveLeasesMetric {
  @doc("Metric name")
  name: "aspnetcore.rate_limiting.active_request_leases";

  @doc("Metric unit")
  unit: "{request}";

  @doc("Rate limiting policy")
  @encodedName("application/json", "aspnetcore.rate_limiting.policy")
  policy: string;
}

@doc("ASP.NET Core rate limiting request time in queue metric")
model AspNetCoreRateLimitingRequestTimeInQueueMetric {
  @doc("Metric name")
  name: "aspnetcore.rate_limiting.request_time_in_queue";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Rate limiting policy")
  @encodedName("application/json", "aspnetcore.rate_limiting.policy")
  policy: string;

  @doc("Rate limiting result")
  @encodedName("application/json", "aspnetcore.rate_limiting.result")
  result: AspNetCoreRateLimitingResult;
}

// =============================================================================
// ASP.NET Core Request Metrics
// =============================================================================

@doc("ASP.NET Core request duration metric")
model AspNetCoreRequestDurationMetric {
  @doc("Metric name")
  name: "aspnetcore.request.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("HTTP request method")
  @encodedName("application/json", "http.request.method")
  httpRequestMethod: string;

  @doc("HTTP route")
  @encodedName("application/json", "http.route")
  httpRoute?: string;

  @doc("HTTP response status code")
  @encodedName("application/json", "http.response.status_code")
  httpResponseStatusCode?: int32;

  @doc("URL scheme")
  @encodedName("application/json", "url.scheme")
  urlScheme: string;
}

@doc("ASP.NET Core active requests metric")
model AspNetCoreActiveRequestsMetric {
  @doc("Metric name")
  name: "aspnetcore.request.active";

  @doc("Metric unit")
  unit: "{request}";

  @doc("HTTP request method")
  @encodedName("application/json", "http.request.method")
  httpRequestMethod?: string;

  @doc("URL scheme")
  @encodedName("application/json", "url.scheme")
  urlScheme?: string;
}

// =============================================================================
// ASP.NET Core Enumerations
// =============================================================================

@doc("ASP.NET Core routing match status")
enum AspNetCoreRoutingMatchStatus {
  @doc("Route matched successfully")
  success: "success",

  @doc("No route matched")
  failure: "failure",
}

@doc("ASP.NET Core rate limiting results")
enum AspNetCoreRateLimitingResult {
  @doc("Request was acquired")
  acquired: "acquired",

  @doc("Request was rejected")
  rejected: "rejected",

  @doc("Request was cancelled")
  cancelled: "cancelled",

  @doc("Request was released")
  released: "released",

  @doc("Request lease was acquired")
  leaseAcquired: "lease_acquired",

  @doc("Request lease failed")
  leaseFailed: "lease_failed",
}

@doc("ASP.NET Core exception handling results")
enum AspNetCoreExceptionResult {
  @doc("Exception was handled")
  handled: "handled",

  @doc("Exception was not handled")
  unhandled: "unhandled",

  @doc("Exception was skipped")
  skipped: "skipped",

  @doc("Exception was aborted")
  aborted: "aborted",
}

// =============================================================================
// ASP.NET Core Hosting Events
// =============================================================================

@doc("ASP.NET Core request started event")
model AspNetCoreRequestStartedEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "aspnetcore.request.started";

  @doc("HTTP method")
  @encodedName("application/json", "http.request.method")
  httpRequestMethod: string;

  @doc("URL path")
  @encodedName("application/json", "url.path")
  urlPath: string;

  @doc("Connection ID")
  @encodedName("application/json", "aspnetcore.connection.id")
  connectionId: string;

  @doc("Request ID")
  @encodedName("application/json", "aspnetcore.request.id")
  requestId: string;
}

@doc("ASP.NET Core request completed event")
model AspNetCoreRequestCompletedEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "aspnetcore.request.completed";

  @doc("HTTP method")
  @encodedName("application/json", "http.request.method")
  httpRequestMethod: string;

  @doc("HTTP response status code")
  @encodedName("application/json", "http.response.status_code")
  httpResponseStatusCode: int32;

  @doc("Request duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;

  @doc("Request ID")
  @encodedName("application/json", "aspnetcore.request.id")
  requestId: string;
}

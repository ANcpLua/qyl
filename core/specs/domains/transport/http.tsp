// =============================================================================
// QYL v2.0 - HTTP Semantic Conventions (OTel v1.39)
// =============================================================================
// HTTP client and server telemetry attributes following OTel specifications.
// =============================================================================

import "@typespec/versioning";
import "../../common/types.tsp";

using TypeSpec.Versioning;
using Qyl.Common;

@versioned(HttpVersions)
namespace Qyl.Domains.Transport.Http;

// =============================================================================
// Version History
// =============================================================================

@doc("HTTP semantic convention versions")
enum HttpVersions {
  @doc("Legacy HTTP semconv")
  v1_20: "1.20.0",

  @doc("Stable HTTP semconv")
  v1_23: "1.23.0",

  @doc("HTTP semconv v1.38")
  v1_38: "1.38.0",

  @doc("Current HTTP semconv")
  v1_39: "1.39.0",
}

// =============================================================================
// HTTP Span Attributes (Complete OTel v1.39)
// =============================================================================

@doc("HTTP span attributes following OTel Semantic Conventions v1.39")
model HttpAttributes {
  // ---------------------------------------------------------------------------
  // Common Attributes (Client & Server)
  // ---------------------------------------------------------------------------

  @doc("HTTP request method")
  @encodedName("application/json", "http.request.method")
  requestMethod: HttpMethod;

  @doc("Original HTTP method (before any rewrites)")
  @encodedName("application/json", "http.request.method_original")
  requestMethodOriginal?: string;

  @doc("HTTP response status code")
  @encodedName("application/json", "http.response.status_code")
  @minValue(100)
  @maxValue(599)
  responseStatusCode?: int32;

  @doc("Error type if request failed")
  @encodedName("application/json", "error.type")
  errorType?: HttpErrorType;

  @doc("HTTP protocol version")
  @encodedName("application/json", "network.protocol.version")
  networkProtocolVersion?: HttpProtocolVersion;

  // ---------------------------------------------------------------------------
  // Server-Side Attributes
  // ---------------------------------------------------------------------------

  @doc("The matched route template (e.g., '/users/{userId}')")
  @encodedName("application/json", "http.route")
  route?: string;

  @doc("Server address (hostname or IP)")
  @encodedName("application/json", "server.address")
  serverAddress?: string;

  @doc("Server port")
  @encodedName("application/json", "server.port")
  serverPort?: Port;

  // ---------------------------------------------------------------------------
  // Client-Side Attributes
  // ---------------------------------------------------------------------------

  @doc("Full request target URL")
  @encodedName("application/json", "url.full")
  urlFull?: UrlString;

  @doc("URL path component")
  @encodedName("application/json", "url.path")
  urlPath?: UrlPath;

  @doc("URL query string (without leading ?)")
  @encodedName("application/json", "url.query")
  urlQuery?: UrlQuery;

  @doc("URL scheme (http or https)")
  @encodedName("application/json", "url.scheme")
  urlScheme?: UrlScheme;

  // ---------------------------------------------------------------------------
  // Request Attributes
  // ---------------------------------------------------------------------------

  @doc("Size of the request body in bytes")
  @encodedName("application/json", "http.request.body.size")
  requestBodySize?: ByteSize;

  @doc("Request content length header value")
  @encodedName("application/json", "http.request.header.content-length")
  requestContentLength?: ByteSize;

  @doc("Request content type header")
  @encodedName("application/json", "http.request.header.content-type")
  requestContentType?: ContentType;

  @doc("Request User-Agent header")
  @encodedName("application/json", "user_agent.original")
  userAgentOriginal?: UserAgent;

  @doc("Request Accept header")
  @encodedName("application/json", "http.request.header.accept")
  requestAccept?: string;

  @doc("Request Authorization scheme (e.g., 'Bearer')")
  @encodedName("application/json", "http.request.header.authorization")
  requestAuthorizationScheme?: string;

  @doc("Request X-Forwarded-For header")
  @encodedName("application/json", "http.request.header.x-forwarded-for")
  requestXForwardedFor?: string;

  // ---------------------------------------------------------------------------
  // Response Attributes
  // ---------------------------------------------------------------------------

  @doc("Size of the response body in bytes")
  @encodedName("application/json", "http.response.body.size")
  responseBodySize?: ByteSize;

  @doc("Response content length header value")
  @encodedName("application/json", "http.response.header.content-length")
  responseContentLength?: ByteSize;

  @doc("Response content type header")
  @encodedName("application/json", "http.response.header.content-type")
  responseContentType?: ContentType;

  // ---------------------------------------------------------------------------
  // Template Headers (Dynamic Keys: http.request.header.<key>)
  // ---------------------------------------------------------------------------

  @doc("HTTP request headers. Keys map to http.request.header.<key>")
  @encodedName("application/json", "http.request.header")
  requestHeaders?: Record<string[]>;

  @doc("HTTP response headers. Keys map to http.response.header.<key>")
  @encodedName("application/json", "http.response.header")
  responseHeaders?: Record<string[]>;

  // ---------------------------------------------------------------------------
  // Client IP Attributes (Server-Side)
  // ---------------------------------------------------------------------------

  @doc("Client IP address")
  @encodedName("application/json", "client.address")
  clientAddress?: IpAddress;

  @doc("Client port")
  @encodedName("application/json", "client.port")
  clientPort?: Port;

  // ---------------------------------------------------------------------------
  // Connection Attributes
  // ---------------------------------------------------------------------------

  @doc("State of the HTTP connection")
  @encodedName("application/json", "http.connection.state")
  connectionState?: HttpConnectionState;

  @doc("Number of retries for the request")
  @encodedName("application/json", "http.request.resend_count")
  requestResendCount?: int32;

  // ---------------------------------------------------------------------------
  // Deprecated Attributes (for migration)
  // ---------------------------------------------------------------------------

  @doc("Request target (deprecated - use url.path + url.query)")
  @encodedName("application/json", "http.target")
  @removed(HttpVersions.v1_23)
  target?: string;

  @doc("Request URL (deprecated - use url.full)")
  @encodedName("application/json", "http.url")
  @removed(HttpVersions.v1_23)
  url?: UrlString;

  @doc("HTTP method (deprecated - use http.request.method)")
  @encodedName("application/json", "http.method")
  @removed(HttpVersions.v1_23)
  method?: string;

  @doc("Status code (deprecated - use http.response.status_code)")
  @encodedName("application/json", "http.status_code")
  @removed(HttpVersions.v1_23)
  statusCode?: int32;

  @doc("Request content length (deprecated)")
  @encodedName("application/json", "http.request_content_length")
  @removed(HttpVersions.v1_23)
  requestContentLengthLegacy?: ByteSize;

  @doc("Response content length (deprecated)")
  @encodedName("application/json", "http.response_content_length")
  @removed(HttpVersions.v1_23)
  responseContentLengthLegacy?: ByteSize;
}

// =============================================================================
// HTTP Enumerations
// =============================================================================

@doc("Standard HTTP request methods")
enum HttpMethod {
  @doc("GET method - retrieve resource")
  GET: "GET",

  @doc("HEAD method - retrieve headers only")
  HEAD: "HEAD",

  @doc("POST method - create resource")
  POST: "POST",

  @doc("PUT method - replace resource")
  PUT: "PUT",

  @doc("DELETE method - remove resource")
  DELETE: "DELETE",

  @doc("CONNECT method - establish tunnel")
  CONNECT: "CONNECT",

  @doc("OPTIONS method - describe options")
  OPTIONS: "OPTIONS",

  @doc("TRACE method - diagnostic trace")
  TRACE: "TRACE",

  @doc("PATCH method - partial update")
  PATCH: "PATCH",

  @doc("Custom/other method (use _OTHER)")
  OTHER: "_OTHER",
}

@doc("HTTP protocol versions")
enum HttpProtocolVersion {
  @doc("HTTP/1.0")
  http10: "1.0",

  @doc("HTTP/1.1")
  http11: "1.1",

  @doc("HTTP/2")
  http2: "2",

  @doc("HTTP/3 (QUIC)")
  http3: "3",
}

@doc("URL schemes")
enum UrlScheme {
  @doc("Unencrypted HTTP")
  http: "http",

  @doc("TLS-encrypted HTTPS")
  https: "https",

  @doc("WebSocket")
  ws: "ws",

  @doc("Secure WebSocket")
  wss: "wss",
}

@doc("HTTP connection state")
enum HttpConnectionState {
  @doc("Connection is active")
  active: "active",

  @doc("Connection is idle")
  idle: "idle",

  @doc("Connection is closing")
  closing: "closing",

  @doc("Connection is closed")
  closed: "closed",
}

@doc("HTTP error types")
enum HttpErrorType {
  @doc("Timeout waiting for response")
  timeout: "TIMEOUT",

  @doc("Connection refused")
  connectionRefused: "CONNECTION_REFUSED",

  @doc("Host not found (DNS error)")
  hostNotFound: "HOST_NOT_FOUND",

  @doc("SSL/TLS error")
  sslError: "SSL_ERROR",

  @doc("Connection reset")
  connectionReset: "CONNECTION_RESET",

  @doc("Network unreachable")
  networkUnreachable: "NETWORK_UNREACHABLE",

  @doc("Request cancelled")
  cancelled: "CANCELLED",

  @doc("Request too large")
  requestTooLarge: "REQUEST_TOO_LARGE",

  @doc("Response too large")
  responseTooLarge: "RESPONSE_TOO_LARGE",

  @doc("Invalid response")
  invalidResponse: "INVALID_RESPONSE",

  @doc("Too many redirects")
  tooManyRedirects: "TOO_MANY_REDIRECTS",
}

@doc("HTTP status code ranges")
enum HttpStatusClass {
  @doc("1xx Informational")
  informational: "1xx",

  @doc("2xx Success")
  success: "2xx",

  @doc("3xx Redirection")
  redirection: "3xx",

  @doc("4xx Client Error")
  clientError: "4xx",

  @doc("5xx Server Error")
  serverError: "5xx",
}

// =============================================================================
// HTTP Request/Response Models
// =============================================================================

@doc("Complete HTTP request record")
model HttpRequest {
  @doc("Request method")
  method: HttpMethod;

  @doc("Full URL")
  url: UrlString;

  @doc("URL path")
  path: UrlPath;

  @doc("URL query string")
  query?: UrlQuery;

  @doc("Request headers")
  headers: HttpHeader[];

  @doc("Request body size in bytes")
  @encodedName("application/json", "body_size")
  bodySize?: ByteSize;

  @doc("Request body hash (for deduplication)")
  @encodedName("application/json", "body_hash")
  bodyHash?: Sha256Hash;

  @doc("Protocol version")
  @encodedName("application/json", "protocol_version")
  protocolVersion: HttpProtocolVersion;

  @doc("Request timestamp")
  timestamp: utcDateTime;
}

@doc("Complete HTTP response record")
model HttpResponse {
  @doc("Status code")
  @encodedName("application/json", "status_code")
  statusCode: int32;

  @doc("Status text")
  @encodedName("application/json", "status_text")
  statusText?: string;

  @doc("Response headers")
  headers: HttpHeader[];

  @doc("Response body size in bytes")
  @encodedName("application/json", "body_size")
  bodySize?: ByteSize;

  @doc("Response timestamp")
  timestamp: utcDateTime;

  @doc("Time to first byte in milliseconds")
  @encodedName("application/json", "ttfb_ms")
  ttfbMs?: DurationMs;
}

@doc("HTTP header key-value pair")
model HttpHeader {
  @doc("Header name (case-insensitive)")
  name: string;

  @doc("Header value(s)")
  value: string;
}

// =============================================================================
// HTTP Metrics
// =============================================================================

@doc("HTTP server request duration metric")
model HttpServerRequestDurationMetric {
  @doc("Metric name")
  name: "http.server.request.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("HTTP method")
  @encodedName("application/json", "http.request.method")
  requestMethod: HttpMethod;

  @doc("HTTP route")
  @encodedName("application/json", "http.route")
  route: string;

  @doc("Response status code")
  @encodedName("application/json", "http.response.status_code")
  responseStatusCode: int32;

  @doc("URL scheme")
  @encodedName("application/json", "url.scheme")
  urlScheme: UrlScheme;
}

@doc("HTTP server active requests metric")
model HttpServerActiveRequestsMetric {
  @doc("Metric name")
  name: "http.server.active_requests";

  @doc("Metric unit")
  unit: "{request}";

  @doc("HTTP method")
  @encodedName("application/json", "http.request.method")
  requestMethod: HttpMethod;

  @doc("HTTP route")
  @encodedName("application/json", "http.route")
  route?: string;

  @doc("URL scheme")
  @encodedName("application/json", "url.scheme")
  urlScheme: UrlScheme;
}

@doc("HTTP client request duration metric")
model HttpClientRequestDurationMetric {
  @doc("Metric name")
  name: "http.client.request.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("HTTP method")
  @encodedName("application/json", "http.request.method")
  requestMethod: HttpMethod;

  @doc("Server address")
  @encodedName("application/json", "server.address")
  serverAddress: string;

  @doc("Server port")
  @encodedName("application/json", "server.port")
  serverPort: Port;

  @doc("Response status code")
  @encodedName("application/json", "http.response.status_code")
  responseStatusCode?: int32;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: HttpErrorType;
}

// =============================================================================
// HTTP Statistics
// =============================================================================

@doc("Aggregated HTTP statistics")
model HttpStats {
  @doc("Total request count")
  @encodedName("application/json", "total_requests")
  totalRequests: Count;

  @doc("Successful request count (2xx)")
  @encodedName("application/json", "success_count")
  successCount: Count;

  @doc("Client error count (4xx)")
  @encodedName("application/json", "client_error_count")
  clientErrorCount: Count;

  @doc("Server error count (5xx)")
  @encodedName("application/json", "server_error_count")
  serverErrorCount: Count;

  @doc("Error rate (4xx + 5xx / total)")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Average duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("P50 duration in milliseconds")
  @encodedName("application/json", "p50_duration_ms")
  p50DurationMs: float64;

  @doc("P95 duration in milliseconds")
  @encodedName("application/json", "p95_duration_ms")
  p95DurationMs: float64;

  @doc("P99 duration in milliseconds")
  @encodedName("application/json", "p99_duration_ms")
  p99DurationMs: float64;

  @doc("Requests per second")
  @encodedName("application/json", "requests_per_second")
  requestsPerSecond: float64;

  @doc("Total bytes received")
  @encodedName("application/json", "total_bytes_received")
  totalBytesReceived: ByteSize;

  @doc("Total bytes sent")
  @encodedName("application/json", "total_bytes_sent")
  totalBytesSent: ByteSize;

  @doc("Stats by route")
  @encodedName("application/json", "by_route")
  byRoute?: HttpRouteStats[];

  @doc("Stats by status code")
  @encodedName("application/json", "by_status_code")
  byStatusCode?: HttpStatusStats[];
}

@doc("HTTP statistics by route")
model HttpRouteStats {
  @doc("Route template")
  route: string;

  @doc("Request count")
  @encodedName("application/json", "request_count")
  requestCount: Count;

  @doc("Average duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;
}

@doc("HTTP statistics by status code")
model HttpStatusStats {
  @doc("Status code")
  @encodedName("application/json", "status_code")
  statusCode: int32;

  @doc("Request count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

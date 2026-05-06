// Auto-flattened single-file TypeSpec for build convenience.
// Generated at 2026-05-06T03:04:02.228Z from specified qyl/core/specs modules.

// =============================
// External Imports
// =============================
import "@typespec/http";
import "@typespec/versioning";
import "@typespec/json-schema";
import "@typespec/openapi";
import "@typespec/rest";
import "@typespec/openapi3";
import "@typespec/sse";
import "@typespec/events";
import "@qyl/typespec-emit-csharp";
import "@qyl/typespec-emit-duckdb";
import "@qyl/typespec-emit-ts-types";

// =============================
// Global Usings
// =============================
using TypeSpec.Http;
using TypeSpec.Versioning;
using TypeSpec.JsonSchema;
using TypeSpec.OpenAPI;
using Qyl.Common;
using Qyl.OTel.Enums;
using Qyl.OTel.Traces;
using Qyl.OTel.Metrics;
using Qyl.OTel.Resource;
using TypeSpec.Rest;
using Qyl.Emit.CSharp;
using Qyl.Emit.DuckDb;
using Qyl.Emit.TsTypes;
using Qyl.Common.Errors;
using Qyl.Common.Pagination;
using Qyl.OTel.Logs;
using Qyl.OTel.Profiles;
using Qyl.Storage;
using Qyl.Domains.Observe.Session;
using Qyl.Domains.Observe.Log;
using Qyl.Domains.Observe.Error;
using Qyl.Domains.Ops.Deployment;
using Qyl.Domains.Identity;
using Qyl.Domains.Workspace;
using Qyl.Domains.Configurator;
using Qyl.Domains.Issues;
using Qyl.Domains.Alerting;
using Qyl.Domains.Workflow;
using Qyl.Domains.Search;
using TypeSpec.SSE;
using TypeSpec.Events;


// ===== Flattened from: core/specs/common/errors.tsp =====
// =============================================================================
// QYL v2.0 - Error Models
// =============================================================================
// Standardized error responses following RFC 7807 Problem Details.
// =============================================================================



namespace Qyl.Common.Errors;

// =============================================================================
// RFC 7807 Problem Details Base
// =============================================================================

@doc("RFC 7807 Problem Details for HTTP APIs")
@error
model ProblemDetails {
  @doc("A URI reference identifying the problem type")
  @encodedName("application/json", "type")
  problemType: url = "about:blank";

  @doc("A short, human-readable summary of the problem type")
  title: string;

  @doc("The HTTP status code (informational only, actual code set by subtype)")
  status: int32;

  @doc("A human-readable explanation specific to this occurrence")
  detail?: string;

  @doc("A URI reference identifying the specific occurrence")
  instance?: url;

  @doc("Trace ID for correlation")
  @encodedName("application/json", "trace_id")
  @header("X-Trace-Id")
  traceId?: string;

  @doc("Request ID for support")
  @encodedName("application/json", "request_id")
  @header("X-Request-Id")
  requestId?: string;

  @doc("Timestamp of the error")
  timestamp?: utcDateTime;
}

// =============================================================================
// Specific Error Types
// =============================================================================

@doc("Resource not found (404)")
@error
model NotFoundError extends ProblemDetails {
  @statusCode _: 404;
  title: "Not Found";

  @doc("The type of resource that was not found")
  @encodedName("application/json", "resource_type")
  resourceType?: string;

  @doc("The identifier that was not found")
  @encodedName("application/json", "resource_id")
  resourceId?: string;
}

@doc("Bad request - validation failed (400)")
@error
model ValidationError extends ProblemDetails {
  @statusCode _: 400;
  title: "Validation Failed";

  @doc("List of validation errors")
  errors: ValidationErrorDetail[];
}

@doc("Individual validation error detail")
model ValidationErrorDetail {
  @doc("The field/property that failed validation")
  field: string;

  @doc("The error message")
  message: string;

  @doc("The validation rule that failed")
  code: string;

  @doc("The rejected value (if safe to include)")
  @encodedName("application/json", "rejected_value")
  rejectedValue?: string;
}

@doc("Unauthorized - authentication required (401)")
@error
model UnauthorizedError extends ProblemDetails {
  @statusCode _: 401;
  title: "Unauthorized";

  @doc("Authentication scheme expected")
  @header("WWW-Authenticate")
  @encodedName("application/json", "www_authenticate")
  wwwAuthenticate?: string;
}

@doc("Forbidden - insufficient permissions (403)")
@error
model ForbiddenError extends ProblemDetails {
  @statusCode _: 403;
  title: "Forbidden";

  @doc("Required permission that is missing")
  @encodedName("application/json", "required_permission")
  requiredPermission?: string;
}

@doc("Conflict - resource state conflict (409)")
@error
model ConflictError extends ProblemDetails {
  @statusCode _: 409;
  title: "Conflict";

  @doc("The conflicting resource ID")
  @encodedName("application/json", "conflicting_resource")
  conflictingResource?: string;
}

@doc("Rate limited - too many requests (429)")
@error
model RateLimitError extends ProblemDetails {
  @statusCode _: 429;
  title: "Too Many Requests";

  @doc("Seconds until the rate limit resets")
  @header("Retry-After")
  @encodedName("application/json", "retry_after")
  retryAfter: int32;

  @doc("Maximum requests allowed per window")
  @header("X-RateLimit-Limit")
  @encodedName("application/json", "rate_limit")
  rateLimit?: int32;

  @doc("Remaining requests in current window")
  @header("X-RateLimit-Remaining")
  @encodedName("application/json", "rate_limit_remaining")
  rateLimitRemaining?: int32;
}

@doc("Internal server error (500)")
@error
model InternalServerError extends ProblemDetails {
  @statusCode _: 500;
  title: "Internal Server Error";

  @doc("Error code for support reference")
  @encodedName("application/json", "error_code")
  errorCode?: string;
}

@doc("Service unavailable (503)")
@error
model ServiceUnavailableError extends ProblemDetails {
  @statusCode _: 503;
  title: "Service Unavailable";

  @doc("Expected availability time")
  @header("Retry-After")
  @encodedName("application/json", "retry_after")
  retryAfter?: int32;

  @doc("Reason for unavailability")
  reason?: "maintenance" | "overloaded" | "dependency_failure";
}

// =============================================================================
// Error Response Union
// =============================================================================

@doc("Standard error response types")
union ApiError {
  @doc("Resource not found")
  notFound: NotFoundError,

  @doc("Validation failed")
  validation: ValidationError,

  @doc("Authentication required")
  unauthorized: UnauthorizedError,

  @doc("Permission denied")
  forbidden: ForbiddenError,

  @doc("Resource conflict")
  conflict: ConflictError,

  @doc("Rate limit exceeded")
  rateLimit: RateLimitError,

  @doc("Server error")
  serverError: InternalServerError,

  @doc("Service unavailable")
  unavailable: ServiceUnavailableError,
}


// ===== Flattened from: core/specs/common/pagination.tsp =====
// =============================================================================
// QYL v2.0 - Pagination Templates
// =============================================================================
// Reusable pagination patterns for list operations.
// =============================================================================



namespace Qyl.Common.Pagination;

// =============================================================================
// Cursor-Based Pagination (Preferred for Large Datasets)
// =============================================================================

@doc("Cursor-based pagination parameters")
model CursorPaginationParams {
  @doc("Opaque cursor for fetching the next page")
  @query
  cursor?: string;

  @doc("Maximum number of items to return (1-1000)")
  @query
  @minValue(1)
  @maxValue(1000)
  limit?: int32 = 100;

  @doc("Sort direction")
  @query
  order?: SortOrder = SortOrder.desc;
}

@doc("Sort order")
enum SortOrder {
  @doc("Ascending order (oldest first)")
  asc,

  @doc("Descending order (newest first)")
  desc,
}

@doc("Cursor-based paginated response wrapper")
model CursorPage<T> {
  @doc("List of items in this page")
  @pageItems
  items: T[];

  @doc("Cursor for the next page (null if no more pages)")
  @encodedName("application/json", "next_cursor")
  @nextLink
  nextCursor?: string;

  @doc("Cursor for the previous page (null if first page)")
  @encodedName("application/json", "prev_cursor")
  prevCursor?: string;

  @doc("Whether there are more items available")
  @encodedName("application/json", "has_more")
  hasMore: boolean;
}

// =============================================================================
// Offset-Based Pagination (For Small/Medium Datasets)
// =============================================================================

@doc("Offset-based pagination parameters")
model OffsetPaginationParams {
  @doc("Page number (1-indexed)")
  @query
  @pageIndex
  @minValue(1)
  page?: int32 = 1;

  @doc("Items per page (1-1000)")
  @query
  @pageSize
  @minValue(1)
  @maxValue(1000)
  @encodedName("application/json", "page_size")
  pageSize?: int32 = 100;
}

@doc("Offset-based paginated response wrapper")
model OffsetPage<T> {
  @doc("List of items in this page")
  @pageItems
  items: T[];

  @doc("Current page number")
  page: int32;

  @doc("Items per page")
  @encodedName("application/json", "page_size")
  pageSize: int32;

  @doc("Total number of items across all pages")
  @encodedName("application/json", "total_count")
  totalCount: int64;

  @doc("Total number of pages")
  @encodedName("application/json", "total_pages")
  totalPages: int32;

  @doc("Whether there is a next page")
  @encodedName("application/json", "has_next")
  hasNext: boolean;

  @doc("Whether there is a previous page")
  @encodedName("application/json", "has_prev")
  hasPrev: boolean;
}

// =============================================================================
// Time-Range Pagination (For Time-Series Data)
// =============================================================================

@doc("Time-range based query parameters")
model TimeRangeParams {
  @doc("Start of time range (inclusive)")
  @query
  @encodedName("application/json", "start_time")
  startTime?: utcDateTime;

  @doc("End of time range (exclusive)")
  @query
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Maximum number of items to return")
  @query
  @minValue(1)
  @maxValue(10000)
  limit?: int32 = 1000;
}

@doc("Time-range paginated response with continuation")
model TimeRangePage<T> {
  @doc("List of items in this page")
  @pageItems
  items: T[];

  @doc("Actual start time of returned data")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("Actual end time of returned data")
  @encodedName("application/json", "end_time")
  endTime: utcDateTime;

  @doc("Continuation token for next time window")
  @encodedName("application/json", "continuation_token")
  @continuationToken
  continuationToken?: string;

  @doc("Whether there is more data in the time range")
  @encodedName("application/json", "has_more")
  hasMore: boolean;

  @doc("Number of items returned")
  count: int32;
}

// =============================================================================
// Streaming Pagination (For Real-Time Data)
// =============================================================================

@doc("Streaming parameters for SSE endpoints")
model StreamParams {
  @doc("Include historical data from this timestamp")
  @query
  since?: utcDateTime;

  @doc("Filter by trace ID")
  @query
  @encodedName("application/json", "trace_id")
  traceId?: string;

  @doc("Filter by service name")
  @query
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Maximum events per second (rate limiting)")
  @query
  @encodedName("application/json", "max_events_per_second")
  @minValue(1)
  @maxValue(1000)
  maxEventsPerSecond?: int32 = 100;
}

@doc("SSE event wrapper for streaming")
model StreamEvent<T> {
  @doc("Event ID for resumption")
  id: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: string;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Event data payload")
  data: T;

  @doc("Retry interval hint in milliseconds")
  retry?: int32;
}

// =============================================================================
// Aggregation Parameters
// =============================================================================

@doc("Time bucket size for aggregations")
enum TimeBucket {
  @doc("1 minute buckets")
  minute: "1m",

  @doc("5 minute buckets")
  fiveMinutes: "5m",

  @doc("15 minute buckets")
  fifteenMinutes: "15m",

  @doc("1 hour buckets")
  hour: "1h",

  @doc("1 day buckets")
  day: "1d",

  @doc("1 week buckets")
  week: "1w",

  @doc("Auto-select based on time range")
  auto: "auto",
}

@doc("Aggregation query parameters")
model AggregationParams {
  @doc("Time bucket size")
  @query
  bucket?: TimeBucket = TimeBucket.auto;

  @doc("Group by these dimensions")
  @query
  @encodedName("application/json", "group_by")
  groupBy?: string[];

  @doc("Filter expression")
  @query
  filter?: string;
}


// ===== Flattened from: core/specs/common/types.tsp =====
// =============================================================================
// QYL v2.0 - Common Types & Scalars
// =============================================================================
// Foundational types used across all OTel semantic convention models.
// OpenTelemetry Semantic Conventions v1.40 compliant.
// =============================================================================



namespace Qyl.Common;

// =============================================================================
// Version Enum - OTel Semantic Convention Versions
// =============================================================================

@doc("OpenTelemetry Semantic Convention versions supported by QYL")
enum OTelVersion {
  @doc("OTel Semconv v1.27 - Base version")
  v1_27: "1.27.0",

  @doc("OTel Semconv v1.28 - GenAI additions")
  v1_28: "1.28.0",

  @doc("OTel Semconv v1.29 - HTTP improvements")
  v1_29: "1.29.0",

  @doc("OTel Semconv v1.30 - Database updates")
  v1_30: "1.30.0",

  @doc("OTel Semconv v1.38 - GenAI.Agent support")
  v1_38: "1.38.0",

  @doc("OTel Semconv v1.39 - RPC metadata consolidation")
  v1_39: "1.39.0",

  @doc("OTel Semconv v1.40 - Current version (Oracle DB split, RPC cleanup, GenAI cache tokens)")
  v1_40: "1.40.0",
}

// =============================================================================
// Identifier Scalars - Strongly Typed IDs
// =============================================================================

@jsonSchema
@doc("Unique trace identifier (32 lowercase hex characters)")
@pattern("^[a-f0-9]{32}$")
@minLength(32)
@maxLength(32)
@example("0af7651916cd43dd8448eb211c80319c")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(32)")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "TraceId")
scalar TraceId extends string;

@jsonSchema
@doc("Unique span identifier (16 lowercase hex characters)")
@pattern("^[a-f0-9]{16}$")
@minLength(16)
@maxLength(16)
@example("b7ad6b7169203331")
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(16)")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "SpanId")
scalar SpanId extends string;

@jsonSchema
@doc("W3C Trace Context traceparent header format")
@pattern("^[0-9a-f]{2}-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$")
@example("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")
scalar TraceParent extends string;

@jsonSchema
@doc("W3C Trace Context tracestate header (vendor-specific key-value pairs)")
@example("rojo=00f067aa0ba902b7,congo=t61rcWkgMzE")
scalar TraceState extends string;

@jsonSchema
@doc("Unique session identifier")
@minLength(1)
@maxLength(128)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(128)")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "SessionId")
scalar SessionId extends string;

@jsonSchema
@doc("User identifier (pseudonymized for privacy)")
@minLength(1)
@maxLength(256)
scalar UserId extends string;

// =============================================================================
// Temporal Scalars - Timestamps & Durations
// =============================================================================

@jsonSchema
@doc("ISO 8601 timestamp with nanosecond precision (RFC 3339)")
@encode(DateTimeKnownEncoding.rfc3339)
scalar Timestamp extends utcDateTime;

@jsonSchema
@doc("Unix timestamp in nanoseconds since epoch")
@encode(DateTimeKnownEncoding.unixTimestamp, int64)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-struct", true)
@TypeSpec.OpenAPI.extension("x-csharp-type", "UnixNano")
scalar UnixNanos extends utcDateTime;

@jsonSchema
@doc("Duration in nanoseconds")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar DurationNs extends int64;

@jsonSchema
@doc("Duration in milliseconds")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar DurationMs extends float64;

@jsonSchema
@doc("Duration in seconds")
@minValue(0)
scalar DurationS extends float64;

@jsonSchema
@doc("ISO 8601 duration format (e.g., PT1H30M)")
@encode(DurationKnownEncoding.ISO8601)
scalar IsoDuration extends duration;

// =============================================================================
// Network Scalars - IP Addresses & Ports
// =============================================================================

@jsonSchema
@doc("IPv4 address in dotted-decimal notation")
@pattern("^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")
@example("192.168.1.1")
scalar IPv4Address extends string;

@jsonSchema
@doc("IPv6 address in standard notation")
@example("2001:0db8:85a3:0000:0000:8a2e:0370:7334")
scalar IPv6Address extends string;

@jsonSchema
@doc("IP address (IPv4 or IPv6)")
scalar IpAddress extends string;

@jsonSchema
@doc("Network port number (1-65535)")
@minValue(1)
@maxValue(65535)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "INTEGER")
@TypeSpec.OpenAPI.extension("x-csharp-type", "int")
scalar Port extends int32;

@jsonSchema
@doc("MAC address in colon-separated hex notation")
@pattern("^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")
@example("00:1A:2B:3C:4D:5E")
scalar MacAddress extends string;

// =============================================================================
// Size & Count Scalars
// =============================================================================

@jsonSchema
@doc("Size in bytes")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "BIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar ByteSize extends int64;

@jsonSchema
@doc("Token count (for LLM operations)")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "BIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar TokenCount extends int64;

@jsonSchema
@doc("Generic non-negative counter")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "BIGINT")
@TypeSpec.OpenAPI.extension("x-csharp-type", "long")
scalar Count extends int64;

@jsonSchema
@doc("Percentage value (0.0 to 100.0)")
@minValue(0.0)
@maxValue(100.0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar Percentage extends float64;

@jsonSchema
@doc("Ratio value (0.0 to 1.0)")
@minValue(0.0)
@maxValue(1.0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar Ratio extends float64;

// =============================================================================
// GenAI-Specific Scalars
// =============================================================================

@jsonSchema
@doc("Cost in USD (floating point)")
@minValue(0)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar CostUsd extends float64;

@jsonSchema
@doc("Temperature setting for LLM requests (0.0-2.0)")
@minValue(0)
@maxValue(2)
@TypeSpec.OpenAPI.extension("x-duckdb-type", "DOUBLE")
@TypeSpec.OpenAPI.extension("x-csharp-type", "double")
scalar Temperature extends float64;

// =============================================================================
// Semantic Scalars - Special Purpose Types
// =============================================================================

@jsonSchema
@doc("SHA-256 hash in lowercase hex")
@pattern("^[a-f0-9]{64}$")
@minLength(64)
@maxLength(64)
scalar Sha256Hash extends string;

@jsonSchema
@doc("Semantic version string (e.g., 1.2.3)")
@pattern("^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\\+([0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?$")
@example("2.1.0")
scalar SemVer extends string;

@jsonSchema
@doc("URL string (absolute)")
@format("uri")
scalar UrlString extends string;

@jsonSchema
@doc("URL path component")
@example("/api/v1/users")
scalar UrlPath extends string;

@jsonSchema
@doc("URL query string (without leading ?)")
@example("page=1&limit=100")
scalar UrlQuery extends string;

@jsonSchema
@doc("HTTP method name (uppercase)")
@example("GET")
scalar HttpMethod extends string;

@jsonSchema
@doc("MIME content type")
@example("application/json")
scalar ContentType extends string;

@jsonSchema
@doc("User agent string")
@example("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
scalar UserAgent extends string;

@jsonSchema
@doc("Language/locale code (BCP 47)")
@pattern("^[a-z]{2,3}(-[A-Z]{2})?$")
@example("en-US")
scalar LanguageCode extends string;

// =============================================================================
// OpenTelemetry Attribute Value Types
// =============================================================================

@doc("Primitive attribute value types supported by OpenTelemetry")
union AttributeValue {
  @doc("String value")
  stringValue: string,

  @doc("Boolean value")
  boolValue: boolean,

  @doc("64-bit integer value")
  intValue: int64,

  @doc("64-bit floating point value")
  doubleValue: float64,

  @doc("Array of strings")
  stringArrayValue: string[],

  @doc("Array of booleans")
  boolArrayValue: boolean[],

  @doc("Array of integers")
  intArrayValue: int64[],

  @doc("Array of doubles")
  doubleArrayValue: float64[],

  @doc("Bytes value (base64 encoded)")
  bytesValue: bytes,
}

@doc("Key-value attribute pair following OTel conventions")
model Attribute {
  @doc("Attribute key (dot-separated namespace)")
  @minLength(1)
  @maxLength(256)
  key: string;

  @doc("Attribute value")
  value: AttributeValue;
}

@doc("Collection of attributes")
model Attributes {
  @doc("List of key-value attributes")
  items: Attribute[];
}

// =============================================================================
// Instrumentation Scope
// =============================================================================

@doc("Instrumentation scope identifying the library/component emitting telemetry")
model InstrumentationScope {
  @doc("Name of the instrumentation scope (library name)")
  @encodedName("application/json", "name")
  scopeName: string;

  @doc("Version of the instrumentation scope")
  @encodedName("application/json", "version")
  scopeVersion?: SemVer;

  @doc("Additional attributes for the scope")
  @encodedName("application/json", "attributes")
  scopeAttributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;
}


// ===== Flattened from: core/specs/intelligence/causal-rules.tsp =====
// =============================================================================
// QYL v2.0 - Causal Rules
// =============================================================================
// Directed relationship between two diagnostic patterns: if cause is observed,
// effect is likely. Causal rules build a directed graph. Given matched patterns,
// the engine traverses causal edges to identify root causes (patterns with no
// incoming causal edges).
// =============================================================================



namespace Qyl.Intelligence;

@doc("Directed causal relationship between two diagnostic patterns")
model CausalRule {
  @doc("Unique rule identifier")
  id: string;

  @doc("ID of the cause DiagnosticPattern")
  causePattern: string;

  @doc("ID of the effect DiagnosticPattern")
  effectPattern: string;

  @doc("Causal confidence (0.0-1.0)")
  @minValue(0.0)
  @maxValue(1.0)
  strength: float64;

  @doc("Time window for correlation (e.g. 5m, 1h)")
  temporalWindow?: string;
}


// ===== Flattened from: core/specs/intelligence/diagnostic-patterns.tsp =====
// =============================================================================
// QYL v2.0 - Diagnostic Patterns
// =============================================================================
// A named combination of signals that identifies a known failure mode.
// All signals in a pattern must match (conjunction). Multiple patterns can
// match the same telemetry — the engine returns all matches ranked by confidence.
// =============================================================================



namespace Qyl.Intelligence;

@doc("Classification category for diagnostic patterns")
enum PatternCategory {
  @doc("Exception and error patterns")
  error: "error",

  @doc("Performance degradation")
  latency: "latency",

  @doc("Token/cost anomalies")
  cost: "cost",

  @doc("Service health patterns")
  availability: "availability",

  @doc("GenAI-specific failure modes")
  genai: "genai",

  @doc("Database and storage patterns")
  data: "data",
}

@doc("Named combination of signals identifying a known failure mode")
model DiagnosticPattern {
  @doc("Unique pattern identifier (e.g. genai_rate_limit)")
  id: string;

  @doc("Pattern classification")
  category: PatternCategory;

  @doc("Signals that must all match (conjunction)")
  signals: Signal[];

  @doc("What this pattern means diagnostically")
  hypothesis: string;

  @doc("Base confidence weight (0.0-1.0)")
  @minValue(0.0)
  @maxValue(1.0)
  confidence: float64;
}


// ===== Flattened from: core/specs/intelligence/investigation-strategies.tsp =====
// =============================================================================
// QYL v2.0 - Investigation Strategies
// =============================================================================
// A deterministic sequence of steps to investigate a matched pattern.
// The LLM does not invent investigation paths — it selects from known
// strategies and interprets results.
// =============================================================================



namespace Qyl.Intelligence;

@doc("Single step in an investigation strategy")
model InvestigationStep {
  @doc("What to do (e.g. query_traces, get_code_location, compare_deployments)")
  action: string;

  @doc("DuckDB query template or MCP tool name")
  query: string;

  @doc("Human-readable explanation of this step")
  description: string;
}

@doc("Deterministic investigation sequence triggered by a matched pattern")
model InvestigationStrategy {
  @doc("Unique strategy identifier")
  id: string;

  @doc("Trigger — pattern ID or category:X for category-wide triggers")
  triggerPattern: string;

  @doc("Ordered investigation steps")
  steps: InvestigationStep[];
}


// ===== Flattened from: core/specs/intelligence/main.tsp =====
// =============================================================================
// QYL v2.0 - Telemetry Intelligence Model
// =============================================================================
// Canonical reasoning model over telemetry data. Schema-driven, generated,
// deterministic. Defines diagnostic patterns, causal rules, and investigation
// strategies as typed data consumed by Loom, MCP, and dashboard.
//
// Source of truth: specs/telemetry-intelligence.md
// =============================================================================




// ===== Flattened from: core/specs/intelligence/signals.tsp =====
// =============================================================================
// QYL v2.0 - Signal Primitive
// =============================================================================
// The atomic unit of telemetry observation. A single attribute condition.
// Signals reference semconv attributes and promoted DuckDB columns only.
// =============================================================================



namespace Qyl.Intelligence;

@doc("Comparison operator for signal evaluation")
enum SignalOperator {
  @doc("Equals")
  eq: "eq",

  @doc("Not equals")
  neq: "neq",

  @doc("Greater than")
  gt: "gt",

  @doc("Greater than or equal")
  gte: "gte",

  @doc("Less than")
  lt: "lt",

  @doc("Less than or equal")
  lte: "lte",

  @doc("String contains")
  contains: "contains",

  @doc("Attribute is non-null")
  exists: "exists",

  @doc("Attribute is null")
  not_exists: "not_exists",

  @doc("Regex match")
  matches: "matches",

  @doc("Value in set (comma-separated)")
  in_set: "in",
}

@doc("Atomic telemetry observation — a single attribute condition")
model Signal {
  @doc("Telemetry attribute name (semconv or promoted column)")
  attribute: string;

  @doc("Comparison operator")
  operator: SignalOperator;

  @doc("Expected value (type-coerced at evaluation time). Omit for exists/not_exists.")
  value?: string;
}


// ===== Flattened from: core/specs/intelligence/seed/patterns.tsp =====
// =============================================================================
// QYL v2.0 - Seed Diagnostic Patterns (v1)
// =============================================================================
// 19 initial diagnostic patterns (10 infra + 9 agent behavioral)
// Source: specs/telemetry-intelligence.md §5.1 + Microsoft Research AgentRx (March 2026)
// These compile to static registries in DiagnosticPatterns.g.cs.
//
// Signal attributes reference:
//   - OTel semconv promoted columns (status_code, duration_ns, etc.)
//   - qyl promoted columns (gen_ai_provider_name, gen_ai_stop_reason, etc.)
//   - Computed fields (occurrence_rate, span_count_under_parent, etc.)
// =============================================================================

namespace Qyl.Intelligence.Seed;

// ---------------------------------------------------------------------------
// Pattern: genai_rate_limit
// Category: genai
// Signals: status_code=2, gen_ai_provider_name exists, error_type contains rate_limit
// Hypothesis: Provider throttling. Check quota, reduce concurrency, add backoff.
// Confidence: 0.9
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: genai_token_exhaustion
// Category: genai
// Signals: gen_ai_stop_reason=length
// Hypothesis: Context window exceeded. Reduce prompt size or switch to larger model.
// Confidence: 0.85
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: genai_content_filter
// Category: genai
// Signals: gen_ai_stop_reason contains content_filter
// Hypothesis: Content policy violation. Review prompt content.
// Confidence: 0.95
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: db_timeout
// Category: data
// Signals: exception_type=TimeoutException, db.system.name exists, duration_ns > 2000000000
// Hypothesis: Database query timeout. Check query plan, connection pool, lock contention.
// Confidence: 0.85
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: db_n_plus_one
// Category: data
// Signals: db.system.name exists, parent_span_id exists, span_count_under_parent > 10
// Hypothesis: N+1 query pattern. Batch or prefetch related data.
// Confidence: 0.80
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: http_5xx_cluster
// Category: error
// Signals: http.response.status_code gte 500, occurrence_rate > baseline * 3
// Hypothesis: Server error spike. Check recent deployments and upstream dependencies.
// Confidence: 0.75
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: deployment_regression
// Category: error
// Signals: error_type exists, first_seen_at > last_deployment_time
// Hypothesis: New error class after deployment. Compare with previous version.
// Confidence: 0.80
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: cascading_timeout
// Category: latency
// Signals: exception_type contains Timeout, downstream_service_error=true
// Hypothesis: Upstream failure causing downstream timeouts. Investigate root service first.
// Confidence: 0.70
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: memory_pressure_latency
// Category: latency
// Signals: process.runtime.dotnet.gc.duration gt 100, avg_latency > p99_baseline
// Hypothesis: GC pressure causing latency. Check memory allocation patterns.
// Confidence: 0.65
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: cost_spike
// Category: cost
// Signals: gen_ai_cost_usd > daily_average * 3
// Hypothesis: Abnormal cost increase. Identify the model, service, and session responsible.
// Confidence: 0.75
// ---------------------------------------------------------------------------

// =============================================================================
// Agent behavioral failure patterns (AgentRx taxonomy)
// Source: Microsoft Research AgentRx, March 2026
// 9 failure categories from Magentic-One and TAU-Retail benchmarks
// =============================================================================

// ---------------------------------------------------------------------------
// Pattern: agent_intent_plan_misalignment
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.operation.name=invoke_agent, status_code=2
// Hypothesis: Agent plan diverges from user intent. Review task decomposition.
// Confidence: 0.70
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_misinterpret_tool_info
// Category: agent
// Signals: gen_ai.tool.name exists, gen_ai.tool.call.id exists, error_type contains tool
// Hypothesis: Agent misinterpreted tool output or schema.
// Confidence: 0.75
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_rai_policy_violation
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.stop_reason contains content_filter
// Hypothesis: Agent triggered responsible AI policy violation.
// Confidence: 0.95
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_plan_adherence_failure
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.operation.name=invoke_agent, gen_ai.usage.output_tokens>0, child_span_count>20
// Hypothesis: Agent deviated from its own plan. Excessive tool calls suggest improvisation.
// Confidence: 0.65
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_invent_new_info
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.operation.name=invoke_agent
// Hypothesis: Agent fabricated information not in tool outputs. Cross-reference claims vs results.
// Confidence: 0.60
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_invalid_invocation
// Category: agent
// Signals: gen_ai.tool.name exists, error_type contains invalid, gen_ai.agent.name exists
// Hypothesis: Agent called tool with invalid arguments or nonexistent name.
// Confidence: 0.85
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_hallucination_doubt
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.usage.output_tokens>500, gen_ai.tool.call.id not_exists
// Hypothesis: Long response without tool grounding. High hallucination risk.
// Confidence: 0.55
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_instruction_adherence_failure
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.operation.name=invoke_agent
// Hypothesis: Agent ignored or contradicted system prompt constraints.
// Confidence: 0.60
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Pattern: agent_underspecified_intent
// Category: agent
// Signals: gen_ai.agent.name exists, gen_ai.operation.name=invoke_agent, gen_ai.usage.input_tokens<50
// Hypothesis: Ambiguous request with insufficient context for reliable execution.
// Confidence: 0.65
// ---------------------------------------------------------------------------


// ===== Flattened from: core/specs/intelligence/seed/rules.tsp =====
// =============================================================================
// QYL v2.0 - Seed Causal Rules (v1)
// =============================================================================
// 6 initial causal rules. Source: specs/telemetry-intelligence.md §5.2
// These compile to static registries in CausalRules.g.cs.
// =============================================================================

namespace Qyl.Intelligence.Seed;

// ---------------------------------------------------------------------------
// Rule: deploy_causes_regression
// Cause: deployment_regression → Effect: http_5xx_cluster
// Strength: 0.85, Window: 1h
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Rule: rate_limit_causes_cascade
// Cause: genai_rate_limit → Effect: cascading_timeout
// Strength: 0.70, Window: 5m
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Rule: db_timeout_causes_http_error
// Cause: db_timeout → Effect: http_5xx_cluster
// Strength: 0.80, Window: 1m
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Rule: n_plus_one_causes_db_timeout
// Cause: db_n_plus_one → Effect: db_timeout
// Strength: 0.75, Window: 30s
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Rule: memory_causes_timeout
// Cause: memory_pressure_latency → Effect: cascading_timeout
// Strength: 0.65, Window: 5m
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Rule: token_exhaustion_causes_cost
// Cause: genai_token_exhaustion → Effect: cost_spike
// Strength: 0.60, Window: 1h
// ---------------------------------------------------------------------------


// ===== Flattened from: core/specs/intelligence/seed/strategies.tsp =====
// =============================================================================
// QYL v2.0 - Seed Investigation Strategies (v1)
// =============================================================================
// 4 initial investigation strategies. Source: specs/telemetry-intelligence.md §5.3
// These compile to static registries in InvestigationStrategies.g.cs.
//
// Trigger patterns use category:X syntax for category-wide triggers.
// Query templates use DuckDB SQL against the telemetry data model schema.
// =============================================================================

namespace Qyl.Intelligence.Seed;

// ---------------------------------------------------------------------------
// Strategy: investigate_error_issue
// Trigger: category:error (any error category pattern)
// Steps:
//   1. get_issue — SELECT * FROM error_issues WHERE id = ?
//   2. get_events — SELECT * FROM error_issue_events WHERE issue_id = ? ORDER BY timestamp DESC LIMIT 10
//   3. get_traces — SELECT * FROM spans WHERE trace_id IN (?) ORDER BY start_time_unix_nano
//   4. get_code_location — SELECT code_filepath, code_function, code_lineno FROM spans WHERE span_id = ?
//   5. correlate_deployment — SELECT * FROM deployments WHERE service_name = ? AND start_time <= ? ORDER BY start_time DESC LIMIT 1
//   6. check_fix_history — SELECT * FROM fix_runs WHERE issue_id = ?
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Strategy: investigate_latency
// Trigger: category:latency (any latency category pattern)
// Steps:
//   1. identify_service — SELECT service_name, AVG(duration_ns), PERCENTILE_CONT(0.99) ... GROUP BY service_name
//   2. compare_distributions — SELECT duration_ns FROM spans WHERE service_name = ? AND start_time BETWEEN ? AND ?
//   3. find_regression_window — Time-series analysis of p99 latency
//   4. correlate_deployment — SELECT * FROM deployments WHERE service_name = ? AND start_time <= ? ORDER BY start_time DESC LIMIT 1
//   5. inspect_slow_spans — SELECT * FROM spans WHERE service_name = ? AND duration_ns > ? ORDER BY duration_ns DESC LIMIT 20
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Strategy: investigate_cost
// Trigger: category:cost (any cost category pattern)
// Steps:
//   1. identify_model — SELECT gen_ai_request_model, SUM(gen_ai_cost_usd) ... GROUP BY gen_ai_request_model
//   2. identify_service — SELECT service_name, SUM(gen_ai_cost_usd) ... GROUP BY service_name
//   3. identify_session — SELECT session_id, SUM(gen_ai_cost_usd) ... GROUP BY session_id ORDER BY 2 DESC
//   4. trace_to_root — Follow session → traces → spans
//   5. compare_to_baseline — Compare current period vs previous period
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Strategy: investigate_genai
// Trigger: category:genai (any genai category pattern)
// Steps:
//   1. get_error_details — SELECT * FROM spans WHERE status_code = 2 AND gen_ai_provider_name IS NOT NULL
//   2. check_provider_status — Evaluate gen_ai_provider_name + error frequency
//   3. analyze_token_usage — SELECT gen_ai_input_tokens, gen_ai_output_tokens FROM spans WHERE gen_ai_request_model = ?
//   4. check_prompt_patterns — Inspect spans around the error for prompt size trends
//   5. suggest_mitigation — Pattern-specific recommendation
// ---------------------------------------------------------------------------


// ===== Flattened from: core/specs/models/agent/agent-run.tsp =====
// =============================================================================
// QYL v2.0 - Agent Run Storage Model (OTel v1.40)
// =============================================================================
// Materialized agent run records extracted from OTLP spans.
// Maps OTel GenAI semantic convention attributes to DuckDB columns.
//
// OTel semconv mapping:
//   gen_ai.agent.name        → agent_name
//   gen_ai.agent.id          → agent_id
//   gen_ai.agent.description → agent_description
//   gen_ai.operation.name    → operation_name
//   gen_ai.request.model     → gen_ai_request_model
//   gen_ai.provider.name     → gen_ai_provider_name
//   gen_ai.usage.input_tokens  → gen_ai_input_tokens
//   gen_ai.usage.output_tokens → gen_ai_output_tokens
// =============================================================================



namespace Qyl.Domains.Agent.Run;

// =============================================================================
// Agent Run Status
// =============================================================================

@doc("Agent run execution status")
enum AgentRunStatus {
  @doc("Agent is currently executing")
  running: "running",

  @doc("Agent completed successfully")
  completed: "completed",

  @doc("Agent execution failed")
  failed: "failed",

  @doc("Agent execution was cancelled")
  cancelled: "cancelled",
}

// =============================================================================
// Agent Run Entity - Storage Model
// =============================================================================

@doc("Agent run entity materialized from OTLP spans with gen_ai.agent.* attributes")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "agent_runs")
@TypeSpec.OpenAPI.extension("x-csharp-type", "AgentRunEntity")
model AgentRunEntity {
  // === Identifiers ===

  @doc("Unique agent run identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "run_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  runId: string;

  @doc("Parent OTel trace identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_agent_runs_trace_id")
  traceId: TraceId;

  @doc("Root span ID of the agent invocation")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "span_id")
  spanId?: SpanId;

  @doc("Parent run ID for nested agent invocations")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "parent_run_id")
  parentRunId?: string;

  @doc("Session identifier for grouping related runs")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "session_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_agent_runs_session_id")
  sessionId?: SessionId;

  // === Agent Identity (OTel gen_ai.agent.*) ===

  @doc("Agent name - OTel: gen_ai.agent.name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "agent_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_agent_runs_agent_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  agentName: string;

  @doc("Agent identifier - OTel: gen_ai.agent.id")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "agent_id")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  agentId?: string;

  @doc("Agent description - OTel: gen_ai.agent.description")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "agent_description")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  agentDescription?: string;

  // === GenAI Attributes (OTel gen_ai.*) ===

  @doc("Operation name - OTel: gen_ai.operation.name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "operation_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  operationName?: string;

  @doc("LLM model used - OTel: gen_ai.request.model")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_request_model")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_agent_runs_model")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiRequestModel?: string;

  @doc("Provider name - OTel: gen_ai.provider.name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_provider_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_agent_runs_provider")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiProviderName?: string;

  // === Execution State ===

  @doc("Execution status")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "status")
  status: AgentRunStatus;

  // === Token Usage (OTel gen_ai.usage.*) ===

  @doc("Input/prompt tokens - OTel: gen_ai.usage.input_tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_input_tokens")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiInputTokens?: TokenCount;

  @doc("Output/completion tokens - OTel: gen_ai.usage.output_tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_output_tokens")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiOutputTokens?: TokenCount;

  @doc("Estimated cost in USD")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_cost_usd")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiCostUsd?: CostUsd;

  @doc("Number of tool calls made")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "tool_call_count")
  toolCallCount?: int32;

  // === Timing ===

  @doc("Start timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_time_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_agent_runs_start_time")
  startTimeUnixNano: int64;

  @doc("End timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time_unix_nano")
  endTimeUnixNano?: int64;

  @doc("Duration in nanoseconds")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "duration_ns")
  durationNs?: DurationNs;

  // === Service Info ===

  @doc("Service name from resource attributes")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "service_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  serviceName?: string;

  // === Error ===

  @doc("Error message if failed")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "error_message")
  errorMessage?: string;

  // === Attributes (JSON Blobs) ===

  @doc("Additional attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "attributes_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  attributesJson?: string;

  // === Internal ===

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}



// ===== Flattened from: core/specs/otel/enums.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry Core Enumerations
// =============================================================================
// All OTel standard enumerations following Semantic Conventions v1.40.
// =============================================================================

namespace Qyl.OTel.Enums;

// =============================================================================
// Span Enumerations
// =============================================================================

@doc("Span kind describing the relationship between spans")
enum SpanKind {
  @doc("Default. Internal operation within the application")
  unspecified: 0,

  @doc("Internal operation within the application")
  internal: 1,

  @doc("Server-side operation handling a request")
  server: 2,

  @doc("Client-side operation making a request")
  client: 3,

  @doc("Producer creating a message for async processing")
  producer: 4,

  @doc("Consumer processing a message")
  consumer: 5,
}

@doc("Span status code")
enum SpanStatusCode {
  @doc("Status not set")
  unset: 0,

  @doc("Operation completed successfully")
  ok: 1,

  @doc("Operation failed with an error")
  error: 2,
}

@doc("Span flags as defined in W3C Trace Context")
enum SpanFlags {
  @doc("No flags set")
  none: 0x00,

  @doc("Trace is sampled")
  sampled: 0x01,

  @doc("Trace is random (non-deterministic)")
  random: 0x02,
}

// =============================================================================
// Log Enumerations
// =============================================================================

@doc("Log severity number following OTel specification (1-24)")
enum SeverityNumber {
  @doc("Unspecified severity")
  unspecified: 0,

  @doc("TRACE level")
  trace: 1,

  @doc("TRACE2 level")
  trace2: 2,

  @doc("TRACE3 level")
  trace3: 3,

  @doc("TRACE4 level")
  trace4: 4,

  @doc("DEBUG level")
  debug: 5,

  @doc("DEBUG2 level")
  debug2: 6,

  @doc("DEBUG3 level")
  debug3: 7,

  @doc("DEBUG4 level")
  debug4: 8,

  @doc("INFO level")
  info: 9,

  @doc("INFO2 level")
  info2: 10,

  @doc("INFO3 level")
  info3: 11,

  @doc("INFO4 level")
  info4: 12,

  @doc("WARN level")
  warn: 13,

  @doc("WARN2 level")
  warn2: 14,

  @doc("WARN3 level")
  warn3: 15,

  @doc("WARN4 level")
  warn4: 16,

  @doc("ERROR level")
  error: 17,

  @doc("ERROR2 level")
  error2: 18,

  @doc("ERROR3 level")
  error3: 19,

  @doc("ERROR4 level")
  error4: 20,

  @doc("FATAL level")
  fatal: 21,

  @doc("FATAL2 level")
  fatal2: 22,

  @doc("FATAL3 level")
  fatal3: 23,

  @doc("FATAL4 level")
  fatal4: 24,
}

@doc("Log severity text (human-readable)")
enum SeverityText {
  @doc("Trace level logging")
  TRACE: "TRACE",

  @doc("Debug level logging")
  DEBUG: "DEBUG",

  @doc("Informational logging")
  INFO: "INFO",

  @doc("Warning level logging")
  WARN: "WARN",

  @doc("Error level logging")
  ERROR: "ERROR",

  @doc("Fatal/critical level logging")
  FATAL: "FATAL",
}

// =============================================================================
// Metric Enumerations
// =============================================================================

@doc("Metric data type")
enum MetricType {
  @doc("Gauge - instantaneous value")
  gauge: "gauge",

  @doc("Sum - monotonic or non-monotonic counter")
  sum: "sum",

  @doc("Histogram - distribution of values")
  histogram: "histogram",

  @doc("Exponential histogram - distribution with exponential buckets")
  exponentialHistogram: "exponential_histogram",

  @doc("Summary - pre-aggregated quantiles")
  summary: "summary",
}

@doc("Aggregation temporality for metrics")
enum AggregationTemporality {
  @doc("Unspecified temporality")
  unspecified: 0,

  @doc("Delta - values since last report")
  delta: 1,

  @doc("Cumulative - values since start")
  cumulative: 2,
}

@doc("Data point flags")
enum DataPointFlags {
  @doc("No flags set")
  none: 0,

  @doc("Value is absent (no measurement)")
  noRecordedValue: 1,
}

@doc("Metric instrument kinds per OTel specification")
enum InstrumentKind {
  @doc("Counter - monotonically increasing cumulative sum")
  counter: "Counter",

  @doc("UpDownCounter - can increase or decrease")
  upDownCounter: "UpDownCounter",

  @doc("Histogram - distribution of values with buckets")
  histogram: "Histogram",

  @doc("Gauge - instantaneous value at read time")
  gauge: "Gauge",

  @doc("Observable counter - async counter with callback")
  observableCounter: "ObservableCounter",

  @doc("Observable gauge - async gauge with callback")
  observableGauge: "ObservableGauge",

  @doc("Observable up-down counter - async up-down with callback")
  observableUpDownCounter: "ObservableUpDownCounter",
}

// =============================================================================
// Resource Enumerations
// =============================================================================

@doc("Telemetry SDK language")
enum TelemetrySdkLanguage {
  @doc("C++")
  cpp: "cpp",

  @doc(".NET (C#, F#, VB.NET)")
  dotnet: "dotnet",

  @doc("Erlang")
  erlang: "erlang",

  @doc("Go")
  go: "go",

  @doc("Java")
  java: "java",

  @doc("Node.js")
  nodejs: "nodejs",

  @doc("PHP")
  php: "php",

  @doc("Python")
  python: "python",

  @doc("Ruby")
  ruby: "ruby",

  @doc("Rust")
  rust: "rust",

  @doc("Swift")
  swift: "swift",

  @doc("Web browser JavaScript")
  webjs: "webjs",
}

// =============================================================================
// Error Enumerations
// =============================================================================

@doc("Standardized error types")
enum ErrorType {
  @doc("Generic error")
  error: "_OTHER",

  @doc("Timeout error")
  timeout: "TIMEOUT",

  @doc("Connection refused")
  connectionRefused: "CONNECTION_REFUSED",

  @doc("DNS resolution failed")
  dnsError: "DNS_ERROR",

  @doc("TLS/SSL handshake error")
  tlsError: "TLS_ERROR",

  @doc("Authentication failed")
  authError: "AUTH_ERROR",

  @doc("Permission denied")
  permissionDenied: "PERMISSION_DENIED",

  @doc("Resource not found")
  notFound: "NOT_FOUND",

  @doc("Resource exhausted/rate limited")
  resourceExhausted: "RESOURCE_EXHAUSTED",

  @doc("Invalid argument")
  invalidArgument: "INVALID_ARGUMENT",

  @doc("Operation cancelled")
  cancelled: "CANCELLED",
}

// =============================================================================
// Event Domain Types
// =============================================================================

@doc("Event domain (namespace for event types)")
enum EventDomain {
  @doc("Browser/RUM events")
  browser: "browser",

  @doc("Device events")
  device: "device",

  @doc("Kubernetes events")
  k8s: "k8s",

  @doc("User/session lifecycle events")
  session: "session",

  @doc("GenAI events")
  genAi: "gen_ai",

  @doc("Exception events")
  exception: "exception",

  @doc("Log events")
  log: "log",
}

// =============================================================================
// Profile Enumerations (v1development, Development stability)
// =============================================================================

@doc("Original payload format for profile data")
enum OriginalPayloadFormat {
  @doc("Google pprof format (original_payload SHOULD NOT be populated — lossless mapping)")
  pprof: "pprof",

  @doc("Java Flight Recorder")
  jfr: "jfr",

  @doc("Linux perf.data format")
  linuxPerf: "linux_perf",
}

@doc("Profile frame type — interpreter or compiler of a single frame")
enum ProfileFrameType {
  @doc("Erlang (BEAM VM)")
  beam: "beam",

  @doc("Python (CPython)")
  cpython: "cpython",

  @doc(".NET")
  dotnet: "dotnet",

  @doc("Go")
  go: "go",

  @doc("JVM")
  jvm: "jvm",

  @doc("OS Kernel")
  kernel: "kernel",

  @doc("C, C++, Go, Rust (use more precise value if possible)")
  native: "native",

  @doc("Perl")
  perl: "perl",

  @doc("PHP")
  php: "php",

  @doc("Ruby")
  ruby: "ruby",

  @doc("Rust")
  rust: "rust",

  @doc("V8 JavaScript engine")
  v8js: "v8js",
}


// ===== Flattened from: core/specs/models/agent/tool-call.tsp =====
// =============================================================================
// QYL v2.0 - Tool Call Storage Model (OTel v1.40)
// =============================================================================
// Materialized tool call records extracted from OTLP spans.
// Maps OTel GenAI tool semantic convention attributes to DuckDB columns.
//
// OTel semconv mapping:
//   gen_ai.tool.name           → tool_name
//   gen_ai.tool.type           → tool_type
//   gen_ai.tool.call.id        → call_id
//   gen_ai.tool.call.arguments → arguments_json
//   gen_ai.tool.call.result    → result_json
//   gen_ai.tool.description    → tool_description
//   gen_ai.operation.name      → operation_name (= "execute_tool")
// =============================================================================



namespace Qyl.Domains.Agent.ToolCall;

// =============================================================================
// Tool Call Status
// =============================================================================

@doc("Tool call execution status")
enum ToolCallStatus {
  @doc("Tool is currently executing")
  running: "running",

  @doc("Tool completed successfully")
  completed: "completed",

  @doc("Tool execution failed")
  failed: "failed",
}

// =============================================================================
// Tool Call Entity - Storage Model
// =============================================================================

@doc("Tool call entity materialized from OTLP spans with gen_ai.tool.* attributes")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "tool_calls")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ToolCallEntity")
model ToolCallEntity {
  // === Identifiers ===

  @doc("Unique tool call identifier - OTel: gen_ai.tool.call.id")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "call_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  callId: string;

  @doc("Parent agent run identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "run_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_tool_calls_run_id")
  runId: string;

  @doc("Parent OTel trace identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_tool_calls_trace_id")
  traceId: TraceId;

  @doc("Associated OTel span identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "span_id")
  spanId: SpanId;

  // === Tool Identity (OTel gen_ai.tool.*) ===

  @doc("Tool name - OTel: gen_ai.tool.name (e.g., 'Read', 'Bash', 'Edit', 'TodoWrite')")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "tool_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_tool_calls_tool_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  toolName: string;

  @doc("Tool type categorization - OTel: gen_ai.tool.type (e.g., 'function', 'extension')")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "tool_type")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  toolType?: string;

  @doc("Tool description - OTel: gen_ai.tool.description")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "tool_description")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  toolDescription?: string;

  // === Tool Call Data (OTel gen_ai.tool.call.*) ===

  @doc("Tool input arguments as JSON - OTel: gen_ai.tool.call.arguments")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "arguments_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  argumentsJson?: string;

  @doc("Tool output result as JSON - OTel: gen_ai.tool.call.result")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "result_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  resultJson?: string;

  // === Execution State ===

  @doc("Execution status")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "status")
  status: ToolCallStatus;

  // === Timing ===

  @doc("Tool call start time in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_time_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_tool_calls_start_time")
  startTimeUnixNano: int64;

  @doc("Tool call end time in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time_unix_nano")
  endTimeUnixNano?: int64;

  @doc("Duration in nanoseconds")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "duration_ns")
  durationNs?: DurationNs;

  // === Error ===

  @doc("Error message if failed")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "error_message")
  errorMessage?: string;

  // === Ordering ===

  @doc("Execution order within the agent run")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "sequence_number")
  @minValue(0)
  sequenceNumber: int32;

  // === Internal ===

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}


// ===== Flattened from: core/specs/models/agent/workflow-checkpoint.tsp =====
// =============================================================================
// QYL v2.0 - Workflow Checkpoint Storage Model (OTel v1.40)
// =============================================================================
// Checkpoint state persistence for workflow execution recovery.
// Enables resume-from-checkpoint after failures or cancellations.
// =============================================================================



namespace Qyl.Domains.Agent.Checkpoint;

// =============================================================================
// Workflow Checkpoint Entity - Storage Model
// =============================================================================

@doc("Workflow checkpoint entity for persisting workflow node state for recovery")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "workflow_checkpoints")
@TypeSpec.OpenAPI.extension("x-csharp-type", "WorkflowCheckpointEntity")
model WorkflowCheckpointEntity {
  // === Identifiers ===

  @doc("Unique checkpoint identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "checkpoint_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  checkpointId: string;

  @doc("Parent workflow execution identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "execution_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_checkpoints_execution_id")
  executionId: string;

  // === Node State ===

  @doc("Workflow node identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "node_id")
  nodeId: string;

  @doc("Serialized node state as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "state_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  stateJson: string;

  // === Ordering ===

  @doc("Checkpoint sequence number within the execution")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "sequence_number")
  @minValue(0)
  sequenceNumber: int32;

  // === Timing ===

  @doc("Checkpoint creation time in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_checkpoints_created_at")
  createdAtUnixNano: int64;
}


// ===== Flattened from: core/specs/models/agent/workflow-execution.tsp =====
// =============================================================================
// QYL v2.0 - Workflow Execution Storage Model (OTel v1.40)
// =============================================================================
// Workflow execution tracking for multi-step AI agent workflows.
// Formalizes the existing manual DuckDB table into TypeSpec.
// =============================================================================



namespace Qyl.Domains.Agent.Workflow;

// =============================================================================
// Workflow Execution Status
// =============================================================================

@doc("Workflow execution status")
enum WorkflowExecutionStatus {
  @doc("Workflow is pending execution")
  pending: "pending",

  @doc("Workflow is currently running")
  running: "running",

  @doc("Workflow completed successfully")
  completed: "completed",

  @doc("Workflow execution failed")
  failed: "failed",

  @doc("Workflow execution was cancelled")
  cancelled: "cancelled",
}

// =============================================================================
// Workflow Trigger Type
// =============================================================================

@doc("What triggered a workflow execution")
enum WorkflowTrigger {
  @doc("Manually triggered by user or agent")
  manual: "manual",

  @doc("Triggered by schedule/cron")
  schedule: "schedule",

  @doc("Triggered by event (e.g., error, alert)")
  event: "event",

  @doc("Triggered by another workflow")
  workflow: "workflow",
}

// =============================================================================
// Workflow Execution Entity - Storage Model
// =============================================================================

@doc("Workflow execution entity for tracking multi-step agent workflows")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "workflow_executions")
@TypeSpec.OpenAPI.extension("x-csharp-type", "WorkflowExecutionEntity")
model WorkflowExecutionEntity {
  // === Identifiers ===

  @doc("Unique execution identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "execution_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  executionId: string;

  @doc("Associated OTel trace identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_workflow_executions_trace_id")
  traceId?: TraceId;

  // === Workflow Identity ===

  @doc("Workflow name identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "workflow_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_workflow_executions_name")
  workflowName: string;

  @doc("What triggered the workflow")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trigger")
  trigger: WorkflowTrigger;

  // === Execution State ===

  @doc("Execution status")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "status")
  status: WorkflowExecutionStatus;

  // === I/O ===

  @doc("Workflow input as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "input_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  inputJson?: string;

  @doc("Workflow output as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "output_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  outputJson?: string;

  // === Token Usage (OTel gen_ai.usage.*) ===

  @doc("Total input tokens - OTel: gen_ai.usage.input_tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_input_tokens")
  genAiInputTokens?: TokenCount;

  @doc("Total output tokens - OTel: gen_ai.usage.output_tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_output_tokens")
  genAiOutputTokens?: TokenCount;

  @doc("Estimated cost in USD")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_cost_usd")
  genAiCostUsd?: CostUsd;

  // === Progress ===

  @doc("Total number of workflow nodes")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "node_count")
  nodeCount?: int32;

  @doc("Number of completed nodes")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "completed_nodes")
  completedNodes?: int32;

  // === Timing ===

  @doc("Start timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_time_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_workflow_executions_start_time")
  startTimeUnixNano: int64;

  @doc("End timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time_unix_nano")
  endTimeUnixNano?: int64;

  @doc("Duration in nanoseconds")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "duration_ns")
  durationNs?: DurationNs;

  // === Error ===

  @doc("Error message if failed")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "error_message")
  errorMessage?: string;

  // === Internal ===

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}


// ===== Flattened from: core/specs/models/alerting.tsp =====
// =============================================================================
// QYL v2.0 - Alerting & Autofix Entities
// =============================================================================
// Alert rules, firings, fix runs, artifacts, policy gates.
// =============================================================================



namespace Qyl.Domains.Alerting;

// =============================================================================
// Alert Rule
// =============================================================================

@doc("Alert rule definition")
@extension("x-duckdb-table", "alert_rules")
@extension("x-csharp-type", "AlertRuleEntity")
model AlertRuleEntity {
  @doc("Rule ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Rule name")
  name: string;

  @doc("Rule description")
  description?: string;

  @doc("Rule type")
  @encodedName("application/json", "rule_type")
  ruleType: AlertRuleType;

  @doc("Condition definition")
  @encodedName("application/json", "condition_json")
  @extension("x-duckdb-type", "JSON")
  conditionJson: string;

  @doc("Threshold definition")
  @encodedName("application/json", "threshold_json")
  @extension("x-duckdb-type", "JSON")
  thresholdJson?: string;

  @doc("Target type for evaluation")
  @encodedName("application/json", "target_type")
  targetType: string;

  @doc("Target filter")
  @encodedName("application/json", "target_filter_json")
  @extension("x-duckdb-type", "JSON")
  targetFilterJson?: string;

  @doc("Alert severity")
  severity: AlertSeverity;

  @doc("Cooldown between firings in seconds")
  @encodedName("application/json", "cooldown_seconds")
  cooldownSeconds: int32;

  @doc("Notification channels")
  @encodedName("application/json", "notification_channels_json")
  @extension("x-duckdb-type", "JSON")
  notificationChannelsJson?: string;

  @doc("Whether rule is enabled")
  enabled: boolean;

  @doc("Last trigger timestamp")
  @encodedName("application/json", "last_triggered_at")
  lastTriggeredAt?: utcDateTime;

  @doc("Total trigger count")
  @encodedName("application/json", "trigger_count")
  triggerCount: int64;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

@doc("Alert rule types")
enum AlertRuleType {
  @doc("Metric threshold alert")
  threshold: "threshold",

  @doc("Error rate alert")
  errorRate: "error_rate",

  @doc("New issue alert")
  newIssue: "new_issue",

  @doc("Regression alert")
  regression: "regression",

  @doc("SLO burn rate alert")
  burnRate: "burn_rate",

  @doc("Anomaly detection alert")
  anomaly: "anomaly",

  @doc("Custom query alert")
  custom: "custom",
}

@doc("Alert severity levels")
enum AlertSeverity {
  @doc("Critical alert")
  critical: "critical",

  @doc("Warning alert")
  warning: "warning",

  @doc("Informational alert")
  info: "info",
}

// =============================================================================
// Alert Firing
// =============================================================================

@doc("Triggered alert instance")
@extension("x-duckdb-table", "alert_firings")
@extension("x-csharp-type", "AlertFiringEntity")
model AlertFiringEntity {
  @doc("Firing ID")
  @key
  id: string;

  @doc("Source rule")
  @encodedName("application/json", "rule_id")
  ruleId: string;

  @doc("Dedup fingerprint")
  fingerprint: string;

  @doc("Alert severity")
  severity: AlertSeverity;

  @doc("Alert title")
  title: string;

  @doc("Alert message")
  message?: string;

  @doc("Measured value that triggered the alert")
  @encodedName("application/json", "trigger_value")
  triggerValue?: float64;

  @doc("Threshold value")
  @encodedName("application/json", "threshold_value")
  thresholdValue?: float64;

  @doc("Additional context")
  @encodedName("application/json", "context_json")
  @extension("x-duckdb-type", "JSON")
  contextJson?: string;

  @doc("Firing status")
  status: AlertFiringStatus;

  @doc("Acknowledgment timestamp")
  @encodedName("application/json", "acknowledged_at")
  acknowledgedAt?: utcDateTime;

  @doc("Acknowledged by")
  @encodedName("application/json", "acknowledged_by")
  acknowledgedBy?: string;

  @doc("Resolution timestamp")
  @encodedName("application/json", "resolved_at")
  resolvedAt?: utcDateTime;

  @doc("Firing timestamp")
  @encodedName("application/json", "fired_at")
  firedAt: utcDateTime;

  @doc("Dedup key for suppressing duplicates")
  @encodedName("application/json", "dedup_key")
  dedupKey?: string;
}

@doc("Alert firing status")
enum AlertFiringStatus {
  @doc("Currently firing")
  firing: "firing",

  @doc("Acknowledged by operator")
  acknowledged: "acknowledged",

  @doc("Resolved (condition cleared)")
  resolved: "resolved",

  @doc("Suppressed by cooldown")
  suppressed: "suppressed",
}

// =============================================================================
// Fix Run
// =============================================================================

@doc("AI-assisted fix attempt")
@extension("x-duckdb-table", "fix_runs")
@extension("x-csharp-type", "FixRunEntity")
model FixRunEntity {
  @doc("Fix run ID")
  @key
  id: string;

  @doc("Target issue")
  @encodedName("application/json", "issue_id")
  issueId: string;

  @doc("Triggering alert firing")
  @encodedName("application/json", "alert_firing_id")
  alertFiringId?: string;

  @doc("What triggered the fix")
  @encodedName("application/json", "trigger_type")
  triggerType: FixTriggerType;

  @doc("Fix strategy")
  strategy: string;

  @doc("AI model used")
  @encodedName("application/json", "model_name")
  modelName?: string;

  @doc("AI provider")
  @encodedName("application/json", "model_provider")
  modelProvider?: string;

  @doc("Fix run status")
  status: FixRunStatus;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Tokens consumed")
  @encodedName("application/json", "tokens_used")
  tokensUsed?: int32;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;
}

@doc("Fix trigger types")
enum FixTriggerType {
  @doc("Triggered by alert")
  alert: "alert",

  @doc("Triggered manually")
  manual: "manual",

  @doc("Triggered by MCP tool")
  mcp: "mcp",

  @doc("Triggered by schedule")
  scheduled: "scheduled",
}

@doc("Fix run status")
enum FixRunStatus {
  @doc("Pending execution")
  pending: "pending",

  @doc("Running")
  running: "running",

  @doc("Awaiting approval")
  awaitingApproval: "awaiting_approval",

  @doc("Approved and applied")
  applied: "applied",

  @doc("Rejected")
  rejected: "rejected",

  @doc("Failed")
  failed: "failed",
}

// =============================================================================
// Fix Artifact
// =============================================================================

@doc("Fix run output artifact")
@extension("x-duckdb-table", "fix_artifacts")
@extension("x-csharp-type", "FixArtifactEntity")
model FixArtifactEntity {
  @doc("Artifact ID")
  @key
  id: string;

  @doc("Parent fix run")
  @encodedName("application/json", "fix_run_id")
  fixRunId: string;

  @doc("Artifact type")
  @encodedName("application/json", "artifact_type")
  artifactType: FixArtifactType;

  @doc("Artifact name")
  name: string;

  @doc("Content MIME type")
  @encodedName("application/json", "content_type")
  contentType: string;

  @doc("Artifact content")
  content?: string;

  @doc("Content hash for dedup")
  @encodedName("application/json", "content_hash")
  contentHash?: string;

  @doc("Size in bytes")
  @encodedName("application/json", "size_bytes")
  sizeBytes?: int64;

  @doc("Artifact metadata")
  @encodedName("application/json", "metadata_json")
  @extension("x-duckdb-type", "JSON")
  metadataJson?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

@doc("Fix artifact types")
enum FixArtifactType {
  @doc("Code patch (diff)")
  patch: "patch",

  @doc("Execution log")
  log: "log",

  @doc("Analysis report")
  report: "report",

  @doc("Prompt used for AI")
  prompt: "prompt",

  @doc("AI model response")
  response: "response",

  @doc("Test results")
  testResults: "test_results",
}

// =============================================================================
// Fix Policy Gate
// =============================================================================

@doc("Autofix policy gate decision")
@extension("x-duckdb-table", "fix_policy_gates")
@extension("x-csharp-type", "FixPolicyGateEntity")
model FixPolicyGateEntity {
  @doc("Gate ID")
  @key
  id: string;

  @doc("Parent fix run")
  @encodedName("application/json", "fix_run_id")
  fixRunId: string;

  @doc("Gate type")
  @encodedName("application/json", "gate_type")
  gateType: PolicyGateType;

  @doc("Gate name")
  @encodedName("application/json", "gate_name")
  gateName: string;

  @doc("Decision")
  decision: GateDecision;

  @doc("Decision reason")
  reason?: string;

  @doc("Gate input data")
  @encodedName("application/json", "input_json")
  @extension("x-duckdb-type", "JSON")
  inputJson?: string;

  @doc("Gate output data")
  @encodedName("application/json", "output_json")
  @extension("x-duckdb-type", "JSON")
  outputJson?: string;

  @doc("Evaluation timestamp")
  @encodedName("application/json", "evaluated_at")
  evaluatedAt: utcDateTime;
}

@doc("Policy gate types")
enum PolicyGateType {
  @doc("Scope check (allowed files/directories)")
  scope: "scope",

  @doc("Safety check (no destructive changes)")
  safety: "safety",

  @doc("Test gate (tests must pass)")
  test: "test",

  @doc("Review gate (human approval)")
  review: "review",

  @doc("Budget gate (token/cost limit)")
  budget: "budget",
}

@doc("Gate decision outcomes")
enum GateDecision {
  @doc("Gate passed")
  allow: "allow",

  @doc("Gate blocked the fix")
  deny: "deny",

  @doc("Gate deferred to human")
  defer: "defer",
}


// ===== Flattened from: core/specs/models/configurator.tsp =====
// =============================================================================
// QYL v2.0 - Configurator & Code Generation Entities
// =============================================================================
// Generation profiles, selections, jobs, schema promotions.
// =============================================================================



namespace Qyl.Domains.Configurator;

// =============================================================================
// Generation Profile
// =============================================================================

@doc("Named instrumentation profile for code generation")
@extension("x-duckdb-table", "generation_profiles")
@extension("x-csharp-type", "GenerationProfileEntity")
model GenerationProfileEntity {
  @doc("Profile ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Profile name")
  name: string;

  @doc("Profile description")
  description?: string;

  @doc("Target framework (e.g. net10.0)")
  @encodedName("application/json", "target_framework")
  targetFramework: string;

  @doc("Target language")
  @encodedName("application/json", "target_language")
  targetLanguage: string;

  @doc("Semantic conventions version")
  @encodedName("application/json", "semconv_version")
  semconvVersion: string;

  @doc("Enabled features/modules")
  @encodedName("application/json", "features_json")
  @extension("x-duckdb-type", "JSON")
  featuresJson: string;

  @doc("Template customizations")
  @encodedName("application/json", "template_overrides_json")
  @extension("x-duckdb-type", "JSON")
  templateOverridesJson?: string;

  @doc("Whether this is the default profile")
  @encodedName("application/json", "is_default")
  isDefault: boolean;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

// =============================================================================
// Generation Selection
// =============================================================================

@doc("Selected semconv/feature per workspace for code generation")
@extension("x-duckdb-table", "generation_selections")
@extension("x-csharp-type", "GenerationSelectionEntity")
model GenerationSelectionEntity {
  @doc("Selection ID")
  @key
  id: string;

  @doc("Workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Profile")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Selection type (semconv_group, feature, custom_attribute)")
  @encodedName("application/json", "selection_type")
  selectionType: string;

  @doc("Selection key (e.g. http, db, genai)")
  @encodedName("application/json", "selection_key")
  selectionKey: string;

  @doc("Whether enabled")
  enabled: boolean;

  @doc("Selection-specific configuration")
  @encodedName("application/json", "config_json")
  @extension("x-duckdb-type", "JSON")
  configJson?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

// =============================================================================
// Generation Job
// =============================================================================

@doc("Code generation job entry")
@extension("x-duckdb-table", "generation_jobs")
@extension("x-csharp-type", "GenerationJobEntity")
model GenerationJobEntity {
  @doc("Job ID")
  @key
  id: string;

  @doc("Workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Profile")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Job type")
  @encodedName("application/json", "job_type")
  jobType: GenerationJobType;

  @doc("Job status")
  status: JobStatus;

  @doc("Priority (higher = more urgent)")
  priority: int32;

  @doc("Hash of inputs for dedup")
  @encodedName("application/json", "input_hash")
  inputHash?: string;

  @doc("Local path where output was written")
  @encodedName("application/json", "output_path")
  outputPath?: string;

  @doc("Hash of generated output")
  @encodedName("application/json", "output_hash")
  outputHash?: string;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Queue timestamp")
  @encodedName("application/json", "queued_at")
  queuedAt: utcDateTime;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;
}

@doc("Generation job types")
enum GenerationJobType {
  @doc("Full regeneration")
  full: "full",

  @doc("Incremental update")
  incremental: "incremental",

  @doc("Preview/dry-run")
  preview: "preview",
}

@doc("Job lifecycle status")
enum JobStatus {
  @doc("Queued for execution")
  queued: "queued",

  @doc("Currently executing")
  running: "running",

  @doc("Successfully completed")
  completed: "completed",

  @doc("Failed with error")
  failed: "failed",

  @doc("Cancelled by user")
  cancelled: "cancelled",
}

// =============================================================================
// Generation Job Event
// =============================================================================

@doc("Job lifecycle event")
@extension("x-duckdb-table", "generation_job_events")
@extension("x-csharp-type", "GenerationJobEventEntity")
model GenerationJobEventEntity {
  @doc("Event ID")
  @key
  id: string;

  @doc("Parent job")
  @encodedName("application/json", "job_id")
  jobId: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: string;

  @doc("Human-readable message")
  message?: string;

  @doc("Event-specific data")
  @encodedName("application/json", "details_json")
  @extension("x-duckdb-type", "JSON")
  detailsJson?: string;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Schema Promotion
// =============================================================================

@doc("Attribute-to-column promotion from profiles")
@extension("x-duckdb-table", "schema_promotions")
@extension("x-csharp-type", "SchemaPromotionEntity")
model SchemaPromotionEntity {
  @doc("Promotion ID")
  @key
  id: string;

  @doc("Source profile")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("JSON path in attributes_json")
  @encodedName("application/json", "source_attribute")
  sourceAttribute: string;

  @doc("Promoted column name")
  @encodedName("application/json", "target_column")
  targetColumn: string;

  @doc("DuckDB type for the column")
  @encodedName("application/json", "target_type")
  targetType: string;

  @doc("Target table (spans, logs, etc.)")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("Index type (btree, hash, or null)")
  @encodedName("application/json", "index_type")
  indexType?: string;

  @doc("Promotion status")
  status: PromotionStatus;

  @doc("Application timestamp")
  @encodedName("application/json", "applied_at")
  appliedAt?: utcDateTime;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

@doc("Schema promotion status")
enum PromotionStatus {
  @doc("Pending application")
  pending: "pending",

  @doc("Successfully applied")
  applied: "applied",

  @doc("Rolled back")
  rolledBack: "rolled_back",
}

// =============================================================================
// Schema Change Job
// =============================================================================

@doc("Schema adaptation DDL job")
@extension("x-duckdb-table", "schema_change_jobs")
@extension("x-csharp-type", "SchemaChangeJobEntity")
model SchemaChangeJobEntity {
  @doc("Job ID")
  @key
  id: string;

  @doc("Link to promotion if applicable")
  @encodedName("application/json", "promotion_id")
  promotionId?: string;

  @doc("Change type")
  @encodedName("application/json", "change_type")
  changeType: SchemaChangeType;

  @doc("Target table")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("DDL statement to execute")
  @encodedName("application/json", "ddl_statement")
  ddlStatement: string;

  @doc("Rollback DDL")
  @encodedName("application/json", "rollback_ddl")
  rollbackDdl?: string;

  @doc("Job status")
  status: JobStatus;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Queue timestamp")
  @encodedName("application/json", "queued_at")
  queuedAt: utcDateTime;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;
}

@doc("Schema change types")
enum SchemaChangeType {
  @doc("Add a column")
  addColumn: "add_column",

  @doc("Drop a column")
  dropColumn: "drop_column",

  @doc("Add an index")
  addIndex: "add_index",

  @doc("Drop an index")
  dropIndex: "drop_index",

  @doc("Alter column type")
  alterType: "alter_type",
}

// =============================================================================
// Schema Change Event
// =============================================================================

@doc("DDL execution audit event")
@extension("x-duckdb-table", "schema_change_events")
@extension("x-csharp-type", "SchemaChangeEventEntity")
model SchemaChangeEventEntity {
  @doc("Event ID")
  @key
  id: string;

  @doc("Parent job")
  @encodedName("application/json", "job_id")
  jobId: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: string;

  @doc("Human-readable message")
  message?: string;

  @doc("Event-specific data")
  @encodedName("application/json", "details_json")
  @extension("x-duckdb-type", "JSON")
  detailsJson?: string;

  @doc("Rows affected by DDL")
  @encodedName("application/json", "rows_affected")
  rowsAffected?: int64;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}


// ===== Flattened from: core/specs/models/db.tsp =====
// =============================================================================
// QYL v2.0 - Database Semantic Conventions (OTel v1.40)
// =============================================================================
// Database telemetry attributes with 45+ DBMS types.
// =============================================================================



@versioned(DbVersions)
namespace Qyl.Domains.Data.Db;

// =============================================================================
// Version History
// =============================================================================

@doc("Database semantic convention versions")
enum DbVersions {
  @doc("Legacy DB semconv")
  v1_20: "1.20.0",

  @doc("Stable DB semconv with namespace")
  v1_24: "1.24.0",

  @doc("DB semconv v1.38")
  v1_38: "1.38.0",

  @doc("Current DB semconv")
  v1_39: "1.39.0",
}

// =============================================================================
// Database Span Attributes (Complete OTel v1.40)
// =============================================================================

@doc("Database span attributes following OTel Semantic Conventions v1.40")
model DbAttributes {
  // ---------------------------------------------------------------------------
  // System & Connection
  // ---------------------------------------------------------------------------

  @doc("Database management system identifier")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Database namespace (database name, schema, etc.)")
  @encodedName("application/json", "db.namespace")
  `namespace`?: string;

  @doc("Database operation name (query, insert, delete, etc.)")
  @encodedName("application/json", "db.operation.name")
  operationName?: DbOperationName;

  @doc("Batch size (number of statements in batch)")
  @encodedName("application/json", "db.operation.batch.size")
  batchSize?: int32;

  // ---------------------------------------------------------------------------
  // Query Details
  // ---------------------------------------------------------------------------

  @doc("Database statement (query text, may be sanitized)")
  @encodedName("application/json", "db.query.text")
  queryText?: string;

  @doc("Parameterized query template")
  @encodedName("application/json", "db.query.summary")
  querySummary?: string;

  @doc("Query parameters (for prepared statements). Keys map to db.query.parameter.<key>")
  @encodedName("application/json", "db.query.parameter")
  queryParameters?: Record<string>;

  // ---------------------------------------------------------------------------
  // Collection/Table Information
  // ---------------------------------------------------------------------------

  @doc("Table name")
  @encodedName("application/json", "db.collection.name")
  collectionName?: string;

  // ---------------------------------------------------------------------------
  // Response Metrics
  // ---------------------------------------------------------------------------

  @doc("Number of rows affected/returned")
  @encodedName("application/json", "db.response.returned_rows")
  returnedRows?: int64;

  @doc("Status code from database")
  @encodedName("application/json", "db.response.status_code")
  responseStatusCode?: string;

  // ---------------------------------------------------------------------------
  // Server Information
  // ---------------------------------------------------------------------------

  @doc("Database server address")
  @encodedName("application/json", "server.address")
  serverAddress?: string;

  @doc("Database server port")
  @encodedName("application/json", "server.port")
  serverPort?: Port;

  // ---------------------------------------------------------------------------
  // Connection Pool
  // ---------------------------------------------------------------------------

  @doc("Connection pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  connectionPoolName?: string;

  @doc("Connection state")
  @encodedName("application/json", "db.client.connection.state")
  connectionState?: DbConnectionState;

  // ---------------------------------------------------------------------------
  // Deprecated Attributes (for migration)
  // ---------------------------------------------------------------------------

  @doc("Database name (deprecated - use db.namespace)")
  @encodedName("application/json", "db.name")
  @removed(DbVersions.v1_24)
  dbName?: string;

  @doc("Database statement (deprecated - use db.query.text)")
  @encodedName("application/json", "db.statement")
  @removed(DbVersions.v1_24)
  statement?: string;

  @doc("Database operation (deprecated - use db.operation.name)")
  @encodedName("application/json", "db.operation")
  @removed(DbVersions.v1_24)
  operation?: string;

  @doc("Database user (deprecated)")
  @encodedName("application/json", "db.user")
  @removed(DbVersions.v1_24)
  user?: string;
}

// =============================================================================
// Database System Enumeration (45+ DBMS Types)
// =============================================================================

@doc("Database management system identifiers")
enum DbSystem {
  // ---------------------------------------------------------------------------
  // Relational Databases
  // ---------------------------------------------------------------------------

  @doc("PostgreSQL")
  postgresql: "postgresql",

  @doc("MySQL / MariaDB")
  mysql: "mysql",

  @doc("MariaDB")
  mariadb: "mariadb",

  @doc("Microsoft SQL Server")
  mssql: "mssql",

  @doc("Oracle Database")
  oracle: "oracle",

  @doc("IBM DB2")
  db2: "db2",

  @doc("SQLite")
  sqlite: "sqlite",

  @doc("SAP HANA")
  hana: "hana",

  @doc("SAP MaxDB")
  maxdb: "maxdb",

  @doc("SAP SQL Anywhere")
  sqlanywhere: "sqlanywhere",

  @doc("Teradata")
  teradata: "teradata",

  @doc("Sybase")
  sybase: "sybase",

  @doc("Informix")
  informix: "informix",

  @doc("Firebird")
  firebird: "firebird",

  @doc("Interbase")
  interbase: "interbase",

  @doc("H2 Database")
  h2: "h2",

  @doc("HSQLDB")
  hsqldb: "hsqldb",

  @doc("Apache Derby")
  derby: "derby",

  @doc("CockroachDB")
  cockroachdb: "cockroachdb",

  @doc("TiDB")
  tidb: "tidb",

  @doc("YugabyteDB")
  yugabyte: "yugabyte",

  @doc("Vitess")
  vitess: "vitess",

  @doc("ClickHouse")
  clickhouse: "clickhouse",

  @doc("Snowflake")
  snowflake: "snowflake",

  @doc("Amazon Redshift")
  redshift: "redshift",

  @doc("Google BigQuery")
  bigquery: "bigquery",

  @doc("Azure Synapse")
  synapse: "synapse",

  @doc("Databricks")
  databricks: "databricks",

  @doc("Presto")
  presto: "presto",

  @doc("Trino")
  trino: "trino",

  @doc("Apache Hive")
  hive: "hive",

  @doc("Apache Spark SQL")
  sparksql: "sparksql",

  @doc("Apache Impala")
  impala: "impala",

  @doc("Apache Drill")
  drill: "drill",

  @doc("DuckDB")
  duckdb: "duckdb",

  @doc("Vertica")
  vertica: "vertica",

  @doc("Greenplum")
  greenplum: "greenplum",

  @doc("Netezza")
  netezza: "netezza",

  // ---------------------------------------------------------------------------
  // NoSQL Document Databases
  // ---------------------------------------------------------------------------

  @doc("MongoDB")
  mongodb: "mongodb",

  @doc("Couchbase")
  couchbase: "couchbase",

  @doc("CouchDB")
  couchdb: "couchdb",

  @doc("Amazon DocumentDB")
  documentdb: "documentdb",

  @doc("Azure Cosmos DB")
  cosmosdb: "cosmosdb",

  @doc("Google Cloud Firestore")
  firestore: "firestore",

  @doc("RavenDB")
  ravendb: "ravendb",

  @doc("ArangoDB")
  arangodb: "arangodb",

  // ---------------------------------------------------------------------------
  // Key-Value Stores
  // ---------------------------------------------------------------------------

  @doc("Redis")
  redis: "redis",

  @doc("Valkey")
  valkey: "valkey",

  @doc("KeyDB")
  keydb: "keydb",

  @doc("Memcached")
  memcached: "memcached",

  @doc("Amazon DynamoDB")
  dynamodb: "dynamodb",

  @doc("Azure Table Storage")
  azuretable: "azuretable",

  @doc("Hazelcast")
  hazelcast: "hazelcast",

  @doc("etcd")
  etcd: "etcd",

  @doc("Apache Ignite")
  ignite: "ignite",

  @doc("RocksDB")
  rocksdb: "rocksdb",

  @doc("LevelDB")
  leveldb: "leveldb",

  @doc("Berkeley DB")
  berkeleydb: "berkeleydb",

  // ---------------------------------------------------------------------------
  // Wide-Column Stores
  // ---------------------------------------------------------------------------

  @doc("Apache Cassandra")
  cassandra: "cassandra",

  @doc("ScyllaDB")
  scylla: "scylla",

  @doc("Apache HBase")
  hbase: "hbase",

  @doc("Google Cloud Bigtable")
  bigtable: "bigtable",

  // ---------------------------------------------------------------------------
  // Graph Databases
  // ---------------------------------------------------------------------------

  @doc("Neo4j")
  neo4j: "neo4j",

  @doc("Amazon Neptune")
  neptune: "neptune",

  @doc("ArangoDB (graph mode)")
  arangodbGraph: "arangodb_graph",

  @doc("JanusGraph")
  janusgraph: "janusgraph",

  @doc("TigerGraph")
  tigergraph: "tigergraph",

  @doc("Dgraph")
  dgraph: "dgraph",

  // ---------------------------------------------------------------------------
  // Time-Series Databases
  // ---------------------------------------------------------------------------

  @doc("InfluxDB")
  influxdb: "influxdb",

  @doc("TimescaleDB")
  timescaledb: "timescaledb",

  @doc("Prometheus")
  prometheus: "prometheus",

  @doc("QuestDB")
  questdb: "questdb",

  @doc("Amazon Timestream")
  timestream: "timestream",

  @doc("Apache Druid")
  druid: "druid",

  @doc("VictoriaMetrics")
  victoriametrics: "victoriametrics",

  // ---------------------------------------------------------------------------
  // Search Engines
  // ---------------------------------------------------------------------------

  @doc("Elasticsearch")
  elasticsearch: "elasticsearch",

  @doc("OpenSearch")
  opensearch: "opensearch",

  @doc("Apache Solr")
  solr: "solr",

  @doc("Splunk")
  splunk: "splunk",

  @doc("Meilisearch")
  meilisearch: "meilisearch",

  @doc("Typesense")
  typesense: "typesense",

  @doc("Algolia")
  algolia: "algolia",

  // ---------------------------------------------------------------------------
  // Vector Databases
  // ---------------------------------------------------------------------------

  @doc("Pinecone")
  pinecone: "pinecone",

  @doc("Milvus")
  milvus: "milvus",

  @doc("Weaviate")
  weaviate: "weaviate",

  @doc("Qdrant")
  qdrant: "qdrant",

  @doc("Chroma")
  chroma: "chroma",

  @doc("pgvector (PostgreSQL extension)")
  pgvector: "pgvector",

  // ---------------------------------------------------------------------------
  // Embedded & File-Based
  // ---------------------------------------------------------------------------

  @doc("Microsoft Access")
  msaccess: "msaccess",

  @doc("FileMaker")
  filemaker: "filemaker",

  @doc("FoxPro")
  foxpro: "foxpro",

  @doc("dBase")
  dbase: "dbase",

  // ---------------------------------------------------------------------------
  // Other
  // ---------------------------------------------------------------------------

  @doc("ODBC (generic)")
  odbc: "odbc",

  @doc("JDBC (generic)")
  jdbc: "jdbc",

  @doc("Other/unknown database system")
  other: "other",
}

// =============================================================================
// Database Operation Names
// =============================================================================

@doc("Database operation names")
enum DbOperationName {
  @doc("SELECT query")
  select: "SELECT",

  @doc("INSERT query")
  insert: "INSERT",

  @doc("UPDATE query")
  update: "UPDATE",

  @doc("DELETE query")
  delete: "DELETE",

  @doc("CREATE operation")
  create: "CREATE",

  @doc("DROP operation")
  drop: "DROP",

  @doc("ALTER operation")
  alter: "ALTER",

  @doc("TRUNCATE operation")
  truncate: "TRUNCATE",

  @doc("EXECUTE stored procedure")
  execute: "EXECUTE",

  @doc("CALL stored procedure")
  call: "CALL",

  @doc("BEGIN transaction")
  begin: "BEGIN",

  @doc("COMMIT transaction")
  commit: "COMMIT",

  @doc("ROLLBACK transaction")
  rollback: "ROLLBACK",

  @doc("SAVEPOINT in transaction")
  savepoint: "SAVEPOINT",

  // NoSQL Operations
  @doc("Find/query operation")
  find: "find",

  @doc("Find one operation")
  findOne: "findOne",

  @doc("Insert one operation")
  insertOne: "insertOne",

  @doc("Insert many operation")
  insertMany: "insertMany",

  @doc("Update one operation")
  updateOne: "updateOne",

  @doc("Update many operation")
  updateMany: "updateMany",

  @doc("Delete one operation")
  deleteOne: "deleteOne",

  @doc("Delete many operation")
  deleteMany: "deleteMany",

  @doc("Aggregate operation")
  aggregate: "aggregate",

  @doc("Count operation")
  count: "count",

  @doc("Get operation")
  get: "GET",

  @doc("Set operation")
  set: "SET",

  @doc("Delete operation")
  del: "DEL",

  @doc("Ping operation")
  ping: "PING",
}

// =============================================================================
// Database Connection States
// =============================================================================

@doc("Database connection states")
enum DbConnectionState {
  @doc("Connection is idle in pool")
  idle: "idle",

  @doc("Connection is in use")
  used: "used",
}

// =============================================================================
// Database Metrics
// =============================================================================

@doc("Database client operation duration metric")
model DbClientOperationDurationMetric {
  @doc("Metric name")
  name: "db.client.operation.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Database namespace")
  @encodedName("application/json", "db.namespace")
  `namespace`?: string;

  @doc("Operation name")
  @encodedName("application/json", "db.operation.name")
  operationName: DbOperationName;

  @doc("Collection/table name")
  @encodedName("application/json", "db.collection.name")
  collectionName?: string;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: string;
}

@doc("Database connection pool size metric")
model DbConnectionPoolSizeMetric {
  @doc("Metric name")
  name: "db.client.connection.count";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  poolName: string;

  @doc("Connection state")
  @encodedName("application/json", "db.client.connection.state")
  state: DbConnectionState;
}

@doc("Database connection create time metric")
model DbConnectionCreateTimeMetric {
  @doc("Metric name")
  name: "db.client.connection.create_time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  poolName: string;
}

@doc("Database connection wait time metric")
model DbConnectionWaitTimeMetric {
  @doc("Metric name")
  name: "db.client.connection.wait_time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  poolName: string;
}

@doc("Database connection use time metric")
model DbConnectionUseTimeMetric {
  @doc("Metric name")
  name: "db.client.connection.use_time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  poolName: string;
}

@doc("Database connection pending requests metric")
model DbConnectionPendingRequestsMetric {
  @doc("Metric name")
  name: "db.client.connection.pending_requests";

  @doc("Metric unit")
  unit: "{request}";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  poolName: string;
}

@doc("Database connection timeouts metric")
model DbConnectionTimeoutsMetric {
  @doc("Metric name")
  name: "db.client.connection.timeouts";

  @doc("Metric unit")
  unit: "{timeout}";

  @doc("Database system")
  @encodedName("application/json", "db.system.name")
  systemName: DbSystem;

  @doc("Pool name")
  @encodedName("application/json", "db.client.connection.pool.name")
  poolName: string;
}

// =============================================================================
// Database Statistics
// =============================================================================

@doc("Aggregated database statistics")
model DbStats {
  @doc("Total queries")
  @encodedName("application/json", "total_queries")
  totalQueries: Count;

  @doc("Total query duration in seconds")
  @encodedName("application/json", "total_duration_s")
  totalDurationS: float64;

  @doc("Average query duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("Query error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Stats by database system")
  @encodedName("application/json", "by_system")
  bySystem?: DbSystemStats[];

  @doc("Stats by operation")
  @encodedName("application/json", "by_operation")
  byOperation?: DbOperationStats[];

  @doc("Slowest queries")
  @encodedName("application/json", "slowest_queries")
  slowestQueries?: DbSlowQuery[];
}

@doc("Statistics by database system")
model DbSystemStats {
  @doc("Database system")
  system: DbSystem;

  @doc("Query count")
  @encodedName("application/json", "query_count")
  queryCount: Count;

  @doc("Average duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;
}

@doc("Statistics by operation type")
model DbOperationStats {
  @doc("Operation name")
  operation: DbOperationName;

  @doc("Query count")
  @encodedName("application/json", "query_count")
  queryCount: Count;

  @doc("Average duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;
}

@doc("Slow query record")
model DbSlowQuery {
  @doc("Query text (sanitized)")
  @encodedName("application/json", "query_text")
  queryText: string;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs: DurationMs;

  @doc("Database system")
  system: DbSystem;

  @doc("Database namespace")
  `namespace`?: string;

  @doc("Operation name")
  operation: DbOperationName;

  @doc("Timestamp")
  timestamp: utcDateTime;
}


// ===== Flattened from: core/specs/models/deployment.tsp =====
// =============================================================================
// QYL v2.0 - Deployment Semantic Conventions (OTel v1.40)
// =============================================================================
// Deployment/release telemetry attributes.
// =============================================================================



namespace Qyl.Domains.Ops.Deployment;

// =============================================================================
// Deployment Attributes
// =============================================================================

@doc("Deployment resource attributes")
model DeploymentAttributes {
  @doc("Deployment environment name")
  @encodedName("application/json", "deployment.environment.name")
  environmentName?: string;

  @doc("Deployment ID")
  @encodedName("application/json", "deployment.id")
  id?: string;

  @doc("Deployment name")
  @encodedName("application/json", "deployment.name")
  name?: string;

  @doc("Deployment status")
  @encodedName("application/json", "deployment.status")
  status?: DeploymentStatus;
}

// =============================================================================
// Deployment Enumerations
// =============================================================================

@doc("Deployment environments")
enum DeploymentEnvironment {
  @doc("Development environment")
  development: "development",

  @doc("Testing environment")
  testing: "testing",

  @doc("Staging environment")
  staging: "staging",

  @doc("Production environment")
  production: "production",

  @doc("Preview/ephemeral environment")
  preview: "preview",

  @doc("Canary environment")
  canary: "canary",
}

@doc("Deployment status")
enum DeploymentStatus {
  @doc("Deployment pending")
  pending: "pending",

  @doc("Deployment in progress")
  inProgress: "in_progress",

  @doc("Deployment succeeded")
  success: "success",

  @doc("Deployment failed")
  failed: "failed",

  @doc("Deployment rolled back")
  rolledBack: "rolled_back",

  @doc("Deployment cancelled")
  cancelled: "cancelled",
}

@doc("Deployment strategies")
enum DeploymentStrategy {
  @doc("Rolling update")
  rolling: "rolling",

  @doc("Blue-green deployment")
  blueGreen: "blue_green",

  @doc("Canary deployment")
  canary: "canary",

  @doc("Recreate (stop old, start new)")
  recreate: "recreate",

  @doc("A/B testing deployment")
  abTest: "ab_test",

  @doc("Shadow deployment")
  shadow: "shadow",

  @doc("Feature flag controlled")
  featureFlag: "feature_flag",
}

// =============================================================================
// Deployment Entity Model
// =============================================================================

@doc("Complete deployment record")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "deployments")
@TypeSpec.OpenAPI.extension("x-csharp-type", "DeploymentEntity")
model DeploymentEntity {
  @doc("Deployment ID")
  @key
  @encodedName("application/json", "deployment.id")
  deploymentId: string;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Service version")
  @encodedName("application/json", "service.version")
  serviceVersion: SemVer;

  @doc("Environment")
  environment: DeploymentEnvironment;

  @doc("Status")
  status: DeploymentStatus;

  @doc("Strategy")
  strategy: DeploymentStrategy;

  @doc("Start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("End time")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Duration in seconds")
  @encodedName("application/json", "duration_s")
  durationS?: DurationS;

  @doc("Deployed by (user/system)")
  @encodedName("application/json", "deployed_by")
  deployedBy?: string;

  @doc("Git commit SHA")
  @encodedName("application/json", "git_commit")
  gitCommit?: string;

  @doc("Git branch")
  @encodedName("application/json", "git_branch")
  gitBranch?: string;

  @doc("Previous version")
  @encodedName("application/json", "previous_version")
  previousVersion?: SemVer;

  @doc("Rollback target (if rolled back)")
  @encodedName("application/json", "rollback_target")
  rollbackTarget?: string;

  @doc("Replica count")
  @encodedName("application/json", "replica_count")
  replicaCount?: int32;

  @doc("Healthy replica count")
  @encodedName("application/json", "healthy_replicas")
  healthyReplicas?: int32;

  @doc("Error message (if failed)")
  @encodedName("application/json", "error_message")
  errorMessage?: string;
}

// =============================================================================
// Deployment Events
// =============================================================================

@doc("Deployment lifecycle event")
model DeploymentEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: DeploymentEventName;

  @doc("Deployment ID")
  @encodedName("application/json", "deployment.id")
  deploymentId: string;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Status")
  status: DeploymentStatus;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Deployment event names")
enum DeploymentEventName {
  @doc("Deployment started")
  started: "deployment.started",

  @doc("Deployment completed")
  completed: "deployment.completed",

  @doc("Deployment failed")
  failed: "deployment.failed",

  @doc("Deployment rolled back")
  rolledBack: "deployment.rolled_back",

  @doc("Health check passed")
  healthCheckPassed: "deployment.health_check.passed",

  @doc("Health check failed")
  healthCheckFailed: "deployment.health_check.failed",
}

// =============================================================================
// Deployment Metrics
// =============================================================================

@doc("Deployment duration metric")
model DeploymentDurationMetric {
  @doc("Metric name")
  name: "deployment.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Status")
  @encodedName("application/json", "deployment.status")
  status: DeploymentStatus;
}

@doc("Deployment count metric")
model DeploymentCountMetric {
  @doc("Metric name")
  name: "deployment.count";

  @doc("Metric unit")
  unit: "{deployment}";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Status")
  @encodedName("application/json", "deployment.status")
  status: DeploymentStatus;
}

// =============================================================================
// DORA Metrics
// =============================================================================

@doc("DORA deployment frequency metric")
model DoraDeploymentFrequencyMetric {
  @doc("Metric name")
  name: "dora.deployment_frequency";

  @doc("Metric unit (deployments per day)")
  unit: "{deployment}/d";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName?: string;
}

@doc("DORA lead time for changes metric")
model DoraLeadTimeMetric {
  @doc("Metric name")
  name: "dora.lead_time_for_changes";

  @doc("Metric unit (hours)")
  unit: "h";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName?: string;
}

@doc("DORA change failure rate metric")
model DoraChangeFailureRateMetric {
  @doc("Metric name")
  name: "dora.change_failure_rate";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName?: string;
}

@doc("DORA mean time to recovery metric")
model DoraMttrMetric {
  @doc("Metric name")
  name: "dora.mttr";

  @doc("Metric unit (hours)")
  unit: "h";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environment: DeploymentEnvironment;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName?: string;
}


// ===== Flattened from: core/specs/models/error.tsp =====
// =============================================================================
// QYL v2.0 - Error Semantic Conventions (OTel v1.40)
// =============================================================================
// Error type and error handling telemetry attributes.
// =============================================================================



namespace Qyl.Domains.Observe.Error;

// =============================================================================
// Error Type Attribute
// =============================================================================

@doc("Error type attribute (OTel stable)")
model ErrorAttributes {
  @doc("Error type - full exception class name or error code")
  @encodedName("application/json", "error.type")
  errorType: string;
}

// =============================================================================
// Error Categories
// =============================================================================

@doc("High-level error categories")
enum ErrorCategory {
  @doc("Client error (4xx)")
  client: "client",

  @doc("Server error (5xx)")
  server: "server",

  @doc("Network error")
  network: "network",

  @doc("Timeout error")
  timeout: "timeout",

  @doc("Validation error")
  validation: "validation",

  @doc("Authentication error")
  authentication: "authentication",

  @doc("Authorization error")
  authorization: "authorization",

  @doc("Rate limit error")
  rateLimit: "rate_limit",

  @doc("Resource not found")
  notFound: "not_found",

  @doc("Conflict error")
  conflict: "conflict",

  @doc("Internal error")
  internal: "internal",

  @doc("External service error")
  external: "external",

  @doc("Database error")
  database: "database",

  @doc("Configuration error")
  configuration: "configuration",

  @doc("Unknown error")
  `unknown`: "unknown",
}

// =============================================================================
// Error Entity
// =============================================================================

@doc("Error entity for tracking and analysis")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "errors")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ErrorEntity")
model ErrorEntity {
  @doc("Error ID")
  @key
  @encodedName("application/json", "error_id")
  errorId: string;

  @doc("Error type (class name or code)")
  @encodedName("application/json", "error.type")
  errorType: string;

  @doc("Error message")
  message: string;

  @doc("Error category")
  category: ErrorCategory;

  @doc("Fingerprint for grouping")
  fingerprint: string;

  @doc("First occurrence")
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;

  @doc("Last occurrence")
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;

  @doc("Occurrence count")
  @encodedName("application/json", "occurrence_count")
  occurrenceCount: Count;

  @doc("Affected users count")
  @encodedName("application/json", "affected_users")
  affectedUsers?: Count;

  @doc("Affected services")
  @encodedName("application/json", "affected_services")
  affectedServices?: string[];

  @doc("Status")
  status: ErrorStatus;

  @doc("Assigned to")
  @encodedName("application/json", "assigned_to")
  assignedTo?: string;

  @doc("Issue tracker URL")
  @encodedName("application/json", "issue_url")
  issueUrl?: UrlString;

  @doc("Sample trace IDs")
  @encodedName("application/json", "sample_traces")
  sampleTraces?: TraceId[];
}

@doc("Error tracking status")
enum ErrorStatus {
  @doc("New/unreviewed")
  new: "new",

  @doc("Acknowledged")
  acknowledged: "acknowledged",

  @doc("In progress")
  inProgress: "in_progress",

  @doc("Resolved")
  resolved: "resolved",

  @doc("Ignored")
  ignored: "ignored",

  @doc("Regressed")
  regressed: "regressed",

  @doc("Won't fix")
  wontFix: "wont_fix",
}

// =============================================================================
// Error Budget
// =============================================================================

@doc("Error budget tracking for SLOs")
model ErrorBudget {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("SLO target (e.g., 99.9%)")
  @encodedName("application/json", "slo_target")
  sloTarget: Percentage;

  @doc("Current SLI (actual success rate)")
  @encodedName("application/json", "current_sli")
  currentSli: Percentage;

  @doc("Budget remaining")
  @encodedName("application/json", "budget_remaining")
  budgetRemaining: Percentage;

  @doc("Budget consumed")
  @encodedName("application/json", "budget_consumed")
  budgetConsumed: Percentage;

  @doc("Budget period start")
  @encodedName("application/json", "period_start")
  periodStart: utcDateTime;

  @doc("Budget period end")
  @encodedName("application/json", "period_end")
  periodEnd: utcDateTime;

  @doc("Burn rate (how fast budget is being consumed)")
  @encodedName("application/json", "burn_rate")
  burnRate: float64;

  @doc("Budget status")
  status: ErrorBudgetStatus;
}

@doc("Error budget status")
enum ErrorBudgetStatus {
  @doc("Healthy - plenty of budget remaining")
  healthy: "healthy",

  @doc("Warning - budget is being consumed faster than expected")
  warning: "warning",

  @doc("Critical - budget nearly exhausted")
  critical: "critical",

  @doc("Exhausted - no budget remaining")
  exhausted: "exhausted",
}

// =============================================================================
// Error Rate Metrics
// =============================================================================

@doc("Error rate metric")
model ErrorRateMetric {
  @doc("Metric name")
  name: "error.rate";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Error category")
  @encodedName("application/json", "error.category")
  category?: ErrorCategory;

  @doc("Endpoint")
  endpoint?: string;
}

@doc("Error count metric")
model ErrorCountMetric {
  @doc("Metric name")
  name: "errors";

  @doc("Metric unit")
  unit: "{error}";

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Error type")
  @encodedName("application/json", "error.type")
  errorType: string;

  @doc("Error category")
  @encodedName("application/json", "error.category")
  category?: ErrorCategory;
}

// =============================================================================
// Error Analysis
// =============================================================================

@doc("Error correlation result")
model ErrorCorrelation {
  @doc("Error ID")
  @encodedName("application/json", "error_id")
  errorId: string;

  @doc("Correlated errors")
  @encodedName("application/json", "correlated_errors")
  correlatedErrors: CorrelatedError[];

  @doc("Potential root cause")
  @encodedName("application/json", "root_cause")
  rootCause?: string;

  @doc("Common attributes")
  @encodedName("application/json", "common_attributes")
  commonAttributes?: Attribute[];
}

@doc("Correlated error")
model CorrelatedError {
  @doc("Error ID")
  @encodedName("application/json", "error_id")
  errorId: string;

  @doc("Error type")
  @encodedName("application/json", "error_type")
  errorType: string;

  @doc("Correlation strength")
  @encodedName("application/json", "correlation_strength")
  correlationStrength: Ratio;

  @doc("Temporal relationship")
  @encodedName("application/json", "temporal_relationship")
  temporalRelationship: TemporalRelationship;
}

@doc("Temporal relationship between errors")
enum TemporalRelationship {
  @doc("Errors occur together")
  concurrent: "concurrent",

  @doc("This error precedes the other")
  precedes: "precedes",

  @doc("This error follows the other")
  follows: "follows",

  @doc("No clear temporal pattern")
  unrelated: "unrelated",
}

// =============================================================================
// Error Statistics
// =============================================================================

@doc("Error statistics")
model ErrorStats {
  @doc("Total error count")
  @encodedName("application/json", "total_count")
  totalCount: Count;

  @doc("Unique error types")
  @encodedName("application/json", "unique_types")
  uniqueTypes: int32;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Errors by category")
  @encodedName("application/json", "by_category")
  byCategory: ErrorCategoryStats[];

  @doc("Errors by service")
  @encodedName("application/json", "by_service")
  byService?: ErrorServiceStats[];

  @doc("Top errors")
  @encodedName("application/json", "top_errors")
  topErrors: ErrorTypeStats[];

  @doc("Trend")
  trend: ErrorTrend;
}

@doc("Error stats by category")
model ErrorCategoryStats {
  @doc("Category")
  category: ErrorCategory;

  @doc("Count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

@doc("Error stats by service")
model ErrorServiceStats {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("Error count")
  count: Count;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Top error type")
  @encodedName("application/json", "top_error_type")
  topErrorType: string;
}

@doc("Error stats by type")
model ErrorTypeStats {
  @doc("Error type")
  @encodedName("application/json", "error_type")
  errorType: string;

  @doc("Count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;

  @doc("Affected users")
  @encodedName("application/json", "affected_users")
  affectedUsers?: Count;

  @doc("Status")
  status: ErrorStatus;
}

@doc("Error trend")
enum ErrorTrend {
  @doc("Errors increasing")
  increasing: "increasing",

  @doc("Errors decreasing")
  decreasing: "decreasing",

  @doc("Errors stable")
  stable: "stable",

  @doc("Anomalous spike")
  spike: "spike",
}


// ===== Flattened from: core/specs/models/genai.tsp =====
// =============================================================================
// QYL v2.0 - GenAI Semantic Conventions (OTel v1.40)
// =============================================================================
// Comprehensive AI/LLM telemetry following OpenTelemetry GenAI specifications.
// https://opentelemetry.io/docs/specs/semconv/gen-ai/
// =============================================================================



@versioned(GenAiVersions)
namespace Qyl.Domains.AI.GenAi;

// =============================================================================
// Version History
// =============================================================================

@doc("GenAI semantic convention versions")
enum GenAiVersions {
  @doc("Initial GenAI semconv (OTel 1.27)")
  v1_27: "1.27.0",

  @doc("Added agent support (OTel 1.28)")
  v1_28: "1.28.0",

  @doc("Token type refinements (OTel 1.29)")
  v1_29: "1.29.0",

  @doc("Deprecated gen_ai.system (OTel 1.37)")
  v1_37: "1.37.0",

  @doc("GenAI.Agent support (OTel 1.38)")
  v1_38: "1.38.0",

  @doc("RPC metadata consolidation (OTel 1.39)")
  v1_39: "1.39.0",

  @doc("Current version (OTel 1.40)")
  v1_40: "1.40.0",
}

// =============================================================================
// GenAI Span Attributes (Complete OTel v1.40)
// =============================================================================

@doc("GenAI span attributes following OTel Semantic Conventions v1.40")
model GenAiSpanAttributes {
  // ---------------------------------------------------------------------------
  // Provider & System (REQUIRED)
  // ---------------------------------------------------------------------------

  @doc("The name of the GenAI provider (e.g., 'openai', 'anthropic', 'google', 'azure')")
  @encodedName("application/json", "gen_ai.provider.name")
  providerName: GenAiProvider;

  @doc("The name of the GenAI system/product (e.g., 'ChatGPT', 'Claude', 'Gemini')")
  @encodedName("application/json", "gen_ai.system")
  @removed(GenAiVersions.v1_37)
  system?: string;

  // ---------------------------------------------------------------------------
  // Operation Details
  // ---------------------------------------------------------------------------

  @doc("The type of GenAI operation being performed")
  @encodedName("application/json", "gen_ai.operation.name")
  operationName: GenAiOperationType;

  // ---------------------------------------------------------------------------
  // Request Attributes (Model & Parameters)
  // ---------------------------------------------------------------------------

  @doc("The model ID requested by the client (e.g., 'gpt-4o', 'claude-3-opus-20240229')")
  @encodedName("application/json", "gen_ai.request.model")
  requestModel: string;

  @doc("The temperature parameter for randomness control")
  @encodedName("application/json", "gen_ai.request.temperature")
  @minValue(0.0)
  @maxValue(2.0)
  requestTemperature?: float64;

  @doc("The top_p (nucleus sampling) parameter")
  @encodedName("application/json", "gen_ai.request.top_p")
  @minValue(0.0)
  @maxValue(1.0)
  requestTopP?: float64;

  @doc("Number of highest probability tokens to consider for sampling")
  @encodedName("application/json", "gen_ai.request.top_k")
  requestTopK?: float64;

  @doc("Maximum tokens to generate in the response")
  @encodedName("application/json", "gen_ai.request.max_tokens")
  @minValue(1)
  requestMaxTokens?: int64;

  @doc("Stop sequences that will halt generation")
  @encodedName("application/json", "gen_ai.request.stop_sequences")
  requestStopSequences?: string[];

  @doc("Frequency penalty to reduce repetition (-2.0 to 2.0)")
  @encodedName("application/json", "gen_ai.request.frequency_penalty")
  @minValue(-2.0)
  @maxValue(2.0)
  requestFrequencyPenalty?: float64;

  @doc("Presence penalty for new topics (-2.0 to 2.0)")
  @encodedName("application/json", "gen_ai.request.presence_penalty")
  @minValue(-2.0)
  @maxValue(2.0)
  requestPresencePenalty?: float64;

  @doc("Seed for deterministic sampling")
  @encodedName("application/json", "gen_ai.request.seed")
  requestSeed?: int64;

  @doc("Encoding format for embeddings")
  @encodedName("application/json", "gen_ai.request.encoding_format")
  requestEncodingFormat?: EmbeddingEncodingFormat;

  @doc("Number of dimensions for embeddings")
  @encodedName("application/json", "gen_ai.request.embedding_dimensions")
  @minValue(1)
  requestEmbeddingDimensions?: int32;

  // ---------------------------------------------------------------------------
  // Response Attributes
  // ---------------------------------------------------------------------------

  @doc("The actual model used (may differ from request)")
  @encodedName("application/json", "gen_ai.response.model")
  responseModel?: string;

  @doc("Response ID from the provider")
  @encodedName("application/json", "gen_ai.response.id")
  responseId?: string;

  @doc("Reasons why generation stopped")
  @encodedName("application/json", "gen_ai.response.finish_reasons")
  responseFinishReasons?: GenAiFinishReason[];

  // ---------------------------------------------------------------------------
  // Token Usage (RECOMMENDED)
  // ---------------------------------------------------------------------------

  @doc("Number of input/prompt tokens consumed")
  @encodedName("application/json", "gen_ai.usage.input_tokens")
  usageInputTokens?: TokenCount;

  @doc("Number of output/completion tokens generated")
  @encodedName("application/json", "gen_ai.usage.output_tokens")
  usageOutputTokens?: TokenCount;

  @doc("Total tokens (deprecated - use input + output)")
  @encodedName("application/json", "gen_ai.usage.total_tokens")
  @removed(GenAiVersions.v1_38)
  usageTotalTokens?: TokenCount;

  @doc("Number of cached input tokens (prompt caching)")
  @encodedName("application/json", "gen_ai.usage.input_tokens.cached")
  @added(GenAiVersions.v1_38)
  usageInputTokensCached?: TokenCount;

  @doc("Number of reasoning tokens (for o1-like models)")
  @encodedName("application/json", "gen_ai.usage.output_tokens.reasoning")
  @added(GenAiVersions.v1_38)
  usageOutputTokensReasoning?: TokenCount;

  @doc("Number of cache read input tokens (prompt caching)")
  @encodedName("application/json", "gen_ai.usage.cache_read.input_tokens")
  @added(GenAiVersions.v1_40)
  usageCacheReadInputTokens?: TokenCount;

  @doc("Number of cache creation input tokens")
  @encodedName("application/json", "gen_ai.usage.cache_creation.input_tokens")
  @added(GenAiVersions.v1_40)
  usageCacheCreationInputTokens?: TokenCount;

  // ---------------------------------------------------------------------------
  // Deprecated Attributes (for migration)
  // ---------------------------------------------------------------------------

  @doc("Prompt tokens (deprecated - use gen_ai.usage.input_tokens)")
  @encodedName("application/json", "gen_ai.usage.prompt_tokens")
  @removed(GenAiVersions.v1_28)
  usagePromptTokens?: TokenCount;

  @doc("Completion tokens (deprecated - use gen_ai.usage.output_tokens)")
  @encodedName("application/json", "gen_ai.usage.completion_tokens")
  @removed(GenAiVersions.v1_28)
  usageCompletionTokens?: TokenCount;

  // ---------------------------------------------------------------------------
  // Agent Attributes (v1.38+)
  // ---------------------------------------------------------------------------

  @doc("Unique identifier for the AI agent")
  @encodedName("application/json", "gen_ai.agent.id")
  @added(GenAiVersions.v1_38)
  agentId?: string;

  @doc("Human-readable name of the AI agent")
  @encodedName("application/json", "gen_ai.agent.name")
  @added(GenAiVersions.v1_38)
  agentName?: string;

  @doc("Description of the agent's purpose")
  @encodedName("application/json", "gen_ai.agent.description")
  @added(GenAiVersions.v1_38)
  agentDescription?: string;

  @doc("Version of the AI agent")
  @encodedName("application/json", "gen_ai.agent.version")
  @added(GenAiVersions.v1_40)
  agentVersion?: string;

  // ---------------------------------------------------------------------------
  // Conversation & Tool Calling
  // ---------------------------------------------------------------------------

  @doc("Conversation identifier for multi-turn interactions")
  @encodedName("application/json", "gen_ai.conversation.id")
  conversationId?: string;

  @doc("Names of tools available to the model")
  @encodedName("application/json", "gen_ai.request.tool_names")
  requestToolNames?: string[];

  @doc("Tool choice mode")
  @encodedName("application/json", "gen_ai.request.tool_choice")
  requestToolChoice?: GenAiToolChoice;

  @doc("The name of the tool utilized")
  @encodedName("application/json", "gen_ai.tool.name")
  toolName?: string;

  @doc("The tool call identifier")
  @encodedName("application/json", "gen_ai.tool.call.id")
  toolCallId?: string;

  // ---------------------------------------------------------------------------
  // Error Information
  // ---------------------------------------------------------------------------

  @doc("Error type if the request failed")
  @encodedName("application/json", "error.type")
  errorType?: GenAiErrorType;

  // ---------------------------------------------------------------------------
  // Data Source & Evaluation Attributes (OTel 1.40)
  // ---------------------------------------------------------------------------

  @doc("Identifier for the data source being used")
  @encodedName("application/json", "gen_ai.data_source.id")
  dataSourceId?: string;

  @doc("Explanation from an evaluation")
  @encodedName("application/json", "gen_ai.evaluation.explanation")
  evaluationExplanation?: string;

  @doc("Name of the evaluation being performed")
  @encodedName("application/json", "gen_ai.evaluation.name")
  evaluationName?: string;

  @doc("Label for the evaluation score")
  @encodedName("application/json", "gen_ai.evaluation.score.label")
  evaluationScoreLabel?: string;

  @doc("Numeric value of the evaluation score")
  @encodedName("application/json", "gen_ai.evaluation.score.value")
  evaluationScoreValue?: float64;

  // ---------------------------------------------------------------------------
  // Input/Output Messages & Prompt Attributes
  // ---------------------------------------------------------------------------

  @doc("Input messages content (may be redacted)")
  @encodedName("application/json", "gen_ai.input.messages")
  inputMessages?: string;

  @doc("Output messages content (may be redacted)")
  @encodedName("application/json", "gen_ai.output.messages")
  outputMessages?: string;

  @doc("Output type (text, json, image, etc.)")
  @encodedName("application/json", "gen_ai.output.type")
  outputType?: GenAiOutputType;

  @doc("Prompt content (may be redacted)")
  @encodedName("application/json", "gen_ai.prompt")
  prompt?: string;

  @doc("Name of the prompt template being used")
  @encodedName("application/json", "gen_ai.prompt.name")
  promptName?: string;

  @doc("System instructions provided to the model")
  @encodedName("application/json", "gen_ai.system_instructions")
  systemInstructions?: string;

  @doc("Completion content (may be redacted)")
  @encodedName("application/json", "gen_ai.completion")
  completion?: string;

  // ---------------------------------------------------------------------------
  // Extended Request Attributes
  // ---------------------------------------------------------------------------

  @doc("Number of choices/completions requested")
  @encodedName("application/json", "gen_ai.request.choice.count")
  @minValue(1)
  requestChoiceCount?: int32;

  @doc("Encoding formats requested for embeddings")
  @encodedName("application/json", "gen_ai.request.encoding_formats")
  requestEncodingFormats?: string[];

  // ---------------------------------------------------------------------------
  // Extended Tool Attributes
  // ---------------------------------------------------------------------------

  @doc("Arguments passed to the tool call (JSON)")
  @encodedName("application/json", "gen_ai.tool.call.arguments")
  toolCallArguments?: string;

  @doc("Result returned from the tool call")
  @encodedName("application/json", "gen_ai.tool.call.result")
  toolCallResult?: string;

  @doc("Tool definitions provided to the model (JSON)")
  @encodedName("application/json", "gen_ai.tool.definitions")
  toolDefinitions?: string;

  @doc("Description of the tool")
  @encodedName("application/json", "gen_ai.tool.description")
  toolDescription?: string;

  @doc("Type of the tool (function, retrieval, etc.)")
  @encodedName("application/json", "gen_ai.tool.type")
  toolType?: GenAiToolType;

  @doc("Token type for usage tracking")
  @encodedName("application/json", "gen_ai.token.type")
  tokenType?: GenAiTokenType;

  // ---------------------------------------------------------------------------
  // Embeddings Attributes
  // ---------------------------------------------------------------------------

  @doc("Number of dimensions in the embedding")
  @encodedName("application/json", "gen_ai.embeddings.dimension.count")
  @minValue(1)
  embeddingsDimensionCount?: int32;
}

// =============================================================================
// GenAI Exported Attribute Models (OTel 1.40 compliant)
// =============================================================================

@doc("GenAI request-specific attributes")
model GenAiRequestAttributes {
  @doc("The model ID for the request")
  @encodedName("application/json", "gen_ai.request.model")
  requestModel: string;

  @doc("Temperature parameter for randomness control")
  @encodedName("application/json", "gen_ai.request.temperature")
  @minValue(0.0)
  @maxValue(2.0)
  temperature?: float64;

  @doc("Maximum tokens to generate")
  @encodedName("application/json", "gen_ai.request.max_tokens")
  @minValue(1)
  maxTokens?: int64;

  @doc("Top-p (nucleus sampling) parameter")
  @encodedName("application/json", "gen_ai.request.top_p")
  @minValue(0.0)
  @maxValue(1.0)
  topP?: float64;

  @doc("Number of highest probability tokens to consider for sampling")
  @encodedName("application/json", "gen_ai.request.top_k")
  topK?: float64;

  @doc("Stop sequences that will halt generation")
  @encodedName("application/json", "gen_ai.request.stop_sequences")
  stopSequences?: string[];

  @doc("Frequency penalty parameter")
  @encodedName("application/json", "gen_ai.request.frequency_penalty")
  @minValue(-2.0)
  @maxValue(2.0)
  frequencyPenalty?: float64;

  @doc("Presence penalty parameter")
  @encodedName("application/json", "gen_ai.request.presence_penalty")
  @minValue(-2.0)
  @maxValue(2.0)
  presencePenalty?: float64;

  @doc("Random seed for deterministic generation")
  @encodedName("application/json", "gen_ai.request.seed")
  seed?: int64;
}

@doc("GenAI response-specific attributes")
model GenAiResponseAttributes {
  @doc("The model ID returned by the provider")
  @encodedName("application/json", "gen_ai.response.model")
  responseModel?: string;

  @doc("Unique identifier for this completion")
  @encodedName("application/json", "gen_ai.response.id")
  id?: string;

  @doc("Finish reasons for each choice")
  @encodedName("application/json", "gen_ai.response.finish_reasons")
  finishReasons?: GenAiFinishReason[];
}

@doc("GenAI usage/token attributes")
model GenAiUsageAttributes {
  @doc("Number of input/prompt tokens")
  @encodedName("application/json", "gen_ai.usage.input_tokens")
  inputTokens: TokenCount;

  @doc("Number of output/completion tokens")
  @encodedName("application/json", "gen_ai.usage.output_tokens")
  outputTokens: TokenCount;

  @doc("Total tokens (computed: inputTokens + outputTokens)")
  @encodedName("application/json", "gen_ai.usage.total_tokens")
  totalTokens: TokenCount;

  @doc("Number of cached input tokens (prompt caching)")
  @encodedName("application/json", "gen_ai.usage.input_tokens.cached")
  @added(GenAiVersions.v1_38)
  cachedInputTokens?: TokenCount;

  @doc("Number of reasoning tokens (for o1-like models)")
  @encodedName("application/json", "gen_ai.usage.output_tokens.reasoning")
  @added(GenAiVersions.v1_38)
  reasoningTokens?: TokenCount;
}

@doc("GenAI message in conversation")
model GenAiMessage {
  @doc("Role of the message sender")
  @encodedName("application/json", "msg_role")
  msgRole: GenAiMessageRole;

  @doc("Message content (text)")
  @encodedName("application/json", "msg_content")
  msgContent?: string;

  @doc("Content type")
  @encodedName("application/json", "content_type")
  contentType?: GenAiContentType;

  @doc("Optional name for the sender")
  @encodedName("application/json", "msg_name")
  msgName?: string;

  @doc("Tool calls made by assistant")
  @encodedName("application/json", "tool_calls")
  toolCalls?: GenAiToolCallEvent[];

  @doc("Tool call ID for tool results")
  @encodedName("application/json", "tool_call_id")
  toolCallId?: string;
}

@doc("Cost estimation for GenAI operations")
model GenAiCostEstimate {
  @doc("Estimated cost for input tokens (USD)")
  @encodedName("application/json", "input_cost_usd")
  @minValue(0.0)
  inputCostUsd: float64;

  @doc("Estimated cost for output tokens (USD)")
  @encodedName("application/json", "output_cost_usd")
  @minValue(0.0)
  outputCostUsd: float64;

  @doc("Total estimated cost (USD)")
  @encodedName("application/json", "total_cost_usd")
  @minValue(0.0)
  totalCostUsd: float64;

  @doc("Cost model/pricing source")
  @encodedName("application/json", "cost_model")
  costModel?: string;

  @doc("Pricing tier applied")
  @encodedName("application/json", "pricing_tier")
  pricingTier?: string;
}

// =============================================================================
// GenAI Enumerations (18+ enums as promised)
// =============================================================================

// =============================================================================
// GenAI Provider Names (OTel semconv 1.40 compliant)
// =============================================================================

@doc("GenAI provider names - OTel semconv 1.40 official values")
enum GenAiProvider {
  // ---------------------------------------------------------------------------
  // OFFICIAL OTel semconv 1.40 values
  // ---------------------------------------------------------------------------

  @doc("Anthropic / Claude")
  anthropic: "anthropic",

  @doc("Amazon Bedrock")
  awsBedrock: "aws.bedrock",

  @doc("Azure AI Inference")
  azureAiInference: "azure.ai.inference",

  @doc("Azure AI OpenAI")
  azureAiOpenai: "azure.ai.openai",

  @doc("Cohere")
  cohere: "cohere",

  @doc("DeepSeek")
  deepseek: "deepseek",

  @doc("Google Gemini")
  gcpGemini: "gcp.gemini",

  @doc("Google GenAI")
  gcpGenAi: "gcp.gen_ai",

  @doc("Google Vertex AI")
  gcpVertexAi: "gcp.vertex_ai",

  @doc("Groq")
  groq: "groq",

  @doc("IBM watsonx.ai")
  ibmWatsonxAi: "ibm.watsonx.ai",

  @doc("Mistral AI")
  mistralAi: "mistral_ai",

  @doc("OpenAI / ChatGPT")
  openai: "openai",

  @doc("Perplexity")
  perplexity: "perplexity",

  @doc("xAI / Grok")
  xAi: "x_ai",

  // ---------------------------------------------------------------------------
  // QYL Extensions (not in OTel spec - use qyl.* for custom attributes)
  // ---------------------------------------------------------------------------

  @doc("Meta / Llama (qyl extension)")
  meta: "meta",

  @doc("Hugging Face (qyl extension)")
  huggingface: "huggingface",

  @doc("Replicate (qyl extension)")
  replicate: "replicate",

  @doc("Together AI (qyl extension)")
  togetherai: "together_ai",

  @doc("Fireworks AI (qyl extension)")
  fireworks: "fireworks",

  @doc("Anyscale (qyl extension)")
  anyscale: "anyscale",

  @doc("Local/self-hosted model (qyl extension)")
  local: "local",

  @doc("Custom/other provider (qyl extension)")
  custom: "custom",
}

// =============================================================================
// GenAI Operation Types (OTel semconv 1.40 compliant)
// =============================================================================

@doc("GenAI operation types - OTel semconv 1.40 official values")
enum GenAiOperationType {
  // ---------------------------------------------------------------------------
  // OFFICIAL OTel semconv 1.40 values
  // ---------------------------------------------------------------------------

  @doc("Chat/conversation completion")
  chat: "chat",

  @doc("Text embedding generation")
  embeddings: "embeddings",

  @doc("Text completion (legacy)")
  textCompletion: "text_completion",

  @doc("Create an AI agent")
  createAgent: "create_agent",

  @doc("Execute a tool/function")
  executeTool: "execute_tool",

  @doc("Generate content (generic)")
  generateContent: "generate_content",

  @doc("Invoke an AI agent")
  invokeAgent: "invoke_agent",

  // ---------------------------------------------------------------------------
  // QYL Extensions (not in OTel spec)
  // ---------------------------------------------------------------------------

  @doc("Image generation (qyl extension)")
  imageGeneration: "image_generation",

  @doc("Audio transcription (qyl extension)")
  audioTranscription: "audio_transcription",

  @doc("Text-to-speech synthesis (qyl extension)")
  textToSpeech: "text_to_speech",

  @doc("Model reranking (qyl extension)")
  rerank: "rerank",

  @doc("Content moderation (qyl extension)")
  moderation: "moderation",
}

@doc("Reasons for generation completion")
enum GenAiFinishReason {
  @doc("Model reached natural stopping point")
  stop: "stop",

  @doc("Hit maximum token limit")
  maxTokens: "max_tokens",

  @doc("Hit length limit")
  length: "length",

  @doc("Content was filtered by safety systems")
  contentFilter: "content_filter",

  @doc("Model requested tool/function call")
  toolCalls: "tool_calls",

  @doc("Model requested function call (deprecated)")
  functionCall: "function_call",

  @doc("Generation was cancelled")
  cancelled: "cancelled",

  @doc("Error occurred during generation")
  error: "error",

  @doc("Recitation detected and blocked")
  recitation: "recitation",

  @doc("Safety stop triggered")
  safety: "safety",

  @doc("Unknown/other reason")
  other: "other",
}

@doc("Tool choice mode for function calling")
enum GenAiToolChoice {
  @doc("Model decides whether to call tools")
  auto: "auto",

  @doc("Model must call at least one tool")
  required: "required",

  @doc("Model must not call any tools")
  none: "none",

  @doc("Model must call a specific tool")
  specific: "specific",
}

@doc("Message roles in chat conversations")
enum GenAiMessageRole {
  @doc("System prompt/instructions")
  system: "system",

  @doc("User message")
  user: "user",

  @doc("Assistant/model response")
  assistant: "assistant",

  @doc("Tool/function result")
  tool: "tool",

  @doc("Function result (deprecated)")
  function: "function",
}

@doc("Content types in messages")
enum GenAiContentType {
  @doc("Plain text content")
  text: "text",

  @doc("Image content")
  image: "image",

  @doc("Audio content")
  audio: "audio",

  @doc("Video content")
  video: "video",

  @doc("File/document content")
  file: "file",

  @doc("Tool call request")
  toolCall: "tool_call",

  @doc("Tool call result")
  toolResult: "tool_result",

  @doc("Thinking/reasoning content")
  thinking: "thinking",

  @doc("Refusal message")
  refusal: "refusal",
}

@doc("Embedding encoding formats")
enum EmbeddingEncodingFormat {
  @doc("Array of floats")
  float: "float",

  @doc("Base64-encoded bytes")
  base64: "base64",
}

@doc("GenAI-specific error types")
enum GenAiErrorType {
  @doc("Authentication failed")
  authenticationError: "authentication_error",

  @doc("Insufficient quota/credits")
  insufficientQuota: "insufficient_quota",

  @doc("Rate limit exceeded")
  rateLimitExceeded: "rate_limit_exceeded",

  @doc("Invalid request parameters")
  invalidRequest: "invalid_request",

  @doc("Context length exceeded")
  contextLengthExceeded: "context_length_exceeded",

  @doc("Content policy violation")
  contentPolicyViolation: "content_policy_violation",

  @doc("Model not found")
  modelNotFound: "model_not_found",

  @doc("Model overloaded")
  modelOverloaded: "model_overloaded",

  @doc("Server error")
  serverError: "server_error",

  @doc("Service unavailable")
  serviceUnavailable: "service_unavailable",

  @doc("Timeout")
  timeout: "timeout",

  @doc("Connection error")
  connectionError: "connection_error",

  @doc("Unknown error")
  `unknown`: "unknown",
}

// =============================================================================
// GenAI Token Types (OTel semconv 1.40 compliant)
// =============================================================================

@doc("Token types for usage tracking - OTel semconv 1.40 official values")
enum GenAiTokenType {
  // ---------------------------------------------------------------------------
  // OFFICIAL OTel semconv 1.40 values
  // ---------------------------------------------------------------------------

  @doc("Input/prompt tokens")
  input: "input",

  @doc("Output/completion tokens")
  output: "output",

  // ---------------------------------------------------------------------------
  // QYL Extensions (not in OTel spec)
  // ---------------------------------------------------------------------------

  @doc("Cached input tokens (qyl extension)")
  cached: "cached",

  @doc("Reasoning tokens for CoT models (qyl extension)")
  reasoning: "reasoning",
}

// =============================================================================
// GenAI Tool Types (OTel semconv 1.40 compliant)
// =============================================================================

@doc("Tool types for GenAI function calling - OTel semconv 1.40 official values")
enum GenAiToolType {
  // ---------------------------------------------------------------------------
  // OFFICIAL OTel semconv 1.40 values
  // ---------------------------------------------------------------------------

  @doc("Function/method call")
  function: "function",

  @doc("Extension tool")
  extension: "extension",

  @doc("Datastore access")
  datastore: "datastore",

  // ---------------------------------------------------------------------------
  // QYL Extensions (not in OTel spec)
  // ---------------------------------------------------------------------------

  @doc("Code interpreter/execution (qyl extension)")
  codeInterpreter: "code_interpreter",

  @doc("MCP (Model Context Protocol) tool (qyl extension)")
  mcp: "mcp",

  @doc("Web search (qyl extension)")
  webSearch: "web_search",
}

@doc("GenAI output types")
enum GenAiOutputType {
  @doc("Text output")
  text: "text",

  @doc("JSON output")
  json: "json",

  @doc("Image output")
  image: "image",

  @doc("Speech/audio output")
  speech: "speech",
}

@doc("Image quality settings")
enum GenAiImageQuality {
  @doc("Standard quality")
  standard: "standard",

  @doc("High quality")
  hd: "hd",

  @doc("Ultra high quality")
  ultra: "ultra",
}

@doc("Image sizes")
enum GenAiImageSize {
  @doc("256x256 pixels")
  size256: "256x256",

  @doc("512x512 pixels")
  size512: "512x512",

  @doc("1024x1024 pixels")
  size1024: "1024x1024",

  @doc("1792x1024 pixels (wide)")
  size1792x1024: "1792x1024",

  @doc("1024x1792 pixels (tall)")
  size1024x1792: "1024x1792",
}

@doc("Audio voice options")
enum GenAiVoice {
  @doc("Alloy voice")
  alloy: "alloy",

  @doc("Echo voice")
  echo: "echo",

  @doc("Fable voice")
  fable: "fable",

  @doc("Onyx voice")
  onyx: "onyx",

  @doc("Nova voice")
  nova: "nova",

  @doc("Shimmer voice")
  shimmer: "shimmer",
}

@doc("Audio response formats")
enum GenAiAudioFormat {
  @doc("MP3 format")
  mp3: "mp3",

  @doc("Opus format")
  opus: "opus",

  @doc("AAC format")
  aac: "aac",

  @doc("FLAC format")
  flac: "flac",

  @doc("WAV format")
  wav: "wav",

  @doc("PCM format")
  pcm: "pcm",
}

@doc("Moderation categories")
enum GenAiModerationCategory {
  @doc("Hate speech")
  hate: "hate",

  @doc("Hate speech with threats")
  hateThreatening: "hate/threatening",

  @doc("Harassment")
  harassment: "harassment",

  @doc("Harassment with threats")
  harassmentThreatening: "harassment/threatening",

  @doc("Self-harm content")
  selfHarm: "self-harm",

  @doc("Self-harm instructions")
  selfHarmInstructions: "self-harm/instructions",

  @doc("Self-harm intent")
  selfHarmIntent: "self-harm/intent",

  @doc("Sexual content")
  sexual: "sexual",

  @doc("Sexual content involving minors")
  sexualMinors: "sexual/minors",

  @doc("Violence")
  violence: "violence",

  @doc("Graphic violence")
  violenceGraphic: "violence/graphic",
}

@doc("Content filter severity levels")
enum GenAiFilterSeverity {
  @doc("Safe content")
  safe: "safe",

  @doc("Low severity")
  low: "low",

  @doc("Medium severity")
  medium: "medium",

  @doc("High severity")
  high: "high",
}

@doc("Response format types")
enum GenAiResponseFormat {
  @doc("Plain text response")
  text: "text",

  @doc("JSON object response")
  jsonObject: "json_object",

  @doc("JSON schema-constrained response")
  jsonSchema: "json_schema",
}

// =============================================================================
// GenAI Event Models
// =============================================================================

@doc("Event for prompt/message content")
model GenAiPromptEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "gen_ai.content.prompt";

  @doc("Message index in conversation")
  @encodedName("application/json", "gen_ai.prompt.index")
  promptIndex: int32;

  @doc("Role of the message sender")
  @encodedName("application/json", "gen_ai.prompt.role")
  promptRole: GenAiMessageRole;

  @doc("Content type")
  @encodedName("application/json", "gen_ai.prompt.content_type")
  contentType?: GenAiContentType;

  @doc("Content (may be redacted for privacy)")
  @encodedName("application/json", "gen_ai.prompt.content")
  content?: string;

  @doc("Token count for this message")
  @encodedName("application/json", "gen_ai.prompt.tokens")
  tokens?: TokenCount;
}

@doc("Event for completion/response content")
model GenAiCompletionEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "gen_ai.content.completion";

  @doc("Choice index for multi-choice responses")
  @encodedName("application/json", "gen_ai.completion.index")
  completionIndex: int32;

  @doc("Role (always assistant)")
  @encodedName("application/json", "gen_ai.completion.role")
  completionRole: "assistant";

  @doc("Finish reason for this choice")
  @encodedName("application/json", "gen_ai.completion.finish_reason")
  finishReason?: GenAiFinishReason;

  @doc("Content (may be redacted for privacy)")
  @encodedName("application/json", "gen_ai.completion.content")
  content?: string;

  @doc("Token count for this completion")
  @encodedName("application/json", "gen_ai.completion.tokens")
  tokens?: TokenCount;
}

@doc("Event for tool/function calls")
model GenAiToolCallEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "gen_ai.tool.call";

  @doc("Tool call ID")
  @encodedName("application/json", "gen_ai.tool.id")
  toolId: string;

  @doc("Tool/function name")
  @encodedName("application/json", "gen_ai.tool.name")
  toolName: string;

  @doc("Tool arguments (JSON string)")
  @encodedName("application/json", "gen_ai.tool.arguments")
  toolArguments?: string;
}

@doc("Event for tool/function results")
model GenAiToolResultEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: "gen_ai.tool.result";

  @doc("Tool call ID")
  @encodedName("application/json", "gen_ai.tool.id")
  toolId: string;

  @doc("Tool result (may be truncated)")
  @encodedName("application/json", "gen_ai.tool.result")
  toolResult?: string;

  @doc("Whether the tool execution succeeded")
  @encodedName("application/json", "gen_ai.tool.success")
  toolSuccess: boolean;

  @doc("Error message if failed")
  @encodedName("application/json", "gen_ai.tool.error")
  toolError?: string;
}

// =============================================================================
// GenAI Metrics
// =============================================================================

@doc("GenAI token usage metric (counter)")
model GenAiTokenUsageMetric {
  @doc("Metric name")
  name: "gen_ai.client.token.usage";

  @doc("Metric unit")
  unit: "{token}";

  @doc("Token type (input or output)")
  @encodedName("application/json", "gen_ai.token.type")
  tokenType: GenAiTokenType;

  @doc("Provider name")
  @encodedName("application/json", "gen_ai.provider.name")
  providerName: GenAiProvider;

  @doc("Model name")
  @encodedName("application/json", "gen_ai.request.model")
  requestModel: string;
}

@doc("GenAI operation duration metric (histogram)")
model GenAiOperationDurationMetric {
  @doc("Metric name")
  name: "gen_ai.client.operation.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Operation type")
  @encodedName("application/json", "gen_ai.operation.name")
  operationName: GenAiOperationType;

  @doc("Provider name")
  @encodedName("application/json", "gen_ai.provider.name")
  providerName: GenAiProvider;

  @doc("Model name")
  @encodedName("application/json", "gen_ai.request.model")
  requestModel: string;

  @doc("Error type if failed")
  @encodedName("application/json", "error.type")
  errorType?: GenAiErrorType;
}

@doc("Time to first token metric (histogram)")
model GenAiTimeToFirstTokenMetric {
  @doc("Metric name")
  name: "gen_ai.client.time_to_first_token";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Provider name")
  @encodedName("application/json", "gen_ai.provider.name")
  providerName: GenAiProvider;

  @doc("Model name")
  @encodedName("application/json", "gen_ai.request.model")
  requestModel: string;
}

// =============================================================================
// GenAI Aggregated Statistics
// =============================================================================

@doc("Aggregated GenAI usage statistics")
model GenAiStats {
  @doc("Total number of GenAI operations")
  @encodedName("application/json", "total_operations")
  totalOperations: Count;

  @doc("Total input tokens consumed")
  @encodedName("application/json", "total_input_tokens")
  totalInputTokens: TokenCount;

  @doc("Total output tokens generated")
  @encodedName("application/json", "total_output_tokens")
  totalOutputTokens: TokenCount;

  @doc("Total cached input tokens")
  @encodedName("application/json", "total_cached_tokens")
  totalCachedTokens?: TokenCount;

  @doc("Token usage by model")
  @encodedName("application/json", "usage_by_model")
  usageByModel: GenAiModelUsage[];

  @doc("Token usage by provider")
  @encodedName("application/json", "usage_by_provider")
  usageByProvider: GenAiProviderUsage[];

  @doc("Usage by operation type")
  @encodedName("application/json", "usage_by_operation")
  usageByOperation: GenAiOperationUsage[];

  @doc("Error statistics")
  @encodedName("application/json", "error_stats")
  errorStats: GenAiErrorStats;

  @doc("Latency percentiles (in milliseconds)")
  @encodedName("application/json", "latency_percentiles")
  latencyPercentiles: LatencyPercentiles;

  @doc("Estimated cost in USD (if available)")
  @encodedName("application/json", "estimated_cost_usd")
  estimatedCostUsd?: float64;
}

@doc("Model-level usage statistics")
model GenAiModelUsage {
  @doc("Model identifier")
  @encodedName("application/json", "model_name")
  modelName: string;

  @doc("Provider name")
  provider: GenAiProvider;

  @doc("Operation count")
  @encodedName("application/json", "operation_count")
  operationCount: Count;

  @doc("Input tokens")
  @encodedName("application/json", "input_tokens")
  inputTokens: TokenCount;

  @doc("Output tokens")
  @encodedName("application/json", "output_tokens")
  outputTokens: TokenCount;

  @doc("Error count")
  @encodedName("application/json", "error_count")
  errorCount: Count;

  @doc("Average latency in milliseconds")
  @encodedName("application/json", "avg_latency_ms")
  avgLatencyMs: float64;
}

@doc("Provider-level usage statistics")
model GenAiProviderUsage {
  @doc("Provider name")
  provider: GenAiProvider;

  @doc("Operation count")
  @encodedName("application/json", "operation_count")
  operationCount: Count;

  @doc("Total tokens (input + output)")
  @encodedName("application/json", "total_tokens")
  totalTokens: TokenCount;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;
}

@doc("Operation-level usage statistics")
model GenAiOperationUsage {
  @doc("Operation type")
  @encodedName("application/json", "operation_type")
  operationType: GenAiOperationType;

  @doc("Operation count")
  @encodedName("application/json", "operation_count")
  operationCount: Count;

  @doc("Average tokens per operation")
  @encodedName("application/json", "avg_tokens")
  avgTokens: float64;
}

@doc("Error statistics")
model GenAiErrorStats {
  @doc("Total error count")
  @encodedName("application/json", "total_errors")
  totalErrors: Count;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Errors by type")
  @encodedName("application/json", "by_type")
  byType: GenAiErrorCount[];
}

@doc("Error count by type")
model GenAiErrorCount {
  @doc("Error type")
  @encodedName("application/json", "error_type")
  errorType: GenAiErrorType;

  @doc("Count")
  count: Count;
}

@doc("Latency percentiles")
model LatencyPercentiles {
  @doc("P50 (median) in milliseconds")
  p50: float64;

  @doc("P75 in milliseconds")
  p75: float64;

  @doc("P90 in milliseconds")
  p90: float64;

  @doc("P95 in milliseconds")
  p95: float64;

  @doc("P99 in milliseconds")
  p99: float64;
}

// =============================================================================
// Vendor-Specific Extensions (OTel 1.40)
// =============================================================================

@doc("OpenAI-specific request attributes")
model OpenAiRequestAttributes {
  @doc("Response format requested (text, json_object, json_schema)")
  @encodedName("application/json", "gen_ai.openai.request.response_format")
  responseFormat?: OpenAiResponseFormat;

  @doc("Random seed for deterministic generation")
  @encodedName("application/json", "gen_ai.openai.request.seed")
  seed?: int64;

  @doc("Service tier for the request (auto, default, etc.)")
  @encodedName("application/json", "gen_ai.openai.request.service_tier")
  serviceTier?: string;
}

@doc("OpenAI-specific response attributes")
model OpenAiResponseAttributes {
  @doc("Service tier used for the response")
  @encodedName("application/json", "gen_ai.openai.response.service_tier")
  serviceTier?: string;

  @doc("System fingerprint for reproducibility")
  @encodedName("application/json", "gen_ai.openai.response.system_fingerprint")
  systemFingerprint?: string;
}

@doc("OpenAI response format types")
enum OpenAiResponseFormat {
  @doc("Plain text response")
  text: "text",

  @doc("JSON object response")
  jsonObject: "json_object",

  @doc("JSON schema-constrained response")
  jsonSchema: "json_schema",
}


// ===== Flattened from: core/specs/otel/span.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry Span Model
// =============================================================================
// Distributed tracing span representation following OTel specification.
// =============================================================================



namespace Qyl.OTel.Traces;

// =============================================================================
// Span Model
// =============================================================================

@doc("OpenTelemetry Span representing a single operation in a distributed trace")
model Span {
  @doc("Unique span identifier (16 hex chars)")
  @key
  @encodedName("application/json", "span_id")
  spanId: SpanId;

  @doc("Trace identifier (32 hex chars)")
  @encodedName("application/json", "trace_id")
  traceId: TraceId;

  @doc("Parent span identifier (null for root spans)")
  @encodedName("application/json", "parent_span_id")
  parentSpanId?: SpanId;

  @doc("W3C trace state")
  @encodedName("application/json", "trace_state")
  traceState?: TraceState;

  @doc("Human-readable span name")
  @minLength(1)
  name: string;

  @doc("Span kind")
  kind: SpanKind;

  @doc("Start timestamp in nanoseconds since epoch")
  @encodedName("application/json", "start_time_unix_nano")
  startTimeUnixNano: int64;

  @doc("End timestamp in nanoseconds since epoch")
  @encodedName("application/json", "end_time_unix_nano")
  endTimeUnixNano: int64;

  @doc("Span attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;

  @doc("Span events (logs attached to span)")
  events?: SpanEvent[];

  @doc("Dropped events count")
  @encodedName("application/json", "dropped_events_count")
  droppedEventsCount?: Count;

  @doc("Links to other spans")
  links?: SpanLink[];

  @doc("Dropped links count")
  @encodedName("application/json", "dropped_links_count")
  droppedLinksCount?: Count;

  @doc("Span status")
  status: SpanStatus;

  @doc("Span flags")
  flags?: int32;

  @doc("Resource describing the entity that produced this span")
  resource: Qyl.OTel.Resource.Resource;

  @doc("Instrumentation scope")
  @encodedName("application/json", "instrumentation_scope")
  instrumentationScope?: InstrumentationScope;
}

@doc("Span status")
model SpanStatus {
  @doc("Status code")
  code: SpanStatusCode;

  @doc("Status message (only for ERROR status)")
  message?: string;
}

@doc("Event occurring during a span's lifetime")
model SpanEvent {
  @doc("Event name")
  @minLength(1)
  name: string;

  @doc("Event timestamp in nanoseconds since epoch")
  @encodedName("application/json", "time_unix_nano")
  timeUnixNano: int64;

  @doc("Event attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;
}

@doc("Link to another span (e.g., batch processing)")
model SpanLink {
  @doc("Linked trace ID")
  @encodedName("application/json", "trace_id")
  traceId: TraceId;

  @doc("Linked span ID")
  @encodedName("application/json", "span_id")
  spanId: SpanId;

  @doc("Trace state of the linked span")
  @encodedName("application/json", "trace_state")
  traceState?: TraceState;

  @doc("Link attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;

  @doc("Link flags")
  flags?: int32;
}

// =============================================================================
// Trace Model (Collection of Spans)
// =============================================================================

@doc("Complete trace containing all related spans")
model Trace {
  @doc("Trace identifier")
  @key
  @encodedName("application/json", "trace_id")
  traceId: TraceId;

  @doc("All spans in this trace")
  spans: Span[];

  @doc("Root span of the trace")
  @encodedName("application/json", "root_span")
  rootSpan?: Span;

  @doc("Total span count")
  @encodedName("application/json", "span_count")
  spanCount: int32;

  @doc("Trace duration in nanoseconds")
  @encodedName("application/json", "duration_ns")
  durationNs: DurationNs;

  @doc("Trace start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("Trace end time")
  @encodedName("application/json", "end_time")
  endTime: utcDateTime;

  @doc("Services involved in this trace")
  services: string[];

  @doc("Whether trace contains errors")
  @encodedName("application/json", "has_error")
  hasError: boolean;
}

// =============================================================================
// Span Query Filters
// =============================================================================

@doc("Filter parameters for querying spans")
model SpanQueryFilters {
  @doc("Filter by trace ID")
  @query
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Filter by parent span ID")
  @query
  @encodedName("application/json", "parent_span_id")
  parentSpanId?: SpanId;

  @doc("Filter by span name (supports wildcards)")
  @query
  name?: string;

  @doc("Filter by span kind")
  @query
  kind?: SpanKind;

  @doc("Filter by service name")
  @query
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Filter by status code")
  @query
  @encodedName("application/json", "status_code")
  statusCode?: SpanStatusCode;

  @doc("Minimum duration in nanoseconds")
  @query
  @encodedName("application/json", "min_duration_ns")
  minDurationNs?: DurationNs;

  @doc("Maximum duration in nanoseconds")
  @query
  @encodedName("application/json", "max_duration_ns")
  maxDurationNs?: DurationNs;

  @doc("Filter by attribute key-value pair")
  @query
  attribute?: string;
}

// =============================================================================
// Span Aggregations
// =============================================================================

@doc("Aggregated span statistics")
model SpanStats {
  @doc("Total span count")
  @encodedName("application/json", "total_count")
  totalCount: Count;

  @doc("Error count")
  @encodedName("application/json", "error_count")
  errorCount: Count;

  @doc("Error rate (0.0 - 1.0)")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Average duration in nanoseconds")
  @encodedName("application/json", "avg_duration_ns")
  avgDurationNs: float64;

  @doc("P50 duration in nanoseconds")
  @encodedName("application/json", "p50_duration_ns")
  p50DurationNs: DurationNs;

  @doc("P90 duration in nanoseconds")
  @encodedName("application/json", "p90_duration_ns")
  p90DurationNs: DurationNs;

  @doc("P99 duration in nanoseconds")
  @encodedName("application/json", "p99_duration_ns")
  p99DurationNs: DurationNs;

  @doc("Minimum duration in nanoseconds")
  @encodedName("application/json", "min_duration_ns")
  minDurationNs: DurationNs;

  @doc("Maximum duration in nanoseconds")
  @encodedName("application/json", "max_duration_ns")
  maxDurationNs: DurationNs;

  @doc("Throughput (spans per second)")
  @encodedName("application/json", "throughput_per_second")
  throughputPerSecond: float64;

  @doc("Stats grouped by service")
  @encodedName("application/json", "by_service")
  byService?: SpanStatsByDimension[];

  @doc("Stats grouped by operation name")
  @encodedName("application/json", "by_operation")
  byOperation?: SpanStatsByDimension[];
}

@doc("Span statistics for a specific dimension value")
model SpanStatsByDimension {
  @doc("Dimension value (e.g., service name)")
  dimension: string;

  @doc("Span count for this dimension")
  count: Count;

  @doc("Average duration in nanoseconds")
  @encodedName("application/json", "avg_duration_ns")
  avgDurationNs: float64;

  @doc("Error rate for this dimension")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;
}


// ===== Flattened from: core/specs/otel/resource.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry Resource Model
// =============================================================================
// Resource attributes identifying the entity producing telemetry.
// =============================================================================



namespace Qyl.OTel.Resource;

// =============================================================================
// Resource Model
// =============================================================================

@doc("Resource describes the entity producing telemetry")
model Resource {
  @doc("Service name (required)")
  @encodedName("application/json", "service.name")
  serviceName: string;

  @doc("Service namespace for grouping")
  @encodedName("application/json", "service.namespace")
  serviceNamespace?: string;

  @doc("Service instance ID (unique per instance)")
  @encodedName("application/json", "service.instance.id")
  serviceInstanceId?: string;

  @doc("Service version")
  @encodedName("application/json", "service.version")
  serviceVersion?: SemVer;

  @doc("Telemetry SDK name")
  @encodedName("application/json", "telemetry.sdk.name")
  telemetrySdkName?: string;

  @doc("Telemetry SDK language")
  @encodedName("application/json", "telemetry.sdk.language")
  telemetrySdkLanguage?: Enums.TelemetrySdkLanguage;

  @doc("Telemetry SDK version")
  @encodedName("application/json", "telemetry.sdk.version")
  telemetrySdkVersion?: SemVer;

  @doc("Auto-instrumentation agent name")
  @encodedName("application/json", "telemetry.auto.version")
  telemetryAutoVersion?: SemVer;

  @doc("Deployment environment (e.g., production, staging)")
  @encodedName("application/json", "deployment.environment.name")
  deploymentEnvironment?: string;

  @doc("Cloud provider")
  @encodedName("application/json", "cloud.provider")
  cloudProvider?: CloudProvider;

  @doc("Cloud region")
  @encodedName("application/json", "cloud.region")
  cloudRegion?: string;

  @doc("Cloud availability zone")
  @encodedName("application/json", "cloud.availability_zone")
  cloudAvailabilityZone?: string;

  @doc("Cloud account ID")
  @encodedName("application/json", "cloud.account.id")
  cloudAccountId?: string;

  @doc("Cloud platform (e.g., aws_ecs, gcp_cloud_run)")
  @encodedName("application/json", "cloud.platform")
  cloudPlatform?: string;

  @doc("Host name")
  @encodedName("application/json", "host.name")
  hostName?: string;

  @doc("Host ID")
  @encodedName("application/json", "host.id")
  hostId?: string;

  @doc("Host type (e.g., n1-standard-1)")
  @encodedName("application/json", "host.type")
  hostType?: string;

  @doc("Host architecture (e.g., amd64, arm64)")
  @encodedName("application/json", "host.arch")
  hostArch?: HostArch;

  @doc("Operating system type")
  @encodedName("application/json", "os.type")
  osType?: OsType;

  @doc("Operating system description")
  @encodedName("application/json", "os.description")
  osDescription?: string;

  @doc("Operating system version")
  @encodedName("application/json", "os.version")
  osVersion?: string;

  @doc("Process ID")
  @encodedName("application/json", "process.pid")
  processPid?: int64;

  @doc("Process executable name")
  @encodedName("application/json", "process.executable.name")
  processExecutableName?: string;

  @doc("Process command line")
  @encodedName("application/json", "process.command_line")
  processCommandLine?: string;

  @doc("Process runtime name")
  @encodedName("application/json", "process.runtime.name")
  processRuntimeName?: string;

  @doc("Process runtime version")
  @encodedName("application/json", "process.runtime.version")
  processRuntimeVersion?: string;

  @doc("Container ID")
  @encodedName("application/json", "container.id")
  containerId?: string;

  @doc("Container name")
  @encodedName("application/json", "container.name")
  containerName?: string;

  @doc("Container image name")
  @encodedName("application/json", "container.image.name")
  containerImageName?: string;

  @doc("Container image tag")
  @encodedName("application/json", "container.image.tag")
  containerImageTag?: string;

  @doc("Kubernetes cluster name")
  @encodedName("application/json", "k8s.cluster.name")
  k8sClusterName?: string;

  @doc("Kubernetes namespace")
  @encodedName("application/json", "k8s.namespace.name")
  k8sNamespaceName?: string;

  @doc("Kubernetes pod name")
  @encodedName("application/json", "k8s.pod.name")
  k8sPodName?: string;

  @doc("Kubernetes pod UID")
  @encodedName("application/json", "k8s.pod.uid")
  k8sPodUid?: string;

  @doc("Kubernetes deployment name")
  @encodedName("application/json", "k8s.deployment.name")
  k8sDeploymentName?: string;

  @doc("Additional resource attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;
}

// =============================================================================
// Resource-Related Enums
// =============================================================================

@doc("Cloud provider types")
enum CloudProvider {
  @doc("Alibaba Cloud")
  alibabaCloud: "alibaba_cloud",

  @doc("Amazon Web Services")
  aws: "aws",

  @doc("Microsoft Azure")
  azure: "azure",

  @doc("Google Cloud Platform")
  gcp: "gcp",

  @doc("Heroku")
  heroku: "heroku",

  @doc("IBM Cloud")
  ibmCloud: "ibm_cloud",

  @doc("Tencent Cloud")
  tencentCloud: "tencent_cloud",
}

@doc("Host architecture types")
enum HostArch {
  @doc("AMD64 / x86_64")
  amd64: "amd64",

  @doc("ARM32")
  arm32: "arm32",

  @doc("ARM64 / aarch64")
  arm64: "arm64",

  @doc("Itanium")
  ia64: "ia64",

  @doc("PowerPC 32-bit")
  ppc32: "ppc32",

  @doc("PowerPC 64-bit")
  ppc64: "ppc64",

  @doc("IBM z/Architecture")
  s390x: "s390x",

  @doc("x86 32-bit")
  x86: "x86",
}

@doc("Operating system types")
enum OsType {
  @doc("Microsoft Windows")
  windows: "windows",

  @doc("Linux")
  linux: "linux",

  @doc("Apple macOS")
  darwin: "darwin",

  @doc("FreeBSD")
  freebsd: "freebsd",

  @doc("NetBSD")
  netbsd: "netbsd",

  @doc("OpenBSD")
  openbsd: "openbsd",

  @doc("DragonFly BSD")
  dragonflybsd: "dragonflybsd",

  @doc("HP-UX")
  hpux: "hpux",

  @doc("AIX")
  aix: "aix",

  @doc("Oracle Solaris")
  solaris: "solaris",

  @doc("IBM z/OS")
  zOs: "z_os",
}


// ===== Flattened from: core/specs/otel/metrics.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry Metrics Model
// =============================================================================
// Metrics data model with discriminated unions for different metric types.
// =============================================================================



namespace Qyl.OTel.Metrics;

// =============================================================================
// Metric Model
// =============================================================================

@doc("OpenTelemetry Metric containing measurement data")
model Metric {
  @doc("Metric name (e.g., http.server.request.duration)")
  @minLength(1)
  name: string;

  @doc("Metric description")
  description?: string;

  @doc("Metric unit (e.g., 's', 'By', '1')")
  unit?: string;

  @doc("Metric data (discriminated by type)")
  data: MetricData;

  @doc("Metric metadata attributes")
  metadata?: Attribute[];

  @doc("Resource describing the entity that produced this metric")
  resource: Qyl.OTel.Resource.Resource;

  @doc("Instrumentation scope")
  @encodedName("application/json", "instrumentation_scope")
  instrumentationScope?: InstrumentationScope;
}

// =============================================================================
// Metric Data Types (Discriminated Union)
// =============================================================================

@doc("Metric data discriminated by type")
@discriminator("type")
model MetricData {
  @doc("Metric type discriminator")
  type: MetricType;
}

@doc("Gauge metric - instantaneous value at a point in time")
model GaugeData extends MetricData {
  /** Discriminator identifying this as gauge metric data */
  type: MetricType.gauge;

  @doc("Gauge data points")
  @encodedName("application/json", "data_points")
  dataPoints: NumberDataPoint[];
}

@doc("Sum metric - cumulative or delta counter")
model SumData extends MetricData {
  /** Discriminator identifying this as sum metric data */
  type: MetricType.sum;

  @doc("Sum data points")
  @encodedName("application/json", "data_points")
  dataPoints: NumberDataPoint[];

  @doc("Whether the sum is monotonically increasing")
  @encodedName("application/json", "is_monotonic")
  isMonotonic: boolean;

  @doc("Aggregation temporality")
  @encodedName("application/json", "aggregation_temporality")
  aggregationTemporality: AggregationTemporality;
}

@doc("Histogram metric - distribution of values in buckets")
model HistogramData extends MetricData {
  /** Discriminator identifying this as histogram metric data */
  type: MetricType.histogram;

  @doc("Histogram data points")
  @encodedName("application/json", "data_points")
  dataPoints: HistogramDataPoint[];

  @doc("Aggregation temporality")
  @encodedName("application/json", "aggregation_temporality")
  aggregationTemporality: AggregationTemporality;
}

@doc("Exponential histogram metric - distribution with exponential bucket boundaries")
model ExponentialHistogramData extends MetricData {
  /** Discriminator identifying this as exponential histogram metric data */
  type: MetricType.exponentialHistogram;

  @doc("Exponential histogram data points")
  @encodedName("application/json", "data_points")
  dataPoints: ExponentialHistogramDataPoint[];

  @doc("Aggregation temporality")
  @encodedName("application/json", "aggregation_temporality")
  aggregationTemporality: AggregationTemporality;
}

@doc("Summary metric - pre-aggregated quantile distribution")
model SummaryData extends MetricData {
  /** Discriminator identifying this as summary metric data */
  type: MetricType.summary;

  @doc("Summary data points")
  @encodedName("application/json", "data_points")
  dataPoints: SummaryDataPoint[];
}

// =============================================================================
// Data Point Models
// =============================================================================

@doc("Base data point with common fields")
model DataPointBase {
  @doc("Start timestamp in nanoseconds since epoch")
  @encodedName("application/json", "start_time_unix_nano")
  startTimeUnixNano: int64;

  @doc("End timestamp in nanoseconds since epoch")
  @encodedName("application/json", "time_unix_nano")
  timeUnixNano: int64;

  @doc("Data point attributes")
  attributes?: Attribute[];

  @doc("Data point flags")
  flags?: DataPointFlags;
}

@doc("Numeric data point (for Gauge and Sum)")
model NumberDataPoint extends DataPointBase {
  @doc("Value as integer")
  @encodedName("application/json", "as_int")
  asInt?: int64;

  @doc("Value as double")
  @encodedName("application/json", "as_double")
  asDouble?: float64;

  @doc("Exemplars for the data point")
  exemplars?: Exemplar[];
}

@doc("Histogram data point")
model HistogramDataPoint extends DataPointBase {
  @doc("Number of values in the histogram")
  count: int64;

  @doc("Sum of all values")
  sum?: float64;

  @doc("Bucket counts")
  @encodedName("application/json", "bucket_counts")
  bucketCounts: int64[];

  @doc("Explicit bucket boundaries")
  @encodedName("application/json", "explicit_bounds")
  explicitBounds: float64[];

  @doc("Minimum value")
  min?: float64;

  @doc("Maximum value")
  max?: float64;

  @doc("Exemplars for the data point")
  exemplars?: Exemplar[];
}

@doc("Exponential histogram data point")
model ExponentialHistogramDataPoint extends DataPointBase {
  @doc("Number of values")
  count: int64;

  @doc("Sum of all values")
  sum?: float64;

  @doc("Scale factor for bucket boundaries")
  scale: int32;

  @doc("Zero count")
  @encodedName("application/json", "zero_count")
  zeroCount: int64;

  @doc("Zero threshold")
  @encodedName("application/json", "zero_threshold")
  zeroThreshold?: float64;

  @doc("Positive bucket counts")
  positive: ExponentialBuckets;

  @doc("Negative bucket counts")
  negative: ExponentialBuckets;

  @doc("Minimum value")
  min?: float64;

  @doc("Maximum value")
  max?: float64;

  @doc("Exemplars for the data point")
  exemplars?: Exemplar[];
}

@doc("Exponential histogram buckets")
model ExponentialBuckets {
  @doc("Offset of the first bucket")
  offset: int32;

  @doc("Bucket counts")
  @encodedName("application/json", "bucket_counts")
  bucketCounts: int64[];
}

@doc("Summary data point")
model SummaryDataPoint extends DataPointBase {
  @doc("Number of values")
  count: int64;

  @doc("Sum of all values")
  sum: float64;

  @doc("Quantile values")
  @encodedName("application/json", "quantile_values")
  quantileValues: QuantileValue[];
}

@doc("Quantile value for summary")
model QuantileValue {
  @doc("Quantile (0.0 to 1.0)")
  @minValue(0.0)
  @maxValue(1.0)
  quantile: float64;

  @doc("Value at this quantile")
  value: float64;
}

@doc("Exemplar - sample trace linked to metric")
model Exemplar {
  @doc("Timestamp in nanoseconds since epoch")
  @encodedName("application/json", "time_unix_nano")
  timeUnixNano: int64;

  @doc("Value as integer")
  @encodedName("application/json", "as_int")
  asInt?: int64;

  @doc("Value as double")
  @encodedName("application/json", "as_double")
  asDouble?: float64;

  @doc("Span ID of the exemplar")
  @encodedName("application/json", "span_id")
  spanId?: SpanId;

  @doc("Trace ID of the exemplar")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Filtered attributes")
  @encodedName("application/json", "filtered_attributes")
  filteredAttributes?: Attribute[];
}

// =============================================================================
// Metric Query & Aggregation
// =============================================================================

@doc("Metric query filters")
model MetricQueryFilters {
  @doc("Filter by metric name (supports wildcards)")
  @query
  name?: string;

  @doc("Filter by metric type")
  @query
  type?: MetricType;

  @doc("Filter by service name")
  @query
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Filter by attribute key-value pair")
  @query
  attribute?: string;

  @doc("Aggregation function")
  @query
  @encodedName("application/json", "agg_func")
  aggFunc?: AggregationFunction;
}

@doc("Aggregation functions for metrics")
enum AggregationFunction {
  @doc("Sum of values")
  sum: "sum",

  @doc("Average of values")
  avg: "avg",

  @doc("Minimum value")
  min: "min",

  @doc("Maximum value")
  max: "max",

  @doc("Count of values")
  count: "count",

  @doc("Latest value")
  last: "last",

  @doc("Rate of change per second")
  rate: "rate",

  @doc("Increase over time range")
  increase: "increase",
}

@doc("Time series data for graphing")
model TimeSeries {
  @doc("Metric name")
  metric: string;

  @doc("Dimension labels")
  labels: Attribute[];

  @doc("Data points over time")
  points: TimeSeriesPoint[];
}

@doc("Single point in a time series")
model TimeSeriesPoint {
  @doc("Timestamp")
  timestamp: utcDateTime;

  @doc("Value")
  value: float64;
}


// ===== Flattened from: core/specs/models/http.tsp =====
// =============================================================================
// QYL v2.0 - HTTP Semantic Conventions (OTel v1.40)
// =============================================================================
// HTTP client and server telemetry attributes following OTel specifications.
// =============================================================================



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
// HTTP Span Attributes (Complete OTel v1.40)
// =============================================================================

@doc("HTTP span attributes following OTel Semantic Conventions v1.40")
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


// ===== Flattened from: core/specs/models/identity.tsp =====
// =============================================================================
// QYL v2.0 - User/Client/Service Semantic Conventions (OTel v1.40)
// =============================================================================
// User, client, service, and app identity attributes.
// =============================================================================



namespace Qyl.Domains.Identity;

// =============================================================================
// User Attributes
// =============================================================================

@doc("User attributes")
model UserAttributes {
  @doc("User ID (unique identifier)")
  @encodedName("application/json", "user.id")
  id?: UserId;

  @doc("User name (may be pseudonymized)")
  @encodedName("application/json", "user.name")
  name?: string;

  @doc("User email (may be hashed)")
  @encodedName("application/json", "user.email")
  email?: string;

  @doc("User full name")
  @encodedName("application/json", "user.full_name")
  fullName?: string;

  @doc("User hash (for privacy)")
  @encodedName("application/json", "user.hash")
  hash?: string;

  @doc("User roles")
  @encodedName("application/json", "user.roles")
  roles?: string[];
}

// =============================================================================
// Client Attributes
// =============================================================================

@doc("Client attributes")
model ClientAttributes {
  @doc("Client IP address")
  @encodedName("application/json", "client.address")
  address?: IpAddress;

  @doc("Client port")
  @encodedName("application/json", "client.port")
  port?: Port;
}

// =============================================================================
// Service Attributes
// =============================================================================

@doc("Service resource attributes")
model ServiceAttributes {
  @doc("Service name (required)")
  @encodedName("application/json", "service.name")
  name: string;

  @doc("Service namespace")
  @encodedName("application/json", "service.namespace")
  `namespace`?: string;

  @doc("Service instance ID (unique per instance)")
  @encodedName("application/json", "service.instance.id")
  instanceId?: string;

  @doc("Service version")
  @encodedName("application/json", "service.version")
  version?: SemVer;
}

// =============================================================================
// App Attributes
// =============================================================================

@doc("Application attributes")
model AppAttributes {
  @doc("Application name")
  @encodedName("application/json", "app.name")
  name?: string;

  @doc("Application version")
  @encodedName("application/json", "app.version")
  version?: SemVer;

  @doc("Application build ID")
  @encodedName("application/json", "app.build.id")
  buildId?: string;
}

// =============================================================================
// Server Attributes
// =============================================================================

@doc("Server attributes")
model ServerAttributes {
  @doc("Server address (hostname or IP)")
  @encodedName("application/json", "server.address")
  address?: string;

  @doc("Server port")
  @encodedName("application/json", "server.port")
  port?: Port;
}

// =============================================================================
// Destination Attributes
// =============================================================================

@doc("Destination attributes (for client spans)")
model DestinationAttributes {
  @doc("Destination address")
  @encodedName("application/json", "destination.address")
  address?: string;

  @doc("Destination port")
  @encodedName("application/json", "destination.port")
  port?: Port;
}

// =============================================================================
// Source Attributes
// =============================================================================

@doc("Source attributes")
model SourceAttributes {
  @doc("Source address")
  @encodedName("application/json", "source.address")
  address?: IpAddress;

  @doc("Source port")
  @encodedName("application/json", "source.port")
  port?: Port;
}

// =============================================================================
// Peer Attributes (Deprecated, for compatibility)
// =============================================================================

@doc("Peer attributes (deprecated)")
model PeerAttributes {
  @doc("Peer service name")
  @encodedName("application/json", "peer.service")
  service?: string;
}

// =============================================================================
// Identity Statistics
// =============================================================================

@doc("User activity statistics")
model UserActivityStats {
  @doc("Total unique users")
  @encodedName("application/json", "total_users")
  totalUsers: Count;

  @doc("Active users in time range")
  @encodedName("application/json", "active_users")
  activeUsers: Count;

  @doc("New users in time range")
  @encodedName("application/json", "new_users")
  newUsers: Count;

  @doc("Users by role")
  @encodedName("application/json", "by_role")
  byRole?: UserRoleStats[];
}

@doc("User stats by role")
model UserRoleStats {
  @doc("Role name")
  role: string;

  @doc("User count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

@doc("Service dependency map")
model ServiceDependency {
  @doc("Source service")
  @encodedName("application/json", "source_service")
  sourceService: string;

  @doc("Target service")
  @encodedName("application/json", "target_service")
  targetService: string;

  @doc("Request count")
  @encodedName("application/json", "request_count")
  requestCount: Count;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Average latency in milliseconds")
  @encodedName("application/json", "avg_latency_ms")
  avgLatencyMs: float64;
}


// ===== Flattened from: core/specs/models/issues.tsp =====
// =============================================================================
// QYL v2.0 - Error Issue Engine Entities
// =============================================================================
// Issue aggregates, events, breadcrumbs, regressions, ownership, releases.
// Sentry-replacement error tracking engine.
// =============================================================================



namespace Qyl.Domains.Issues;

// =============================================================================
// Error Issue
// =============================================================================

@doc("Error issue aggregate with lifecycle tracking")
@extension("x-duckdb-table", "error_issues")
@extension("x-csharp-type", "ErrorIssueEntity")
model ErrorIssueEntity {
  @doc("Issue ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Error fingerprint for grouping")
  fingerprint: string;

  @doc("Issue title")
  title: string;

  @doc("Culprit (function/module causing the error)")
  culprit?: string;

  @doc("Error type (exception class name or code)")
  @encodedName("application/json", "error_type")
  errorType: string;

  @doc("Error category")
  category: string;

  @doc("Severity level")
  level: IssueLevel;

  @doc("Platform (csharp, javascript, python, etc.)")
  platform?: string;

  @doc("First occurrence")
  @encodedName("application/json", "first_seen_at")
  firstSeenAt: utcDateTime;

  @doc("Last occurrence")
  @encodedName("application/json", "last_seen_at")
  lastSeenAt: utcDateTime;

  @doc("Total occurrence count")
  @encodedName("application/json", "occurrence_count")
  occurrenceCount: int64;

  @doc("Affected unique users count")
  @encodedName("application/json", "affected_users_count")
  affectedUsersCount: int32;

  @doc("Issue status")
  status: IssueStatus;

  @doc("Issue substatus")
  substatus?: string;

  @doc("Priority level")
  priority: IssuePriority;

  @doc("Assigned team member")
  @encodedName("application/json", "assigned_to")
  assignedTo?: string;

  @doc("Resolution timestamp")
  @encodedName("application/json", "resolved_at")
  resolvedAt?: utcDateTime;

  @doc("Resolved by")
  @encodedName("application/json", "resolved_by")
  resolvedBy?: string;

  @doc("Number of regressions")
  @encodedName("application/json", "regression_count")
  regressionCount: int32;

  @doc("Last associated release")
  @encodedName("application/json", "last_release")
  lastRelease?: string;

  @doc("Issue tags")
  @encodedName("application/json", "tags_json")
  @extension("x-duckdb-type", "JSON")
  tagsJson?: string;

  @doc("Issue metadata")
  @encodedName("application/json", "metadata_json")
  @extension("x-duckdb-type", "JSON")
  metadataJson?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

@doc("Issue severity level")
enum IssueLevel {
  @doc("Debug level")
  debug: "debug",

  @doc("Info level")
  info: "info",

  @doc("Warning level")
  warning: "warning",

  @doc("Error level")
  error: "error",

  @doc("Fatal/critical level")
  fatal: "fatal",
}

@doc("Issue lifecycle status")
enum IssueStatus {
  @doc("Unresolved/new")
  unresolved: "unresolved",

  @doc("Acknowledged by team")
  acknowledged: "acknowledged",

  @doc("Being investigated")
  investigating: "investigating",

  @doc("Fix in progress")
  inProgress: "in_progress",

  @doc("Resolved")
  resolved: "resolved",

  @doc("Ignored")
  ignored: "ignored",

  @doc("Regressed after resolution")
  regressed: "regressed",
}

@doc("Issue priority")
enum IssuePriority {
  @doc("Critical priority")
  critical: "critical",

  @doc("High priority")
  high: "high",

  @doc("Medium priority")
  medium: "medium",

  @doc("Low priority")
  low: "low",
}

// =============================================================================
// Error Issue Event
// =============================================================================

@doc("Individual error occurrence linked to an issue")
@extension("x-duckdb-table", "error_issue_events")
@extension("x-csharp-type", "ErrorIssueEventEntity")
model ErrorIssueEventEntity {
  @doc("Event ID")
  @key
  id: string;

  @doc("Parent issue")
  @encodedName("application/json", "issue_id")
  issueId: string;

  @doc("Associated trace ID")
  @encodedName("application/json", "trace_id")
  traceId?: string;

  @doc("Associated span ID")
  @encodedName("application/json", "span_id")
  spanId?: string;

  @doc("Error message")
  message?: string;

  @doc("Stack trace")
  @encodedName("application/json", "stack_trace")
  stackTrace?: string;

  @doc("Parsed stack frames")
  @encodedName("application/json", "stack_frames_json")
  @extension("x-duckdb-type", "JSON")
  stackFramesJson?: string;

  @doc("Environment (dev, staging, prod)")
  environment?: string;

  @doc("Release version")
  @encodedName("application/json", "release_version")
  releaseVersion?: string;

  @doc("Affected user ID")
  @encodedName("application/json", "user_id")
  userId?: string;

  @doc("Client IP address")
  @encodedName("application/json", "user_ip")
  userIp?: string;

  @doc("Request URL")
  @encodedName("application/json", "request_url")
  requestUrl?: string;

  @doc("HTTP request method")
  @encodedName("application/json", "request_method")
  requestMethod?: string;

  @doc("Browser info")
  browser?: string;

  @doc("Operating system")
  os?: string;

  @doc("Device info")
  device?: string;

  @doc("Runtime name")
  runtime?: string;

  @doc("Runtime version")
  @encodedName("application/json", "runtime_version")
  runtimeVersion?: string;

  @doc("Additional context data")
  @encodedName("application/json", "context_json")
  @extension("x-duckdb-type", "JSON")
  contextJson?: string;

  @doc("Event tags")
  @encodedName("application/json", "tags_json")
  @extension("x-duckdb-type", "JSON")
  tagsJson?: string;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Error Breadcrumb
// =============================================================================

@doc("Pre-error context breadcrumb")
@extension("x-duckdb-table", "error_breadcrumbs")
@extension("x-csharp-type", "ErrorBreadcrumbEntity")
model ErrorBreadcrumbEntity {
  @doc("Breadcrumb ID")
  @key
  id: string;

  @doc("Parent event")
  @encodedName("application/json", "event_id")
  eventId: string;

  @doc("Breadcrumb type")
  @encodedName("application/json", "breadcrumb_type")
  breadcrumbType: BreadcrumbType;

  @doc("Category (e.g. http, db, ui)")
  category?: string;

  @doc("Breadcrumb message")
  message?: string;

  @doc("Severity level")
  level: string;

  @doc("Breadcrumb data")
  @encodedName("application/json", "data_json")
  @extension("x-duckdb-type", "JSON")
  dataJson?: string;

  @doc("Breadcrumb timestamp")
  timestamp: utcDateTime;
}

@doc("Breadcrumb types")
enum BreadcrumbType {
  @doc("Navigation event")
  navigation: "navigation",

  @doc("HTTP request")
  http: "http",

  @doc("Database query")
  query: "query",

  @doc("User interaction")
  user: "user",

  @doc("Log message")
  log: "log",

  @doc("Error occurrence")
  error: "error",

  @doc("Debug information")
  debug: "debug",

  @doc("Default/other")
  default: "default",
}

// =============================================================================
// Error Regression
// =============================================================================

@doc("Regression window across releases")
@extension("x-duckdb-table", "error_regressions")
@extension("x-csharp-type", "ErrorRegressionEntity")
model ErrorRegressionEntity {
  @doc("Regression ID")
  @key
  id: string;

  @doc("Regressed issue")
  @encodedName("application/json", "issue_id")
  issueId: string;

  @doc("Release where issue was resolved")
  @encodedName("application/json", "resolved_in_release")
  resolvedInRelease: string;

  @doc("Release where issue regressed")
  @encodedName("application/json", "regressed_in_release")
  regressedInRelease: string;

  @doc("Resolution timestamp")
  @encodedName("application/json", "resolved_at")
  resolvedAt: utcDateTime;

  @doc("Regression timestamp")
  @encodedName("application/json", "regressed_at")
  regressedAt: utcDateTime;

  @doc("Occurrences before regression")
  @encodedName("application/json", "occurrence_count_before")
  occurrenceCountBefore: int32;

  @doc("Occurrences after regression")
  @encodedName("application/json", "occurrence_count_after")
  occurrenceCountAfter: int32;

  @doc("Whether auto-detected")
  @encodedName("application/json", "auto_detected")
  autoDetected: boolean;

  @doc("Detection timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

// =============================================================================
// Error Ownership
// =============================================================================

@doc("Code ownership rule for auto-triage")
@extension("x-duckdb-table", "error_ownership")
@extension("x-csharp-type", "ErrorOwnershipEntity")
model ErrorOwnershipEntity {
  @doc("Rule ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Rule type (path, tag, url, module)")
  @encodedName("application/json", "rule_type")
  ruleType: string;

  @doc("Match pattern (glob, regex, or exact)")
  pattern: string;

  @doc("Owner type (user, team)")
  @encodedName("application/json", "owner_type")
  ownerType: string;

  @doc("Owner identifier")
  @encodedName("application/json", "owner_id")
  ownerId: string;

  @doc("Rule priority (higher wins)")
  priority: int32;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

// =============================================================================
// Error Release Marker
// =============================================================================

@doc("Deploy marker for issue auto-transition")
@extension("x-duckdb-table", "error_release_markers")
@extension("x-csharp-type", "ErrorReleaseMarkerEntity")
model ErrorReleaseMarkerEntity {
  @doc("Marker ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Release version")
  @encodedName("application/json", "release_version")
  releaseVersion: string;

  @doc("Target environment")
  environment: string;

  @doc("Git commit SHA")
  @encodedName("application/json", "commit_sha")
  commitSha?: string;

  @doc("Commit message")
  @encodedName("application/json", "commit_message")
  commitMessage?: string;

  @doc("Deployed by")
  @encodedName("application/json", "deployed_by")
  deployedBy?: string;

  @doc("Issues resolved by this release")
  @encodedName("application/json", "issues_resolved_count")
  issuesResolvedCount: int32;

  @doc("Issues regressed by this release")
  @encodedName("application/json", "issues_regressed_count")
  issuesRegressedCount: int32;

  @doc("Deployment timestamp")
  @encodedName("application/json", "deployed_at")
  deployedAt: utcDateTime;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}


// ===== Flattened from: core/specs/models/log.tsp =====
// =============================================================================
// QYL v2.0 - Log Semantic Conventions (OTel v1.40)
// =============================================================================
// Log record attributes and log-specific semantic conventions.
// =============================================================================



namespace Qyl.Domains.Observe.Log;

// =============================================================================
// Log Attributes
// =============================================================================

@doc("Log record attributes")
model LogAttributes {
  @doc("Log record unique identifier")
  @encodedName("application/json", "log.record.uid")
  recordUid?: string;

  @doc("Original log file name")
  @encodedName("application/json", "log.file.name")
  fileName?: string;

  @doc("Full path to log file")
  @encodedName("application/json", "log.file.path")
  filePath?: string;

  @doc("Original log file name resolved")
  @encodedName("application/json", "log.file.name_resolved")
  fileNameResolved?: string;

  @doc("Full path to log file resolved")
  @encodedName("application/json", "log.file.path_resolved")
  filePathResolved?: string;

  @doc("Log iostream (stdout/stderr)")
  @encodedName("application/json", "log.iostream")
  iostream?: LogIostream;
}

@doc("Log IO streams")
enum LogIostream {
  @doc("Standard output")
  stdout: "stdout",

  @doc("Standard error")
  stderr: "stderr",
}

// =============================================================================
// Event Attributes
// =============================================================================

@doc("Event domain for event.name categorization")
enum EventDomain {
  @doc("Browser events")
  browser: "browser",

  @doc("Device events")
  device: "device",

  @doc("Kubernetes events")
  k8s: "k8s",

  @doc("User events")
  user: "user",

  @doc("Session events")
  session: "session",

  @doc("Application events")
  app: "app",

  @doc("Feature flag events")
  featureFlag: "feature_flag",

  @doc("Deployment events")
  deployment: "deployment",

  @doc("CI/CD events")
  cicd: "cicd",
}

@doc("Log event attributes")
model EventAttributes {
  @doc("Event name (with domain prefix)")
  @encodedName("application/json", "event.name")
  name: string;

  @doc("Event domain")
  @encodedName("application/json", "event.domain")
  domain?: EventDomain;
}

// =============================================================================
// Log Severity Mapping
// =============================================================================

@doc("Log severity text to number mapping")
model SeverityMapping {
  @doc("Severity text")
  text: string;

  @doc("Severity number (1-24)")
  number: SeverityNumber;
}

// =============================================================================
// Log Query Models
// =============================================================================

@doc("Log search query")
model LogQuery {
  @doc("Free text search")
  query?: string;

  @doc("Severity filter")
  @encodedName("application/json", "severity_min")
  severityMin?: SeverityNumber;

  @doc("Service name filter")
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Trace ID filter")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Span ID filter")
  @encodedName("application/json", "span_id")
  spanId?: SpanId;

  @doc("Time range start")
  @encodedName("application/json", "time_start")
  timeStart?: utcDateTime;

  @doc("Time range end")
  @encodedName("application/json", "time_end")
  timeEnd?: utcDateTime;

  @doc("Attribute filters")
  @encodedName("application/json", "attribute_filters")
  attributeFilters?: AttributeFilter[];

  @doc("Limit")
  @minValue(1)
  @maxValue(10000)
  limit?: int32;

  @doc("Order by")
  @encodedName("application/json", "order_by")
  orderBy?: LogOrderBy;
}

@doc("Attribute filter")
model AttributeFilter {
  @doc("Attribute key")
  key: string;

  @doc("Filter operator")
  operator: FilterOperator;

  @doc("Filter value")
  value: string;
}

@doc("Filter operators")
enum FilterOperator {
  @doc("Equals")
  eq: "eq",

  @doc("Not equals")
  neq: "neq",

  @doc("Contains")
  contains: "contains",

  @doc("Starts with")
  startsWith: "starts_with",

  @doc("Ends with")
  endsWith: "ends_with",

  @doc("Regex match")
  regex: "regex",

  @doc("Greater than")
  gt: "gt",

  @doc("Greater than or equal")
  gte: "gte",

  @doc("Less than")
  lt: "lt",

  @doc("Less than or equal")
  lte: "lte",

  @doc("In list")
  in: "in",

  @doc("Not in list")
  notIn: "not_in",

  @doc("Exists")
  exists: "exists",

  @doc("Does not exist")
  notExists: "not_exists",
}

@doc("Log ordering options")
enum LogOrderBy {
  @doc("Timestamp ascending")
  timestampAsc: "timestamp_asc",

  @doc("Timestamp descending")
  timestampDesc: "timestamp_desc",

  @doc("Severity ascending")
  severityAsc: "severity_asc",

  @doc("Severity descending")
  severityDesc: "severity_desc",
}

// =============================================================================
// Log Aggregation
// =============================================================================

@doc("Log aggregation request")
model LogAggregation {
  @doc("Group by fields")
  @encodedName("application/json", "group_by")
  groupBy: string[];

  @doc("Aggregation function")
  function: AggregationFunction;

  @doc("Field to aggregate (for non-count)")
  field?: string;

  @doc("Time bucket (for time series)")
  @encodedName("application/json", "time_bucket")
  timeBucket?: TimeBucket;

  @doc("Top N results")
  @encodedName("application/json", "top_n")
  topN?: int32;
}

@doc("Aggregation functions")
enum AggregationFunction {
  @doc("Count")
  count: "count",

  @doc("Sum")
  sum: "sum",

  @doc("Average")
  avg: "avg",

  @doc("Minimum")
  min: "min",

  @doc("Maximum")
  max: "max",

  @doc("Percentile 50")
  p50: "p50",

  @doc("Percentile 90")
  p90: "p90",

  @doc("Percentile 95")
  p95: "p95",

  @doc("Percentile 99")
  p99: "p99",

  @doc("Count distinct")
  countDistinct: "count_distinct",
}

@doc("Time bucket sizes")
enum TimeBucket {
  @doc("1 second")
  s1: "1s",

  @doc("10 seconds")
  s10: "10s",

  @doc("30 seconds")
  s30: "30s",

  @doc("1 minute")
  m1: "1m",

  @doc("5 minutes")
  m5: "5m",

  @doc("15 minutes")
  m15: "15m",

  @doc("30 minutes")
  m30: "30m",

  @doc("1 hour")
  h1: "1h",

  @doc("6 hours")
  h6: "6h",

  @doc("12 hours")
  h12: "12h",

  @doc("1 day")
  d1: "1d",

  @doc("1 week")
  w1: "1w",
}

// =============================================================================
// Log Statistics
// =============================================================================

@doc("Log statistics")
model LogStats {
  @doc("Total log count")
  @encodedName("application/json", "total_count")
  totalCount: Count;

  @doc("Logs by severity")
  @encodedName("application/json", "by_severity")
  bySeverity: LogSeverityStats[];

  @doc("Logs by service")
  @encodedName("application/json", "by_service")
  byService?: LogServiceStats[];

  @doc("Log rate per second")
  @encodedName("application/json", "rate_per_second")
  ratePerSecond: float64;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;
}

@doc("Log stats by severity")
model LogSeverityStats {
  @doc("Severity number")
  severity: SeverityNumber;

  @doc("Severity text")
  @encodedName("application/json", "severity_text")
  severityText: string;

  @doc("Count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

@doc("Log stats by service")
model LogServiceStats {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("Total count")
  count: Count;

  @doc("Error count")
  @encodedName("application/json", "error_count")
  errorCount: Count;

  @doc("Rate per second")
  @encodedName("application/json", "rate_per_second")
  ratePerSecond: float64;
}

// =============================================================================
// Log Patterns
// =============================================================================

@doc("Detected log pattern")
model LogPattern {
  @doc("Pattern ID")
  @encodedName("application/json", "pattern_id")
  patternId: string;

  @doc("Pattern template")
  template: string;

  @doc("Sample log message")
  sample: string;

  @doc("Occurrence count")
  count: Count;

  @doc("First seen")
  @encodedName("application/json", "first_seen")
  firstSeen: utcDateTime;

  @doc("Last seen")
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;

  @doc("Trend")
  trend: LogPatternTrend;

  @doc("Severity distribution")
  @encodedName("application/json", "severity_distribution")
  severityDistribution?: LogSeverityStats[];
}

@doc("Log pattern trend")
enum LogPatternTrend {
  @doc("Increasing")
  increasing: "increasing",

  @doc("Decreasing")
  decreasing: "decreasing",

  @doc("Stable")
  stable: "stable",

  @doc("New pattern")
  new: "new",

  @doc("Anomalous spike")
  spike: "spike",
}

// =============================================================================
// Structured Log Format
// =============================================================================

@doc("Structured log format detection")
enum StructuredLogFormat {
  @doc("JSON format")
  json: "json",

  @doc("Logfmt format")
  logfmt: "logfmt",

  @doc("Apache Common Log Format")
  clf: "clf",

  @doc("Apache Combined Log Format")
  combined: "combined",

  @doc("Syslog format")
  syslog: "syslog",

  @doc("W3C Extended Log Format")
  w3c: "w3c",

  @doc("CSV format")
  csv: "csv",

  @doc("Unstructured/plaintext")
  plaintext: "plaintext",
}


// ===== Flattened from: core/specs/models/messaging.tsp =====
// =============================================================================
// QYL v2.0 - Messaging Semantic Conventions (OTel v1.40)
// =============================================================================
// Message broker and queue telemetry attributes.
// =============================================================================



namespace Qyl.Domains.Transport.Messaging;

// =============================================================================
// Messaging Span Attributes
// =============================================================================

@doc("Messaging span attributes")
model MessagingAttributes {
  @doc("The messaging system (e.g., kafka, rabbitmq, aws_sqs)")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Message destination name (queue or topic)")
  @encodedName("application/json", "messaging.destination.name")
  destinationName?: string;

  @doc("Destination kind (queue or topic)")
  @encodedName("application/json", "messaging.destination.kind")
  destinationKind?: MessagingDestinationKind;

  @doc("Whether the destination is temporary")
  @encodedName("application/json", "messaging.destination.temporary")
  destinationTemporary?: boolean;

  @doc("Whether the destination is anonymous")
  @encodedName("application/json", "messaging.destination.anonymous")
  destinationAnonymous?: boolean;

  @doc("Partition number (for partitioned systems)")
  @encodedName("application/json", "messaging.destination.partition.id")
  destinationPartitionId?: string;

  @doc("Destination template (e.g., for subscription patterns)")
  @encodedName("application/json", "messaging.destination.template")
  destinationTemplate?: string;

  @doc("Message ID")
  @encodedName("application/json", "messaging.message.id")
  messageId?: string;

  @doc("Conversation/correlation ID")
  @encodedName("application/json", "messaging.message.conversation_id")
  messageConversationId?: string;

  @doc("Message body size in bytes")
  @encodedName("application/json", "messaging.message.body.size")
  messageBodySize?: ByteSize;

  @doc("Total message size including metadata")
  @encodedName("application/json", "messaging.message.envelope.size")
  messageEnvelopeSize?: ByteSize;

  @doc("Messaging operation type")
  @encodedName("application/json", "messaging.operation.type")
  operationType?: MessagingOperationType;

  @doc("Operation name")
  @encodedName("application/json", "messaging.operation.name")
  operationName?: string;

  @doc("Batch message count")
  @encodedName("application/json", "messaging.batch.message_count")
  batchMessageCount?: int32;

  @doc("Consumer group name")
  @encodedName("application/json", "messaging.consumer.group.name")
  consumerGroupName?: string;

  @doc("Consumer ID")
  @encodedName("application/json", "messaging.client.id")
  clientId?: string;

  // ---------------------------------------------------------------------------
  // Kafka-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("Kafka consumer group")
  @encodedName("application/json", "messaging.kafka.consumer.group")
  kafkaConsumerGroup?: string;

  @doc("Kafka message key")
  @encodedName("application/json", "messaging.kafka.message.key")
  kafkaMessageKey?: string;

  @doc("Kafka message offset")
  @encodedName("application/json", "messaging.kafka.message.offset")
  kafkaMessageOffset?: int64;

  @doc("Kafka message tombstone")
  @encodedName("application/json", "messaging.kafka.message.tombstone")
  kafkaMessageTombstone?: boolean;

  // ---------------------------------------------------------------------------
  // RabbitMQ-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("RabbitMQ delivery tag")
  @encodedName("application/json", "messaging.rabbitmq.delivery_tag")
  rabbitmqDeliveryTag?: int64;

  @doc("RabbitMQ routing key")
  @encodedName("application/json", "messaging.rabbitmq.destination.routing_key")
  rabbitmqRoutingKey?: string;

  // ---------------------------------------------------------------------------
  // AWS SQS/SNS-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("AWS SQS message group ID")
  @encodedName("application/json", "messaging.aws_sqs.message.group_id")
  awsSqsMessageGroupId?: string;

  @doc("AWS SQS message deduplication ID")
  @encodedName("application/json", "messaging.aws_sqs.message.deduplication_id")
  awsSqsMessageDeduplicationId?: string;

  // ---------------------------------------------------------------------------
  // Azure Service Bus-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("Azure Service Bus message enqueued time")
  @encodedName("application/json", "messaging.servicebus.message.enqueued_time")
  servicebusEnqueuedTime?: utcDateTime;
}

// =============================================================================
// Messaging Enumerations
// =============================================================================

@doc("Messaging systems")
enum MessagingSystem {
  @doc("Apache Kafka")
  kafka: "kafka",

  @doc("RabbitMQ")
  rabbitmq: "rabbitmq",

  @doc("Amazon SQS")
  awsSqs: "aws_sqs",

  @doc("Amazon SNS")
  awsSns: "aws_sns",

  @doc("Azure Service Bus")
  azureServiceBus: "azure_servicebus",

  @doc("Azure Event Hubs")
  azureEventHubs: "azure_eventhubs",

  @doc("Google Cloud Pub/Sub")
  gcpPubsub: "gcp_pubsub",

  @doc("Apache ActiveMQ")
  activemq: "activemq",

  @doc("Apache ActiveMQ Artemis")
  activemqArtemis: "activemq_artemis",

  @doc("Redis Pub/Sub")
  redisPubsub: "redis_pubsub",

  @doc("Redis Streams")
  redisStreams: "redis_streams",

  @doc("NATS")
  nats: "nats",

  @doc("Pulsar")
  pulsar: "pulsar",

  @doc("JMS")
  jms: "jms",

  @doc("IBM MQ")
  ibmmq: "ibmmq",

  @doc("RocketMQ")
  rocketmq: "rocketmq",
}

@doc("Messaging destination kinds")
enum MessagingDestinationKind {
  @doc("Queue (point-to-point)")
  queue: "queue",

  @doc("Topic (publish-subscribe)")
  topic: "topic",
}

@doc("Messaging operation types")
enum MessagingOperationType {
  @doc("Message publish/send")
  publish: "publish",

  @doc("Message receive (non-blocking)")
  receive: "receive",

  @doc("Message process (blocking consumer)")
  process: "process",

  @doc("Create destination")
  create: "create",

  @doc("Settle/acknowledge message")
  settle: "settle",
}

@doc("Message delivery guarantees")
enum MessagingDeliveryGuarantee {
  @doc("At most once delivery")
  atMostOnce: "at_most_once",

  @doc("At least once delivery")
  atLeastOnce: "at_least_once",

  @doc("Exactly once delivery")
  exactlyOnce: "exactly_once",
}

// =============================================================================
// Messaging Metrics
// =============================================================================

@doc("Messaging publish duration metric")
model MessagingPublishDurationMetric {
  @doc("Metric name")
  name: "messaging.publish.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;
}

@doc("Messaging receive duration metric")
model MessagingReceiveDurationMetric {
  @doc("Metric name")
  name: "messaging.receive.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;

  @doc("Consumer group")
  @encodedName("application/json", "messaging.consumer.group.name")
  consumerGroupName?: string;
}

@doc("Messaging process duration metric")
model MessagingProcessDurationMetric {
  @doc("Metric name")
  name: "messaging.process.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;
}

@doc("Messaging publish messages metric")
model MessagingPublishMessagesMetric {
  @doc("Metric name")
  name: "messaging.publish.messages";

  @doc("Metric unit")
  unit: "{message}";

  @doc("Messaging system")
  @encodedName("application/json", "messaging.system")
  system: MessagingSystem;

  @doc("Destination name")
  @encodedName("application/json", "messaging.destination.name")
  destinationName: string;
}


// ===== Flattened from: core/specs/models/otel-config.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry SDK Semantic Conventions (OTel v1.40)
// =============================================================================
// OTel SDK configuration and telemetry scope attributes.
// =============================================================================



namespace Qyl.Domains.Observe.Otel;

// =============================================================================
// Telemetry SDK Attributes
// =============================================================================

@doc("Telemetry SDK resource attributes")
model TelemetrySdkAttributes {
  @doc("SDK name")
  @encodedName("application/json", "telemetry.sdk.name")
  name: string;

  @doc("SDK language")
  @encodedName("application/json", "telemetry.sdk.language")
  language: TelemetrySdkLanguage;

  @doc("SDK version")
  @encodedName("application/json", "telemetry.sdk.version")
  version: SemVer;
}

@doc("Telemetry distro attributes")
model TelemetryDistroAttributes {
  @doc("Distro name")
  @encodedName("application/json", "telemetry.distro.name")
  name?: string;

  @doc("Distro version")
  @encodedName("application/json", "telemetry.distro.version")
  version?: SemVer;
}

// =============================================================================
// SDK Language Enumeration
// =============================================================================

@doc("Telemetry SDK languages")
enum TelemetrySdkLanguage {
  @doc("C++")
  cpp: "cpp",

  @doc(".NET")
  dotnet: "dotnet",

  @doc("Erlang")
  erlang: "erlang",

  @doc("Go")
  go: "go",

  @doc("Java")
  java: "java",

  @doc("Node.js")
  nodejs: "nodejs",

  @doc("PHP")
  php: "php",

  @doc("Python")
  python: "python",

  @doc("Ruby")
  ruby: "ruby",

  @doc("Rust")
  rust: "rust",

  @doc("Swift")
  swift: "swift",

  @doc("Kotlin")
  kotlin: "kotlin",

  @doc("Scala")
  scala: "scala",
}

// =============================================================================
// Instrumentation Scope Attributes
// =============================================================================

@doc("Extended instrumentation scope with telemetry details")
model InstrumentationScopeDetails {
  @doc("Scope name (library/module name)")
  name: string;

  @doc("Scope version")
  version?: SemVer;

  @doc("Schema URL")
  @encodedName("application/json", "schema_url")
  schemaUrl?: UrlString;

  @doc("Scope attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: int32;
}

// =============================================================================
// OTel Collector Configuration
// =============================================================================

@doc("OTel collector endpoint configuration")
model OtelEndpointConfig {
  @doc("OTLP endpoint URL")
  endpoint: UrlString;

  @doc("Protocol")
  protocol: OtlpProtocol;

  @doc("Headers")
  headers?: Record<string>;

  @doc("Compression")
  compression?: OtlpCompression;

  @doc("Timeout in milliseconds")
  @encodedName("application/json", "timeout_ms")
  timeoutMs?: int32;

  @doc("Retry configuration")
  retry?: RetryConfig;
}

@doc("OTLP protocols")
enum OtlpProtocol {
  @doc("gRPC protocol")
  grpc: "grpc",

  @doc("HTTP/protobuf")
  httpProtobuf: "http/protobuf",

  @doc("HTTP/JSON")
  httpJson: "http/json",
}

@doc("OTLP compression types")
enum OtlpCompression {
  @doc("No compression")
  none: "none",

  @doc("Gzip compression")
  gzip: "gzip",

  @doc("Zstd compression")
  zstd: "zstd",
}

@doc("Retry configuration")
model RetryConfig {
  @doc("Enable retry")
  enabled: boolean;

  @doc("Initial interval in milliseconds")
  @encodedName("application/json", "initial_interval_ms")
  initialIntervalMs?: int32;

  @doc("Max interval in milliseconds")
  @encodedName("application/json", "max_interval_ms")
  maxIntervalMs?: int32;

  @doc("Max elapsed time in milliseconds")
  @encodedName("application/json", "max_elapsed_time_ms")
  maxElapsedTimeMs?: int32;

  @doc("Multiplier for exponential backoff")
  multiplier?: float64;
}

// =============================================================================
// Batch Processor Configuration
// =============================================================================

@doc("Batch processor configuration")
model BatchProcessorConfig {
  @doc("Max queue size")
  @encodedName("application/json", "max_queue_size")
  maxQueueSize?: int32;

  @doc("Scheduled delay in milliseconds")
  @encodedName("application/json", "scheduled_delay_ms")
  scheduledDelayMs?: int32;

  @doc("Export timeout in milliseconds")
  @encodedName("application/json", "export_timeout_ms")
  exportTimeoutMs?: int32;

  @doc("Max export batch size")
  @encodedName("application/json", "max_export_batch_size")
  maxExportBatchSize?: int32;
}

// =============================================================================
// Sampling Configuration
// =============================================================================

@doc("Sampler configuration")
model SamplerConfig {
  @doc("Sampler type")
  type: SamplerType;

  @doc("Sample ratio (for ratio-based samplers)")
  ratio?: Ratio;

  @doc("Parent-based root sampler")
  @encodedName("application/json", "root_sampler")
  rootSampler?: SamplerType;
}

@doc("Sampler types")
enum SamplerType {
  @doc("Always on")
  alwaysOn: "always_on",

  @doc("Always off")
  alwaysOff: "always_off",

  @doc("Trace ID ratio based")
  traceIdRatio: "trace_id_ratio",

  @doc("Parent based")
  parentBased: "parent_based",

  @doc("Jaeger remote")
  jaegerRemote: "jaeger_remote",
}

// =============================================================================
// Resource Detection
// =============================================================================

@doc("Resource detector configuration")
model ResourceDetectorConfig {
  @doc("Enabled detectors")
  detectors: ResourceDetector[];

  @doc("Timeout in milliseconds")
  @encodedName("application/json", "timeout_ms")
  timeoutMs?: int32;
}

@doc("Resource detectors")
enum ResourceDetector {
  @doc("Environment variables")
  env: "env",

  @doc("Host")
  host: "host",

  @doc("OS")
  os: "os",

  @doc("Process")
  process: "process",

  @doc("Container")
  container: "container",

  @doc("Kubernetes")
  k8s: "k8s",

  @doc("AWS EC2")
  awsEc2: "aws_ec2",

  @doc("AWS ECS")
  awsEcs: "aws_ecs",

  @doc("AWS EKS")
  awsEks: "aws_eks",

  @doc("AWS Lambda")
  awsLambda: "aws_lambda",

  @doc("Azure VM")
  azureVm: "azure_vm",

  @doc("Azure App Service")
  azureAppService: "azure_app_service",

  @doc("Azure Functions")
  azureFunctions: "azure_functions",

  @doc("GCP Compute Engine")
  gcpGce: "gcp_gce",

  @doc("GCP Cloud Run")
  gcpCloudRun: "gcp_cloud_run",

  @doc("GCP Cloud Functions")
  gcpCloudFunctions: "gcp_cloud_functions",
}

// =============================================================================
// Propagation Configuration
// =============================================================================

@doc("Context propagation configuration")
model PropagationConfig {
  @doc("Propagators to use")
  propagators: Propagator[];
}

@doc("Context propagators")
enum Propagator {
  @doc("W3C Trace Context")
  tracecontext: "tracecontext",

  @doc("W3C Baggage")
  baggage: "baggage",

  @doc("B3 (single header)")
  b3: "b3",

  @doc("B3 (multi header)")
  b3multi: "b3multi",

  @doc("Jaeger")
  jaeger: "jaeger",

  @doc("X-Ray")
  xray: "xray",

  @doc("OpenTracing")
  ot: "ot",
}

// =============================================================================
// Complete SDK Configuration
// =============================================================================

@doc("Complete OTel SDK configuration")
model OtelSdkConfig {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("Service version")
  @encodedName("application/json", "service_version")
  serviceVersion?: SemVer;

  @doc("Deployment environment")
  @encodedName("application/json", "deployment_environment")
  deploymentEnvironment?: string;

  @doc("Traces configuration")
  traces?: TracesConfig;

  @doc("Metrics configuration")
  metrics?: MetricsConfig;

  @doc("Logs configuration")
  logs?: LogsConfig;

  @doc("Resource detectors")
  @encodedName("application/json", "resource_detectors")
  resourceDetectors?: ResourceDetectorConfig;

  @doc("Propagation configuration")
  propagation?: PropagationConfig;
}

@doc("Traces pipeline configuration")
model TracesConfig {
  @doc("Enabled")
  enabled: boolean;

  @doc("Exporter endpoint")
  exporter?: OtelEndpointConfig;

  @doc("Batch processor")
  @encodedName("application/json", "batch_processor")
  batchProcessor?: BatchProcessorConfig;

  @doc("Sampler")
  sampler?: SamplerConfig;
}

@doc("Metrics pipeline configuration")
model MetricsConfig {
  @doc("Enabled")
  enabled: boolean;

  @doc("Exporter endpoint")
  exporter?: OtelEndpointConfig;

  @doc("Export interval in milliseconds")
  @encodedName("application/json", "export_interval_ms")
  exportIntervalMs?: int32;
}

@doc("Logs pipeline configuration")
model LogsConfig {
  @doc("Enabled")
  enabled: boolean;

  @doc("Exporter endpoint")
  exporter?: OtelEndpointConfig;

  @doc("Batch processor")
  @encodedName("application/json", "batch_processor")
  batchProcessor?: BatchProcessorConfig;
}

// =============================================================================
// Telemetry Statistics
// =============================================================================

@doc("Telemetry pipeline statistics")
model TelemetryStats {
  @doc("Spans exported")
  @encodedName("application/json", "spans_exported")
  spansExported: Count;

  @doc("Spans dropped")
  @encodedName("application/json", "spans_dropped")
  spansDropped: Count;

  @doc("Metrics exported")
  @encodedName("application/json", "metrics_exported")
  metricsExported: Count;

  @doc("Metrics dropped")
  @encodedName("application/json", "metrics_dropped")
  metricsDropped: Count;

  @doc("Logs exported")
  @encodedName("application/json", "logs_exported")
  logsExported: Count;

  @doc("Logs dropped")
  @encodedName("application/json", "logs_dropped")
  logsDropped: Count;

  @doc("Export errors")
  @encodedName("application/json", "export_errors")
  exportErrors: Count;

  @doc("Queue utilization ratio")
  @encodedName("application/json", "queue_utilization")
  queueUtilization: Ratio;
}


// ===== Flattened from: core/specs/models/retention.tsp =====
// =============================================================================
// QYL v2.0 - Ops & Retention Entities
// =============================================================================
// Retention policies, compaction state, backfill jobs.
// =============================================================================



namespace Qyl.Domains.Ops.Retention;

// =============================================================================
// Retention Policy
// =============================================================================

@doc("Per-table data retention rule")
@extension("x-duckdb-table", "retention_policies")
@extension("x-csharp-type", "RetentionPolicyEntity")
model RetentionPolicyEntity {
  @doc("Policy ID")
  @key
  id: string;

  @doc("Target DuckDB table")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("Retention period in days")
  @encodedName("application/json", "retention_days")
  retentionDays: int32;

  @doc("Maximum row count (null = unlimited)")
  @encodedName("application/json", "max_row_count")
  maxRowCount?: int64;

  @doc("Cleanup strategy")
  @encodedName("application/json", "cleanup_strategy")
  cleanupStrategy: CleanupStrategy;

  @doc("Last cleanup timestamp")
  @encodedName("application/json", "last_cleanup_at")
  lastCleanupAt?: utcDateTime;

  @doc("Total rows cleaned")
  @encodedName("application/json", "rows_cleaned")
  rowsCleaned: int64;

  @doc("Whether policy is enabled")
  enabled: boolean;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

@doc("Data cleanup strategies")
enum CleanupStrategy {
  @doc("Delete old rows")
  delete: "delete",

  @doc("Archive to cold storage")
  archive: "archive",

  @doc("Compact/aggregate old data")
  compact: "compact",
}

// =============================================================================
// Compaction State
// =============================================================================

@doc("DuckDB compaction progress tracker")
@extension("x-duckdb-table", "compaction_state")
@extension("x-csharp-type", "CompactionStateEntity")
model CompactionStateEntity {
  @doc("State ID")
  @key
  id: string;

  @doc("Target DuckDB table")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("High watermark (newest compacted row)")
  @encodedName("application/json", "high_watermark")
  highWatermark?: utcDateTime;

  @doc("Low watermark (oldest retained row)")
  @encodedName("application/json", "low_watermark")
  lowWatermark?: utcDateTime;

  @doc("Total rows compacted")
  @encodedName("application/json", "rows_compacted")
  rowsCompacted: int64;

  @doc("Rows remaining")
  @encodedName("application/json", "rows_remaining")
  rowsRemaining: int64;

  @doc("Last compaction timestamp")
  @encodedName("application/json", "last_compaction_at")
  lastCompactionAt?: utcDateTime;

  @doc("Last compaction duration in ms")
  @encodedName("application/json", "last_duration_ms")
  lastDurationMs?: int32;

  @doc("Next scheduled compaction")
  @encodedName("application/json", "next_scheduled_at")
  nextScheduledAt?: utcDateTime;

  @doc("Compaction status")
  status: CompactionStatus;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

@doc("Compaction status")
enum CompactionStatus {
  @doc("Idle/waiting")
  idle: "idle",

  @doc("Currently compacting")
  running: "running",

  @doc("Scheduled for next run")
  scheduled: "scheduled",

  @doc("Failed")
  failed: "failed",
}

// =============================================================================
// Backfill Job
// =============================================================================

@doc("Bulk data maintenance job (backfill, reindex, repair)")
@extension("x-duckdb-table", "backfill_jobs")
@extension("x-csharp-type", "BackfillJobEntity")
model BackfillJobEntity {
  @doc("Job ID")
  @key
  id: string;

  @doc("Job type")
  @encodedName("application/json", "job_type")
  jobType: BackfillJobType;

  @doc("Target table")
  @encodedName("application/json", "target_table")
  targetTable: string;

  @doc("Job description")
  description?: string;

  @doc("Row filter")
  @encodedName("application/json", "filter_json")
  @extension("x-duckdb-type", "JSON")
  filterJson?: string;

  @doc("Job status")
  status: BackfillJobStatus;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Total rows to process")
  @encodedName("application/json", "total_rows")
  totalRows?: int64;

  @doc("Rows processed so far")
  @encodedName("application/json", "processed_rows")
  processedRows: int64;

  @doc("Progress percentage")
  @encodedName("application/json", "progress_pct")
  progressPct: float64;

  @doc("Queue timestamp")
  @encodedName("application/json", "queued_at")
  queuedAt: utcDateTime;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;
}

@doc("Backfill job types")
enum BackfillJobType {
  @doc("Backfill missing data")
  backfill: "backfill",

  @doc("Rebuild search index")
  reindex: "reindex",

  @doc("Repair data integrity")
  repair: "repair",

  @doc("Migrate data format")
  migrate: "migrate",
}

@doc("Backfill job status")
enum BackfillJobStatus {
  @doc("Pending execution")
  pending: "pending",

  @doc("Currently running")
  running: "running",

  @doc("Paused")
  paused: "paused",

  @doc("Successfully completed")
  completed: "completed",

  @doc("Failed")
  failed: "failed",

  @doc("Cancelled")
  cancelled: "cancelled",
}


// ===== Flattened from: core/specs/models/rpc.tsp =====
// =============================================================================
// QYL v2.0 - RPC Semantic Conventions (OTel v1.40)
// =============================================================================
// Remote Procedure Call telemetry attributes (gRPC, etc.).
// =============================================================================



namespace Qyl.Domains.Transport.Rpc;

// =============================================================================
// RPC Span Attributes
// =============================================================================

@doc("RPC span attributes")
model RpcAttributes {
  @doc("The RPC system (e.g., grpc, wcf, java_rmi)")
  @encodedName("application/json", "rpc.system")
  system: RpcSystem;

  @doc("The RPC service name")
  @encodedName("application/json", "rpc.service")
  service?: string;

  @doc("The RPC method name")
  @encodedName("application/json", "rpc.method")
  method?: string;

  @doc("Server address")
  @encodedName("application/json", "server.address")
  serverAddress?: string;

  @doc("Server port")
  @encodedName("application/json", "server.port")
  serverPort?: Port;

  // ---------------------------------------------------------------------------
  // gRPC-Specific Attributes
  // ---------------------------------------------------------------------------

  @doc("gRPC status code")
  @encodedName("application/json", "rpc.grpc.status_code")
  grpcStatusCode?: GrpcStatusCode;

  @doc("gRPC request metadata. Keys map to rpc.grpc.request.metadata.<key>")
  @encodedName("application/json", "rpc.grpc.request.metadata")
  grpcRequestMetadata?: Record<string[]>;

  @doc("gRPC response metadata. Keys map to rpc.grpc.response.metadata.<key>")
  @encodedName("application/json", "rpc.grpc.response.metadata")
  grpcResponseMetadata?: Record<string[]>;

  // ---------------------------------------------------------------------------
  // Message Attributes
  // ---------------------------------------------------------------------------

  @doc("Message type (SENT or RECEIVED)")
  @encodedName("application/json", "rpc.message.type")
  messageType?: RpcMessageType;

  @doc("Message ID (sequence number)")
  @encodedName("application/json", "rpc.message.id")
  messageId?: int64;

  @doc("Compressed message size in bytes")
  @encodedName("application/json", "rpc.message.compressed_size")
  messageCompressedSize?: ByteSize;

  @doc("Uncompressed message size in bytes")
  @encodedName("application/json", "rpc.message.uncompressed_size")
  messageUncompressedSize?: ByteSize;

  // ---------------------------------------------------------------------------
  // Connect RPC Attributes
  // ---------------------------------------------------------------------------

  @doc("Connect RPC error code")
  @encodedName("application/json", "rpc.connect_rpc.error_code")
  connectRpcErrorCode?: ConnectRpcErrorCode;

  // ---------------------------------------------------------------------------
  // JSON-RPC Attributes
  // ---------------------------------------------------------------------------

  @doc("JSON-RPC error code")
  @encodedName("application/json", "rpc.jsonrpc.error_code")
  jsonrpcErrorCode?: int32;

  @doc("JSON-RPC error message")
  @encodedName("application/json", "rpc.jsonrpc.error_message")
  jsonrpcErrorMessage?: string;

  @doc("JSON-RPC request ID")
  @encodedName("application/json", "rpc.jsonrpc.request_id")
  jsonrpcRequestId?: string;

  @doc("JSON-RPC version")
  @encodedName("application/json", "rpc.jsonrpc.version")
  jsonrpcVersion?: string;
}

// =============================================================================
// RPC Enumerations
// =============================================================================

@doc("RPC systems")
enum RpcSystem {
  @doc("gRPC protocol")
  grpc: "grpc",

  @doc("Apache Thrift")
  apacheThrift: "apache_thrift",

  @doc("Connect RPC")
  connectRpc: "connect_rpc",

  @doc("JSON-RPC")
  jsonrpc: "jsonrpc",

  @doc("Apache Dubbo")
  apacheDubbo: "apache_dubbo",

  @doc("Windows Communication Foundation")
  wcf: "wcf",

  @doc("Java RMI")
  javaRmi: "java_rmi",

  @doc(".NET Remoting")
  dotnetRemoting: "dotnet_remoting",
}

@doc("gRPC status codes")
enum GrpcStatusCode {
  @doc("Success")
  ok: 0,

  @doc("Cancelled by client")
  cancelled: 1,

  @doc("Unknown error")
  `unknown`: 2,

  @doc("Invalid argument")
  invalidArgument: 3,

  @doc("Deadline exceeded")
  deadlineExceeded: 4,

  @doc("Not found")
  notFound: 5,

  @doc("Already exists")
  alreadyExists: 6,

  @doc("Permission denied")
  permissionDenied: 7,

  @doc("Resource exhausted")
  resourceExhausted: 8,

  @doc("Failed precondition")
  failedPrecondition: 9,

  @doc("Aborted")
  aborted: 10,

  @doc("Out of range")
  outOfRange: 11,

  @doc("Unimplemented")
  unimplemented: 12,

  @doc("Internal error")
  internal: 13,

  @doc("Unavailable")
  unavailable: 14,

  @doc("Data loss")
  dataLoss: 15,

  @doc("Unauthenticated")
  unauthenticated: 16,
}

@doc("Connect RPC error codes")
enum ConnectRpcErrorCode {
  @doc("Cancelled")
  cancelled: "cancelled",

  @doc("Unknown")
  `unknown`: "unknown",

  @doc("Invalid argument")
  invalidArgument: "invalid_argument",

  @doc("Deadline exceeded")
  deadlineExceeded: "deadline_exceeded",

  @doc("Not found")
  notFound: "not_found",

  @doc("Already exists")
  alreadyExists: "already_exists",

  @doc("Permission denied")
  permissionDenied: "permission_denied",

  @doc("Resource exhausted")
  resourceExhausted: "resource_exhausted",

  @doc("Failed precondition")
  failedPrecondition: "failed_precondition",

  @doc("Aborted")
  aborted: "aborted",

  @doc("Out of range")
  outOfRange: "out_of_range",

  @doc("Unimplemented")
  unimplemented: "unimplemented",

  @doc("Internal")
  internal: "internal",

  @doc("Unavailable")
  unavailable: "unavailable",

  @doc("Data loss")
  dataLoss: "data_loss",

  @doc("Unauthenticated")
  unauthenticated: "unauthenticated",
}

@doc("RPC message types")
enum RpcMessageType {
  @doc("Message sent")
  sent: "SENT",

  @doc("Message received")
  received: "RECEIVED",
}

// =============================================================================
// RPC Metrics
// =============================================================================

@doc("RPC server duration metric")
model RpcServerDurationMetric {
  @doc("Metric name")
  name: "rpc.server.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("RPC system")
  @encodedName("application/json", "rpc.system")
  system: RpcSystem;

  @doc("RPC service")
  @encodedName("application/json", "rpc.service")
  service: string;

  @doc("RPC method")
  @encodedName("application/json", "rpc.method")
  method: string;

  @doc("gRPC status code")
  @encodedName("application/json", "rpc.grpc.status_code")
  grpcStatusCode?: GrpcStatusCode;
}

@doc("RPC client duration metric")
model RpcClientDurationMetric {
  @doc("Metric name")
  name: "rpc.client.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("RPC system")
  @encodedName("application/json", "rpc.system")
  system: RpcSystem;

  @doc("RPC service")
  @encodedName("application/json", "rpc.service")
  service: string;

  @doc("RPC method")
  @encodedName("application/json", "rpc.method")
  method: string;

  @doc("Server address")
  @encodedName("application/json", "server.address")
  serverAddress: string;

  @doc("Server port")
  @encodedName("application/json", "server.port")
  serverPort: Port;

  @doc("gRPC status code")
  @encodedName("application/json", "rpc.grpc.status_code")
  grpcStatusCode?: GrpcStatusCode;
}


// ===== Flattened from: core/specs/models/search.tsp =====
// =============================================================================
// QYL v2.0 - Unified Search Entities
// =============================================================================
// Search documents, inverted index, query telemetry.
// =============================================================================



namespace Qyl.Domains.Search;

// =============================================================================
// Search Document
// =============================================================================

@doc("Canonical search document across all entity types")
@extension("x-duckdb-table", "search_documents")
@extension("x-csharp-type", "SearchDocumentEntity")
model SearchDocumentEntity {
  @doc("Document ID")
  @key
  id: string;

  @doc("Source entity type (span, log, issue, workflow, etc.)")
  @encodedName("application/json", "entity_type")
  entityType: SearchEntityType;

  @doc("Source entity ID")
  @encodedName("application/json", "entity_id")
  entityId: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId?: string;

  @doc("Document title")
  title: string;

  @doc("Document body (searchable text)")
  body?: string;

  @doc("Link to source entity")
  url?: string;

  @doc("Document tags")
  @encodedName("application/json", "tags_json")
  @extension("x-duckdb-type", "JSON")
  tagsJson?: string;

  @doc("Additional metadata")
  @encodedName("application/json", "metadata_json")
  @extension("x-duckdb-type", "JSON")
  metadataJson?: string;

  @doc("Relevance boost factor")
  boost: float64;

  @doc("Last indexed timestamp")
  @encodedName("application/json", "indexed_at")
  indexedAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

@doc("Searchable entity types")
enum SearchEntityType {
  @doc("Trace/span")
  span: "span",

  @doc("Log entry")
  log: "log",

  @doc("Error issue")
  issue: "issue",

  @doc("Workflow run")
  workflow: "workflow",

  @doc("Deployment")
  deployment: "deployment",

  @doc("Session")
  session: "session",

  @doc("Alert")
  alert: "alert",

  @doc("Fix run")
  fix: "fix",
}

// =============================================================================
// Search Term (Inverted Index)
// =============================================================================

@doc("Inverted token index entry")
@extension("x-duckdb-table", "search_terms")
@extension("x-csharp-type", "SearchTermEntity")
model SearchTermEntity {
  @doc("Normalized term")
  term: string;

  @doc("Document containing this term")
  @encodedName("application/json", "document_id")
  documentId: string;

  @doc("Field where term appears (title, body, tags)")
  field: string;

  @doc("Term frequency in this document/field")
  @encodedName("application/json", "term_frequency")
  termFrequency: int32;

  @doc("Position offsets for highlighting")
  @encodedName("application/json", "position_offsets_json")
  @extension("x-duckdb-type", "JSON")
  positionOffsetsJson?: string;
}

// =============================================================================
// Search Query Audit
// =============================================================================

@doc("Search query telemetry entry")
@extension("x-duckdb-table", "search_query_audit")
@extension("x-csharp-type", "SearchQueryAuditEntity")
model SearchQueryAuditEntity {
  @doc("Audit entry ID")
  @key
  id: string;

  @doc("Search query text")
  @encodedName("application/json", "query_text")
  queryText: string;

  @doc("Query type")
  @encodedName("application/json", "query_type")
  queryType: string;

  @doc("Filtered entity types")
  @encodedName("application/json", "entity_types_json")
  @extension("x-duckdb-type", "JSON")
  entityTypesJson?: string;

  @doc("Project scope")
  @encodedName("application/json", "project_id")
  projectId?: string;

  @doc("Number of results returned")
  @encodedName("application/json", "result_count")
  resultCount: int32;

  @doc("Clicked result document ID")
  @encodedName("application/json", "clicked_result_id")
  clickedResultId?: string;

  @doc("Position of clicked result")
  @encodedName("application/json", "clicked_position")
  clickedPosition?: int32;

  @doc("Query execution time in ms")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;

  @doc("Querying user")
  @encodedName("application/json", "user_id")
  userId?: string;

  @doc("Query timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Search Request/Response (API models, not stored)
// =============================================================================

@doc("Unified search request")
model SearchRequest {
  @doc("Search query text")
  query: string;

  @doc("Entity type filters")
  @encodedName("application/json", "entity_types")
  entityTypes?: SearchEntityType[];

  @doc("Project scope")
  @encodedName("application/json", "project_id")
  projectId?: string;

  @doc("Maximum results")
  @minValue(1)
  @maxValue(100)
  limit?: int32 = 20;

  @doc("Cursor for pagination")
  cursor?: string;
}

@doc("Unified search response")
model SearchResponse {
  @doc("Search results")
  results: SearchResult[];

  @doc("Total matching documents")
  @encodedName("application/json", "total_count")
  totalCount: int64;

  @doc("Query execution time in ms")
  @encodedName("application/json", "duration_ms")
  durationMs: int32;

  @doc("Next page cursor")
  @encodedName("application/json", "next_cursor")
  nextCursor?: string;

  @doc("Search suggestions")
  suggestions?: string[];
}

@doc("Individual search result")
model SearchResult {
  @doc("Document ID")
  @encodedName("application/json", "document_id")
  documentId: string;

  @doc("Entity type")
  @encodedName("application/json", "entity_type")
  entityType: SearchEntityType;

  @doc("Entity ID")
  @encodedName("application/json", "entity_id")
  entityId: string;

  @doc("Result title")
  title: string;

  @doc("Result snippet with highlights")
  snippet?: string;

  @doc("Relevance score")
  score: float64;

  @doc("Link to entity")
  url?: string;
}


// ===== Flattened from: core/specs/models/session.tsp =====
// =============================================================================
// QYL v2.0 - Session Semantic Conventions (OTel v1.40)
// =============================================================================
// User session telemetry attributes.
// =============================================================================



namespace Qyl.Domains.Observe.Session;

// =============================================================================
// Session Attributes
// =============================================================================

@doc("Session span/event attributes")
model SessionAttributes {
  @doc("Session ID")
  @encodedName("application/json", "session.id")
  id: SessionId;

  @doc("Previous session ID (for session continuity)")
  @encodedName("application/json", "session.previous_id")
  previousId?: SessionId;
}

// =============================================================================
// Session Event Models
// =============================================================================

@doc("Session lifecycle event")
model SessionEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: SessionEventName;

  @doc("Session ID")
  @encodedName("application/json", "session.id")
  sessionId: SessionId;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Event domain")
  @encodedName("application/json", "event.domain")
  eventDomain: "session";
}

@doc("Session event names")
enum SessionEventName {
  @doc("Session started")
  sessionStart: "session.start",

  @doc("Session ended")
  sessionEnd: "session.end",
}

// =============================================================================
// Session Entity Model (Full Session Record)
// =============================================================================

@doc("Complete session entity with aggregated data")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "session_entities")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SessionEntity")
model SessionEntity {
  @doc("Session ID")
  @key
  @encodedName("application/json", "session.id")
  sessionId: SessionId;

  @doc("User ID (if authenticated)")
  @encodedName("application/json", "user.id")
  userId?: UserId;

  @doc("Session start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("Session end time")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Session duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: DurationMs;

  @doc("Total trace count in session")
  @encodedName("application/json", "trace_count")
  @minValue(0)
  traceCount: int32;

  @doc("Total span count in session")
  @encodedName("application/json", "span_count")
  @minValue(0)
  spanCount: int32;

  @doc("Total error count in session")
  @encodedName("application/json", "error_count")
  @minValue(0)
  errorCount: int32;

  @doc("Service names observed in this session")
  services: string[];

  @doc("Session state")
  state: SessionState;

  @doc("Client information")
  client?: SessionClientInfo;

  @doc("Location information")
  geo?: SessionGeoInfo;

  @doc("GenAI usage summary")
  @encodedName("application/json", "genai_usage")
  genaiUsage?: SessionGenAiUsage;
}

@doc("Session states")
enum SessionState {
  @doc("Session is active")
  active: "active",

  @doc("Session is idle")
  idle: "idle",

  @doc("Session has ended normally")
  ended: "ended",

  @doc("Session has timed out")
  timedOut: "timed_out",

  @doc("Session was invalidated")
  invalidated: "invalidated",
}

@doc("Session client information")
model SessionClientInfo {
  @doc("Client IP address")
  ip?: IpAddress;

  @doc("User agent string")
  @encodedName("application/json", "user_agent")
  userAgent?: UserAgent;

  @doc("Device type")
  @encodedName("application/json", "device_type")
  deviceType?: DeviceType;

  @doc("Operating system")
  os?: string;

  @doc("Browser name")
  browser?: string;

  @doc("Browser version")
  @encodedName("application/json", "browser_version")
  browserVersion?: string;
}

@doc("Device types")
enum DeviceType {
  @doc("Desktop computer")
  desktop: "desktop",

  @doc("Mobile phone")
  mobile: "mobile",

  @doc("Tablet device")
  tablet: "tablet",

  @doc("TV or set-top box")
  tv: "tv",

  @doc("Gaming console")
  console: "console",

  @doc("Wearable device")
  wearable: "wearable",

  @doc("IoT device")
  iot: "iot",

  @doc("Bot/crawler")
  bot: "bot",

  @doc("Unknown device")
  `unknown`: "unknown",
}

@doc("Session geographic information")
model SessionGeoInfo {
  @doc("Country code (ISO 3166-1 alpha-2)")
  @encodedName("application/json", "country_code")
  countryCode?: string;

  @doc("Country name")
  @encodedName("application/json", "country_name")
  countryName?: string;

  @doc("Region/state")
  region?: string;

  @doc("City")
  city?: string;

  @doc("Postal code")
  @encodedName("application/json", "postal_code")
  postalCode?: string;

  @doc("Timezone")
  timezone?: string;
}

@doc("Session GenAI usage summary")
model SessionGenAiUsage {
  @doc("Total GenAI requests in session")
  @encodedName("application/json", "request_count")
  @minValue(0)
  requestCount: int32;

  @doc("Total input tokens consumed")
  @encodedName("application/json", "total_input_tokens")
  totalInputTokens: TokenCount;

  @doc("Total output tokens generated")
  @encodedName("application/json", "total_output_tokens")
  totalOutputTokens: TokenCount;

  @doc("Models used in session")
  @encodedName("application/json", "models_used")
  modelsUsed: string[];

  @doc("Providers used in session")
  @encodedName("application/json", "providers_used")
  providersUsed: string[];

  @doc("Estimated cost in USD")
  @encodedName("application/json", "estimated_cost_usd")
  estimatedCostUsd?: float64;
}

// =============================================================================
// Session Statistics
// =============================================================================

@doc("Aggregated session statistics")
model SessionStats {
  @doc("Active sessions count")
  @encodedName("application/json", "active_sessions")
  activeSessions: Count;

  @doc("Total sessions in time range")
  @encodedName("application/json", "total_sessions")
  totalSessions: Count;

  @doc("Unique users in time range")
  @encodedName("application/json", "unique_users")
  uniqueUsers: Count;

  @doc("Average session duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("Sessions with errors")
  @encodedName("application/json", "sessions_with_errors")
  sessionsWithErrors: Count;

  @doc("Sessions with GenAI usage")
  @encodedName("application/json", "sessions_with_genai")
  sessionsWithGenAi: Count;

  @doc("Bounce rate (single-page sessions)")
  @encodedName("application/json", "bounce_rate")
  bounceRate: Ratio;

  @doc("Sessions by device type")
  @encodedName("application/json", "by_device_type")
  byDeviceType?: SessionDeviceStats[];

  @doc("Sessions by country")
  @encodedName("application/json", "by_country")
  byCountry?: SessionCountryStats[];
}

@doc("Session stats by device type")
model SessionDeviceStats {
  @doc("Device type")
  @encodedName("application/json", "device_type")
  deviceType: DeviceType;

  @doc("Session count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

@doc("Session stats by country")
model SessionCountryStats {
  @doc("Country code")
  @encodedName("application/json", "country_code")
  countryCode: string;

  @doc("Country name")
  @encodedName("application/json", "country_name")
  countryName: string;

  @doc("Session count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}


// ===== Flattened from: core/specs/models/system.tsp =====
// =============================================================================
// QYL v2.0 - System Semantic Conventions (OTel v1.40)
// =============================================================================
// Operating system telemetry attributes.
// =============================================================================



namespace Qyl.Domains.Runtime.System;

// =============================================================================
// OS Resource Attributes
// =============================================================================

@doc("Operating system resource attributes")
model OsAttributes {
  @doc("Operating system type")
  @encodedName("application/json", "os.type")
  osType?: OsType;

  @doc("Operating system description")
  @encodedName("application/json", "os.description")
  description?: string;

  @doc("Operating system name")
  @encodedName("application/json", "os.name")
  name?: string;

  @doc("Operating system version")
  @encodedName("application/json", "os.version")
  version?: string;

  @doc("Operating system build ID")
  @encodedName("application/json", "os.build_id")
  buildId?: string;
}

// =============================================================================
// System Metrics
// =============================================================================

@doc("System CPU utilization metric")
model SystemCpuUtilizationMetric {
  @doc("Metric name")
  name: "system.cpu.utilization";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("CPU state")
  @encodedName("application/json", "system.cpu.state")
  cpuState: SystemCpuState;

  @doc("CPU logical number")
  @encodedName("application/json", "system.cpu.logical_number")
  cpuLogicalNumber?: int32;
}

@doc("System CPU time metric")
model SystemCpuTimeMetric {
  @doc("Metric name")
  name: "system.cpu.time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("CPU state")
  @encodedName("application/json", "system.cpu.state")
  cpuState: SystemCpuState;

  @doc("CPU logical number")
  @encodedName("application/json", "system.cpu.logical_number")
  cpuLogicalNumber?: int32;
}

@doc("System CPU frequency metric")
model SystemCpuFrequencyMetric {
  @doc("Metric name")
  name: "system.cpu.frequency";

  @doc("Metric unit (Hz)")
  unit: "Hz";

  @doc("CPU logical number")
  @encodedName("application/json", "system.cpu.logical_number")
  cpuLogicalNumber?: int32;
}

@doc("System memory usage metric")
model SystemMemoryUsageMetric {
  @doc("Metric name")
  name: "system.memory.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Memory state")
  @encodedName("application/json", "system.memory.state")
  memoryState: SystemMemoryState;
}

@doc("System memory utilization metric")
model SystemMemoryUtilizationMetric {
  @doc("Metric name")
  name: "system.memory.utilization";

  @doc("Metric unit (ratio)")
  unit: "1";

  @doc("Memory state")
  @encodedName("application/json", "system.memory.state")
  memoryState: SystemMemoryState;
}

@doc("System disk IO metric")
model SystemDiskIoMetric {
  @doc("Metric name")
  name: "system.disk.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Disk device")
  @encodedName("application/json", "system.disk.device")
  diskDevice: string;

  @doc("IO direction")
  @encodedName("application/json", "system.disk.direction")
  diskDirection: string;
}

@doc("System disk operations metric")
model SystemDiskOperationsMetric {
  @doc("Metric name")
  name: "system.disk.operations";

  @doc("Metric unit")
  unit: "{operation}";

  @doc("Disk device")
  @encodedName("application/json", "system.disk.device")
  diskDevice: string;

  @doc("IO direction")
  @encodedName("application/json", "system.disk.direction")
  diskDirection: string;
}

@doc("System disk IO time metric")
model SystemDiskIoTimeMetric {
  @doc("Metric name")
  name: "system.disk.io_time";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Disk device")
  @encodedName("application/json", "system.disk.device")
  diskDevice: string;
}

@doc("System filesystem usage metric")
model SystemFilesystemUsageMetric {
  @doc("Metric name")
  name: "system.filesystem.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Filesystem device")
  @encodedName("application/json", "system.filesystem.device")
  device: string;

  @doc("Filesystem mount point")
  @encodedName("application/json", "system.filesystem.mountpoint")
  mountpoint: string;

  @doc("Filesystem type")
  @encodedName("application/json", "system.filesystem.type")
  fsType: string;

  @doc("Filesystem state")
  @encodedName("application/json", "system.filesystem.state")
  state: SystemFilesystemState;
}

@doc("System network IO metric")
model SystemNetworkIoMetric {
  @doc("Metric name")
  name: "system.network.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Network device")
  @encodedName("application/json", "system.network.device")
  networkDevice: string;

  @doc("IO direction")
  @encodedName("application/json", "system.network.direction")
  networkDirection: string;
}

@doc("System network connections metric")
model SystemNetworkConnectionsMetric {
  @doc("Metric name")
  name: "system.network.connections";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Network protocol")
  @encodedName("application/json", "system.network.protocol")
  protocol: string;

  @doc("Network state")
  @encodedName("application/json", "system.network.state")
  state: SystemNetworkState;
}

@doc("System paging usage metric")
model SystemPagingUsageMetric {
  @doc("Metric name")
  name: "system.paging.usage";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("Paging state")
  @encodedName("application/json", "system.paging.state")
  pagingState: SystemPagingState;

  @doc("Paging device")
  @encodedName("application/json", "system.paging.device")
  device?: string;
}

@doc("System paging operations metric")
model SystemPagingOperationsMetric {
  @doc("Metric name")
  name: "system.paging.operations";

  @doc("Metric unit")
  unit: "{operation}";

  @doc("Paging type")
  @encodedName("application/json", "system.paging.type")
  pagingType: SystemPagingType;

  @doc("Paging direction")
  @encodedName("application/json", "system.paging.direction")
  pagingDirection: string;
}

@doc("System process count metric")
model SystemProcessCountMetric {
  @doc("Metric name")
  name: "system.process.count";

  @doc("Metric unit")
  unit: "{process}";

  @doc("Process status")
  @encodedName("application/json", "system.process.status")
  processStatus: SystemProcessStatus;
}

// =============================================================================
// System Enumerations
// =============================================================================

@doc("System CPU states")
enum SystemCpuState {
  @doc("User mode")
  user: "user",

  @doc("System mode")
  system: "system",

  @doc("Nice mode")
  nice: "nice",

  @doc("Idle")
  idle: "idle",

  @doc("I/O wait")
  iowait: "iowait",

  @doc("Interrupt")
  interrupt: "interrupt",

  @doc("Steal")
  steal: "steal",
}

@doc("System memory states")
enum SystemMemoryState {
  @doc("Used memory")
  used: "used",

  @doc("Free memory")
  free: "free",

  @doc("Buffered memory")
  buffered: "buffered",

  @doc("Cached memory")
  cached: "cached",

  @doc("Shared memory")
  shared: "shared",
}

@doc("System filesystem states")
enum SystemFilesystemState {
  @doc("Used space")
  used: "used",

  @doc("Free space")
  free: "free",

  @doc("Reserved space")
  reserved: "reserved",
}

@doc("System network states")
enum SystemNetworkState {
  @doc("CLOSE state")
  close: "close",

  @doc("CLOSE_WAIT state")
  closeWait: "close_wait",

  @doc("CLOSING state")
  closing: "closing",

  @doc("DELETE state")
  delete: "delete",

  @doc("ESTABLISHED state")
  established: "established",

  @doc("FIN_WAIT_1 state")
  finWait1: "fin_wait_1",

  @doc("FIN_WAIT_2 state")
  finWait2: "fin_wait_2",

  @doc("LAST_ACK state")
  lastAck: "last_ack",

  @doc("LISTEN state")
  listen: "listen",

  @doc("SYN_RECV state")
  synRecv: "syn_recv",

  @doc("SYN_SENT state")
  synSent: "syn_sent",

  @doc("TIME_WAIT state")
  timeWait: "time_wait",
}

@doc("System paging states")
enum SystemPagingState {
  @doc("Used")
  used: "used",

  @doc("Free")
  free: "free",
}

@doc("System paging types")
enum SystemPagingType {
  @doc("Major page fault")
  major: "major",

  @doc("Minor page fault")
  minor: "minor",
}

@doc("System process status")
enum SystemProcessStatus {
  @doc("Running")
  running: "running",

  @doc("Sleeping")
  sleeping: "sleeping",

  @doc("Stopped")
  stopped: "stopped",

  @doc("Defunct/zombie")
  defunct: "defunct",
}


// ===== Flattened from: core/specs/models/test.tsp =====
// =============================================================================
// QYL v2.0 - Test Semantic Conventions (OTel v1.40)
// =============================================================================
// Test execution telemetry attributes.
// =============================================================================



namespace Qyl.Domains.Observe.Test;

// =============================================================================
// Test Attributes
// =============================================================================

@doc("Test case attributes")
model TestAttributes {
  @doc("Test case name")
  @encodedName("application/json", "test.case.name")
  caseName: string;

  @doc("Test case result")
  @encodedName("application/json", "test.case.result.status")
  caseResultStatus?: TestResultStatus;

  @doc("Test suite name")
  @encodedName("application/json", "test.suite.name")
  suiteName?: string;

  @doc("Test suite run status")
  @encodedName("application/json", "test.suite.run.status")
  suiteRunStatus?: TestSuiteRunStatus;
}

// =============================================================================
// Test Enumerations
// =============================================================================

@doc("Test case result status")
enum TestResultStatus {
  @doc("Test passed")
  pass: "pass",

  @doc("Test failed")
  fail: "fail",
}

@doc("Test suite run status")
enum TestSuiteRunStatus {
  @doc("Suite succeeded")
  success: "success",

  @doc("Suite failed")
  failure: "failure",

  @doc("Suite skipped")
  skipped: "skipped",

  @doc("Suite aborted")
  aborted: "aborted",

  @doc("Suite timed out")
  timedOut: "timed_out",

  @doc("Suite in progress")
  inProgress: "in_progress",
}

// =============================================================================
// Test Run Entity
// =============================================================================

@doc("Test run entity")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "test_runs")
@TypeSpec.OpenAPI.extension("x-csharp-type", "TestRunEntity")
model TestRunEntity {
  @doc("Test run ID")
  @key
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("Test suite name")
  @encodedName("application/json", "suite_name")
  suiteName: string;

  @doc("Run status")
  status: TestSuiteRunStatus;

  @doc("Start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("End time")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int64;

  @doc("Total test count")
  @encodedName("application/json", "total_count")
  totalCount: int32;

  @doc("Passed count")
  @encodedName("application/json", "passed_count")
  passedCount: int32;

  @doc("Failed count")
  @encodedName("application/json", "failed_count")
  failedCount: int32;

  @doc("Skipped count")
  @encodedName("application/json", "skipped_count")
  skippedCount: int32;

  @doc("Flaky count")
  @encodedName("application/json", "flaky_count")
  flakyCount?: int32;

  @doc("Test framework")
  framework?: TestFramework;

  @doc("CI/CD run ID")
  @encodedName("application/json", "cicd_run_id")
  cicdRunId?: string;

  @doc("Git commit SHA")
  @encodedName("application/json", "git_commit")
  gitCommit?: string;

  @doc("Git branch")
  @encodedName("application/json", "git_branch")
  gitBranch?: string;
}

@doc("Test frameworks")
enum TestFramework {
  @doc("Jest")
  jest: "jest",

  @doc("Mocha")
  mocha: "mocha",

  @doc("Vitest")
  vitest: "vitest",

  @doc("Playwright")
  playwright: "playwright",

  @doc("Cypress")
  cypress: "cypress",

  @doc("Selenium")
  selenium: "selenium",

  @doc("pytest")
  pytest: "pytest",

  @doc("unittest")
  unittest: "unittest",

  @doc("JUnit")
  junit: "junit",

  @doc("TestNG")
  testng: "testng",

  @doc("xUnit")
  xunit: "xunit",

  @doc("NUnit")
  nunit: "nunit",

  @doc("MSTest")
  mstest: "mstest",

  @doc("RSpec")
  rspec: "rspec",

  @doc("Go testing")
  goTest: "go_test",

  @doc("Rust cargo test")
  cargoTest: "cargo_test",

  @doc("Other")
  other: "other",
}

// =============================================================================
// Test Case Entity
// =============================================================================

@doc("Test case entity")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "test_cases")
@TypeSpec.OpenAPI.extension("x-csharp-type", "TestCaseEntity")
model TestCaseEntity {
  @doc("Test case ID")
  @key
  @encodedName("application/json", "case_id")
  caseId: string;

  @doc("Test run ID")
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("Test case name")
  name: string;

  @doc("Test file path")
  @encodedName("application/json", "file_path")
  filePath?: string;

  @doc("Result status")
  status: TestResultStatus;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int64;

  @doc("Error message (if failed)")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Error stack trace")
  @encodedName("application/json", "stack_trace")
  stackTrace?: string;

  @doc("Retry count")
  @encodedName("application/json", "retry_count")
  retryCount?: int32;

  @doc("Is flaky")
  @encodedName("application/json", "is_flaky")
  isFlaky?: boolean;

  @doc("Tags")
  tags?: string[];
}

// =============================================================================
// Test Metrics
// =============================================================================

@doc("Test run count metric")
model TestRunCountMetric {
  @doc("Metric name")
  name: "test.runs";

  @doc("Metric unit")
  unit: "{run}";

  @doc("Suite name")
  @encodedName("application/json", "test.suite.name")
  suiteName: string;

  @doc("Status")
  @encodedName("application/json", "test.suite.run.status")
  status: TestSuiteRunStatus;
}

@doc("Test case count metric")
model TestCaseCountMetric {
  @doc("Metric name")
  name: "test.cases";

  @doc("Metric unit")
  unit: "{test}";

  @doc("Suite name")
  @encodedName("application/json", "test.suite.name")
  suiteName: string;

  @doc("Result status")
  @encodedName("application/json", "test.case.result.status")
  resultStatus: TestResultStatus;
}

@doc("Test duration metric")
model TestDurationMetric {
  @doc("Metric name")
  name: "test.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Suite name")
  @encodedName("application/json", "test.suite.name")
  suiteName: string;
}

// =============================================================================
// Test Coverage
// =============================================================================

@doc("Code coverage report")
model CoverageReport {
  @doc("Run ID")
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("Overall line coverage")
  @encodedName("application/json", "line_coverage")
  lineCoverage: Percentage;

  @doc("Overall branch coverage")
  @encodedName("application/json", "branch_coverage")
  branchCoverage?: Percentage;

  @doc("Overall function coverage")
  @encodedName("application/json", "function_coverage")
  functionCoverage?: Percentage;

  @doc("Statement coverage")
  @encodedName("application/json", "statement_coverage")
  statementCoverage?: Percentage;

  @doc("Coverage by file")
  @encodedName("application/json", "by_file")
  byFile?: FileCoverage[];
}

@doc("File coverage")
model FileCoverage {
  @doc("File path")
  @encodedName("application/json", "file_path")
  filePath: string;

  @doc("Line coverage")
  @encodedName("application/json", "line_coverage")
  lineCoverage: Percentage;

  @doc("Covered lines")
  @encodedName("application/json", "covered_lines")
  coveredLines: int32;

  @doc("Total lines")
  @encodedName("application/json", "total_lines")
  totalLines: int32;

  @doc("Uncovered line numbers")
  @encodedName("application/json", "uncovered_lines")
  uncoveredLines?: int32[];
}


// ===== Flattened from: core/specs/models/triage.tsp =====
// =============================================================================
// QYL v2.0 - Triage Models (Loom Pipeline)
// =============================================================================
// AI-powered triage pipeline: score fixability, summarize, and route errors.
// =============================================================================



namespace Qyl.Domains.Loom.Triage;

// =============================================================================
// Triage Automation Level
// =============================================================================

@doc("Automation level determined by fixability score")
enum TriageAutomationLevel {
  @doc("Auto-fix: score >= 0.8, proceed without human review")
  auto: "auto",

  @doc("Assisted: 0.5 <= score < 0.8, suggest fix with human review")
  assisted: "assisted",

  @doc("Manual: 0.2 <= score < 0.5, provide context but human must fix")
  manual: "manual",

  @doc("Skip: score < 0.2, not actionable for automated fix")
  skip: "skip",
}

// =============================================================================
// Triage Result Entity
// =============================================================================

@doc("AI triage assessment for an error issue")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "triage_results")
@TypeSpec.OpenAPI.extension("x-csharp-type", "TriageResultEntity")
model TriageResultEntity {
  @doc("Triage result ID")
  @key
  @encodedName("application/json", "triage_id")
  triageId: string;

  @doc("Associated error issue ID")
  @encodedName("application/json", "issue_id")
  issueId: string;

  @doc("Fixability score (0.0 to 1.0)")
  @encodedName("application/json", "fixability_score")
  fixabilityScore: float64;

  @doc("Automation level derived from score")
  @encodedName("application/json", "automation_level")
  automationLevel: TriageAutomationLevel;

  @doc("AI-generated summary of the issue")
  @encodedName("application/json", "ai_summary")
  aiSummary?: string;

  @doc("Root cause hypothesis from AI analysis")
  @encodedName("application/json", "root_cause_hypothesis")
  rootCauseHypothesis?: string;

  @doc("What triggered this triage")
  @encodedName("application/json", "triggered_by")
  triggeredBy: TriageTrigger;

  @doc("Linked fix run ID (if auto-routed)")
  @encodedName("application/json", "fix_run_id")
  fixRunId?: string;

  @doc("Whether scoring was done by LLM or heuristic fallback")
  @encodedName("application/json", "scoring_method")
  scoringMethod: TriageScoringMethod;

  @doc("When this triage was created")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

// =============================================================================
// Triage Trigger
// =============================================================================

@doc("What triggered the triage assessment")
enum TriageTrigger {
  @doc("New issue created from error ingestion")
  newIssue: "new_issue",

  @doc("Previously resolved issue regressed")
  regression: "regression",

  @doc("Manually triggered by user or agent")
  manual: "manual",

  @doc("Re-triage due to updated context")
  retriage: "retriage",
}

// =============================================================================
// Scoring Method
// =============================================================================

@doc("How the fixability score was determined")
enum TriageScoringMethod {
  @doc("LLM-based structured scoring")
  llm: "llm",

  @doc("Heuristic fallback (no LLM configured)")
  heuristic: "heuristic",
}


// ===== Flattened from: core/specs/models/workflow.tsp =====
// =============================================================================
// QYL v2.0 - Workflow DAG Runtime Entities
// =============================================================================
// Workflow runs, nodes, checkpoints, shared state, events, routing rules.
// =============================================================================



namespace Qyl.Domains.Workflow;

// =============================================================================
// Workflow Run
// =============================================================================

@doc("Top-level workflow execution")
@extension("x-duckdb-table", "workflow_runs")
@extension("x-csharp-type", "WorkflowRunEntity")
model WorkflowRunEntity {
  @doc("Run ID")
  @key
  id: string;

  @doc("Workflow definition ID")
  @encodedName("application/json", "workflow_id")
  workflowId: string;

  @doc("Workflow definition version")
  @encodedName("application/json", "workflow_version")
  workflowVersion: int32;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Trigger type")
  @encodedName("application/json", "trigger_type")
  triggerType: WorkflowTriggerType;

  @doc("Trigger source identifier")
  @encodedName("application/json", "trigger_source")
  triggerSource?: string;

  @doc("Run input data")
  @encodedName("application/json", "input_json")
  @extension("x-duckdb-type", "JSON")
  inputJson?: string;

  @doc("Run output data")
  @encodedName("application/json", "output_json")
  @extension("x-duckdb-type", "JSON")
  outputJson?: string;

  @doc("Run status")
  status: WorkflowRunStatus;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Parent run ID for sub-workflows")
  @encodedName("application/json", "parent_run_id")
  parentRunId?: string;

  @doc("Correlation ID for tracing")
  @encodedName("application/json", "correlation_id")
  correlationId?: string;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

@doc("Workflow trigger types")
enum WorkflowTriggerType {
  @doc("Manual trigger")
  manual: "manual",

  @doc("Alert-triggered")
  alert: "alert",

  @doc("Schedule-triggered")
  schedule: "schedule",

  @doc("Event-triggered")
  event: "event",

  @doc("API-triggered")
  api: "api",

  @doc("MCP tool-triggered")
  mcp: "mcp",
}

@doc("Workflow run status")
enum WorkflowRunStatus {
  @doc("Pending start")
  pending: "pending",

  @doc("Currently running")
  running: "running",

  @doc("Paused/waiting for input")
  paused: "paused",

  @doc("Successfully completed")
  completed: "completed",

  @doc("Failed with error")
  failed: "failed",

  @doc("Cancelled")
  cancelled: "cancelled",

  @doc("Timed out")
  timedOut: "timed_out",
}

// =============================================================================
// Workflow Node
// =============================================================================

@doc("Individual DAG node execution")
@extension("x-duckdb-table", "workflow_nodes")
@extension("x-csharp-type", "WorkflowNodeEntity")
model WorkflowNodeEntity {
  @doc("Execution ID")
  @key
  id: string;

  @doc("Parent run")
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("Node definition ID")
  @encodedName("application/json", "node_id")
  nodeId: string;

  @doc("Node type")
  @encodedName("application/json", "node_type")
  nodeType: WorkflowNodeType;

  @doc("Node name")
  @encodedName("application/json", "node_name")
  nodeName: string;

  @doc("Attempt number (1-based)")
  attempt: int32;

  @doc("Node input data")
  @encodedName("application/json", "input_json")
  @extension("x-duckdb-type", "JSON")
  inputJson?: string;

  @doc("Node output data")
  @encodedName("application/json", "output_json")
  @extension("x-duckdb-type", "JSON")
  outputJson?: string;

  @doc("Node status")
  status: WorkflowRunStatus;

  @doc("Error message if failed")
  @encodedName("application/json", "error_message")
  errorMessage?: string;

  @doc("Retry count")
  @encodedName("application/json", "retry_count")
  retryCount: int32;

  @doc("Maximum retries allowed")
  @encodedName("application/json", "max_retries")
  maxRetries: int32;

  @doc("Timeout in milliseconds")
  @encodedName("application/json", "timeout_ms")
  timeoutMs?: int32;

  @doc("Start timestamp")
  @encodedName("application/json", "started_at")
  startedAt?: utcDateTime;

  @doc("Completion timestamp")
  @encodedName("application/json", "completed_at")
  completedAt?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: int32;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

@doc("Workflow node types")
enum WorkflowNodeType {
  @doc("AI agent invocation")
  agent: "agent",

  @doc("Tool/function call")
  tool: "tool",

  @doc("Conditional branch")
  condition: "condition",

  @doc("Parallel fork")
  fork: "fork",

  @doc("Join/barrier")
  join: "join",

  @doc("Human approval gate")
  approval: "approval",

  @doc("Sub-workflow invocation")
  subWorkflow: "sub_workflow",

  @doc("Transform/map node")
  transform: "transform",

  @doc("Wait/delay node")
  wait: "wait",
}

// =============================================================================
// Workflow Checkpoint
// =============================================================================

@doc("Durable node snapshot for crash recovery")
@extension("x-csharp-type", "WorkflowCheckpointEntity")
model WorkflowCheckpointEntity {
  @doc("Checkpoint ID")
  @key
  id: string;

  @doc("Parent run")
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("Node that created the checkpoint")
  @encodedName("application/json", "node_id")
  nodeId: string;

  @doc("Checkpoint type")
  @encodedName("application/json", "checkpoint_type")
  checkpointType: string;

  @doc("Serialized state")
  @encodedName("application/json", "state_json")
  @extension("x-duckdb-type", "JSON")
  stateJson: string;

  @doc("Monotonic sequence number")
  @encodedName("application/json", "sequence_number")
  sequenceNumber: int64;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

// =============================================================================
// Workflow Shared State
// =============================================================================

@doc("Cross-node versioned KV store per run")
@extension("x-duckdb-table", "workflow_shared_state")
@extension("x-csharp-type", "WorkflowSharedStateEntity")
model WorkflowSharedStateEntity {
  @doc("Run ID")
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("State key")
  key: string;

  @doc("State value")
  @encodedName("application/json", "value_json")
  @extension("x-duckdb-type", "JSON")
  valueJson: string;

  @doc("Optimistic concurrency version")
  version: int32;

  @doc("Last updater")
  @encodedName("application/json", "updated_by")
  updatedBy?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

// =============================================================================
// Workflow Event
// =============================================================================

@doc("Append-only workflow event")
@extension("x-duckdb-table", "workflow_events")
@extension("x-csharp-type", "WorkflowEventEntity")
model WorkflowEventEntity {
  @doc("Event ID")
  @key
  id: string;

  @doc("Parent run")
  @encodedName("application/json", "run_id")
  runId: string;

  @doc("Source node (null for run-level events)")
  @encodedName("application/json", "node_id")
  nodeId?: string;

  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: string;

  @doc("Event name")
  @encodedName("application/json", "event_name")
  eventName: string;

  @doc("Event payload")
  @encodedName("application/json", "payload_json")
  @extension("x-duckdb-type", "JSON")
  payloadJson?: string;

  @doc("Monotonic sequence number")
  @encodedName("application/json", "sequence_number")
  sequenceNumber: int64;

  @doc("Event source identifier")
  source?: string;

  @doc("Correlation ID")
  @encodedName("application/json", "correlation_id")
  correlationId?: string;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Workflow Routing Rule
// =============================================================================

@doc("DAG conditional branch predicate")
@extension("x-duckdb-table", "workflow_routing_rules")
@extension("x-csharp-type", "WorkflowRoutingRuleEntity")
model WorkflowRoutingRuleEntity {
  @doc("Rule ID")
  @key
  id: string;

  @doc("Workflow definition")
  @encodedName("application/json", "workflow_id")
  workflowId: string;

  @doc("Source node")
  @encodedName("application/json", "source_node_id")
  sourceNodeId: string;

  @doc("Target node")
  @encodedName("application/json", "target_node_id")
  targetNodeId: string;

  @doc("Condition type")
  @encodedName("application/json", "condition_type")
  conditionType: string;

  @doc("Condition expression")
  @encodedName("application/json", "condition_expression")
  conditionExpression: string;

  @doc("Evaluation priority (higher first)")
  priority: int32;

  @doc("Whether this is the default/fallback route")
  @encodedName("application/json", "is_default")
  isDefault: boolean;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}


// ===== Flattened from: core/specs/models/workspace.tsp =====
// =============================================================================
// QYL v2.0 - Workspace & Identity Entities
// =============================================================================
// Local-first workspace envelope, handshake, tokens, process registry.
// =============================================================================



namespace Qyl.Domains.Workspace;

// =============================================================================
// Project
// =============================================================================

@doc("Project registry: top-level organizational unit")
@extension("x-duckdb-table", "projects")
@extension("x-csharp-type", "ProjectEntity")
model ProjectEntity {
  @doc("Project ID")
  @key
  id: string;

  @doc("Project name")
  name: string;

  @doc("URL-safe slug (unique)")
  slug: string;

  @doc("Project description")
  description?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;

  @doc("Archive timestamp (null if active)")
  @encodedName("application/json", "archived_at")
  archivedAt?: utcDateTime;
}

// =============================================================================
// Project Environment
// =============================================================================

@doc("Environment row per project (dev, staging, prod)")
@extension("x-duckdb-table", "project_environments")
@extension("x-csharp-type", "ProjectEnvironmentEntity")
model ProjectEnvironmentEntity {
  @doc("Environment ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Environment name (dev, staging, prod)")
  name: string;

  @doc("Display name for UI")
  @encodedName("application/json", "display_name")
  displayName: string;

  @doc("Hex color for UI")
  color?: string;

  @doc("Sort order for display")
  @encodedName("application/json", "sort_order")
  sortOrder: int32;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

// =============================================================================
// Local Node
// =============================================================================

@doc("Local agent/daemon registry entry")
@extension("x-duckdb-table", "local_nodes")
@extension("x-csharp-type", "LocalNodeEntity")
model LocalNodeEntity {
  @doc("Node ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Environment")
  @encodedName("application/json", "environment_id")
  environmentId: string;

  @doc("Machine hostname")
  hostname: string;

  @doc("OS-level machine identifier")
  @encodedName("application/json", "machine_id")
  machineId?: string;

  @doc("Agent binary version")
  @encodedName("application/json", "agent_version")
  agentVersion?: string;

  @doc("Operating system type")
  @encodedName("application/json", "os_type")
  osType?: string;

  @doc("Operating system version")
  @encodedName("application/json", "os_version")
  osVersion?: string;

  @doc("First seen timestamp")
  @encodedName("application/json", "first_seen_at")
  firstSeenAt: utcDateTime;

  @doc("Last seen timestamp")
  @encodedName("application/json", "last_seen_at")
  lastSeenAt: utcDateTime;

  @doc("Node status")
  status: NodeStatus;
}

@doc("Node online status")
enum NodeStatus {
  @doc("Node is online")
  online: "online",

  @doc("Node is offline")
  offline: "offline",

  @doc("Node heartbeat is stale")
  stale: "stale",
}

// =============================================================================
// Workspace Envelope
// =============================================================================

@doc("Workspace envelope: the local-first workspace unit")
@extension("x-duckdb-table", "workspace_envelopes")
@extension("x-csharp-type", "WorkspaceEnvelopeEntity")
model WorkspaceEnvelopeEntity {
  @doc("Workspace ID")
  @key
  id: string;

  @doc("Owning project")
  @encodedName("application/json", "project_id")
  projectId: string;

  @doc("Environment")
  @encodedName("application/json", "environment_id")
  environmentId: string;

  @doc("Host node")
  @encodedName("application/json", "node_id")
  nodeId: string;

  @doc("Workspace name")
  name: string;

  @doc("Local filesystem root path")
  @encodedName("application/json", "root_path")
  rootPath: string;

  @doc("Last heartbeat timestamp")
  @encodedName("application/json", "heartbeat_at")
  heartbeatAt?: utcDateTime;

  @doc("Heartbeat interval in seconds")
  @encodedName("application/json", "heartbeat_interval_seconds")
  heartbeatIntervalSeconds: int32;

  @doc("Workspace status")
  status: WorkspaceStatus;

  @doc("Workspace-level configuration overrides")
  @encodedName("application/json", "config_json")
  @extension("x-duckdb-type", "JSON")
  configJson?: string;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;

  @doc("Last update timestamp")
  @encodedName("application/json", "updated_at")
  updatedAt: utcDateTime;
}

@doc("Workspace lifecycle status")
enum WorkspaceStatus {
  @doc("Active and receiving data")
  active: "active",

  @doc("Temporarily suspended")
  suspended: "suspended",

  @doc("Archived (read-only)")
  archived: "archived",
}

// =============================================================================
// Handshake Session
// =============================================================================

@doc("Browser-local handshake session for workspace verification")
@extension("x-duckdb-table", "handshake_sessions")
@extension("x-csharp-type", "HandshakeSessionEntity")
model HandshakeSessionEntity {
  @doc("Session ID")
  @key
  id: string;

  @doc("Target workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("PKCE-style challenge")
  challenge: string;

  @doc("Challenge method")
  @encodedName("application/json", "challenge_method")
  challengeMethod: string;

  @doc("Browser fingerprint")
  @encodedName("application/json", "browser_fingerprint")
  browserFingerprint?: string;

  @doc("Origin URL")
  @encodedName("application/json", "origin_url")
  originUrl?: string;

  @doc("Handshake state")
  state: HandshakeState;

  @doc("Verification timestamp")
  @encodedName("application/json", "verified_at")
  verifiedAt?: utcDateTime;

  @doc("Expiration timestamp")
  @encodedName("application/json", "expires_at")
  expiresAt: utcDateTime;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

@doc("Handshake session state")
enum HandshakeState {
  @doc("Waiting for verification")
  pending: "pending",

  @doc("Successfully verified")
  verified: "verified",

  @doc("Session expired")
  expired: "expired",

  @doc("Verification rejected")
  rejected: "rejected",
}

// =============================================================================
// Workspace Token
// =============================================================================

@doc("Workspace-scoped API token metadata")
@extension("x-duckdb-table", "workspace_tokens")
@extension("x-csharp-type", "WorkspaceTokenEntity")
model WorkspaceTokenEntity {
  @doc("Token ID")
  @key
  id: string;

  @doc("Owning workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Token display name")
  name: string;

  @doc("SHA-256 hash of token value")
  @encodedName("application/json", "token_hash")
  tokenHash: string;

  @doc("First 8 chars for identification")
  @encodedName("application/json", "token_prefix")
  tokenPrefix: string;

  @doc("Allowed scopes")
  @encodedName("application/json", "scopes_json")
  @extension("x-duckdb-type", "JSON")
  scopesJson?: string;

  @doc("Last used timestamp")
  @encodedName("application/json", "last_used_at")
  lastUsedAt?: utcDateTime;

  @doc("Expiration timestamp")
  @encodedName("application/json", "expires_at")
  expiresAt?: utcDateTime;

  @doc("Revocation timestamp")
  @encodedName("application/json", "revoked_at")
  revokedAt?: utcDateTime;

  @doc("Creation timestamp")
  @encodedName("application/json", "created_at")
  createdAt: utcDateTime;
}

// =============================================================================
// Process Registry
// =============================================================================

@doc("Local process identity per workspace")
@extension("x-duckdb-table", "workspace_process_registry")
@extension("x-csharp-type", "ProcessRegistryEntity")
model ProcessRegistryEntity {
  @doc("Registry entry ID")
  @key
  id: string;

  @doc("Owning workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Process type")
  @encodedName("application/json", "process_type")
  processType: ProcessType;

  @doc("OS process ID")
  pid: int32;

  @doc("Listening port")
  port?: int32;

  @doc("Protocol")
  protocol?: string;

  @doc("Binary file path")
  @encodedName("application/json", "binary_path")
  binaryPath?: string;

  @doc("Binary version")
  @encodedName("application/json", "binary_version")
  binaryVersion?: string;

  @doc("Process start timestamp")
  @encodedName("application/json", "started_at")
  startedAt: utcDateTime;

  @doc("Last heartbeat timestamp")
  @encodedName("application/json", "last_heartbeat_at")
  lastHeartbeatAt?: utcDateTime;

  @doc("Process status")
  status: ProcessStatus;
}

@doc("QYL process types")
enum ProcessType {
  @doc("Collector backend")
  collector: "collector",

  @doc("MCP server")
  mcp: "mcp",

  @doc("Terminal span viewer")
  watch: "watch",

  @doc("Process anomaly detector")
  watchdog: "watchdog",

  @doc("CLI tool")
  cli: "cli",
}

@doc("Process lifecycle status")
enum ProcessStatus {
  @doc("Process is running")
  running: "running",

  @doc("Process has stopped")
  stopped: "stopped",

  @doc("Process crashed")
  crashed: "crashed",
}

// =============================================================================
// Port Lease
// =============================================================================

@doc("Collision-proof port lease entry")
@extension("x-duckdb-table", "port_leases")
@extension("x-csharp-type", "PortLeaseEntity")
model PortLeaseEntity {
  @doc("Leased port number")
  port: int32;

  @doc("Owning workspace")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Owning process")
  @encodedName("application/json", "process_id")
  processId: string;

  @doc("Protocol (tcp/udp)")
  protocol: string;

  @doc("Lease start timestamp")
  @encodedName("application/json", "leased_at")
  leasedAt: utcDateTime;

  @doc("Lease expiration")
  @encodedName("application/json", "expires_at")
  expiresAt: utcDateTime;

  @doc("Release timestamp (null if active)")
  @encodedName("application/json", "released_at")
  releasedAt?: utcDateTime;
}


// ===== Flattened from: core/specs/otel/logs.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry Log Record Model
// =============================================================================
// Structured logging following OTel log data model.
// =============================================================================



namespace Qyl.OTel.Logs;

// =============================================================================
// Log Record Model
// =============================================================================

@doc("OpenTelemetry Log Record")
model LogRecord {
  @doc("Timestamp when the event occurred (nanoseconds since epoch)")
  @encodedName("application/json", "time_unix_nano")
  timeUnixNano: int64;

  @doc("Timestamp when the log was observed/collected (nanoseconds since epoch)")
  @encodedName("application/json", "observed_time_unix_nano")
  observedTimeUnixNano: int64;

  @doc("Severity number (1-24)")
  @encodedName("application/json", "severity_number")
  severityNumber: SeverityNumber;

  @doc("Severity text (DEBUG, INFO, WARN, ERROR, etc.)")
  @encodedName("application/json", "severity_text")
  severityText?: SeverityText;

  @doc("Log body - the main content")
  body: LogBody;

  @doc("Log attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;

  @doc("Flags (trace flags)")
  flags?: int32;

  @doc("Associated trace ID")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Associated span ID")
  @encodedName("application/json", "span_id")
  spanId?: SpanId;

  @doc("Resource describing the entity that produced this log")
  resource: Qyl.OTel.Resource.Resource;

  @doc("Instrumentation scope")
  @encodedName("application/json", "instrumentation_scope")
  instrumentationScope?: InstrumentationScope;
}

// =============================================================================
// Log Body Types (Discriminated Union)
// =============================================================================

@doc("Log body content - can be string, structured, or bytes")
@discriminated(#{ envelope: "none" })
union LogBody {
  @doc("Plain string body")
  stringBody: LogBodyString,

  @doc("Structured key-value body")
  kvListBody: LogBodyKvList,

  @doc("Array body")
  arrayBody: LogBodyArray,

  @doc("Binary body")
  bytesBody: LogBodyBytes,
}

@doc("String log body")
model LogBodyString {
  @doc("String value")
  @encodedName("application/json", "string_value")
  stringValue: string;
}

@doc("Structured key-value log body")
model LogBodyKvList {
  @doc("Key-value pairs")
  @encodedName("application/json", "kv_list_value")
  kvListValue: Attribute[];
}

@doc("Array log body")
model LogBodyArray {
  @doc("Array of values")
  @encodedName("application/json", "array_value")
  arrayValue: AttributeValue[];
}

@doc("Binary log body")
model LogBodyBytes {
  @doc("Binary value (base64 encoded)")
  @encodedName("application/json", "bytes_value")
  bytesValue: bytes;
}

// =============================================================================
// Log Query Filters
// =============================================================================

@doc("Filter parameters for querying logs")
model LogQueryFilters {
  @doc("Filter by trace ID")
  @query
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Filter by span ID")
  @query
  @encodedName("application/json", "span_id")
  spanId?: SpanId;

  @doc("Minimum severity number")
  @query
  @encodedName("application/json", "min_severity")
  minSeverity?: SeverityNumber;

  @doc("Maximum severity number")
  @query
  @encodedName("application/json", "max_severity")
  maxSeverity?: SeverityNumber;

  @doc("Filter by severity text")
  @query
  @encodedName("application/json", "severity_text")
  severityText?: SeverityText;

  @doc("Filter by service name")
  @query
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Full-text search in body")
  @query
  search?: string;

  @doc("Filter by attribute key-value pair")
  @query
  attribute?: string;
}

// =============================================================================
// Log Aggregations
// =============================================================================

@doc("Aggregated log statistics")
model LogStats {
  @doc("Total log count")
  @encodedName("application/json", "total_count")
  totalCount: Count;

  @doc("Log counts by severity")
  @encodedName("application/json", "by_severity")
  bySeverity: LogCountBySeverity[];

  @doc("Log counts by service")
  @encodedName("application/json", "by_service")
  byService: LogCountByDimension[];

  @doc("Logs per second rate")
  @encodedName("application/json", "logs_per_second")
  logsPerSecond: float64;

  @doc("Error log rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;
}

@doc("Log count by severity level")
model LogCountBySeverity {
  @doc("Severity level")
  severity: SeverityText;

  @doc("Log count")
  count: Count;

  @doc("Percentage of total")
  percentage: Percentage;
}

@doc("Log count by dimension")
model LogCountByDimension {
  @doc("Dimension value")
  dimension: string;

  @doc("Log count")
  count: Count;

  @doc("Error count for this dimension")
  @encodedName("application/json", "error_count")
  errorCount: Count;
}

// =============================================================================
// Structured Log Templates
// =============================================================================

@doc("Log message template with placeholders")
model LogTemplate {
  @doc("Template string with {placeholder} syntax")
  template: string;

  @doc("Template parameters")
  parameters: Attribute[];

  @doc("Rendered message")
  @encodedName("application/json", "rendered_message")
  renderedMessage?: string;
}


// ===== Flattened from: core/specs/otel/profiles.tsp =====
// =============================================================================
// QYL v2.0 - OpenTelemetry Profiles Model (v1development)
// =============================================================================
// Continuous profiling signal following OTel Profiles proto specification.
// Proto package: opentelemetry.proto.profiles.v1development
// All attributes are Development stability (semconv v1.39.0-v1.40.0).
// =============================================================================



namespace Qyl.OTel.Profiles;

// =============================================================================
// Profile Model
// =============================================================================

@doc("OpenTelemetry Profile representing a continuous profiling snapshot")
model Profile {
  @doc("Unique profile identifier (16 hex chars)")
  @key
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Sample type describing what was measured")
  @encodedName("application/json", "sample_type")
  sampleType?: ProfileValueType;

  @doc("Profiling samples")
  samples?: ProfileSample[];

  @doc("Profile start timestamp in nanoseconds since epoch")
  @encodedName("application/json", "time_unix_nano")
  timeUnixNano: int64;

  @doc("Profile duration in nanoseconds")
  @encodedName("application/json", "duration_nano")
  durationNano: int64;

  @doc("Sampling period type")
  @encodedName("application/json", "period_type")
  periodType?: ProfileValueType;

  @doc("Sampling period value")
  period?: int64;

  @doc("Original payload format (pprof, jfr, linux_perf)")
  @encodedName("application/json", "original_payload_format")
  originalPayloadFormat?: OriginalPayloadFormat;

  @doc("Original payload bytes (base64 encoded, opaque — not parsed)")
  @encodedName("application/json", "original_payload")
  originalPayload?: bytes;

  @doc("Profile attributes")
  attributes?: Attribute[];

  @doc("Dropped attributes count")
  @encodedName("application/json", "dropped_attributes_count")
  droppedAttributesCount?: Count;

  @doc("Resource describing the entity that produced this profile")
  resource: Qyl.OTel.Resource.Resource;

  @doc("Instrumentation scope")
  @encodedName("application/json", "instrumentation_scope")
  instrumentationScope?: InstrumentationScope;

  // Dictionary tables (dereferenced from ProfilesDictionary)

  @doc("String table for index-based lookups")
  @encodedName("application/json", "string_table")
  stringTable?: string[];

  @doc("Function table")
  @encodedName("application/json", "function_table")
  functionTable?: ProfileFunction[];

  @doc("Location table")
  @encodedName("application/json", "location_table")
  locationTable?: ProfileLocation[];

  @doc("Mapping table (memory-mapped binaries)")
  @encodedName("application/json", "mapping_table")
  mappingTable?: ProfileMapping[];

  @doc("Link table for cross-signal correlation")
  @encodedName("application/json", "link_table")
  linkTable?: ProfileLink[];

  @doc("Stack table (ordered location sequences)")
  @encodedName("application/json", "stack_table")
  stackTable?: ProfileStack[];
}

// =============================================================================
// Profile Sub-Models
// =============================================================================

@doc("Sample type descriptor (what was measured and in what unit)")
model ProfileValueType {
  @doc("Type string index into string table")
  @encodedName("application/json", "type_strindex")
  typeStrindex?: int32;

  @doc("Unit string index into string table")
  @encodedName("application/json", "unit_strindex")
  unitStrindex?: int32;
}

@doc("Individual profiling sample")
model ProfileSample {
  @doc("Stack index into stack table")
  @encodedName("application/json", "stack_index")
  stackIndex?: int32;

  @doc("Attribute indices into attribute table")
  @encodedName("application/json", "attribute_indices")
  attributeIndices?: int32[];

  @doc("Link index into link table for cross-signal correlation")
  @encodedName("application/json", "link_index")
  linkIndex?: int32;

  @doc("Sample values (one per sample type)")
  values?: int64[];

  @doc("Timestamps for each value in nanoseconds since epoch")
  @encodedName("application/json", "timestamps_unix_nano")
  timestampsUnixNano?: int64[];
}

@doc("Code location within a profiled binary")
model ProfileLocation {
  @doc("Mapping index into mapping table")
  @encodedName("application/json", "mapping_index")
  mappingIndex?: int32;

  @doc("Instruction address (program counter)")
  address?: int64;

  @doc("Source lines at this location")
  lines?: ProfileLine[];

  @doc("Attribute indices")
  @encodedName("application/json", "attribute_indices")
  attributeIndices?: int32[];
}

@doc("Source line within a location")
model ProfileLine {
  @doc("Function index into function table")
  @encodedName("application/json", "function_index")
  functionIndex?: int32;

  @doc("Source line number")
  line?: int64;

  @doc("Source column number")
  column?: int64;
}

@doc("Function identity in a profiled binary")
model ProfileFunction {
  @doc("Function name string index")
  @encodedName("application/json", "name_strindex")
  nameStrindex?: int32;

  @doc("System/mangled function name string index")
  @encodedName("application/json", "system_name_strindex")
  systemNameStrindex?: int32;

  @doc("Source filename string index")
  @encodedName("application/json", "filename_strindex")
  filenameStrindex?: int32;

  @doc("First line number of the function in source")
  @encodedName("application/json", "start_line")
  startLine?: int64;
}

@doc("Memory mapping for a loaded binary or shared library")
model ProfileMapping {
  @doc("Start address of the mapping in virtual memory")
  @encodedName("application/json", "memory_start")
  memoryStart?: int64;

  @doc("Limit address (exclusive) of the mapping")
  @encodedName("application/json", "memory_limit")
  memoryLimit?: int64;

  @doc("Offset within the mapped file")
  @encodedName("application/json", "file_offset")
  fileOffset?: int64;

  @doc("Filename string index of the mapped binary")
  @encodedName("application/json", "filename_strindex")
  filenameStrindex?: int32;

  @doc("Attribute indices")
  @encodedName("application/json", "attribute_indices")
  attributeIndices?: int32[];
}

@doc("Cross-signal link from profile sample to trace/span")
model ProfileLink {
  @doc("Linked trace ID (16 bytes, W3C format)")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Linked span ID (8 bytes)")
  @encodedName("application/json", "span_id")
  spanId?: SpanId;
}

@doc("Ordered sequence of location indices forming a call stack")
model ProfileStack {
  @doc("Location indices from leaf (index 0) to root")
  @encodedName("application/json", "location_indices")
  locationIndices?: int32[];
}

// =============================================================================
// Profile Query Filters
// =============================================================================

@doc("Filter parameters for querying profiles")
model ProfileQueryFilters {
  @doc("Filter by trace ID")
  @query
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Filter by span ID")
  @query
  @encodedName("application/json", "span_id")
  spanId?: SpanId;

  @doc("Filter by session ID")
  @query
  @encodedName("application/json", "session_id")
  sessionId?: SessionId;

  @doc("Filter by service name")
  @query
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Filter by sample type (e.g., cpu, alloc_objects, wall)")
  @query
  @encodedName("application/json", "sample_type")
  sampleType?: string;

  @doc("Filter by profile frame type (e.g., dotnet, jvm, cpython)")
  @query
  @encodedName("application/json", "profile_frame_type")
  profileFrameType?: string;

  @doc("Filter by original payload format")
  @query
  @encodedName("application/json", "original_payload_format")
  originalPayloadFormat?: OriginalPayloadFormat;

  @doc("Minimum duration in nanoseconds")
  @query
  @encodedName("application/json", "min_duration_nano")
  minDurationNano?: int64;

  @doc("Start of time range (inclusive, ISO 8601)")
  @query
  @encodedName("application/json", "time_from")
  timeFrom?: utcDateTime;

  @doc("End of time range (exclusive, ISO 8601)")
  @query
  @encodedName("application/json", "time_to")
  timeTo?: utcDateTime;
}

// =============================================================================
// Profile Aggregations
// =============================================================================

@doc("Aggregated profile statistics")
model ProfileStats {
  @doc("Total profile count")
  @encodedName("application/json", "total_count")
  totalCount: Count;

  @doc("Total sample count across all profiles")
  @encodedName("application/json", "total_sample_count")
  totalSampleCount: Count;

  @doc("Average duration in nanoseconds")
  @encodedName("application/json", "avg_duration_nano")
  avgDurationNano: float64;

  @doc("Stats grouped by sample type")
  @encodedName("application/json", "by_sample_type")
  bySampleType?: ProfileStatsByDimension[];

  @doc("Stats grouped by service")
  @encodedName("application/json", "by_service")
  byService?: ProfileStatsByDimension[];

  @doc("Stats grouped by frame type")
  @encodedName("application/json", "by_frame_type")
  byFrameType?: ProfileStatsByDimension[];
}

@doc("Profile statistics for a specific dimension value")
model ProfileStatsByDimension {
  @doc("Dimension value")
  dimension: string;

  @doc("Profile count for this dimension")
  count: Count;

  @doc("Total sample count for this dimension")
  @encodedName("application/json", "sample_count")
  sampleCount: Count;

  @doc("Average duration in nanoseconds")
  @encodedName("application/json", "avg_duration_nano")
  avgDurationNano: float64;
}


// ===== Flattened from: core/specs/otel/storage.tsp =====
// =============================================================================
// QYL v2.0 - Storage Models
// =============================================================================
// Flattened storage models with DuckDB table generation extensions.
// These are optimized for query performance, not wire format fidelity.
// =============================================================================




namespace Qyl.Storage;

// =============================================================================
// SpanRecord - Primary Storage Model
// =============================================================================

@doc("OpenTelemetry span record for storage and query")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "spans")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SpanRecord")
model SpanRecord {
  // === Identifiers (Primary Key) ===

  @doc("Unique span identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "span_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  spanId: SpanId;

  @doc("Trace identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_spans_trace_id")
  traceId: TraceId;

  @doc("Parent span identifier (null for root spans)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "parent_span_id")
  parentSpanId?: SpanId;

  // === Session Grouping ===

  @doc("Session identifier for grouping related traces")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "session_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_spans_session_id")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  sessionId?: SessionId;

  // === Core Span Fields ===

  @doc("Human-readable span name")
  @minLength(1)
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "name")
  name: string;

  @doc("Span kind")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "kind")
  kind: SpanKind;

  @doc("Start timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_time_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_spans_start_time")
  startTimeUnixNano: int64;

  @doc("End timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time_unix_nano")
  endTimeUnixNano: int64;

  @doc("Duration in nanoseconds")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "duration_ns")
  durationNs: DurationNs;

  // === Status ===

  @doc("Span status code")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "status_code")
  statusCode: SpanStatusCode;

  @doc("Status message (only for ERROR status)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "status_message")
  statusMessage?: string;

  // === Service Info ===

  @doc("Service name from resource attributes")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "service_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_spans_service_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  serviceName?: string;

  // === GenAI Promoted Columns (Fast Access) ===

  @doc("GenAI provider name (e.g., openai, anthropic) - OTel 1.40: gen_ai.provider.name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_provider_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_spans_gen_ai_provider_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiProviderName?: string;

  @doc("Requested model name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_request_model")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_spans_gen_ai_request_model")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiRequestModel?: string;

  @doc("Actual response model name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_response_model")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiResponseModel?: string;

  @doc("Input/prompt tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_input_tokens")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiInputTokens?: TokenCount;

  @doc("Output/completion tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_output_tokens")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiOutputTokens?: TokenCount;

  @doc("Request temperature")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_temperature")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiTemperature?: Temperature;

  @doc("Response finish reason")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_stop_reason")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiStopReason?: string;

  @doc("Tool name for tool calls")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_tool_name")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiToolName?: string;

  @doc("Tool call ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_tool_call_id")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiToolCallId?: string;

  @doc("Estimated cost in USD")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_cost_usd")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiCostUsd?: CostUsd;

  // === Attributes (JSON Blob) ===

  @doc("All span attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "attributes_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  attributesJson?: string;

  @doc("Resource attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "resource_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  resourceJson?: string;

  // === W3C Baggage (JSON) ===

  @doc("W3C Baggage key-value pairs as JSON for cross-cutting concern propagation")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "baggage_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  baggageJson?: string;

  // === Schema URL Tracking ===

  @doc("OTel semantic convention schema URL (e.g., https://opentelemetry.io/schemas/1.40.0)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "schema_url")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(256)")
  schemaUrl?: string;

  // === Internal Storage Fields ===

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}

// =============================================================================
// SessionSummary - Aggregated View
// =============================================================================

@doc("Session summary with aggregated metrics")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "sessions")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SessionSummary")
model SessionSummary {
  @doc("Session identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "session_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  sessionId: SessionId;

  @doc("First span timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_time")
  startTime: int64;

  @doc("Last span timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time")
  endTime: int64;

  @doc("Total span count")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "span_count")
  spanCount: Count;

  @doc("Error span count")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "error_count")
  errorCount: Count;

  @doc("Total input tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "total_input_tokens")
  totalInputTokens: TokenCount;

  @doc("Total output tokens")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "total_output_tokens")
  totalOutputTokens: TokenCount;

  @doc("Total cost in USD")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "total_cost_usd")
  totalCostUsd: CostUsd;

  @doc("Primary service name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "service_name")
  serviceName?: string;

  @doc("Primary GenAI provider")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_provider_name")
  genAiProviderName?: string;

  @doc("Primary model used")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_model")
  genAiModel?: string;
}

// =============================================================================
// LogRecordStorage - Log Storage Model
// =============================================================================

@doc("OpenTelemetry log record for storage and query")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "logs")
@TypeSpec.OpenAPI.extension("x-csharp-type", "LogRecordStorage")
model LogRecordStorage {
  // === Identifiers ===

  @doc("Unique log record identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "log_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  logId: string;

  @doc("Associated trace ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_logs_trace_id")
  traceId?: TraceId;

  @doc("Associated span ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "span_id")
  spanId?: SpanId;

  // === Session Grouping ===

  @doc("Session identifier for grouping related logs")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "session_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_logs_session_id")
  sessionId?: SessionId;

  // === Timing ===

  @doc("Timestamp when the event occurred (nanoseconds since epoch)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "time_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_logs_time")
  timeUnixNano: int64;

  @doc("Timestamp when the log was observed/collected")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "observed_time_unix_nano")
  observedTimeUnixNano?: int64;

  // === Severity ===

  @doc("Severity number (1-24)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "severity_number")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_logs_severity")
  severityNumber: SeverityNumber;

  @doc("Severity text (TRACE, DEBUG, INFO, WARN, ERROR, FATAL)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "severity_text")
  severityText?: string;

  // === Content ===

  @doc("Log body - the main content")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "body")
  body?: string;

  // === Service Info ===

  @doc("Service name from resource attributes")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "service_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_logs_service_name")
  serviceName?: string;

  // === Attributes (JSON Blob) ===

  @doc("Log attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "attributes_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  attributesJson?: string;

  @doc("Resource attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "resource_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  resourceJson?: string;

  // === Source Location ===

  @doc("Source file path where the log was emitted")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "source_file")
  sourceFile?: string;

  @doc("Source line number")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "source_line")
  sourceLine?: int32;

  @doc("Source column number")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "source_column")
  sourceColumn?: int32;

  @doc("Source method name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "source_method")
  sourceMethod?: string;

  // === Internal Storage Fields ===

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}

// =============================================================================
// TraceNode - For Hierarchical Display
// =============================================================================

@jsonSchema
@doc("Trace tree node for hierarchical span display")
@TypeSpec.OpenAPI.extension("x-csharp-type", "TraceNode")
model TraceNode {
  @doc("Span data")
  span: SpanRecord;

  @doc("Child spans")
  children: TraceNode[];

  @doc("Depth in tree (0 = root)")
  depth: int32;
}

// =============================================================================
// GenAiSpanData - Extracted GenAI Attributes
// =============================================================================

@jsonSchema
@doc("Extracted GenAI attributes from a span")
@TypeSpec.OpenAPI.extension("x-csharp-type", "GenAiSpanData")
model GenAiSpanData {
  @doc("Provider name")
  providerName?: string;

  @doc("Requested model")
  requestModel?: string;

  @doc("Response model")
  responseModel?: string;

  @doc("Input tokens")
  inputTokens?: TokenCount;

  @doc("Output tokens")
  outputTokens?: TokenCount;

  @doc("Temperature")
  temperature?: Temperature;

  @doc("Finish reason")
  stopReason?: string;

  @doc("Tool name")
  toolName?: string;

  @doc("Tool call ID")
  toolCallId?: string;

  @doc("Estimated cost")
  costUsd?: CostUsd;
}

// =============================================================================
// ProfileRecord - Primary Profile Storage (v1development)
// =============================================================================

@doc("OpenTelemetry profile record for storage and query")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "profiles")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ProfileStorageRow")
model ProfileRecord {
  @doc("Unique profile identifier")
  @key
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  profileId: string;

  @doc("Correlated trace ID (from Link table)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_profiles_trace_id")
  traceId?: TraceId;

  @doc("Correlated span ID (from Link table)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "span_id")
  spanId?: SpanId;

  @doc("Session identifier for grouping related profiles")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "session_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_profiles_session_id")
  sessionId?: SessionId;

  @doc("Profile start timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "time_unix_nano")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_profiles_time")
  timeUnixNano: int64;

  @doc("Profile duration in nanoseconds")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "duration_nano")
  durationNano: int64;

  @doc("Number of samples in this profile")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "sample_count")
  sampleCount: int32;

  @doc("Sample type (e.g., cpu, alloc_objects, wall)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "sample_type")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_profiles_sample_type")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  sampleType?: string;

  @doc("Sample unit (e.g., nanoseconds, bytes, count)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "sample_unit")
  sampleUnit?: string;

  @doc("Original payload format")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "original_payload_format")
  originalPayloadFormat?: string;

  @doc("Service name from resource attributes")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "service_name")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_profiles_service")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  serviceName?: string;

  @doc("Profile frame type (dotnet, jvm, cpython, etc.)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_frame_type")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  profileFrameType?: string;

  @doc("Profile attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "attributes_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  attributesJson?: string;

  @doc("Resource attributes as JSON")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "resource_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  resourceJson?: string;

  @doc("Full profile structure as JSON blob (denormalized for single-query access)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_data_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  profileDataJson?: string;

  @doc("OTel semantic convention schema URL")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "schema_url")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR(256)")
  schemaUrl?: string;

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}

// =============================================================================
// Profile Child Tables (Normalized from ProfilesDictionary)
// =============================================================================

@doc("Dereferenced function from profile dictionary")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "profile_functions")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ProfileFunctionRow")
model ProfileFunctionRecord {
  @doc("Parent profile ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_id")
  profileId: string;

  @doc("Ordinal position in function table")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "ordinal")
  ordinal: int32;

  @doc("Resolved function name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "name")
  name?: string;

  @doc("Resolved system/mangled function name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "system_name")
  systemName?: string;

  @doc("Resolved source filename")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "filename")
  filename?: string;

  @doc("First line number of the function")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_line")
  startLine?: int64;
}

@doc("Dereferenced location from profile dictionary")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "profile_locations")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ProfileLocationRow")
model ProfileLocationRecord {
  @doc("Parent profile ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_id")
  profileId: string;

  @doc("Ordinal position in location table")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "ordinal")
  ordinal: int32;

  @doc("Mapping ordinal for the binary this location belongs to")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "mapping_ordinal")
  mappingOrdinal?: int32;

  @doc("Instruction address")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "address")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
  address?: int64;

  @doc("Source lines as JSON (function ordinal, line, column)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "lines_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  linesJson?: string;
}

@doc("Dereferenced memory mapping from profile dictionary")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "profile_mappings")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ProfileMappingRow")
model ProfileMappingRecord {
  @doc("Parent profile ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_id")
  profileId: string;

  @doc("Ordinal position in mapping table")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "ordinal")
  ordinal: int32;

  @doc("Resolved filename of the mapped binary")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "filename")
  filename?: string;

  @doc("Start address in virtual memory")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "memory_start")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
  memoryStart?: int64;

  @doc("Limit address (exclusive)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "memory_limit")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
  memoryLimit?: int64;

  @doc("Offset within the mapped file")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "file_offset")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "UBIGINT")
  fileOffset?: int64;
}

@doc("Profile sample with resolved cross-signal links")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "profile_samples")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ProfileSampleRow")
model ProfileSampleRecord {
  @doc("Parent profile ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_id")
  profileId: string;

  @doc("Ordinal position in sample list")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "ordinal")
  ordinal: int32;

  @doc("Stack ordinal referencing profile_stacks")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "stack_ordinal")
  stackOrdinal?: int32;

  @doc("Resolved trace ID from link table")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "link_trace_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-index", "idx_profile_samples_trace")
  linkTraceId?: TraceId;

  @doc("Resolved span ID from link table")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "link_span_id")
  linkSpanId?: SpanId;

  @doc("Sample values as JSON array")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "values_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  valuesJson?: string;

  @doc("Sample timestamps as JSON array")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "timestamps_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  timestampsJson?: string;
}

@doc("Profile stack — ordered sequence of location ordinals")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "profile_stacks")
@TypeSpec.OpenAPI.extension("x-csharp-type", "ProfileStackRow")
model ProfileStackRecord {
  @doc("Parent profile ID")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "profile_id")
  profileId: string;

  @doc("Ordinal position in stack table")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "ordinal")
  ordinal: int32;

  @doc("Location ordinals as JSON array (leaf to root)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "location_ordinals_json")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "VARCHAR")
  locationOrdinalsJson?: string;
}


// ===== Flattened from: core/specs/main.tsp =====
// =============================================================================
// QYL v2.0 - Main TypeSpec Entry Point
// =============================================================================
// OpenTelemetry Semantic Conventions v1.40 compliant schema.
//
// Structure:
//   - Common: types, errors, pagination
//   - OTel Core: enums, span, logs, metrics, profiles, resource, storage
//   - Generated: semconv.g.tsp (OTel 1.40 attribute keys from NPM)
//   - Domains: qyl-specific models (stats, entities, etc.)
//   - API: REST routes, SSE streaming
//
// Storage Tables (x-duckdb-table):
//   - Span → spans (otel/span.tsp)
//   - LogRecord → logs (otel/logs.tsp)
//   - SessionEntity → session_entities (models/session.tsp)
//   - ErrorEntity → errors (models/error.tsp)
//   - TestRunEntity → test_runs (models/test.tsp)
//   - TestCaseEntity → test_cases (models/test.tsp)
//   - DeploymentEntity → deployments (models/deployment.tsp)
// =============================================================================


// =============================================================================
// Common Infrastructure
// =============================================================================


// =============================================================================
// qyl-native emit routing (Commit 5 decoration + Commit 6 duckdb tuple mapping)
// =============================================================================


// =============================================================================
// OTel Core Types
// =============================================================================


// =============================================================================
// QYL Domain Models
// =============================================================================
// qyl-specific models (stats, entities, aggregations). Semconv attribute keys
// are used directly as string literals on @encodedName — the earlier typed
// Keys.* TSP bridge was imported but never referenced, so dropped with its
// 6953-line generated semconv.g.tsp.
// =============================================================================

// AI Domain - GenAI stats and cost models

// Transport Domain - HTTP/RPC/Messaging stats

// Data Domain - Database stats

// Observe Domain - Sessions, errors, logs, tests, OTel config

// Ops Domain - Deployments

// Runtime Domain - System stats

// Identity Domain - User stats

// =============================================================================
// API Routes
// =============================================================================


// =============================================================================
// Namespace Re-exports for Convenience
// =============================================================================



// ===== Flattened from: core/specs/qyl-emit-config.tsp =====
// =============================================================================
// QYL Emit Config — central augment-decorator file for the three qyl-native
// TypeSpec emitters (@qyl/typespec-emit-csharp, emit-duckdb, emit-ts-types).
//
// This file decorates the existing hand-authored TypeSpec models so the new
// emitters know:
//   - which C# namespace each model maps to (matches NamespaceRoutingTable.cs
//     exactly, mirroring the current contract-drift outputs)
//   - which models are DuckDB tables and what their SQL names are
//   - which scalars are opaque branded string IDs on the TypeScript side
//
// Commit 5 of the TypeSpec-native-emitters mandate. Decoration-only: the old
// @extension("x-duckdb-table", ...) / @extension("x-csharp-type", ...) calls
// remain in place until Commit 10 deletes the old SchemaGenerator.cs path.
// Both paths emit identical shapes until Commit 6 cuts tspconfig over.
// =============================================================================




// -----------------------------------------------------------------------------
// C# namespace routing — one @@csharpNamespace per namespace declared in any
// hand-authored .tsp file. Values mirror NamespaceRoutingTable.cs exactly.
// -----------------------------------------------------------------------------

@@csharpNamespace(Qyl.Api, "Qyl.Api");
@@csharpNamespace(Qyl.Api.Streaming, "Qyl.Api.Streaming");

@@csharpNamespace(Qyl.Common, "Qyl.Common");
@@csharpNamespace(Qyl.Common.Errors, "Qyl.Common.Errors");
@@csharpNamespace(Qyl.Common.Pagination, "Qyl.Common.Pagination");

@@csharpNamespace(Qyl.Intelligence, "Qyl.Intelligence");

@@csharpNamespace(Qyl.Domains, "Qyl.Domains");
@@csharpNamespace(Qyl.Domains.AI.GenAi, "Qyl.Domains.AI.GenAi");
@@csharpNamespace(Qyl.Domains.Agent.Run, "Qyl.Domains.Agent.Run");
@@csharpNamespace(Qyl.Domains.Agent.ToolCall, "Qyl.Domains.Agent.ToolCall");
@@csharpNamespace(Qyl.Domains.Agent.Checkpoint, "Qyl.Domains.Agent.Checkpoint");
@@csharpNamespace(Qyl.Domains.Agent.Workflow, "Qyl.Domains.Agent.Workflow");
@@csharpNamespace(Qyl.Domains.Alerting, "Qyl.Domains.Alerting");
@@csharpNamespace(Qyl.Domains.Configurator, "Qyl.Domains.Configurator");
@@csharpNamespace(Qyl.Domains.Data.Db, "Qyl.Domains.Data.Db");
@@csharpNamespace(Qyl.Domains.Identity, "Qyl.Domains.Identity");
@@csharpNamespace(Qyl.Domains.Issues, "Qyl.Domains.Issues");
@@csharpNamespace(Qyl.Domains.Loom.Triage, "Qyl.Domains.Loom.Triage");
@@csharpNamespace(Qyl.Domains.Observe.Error, "Qyl.Domains.Observe.Error");
@@csharpNamespace(Qyl.Domains.Observe.Log, "Qyl.Domains.Observe.Log");
@@csharpNamespace(Qyl.Domains.Observe.Otel, "Qyl.Domains.Observe.Otel");
@@csharpNamespace(Qyl.Domains.Observe.Session, "Qyl.Domains.Observe.Session");
@@csharpNamespace(Qyl.Domains.Observe.Test, "Qyl.Domains.Observe.Test");
@@csharpNamespace(Qyl.Domains.Ops.Deployment, "Qyl.Domains.Ops.Deployment");
@@csharpNamespace(Qyl.Domains.Ops.Retention, "Qyl.Domains.Ops.Retention");
@@csharpNamespace(Qyl.Domains.Runtime.System, "Qyl.Domains.Runtime.System");
@@csharpNamespace(Qyl.Domains.Search, "Qyl.Domains.Search");
@@csharpNamespace(Qyl.Domains.Transport.Http, "Qyl.Domains.Transport.Http");
@@csharpNamespace(Qyl.Domains.Transport.Messaging, "Qyl.Domains.Transport.Messaging");
@@csharpNamespace(Qyl.Domains.Transport.Rpc, "Qyl.Domains.Transport.Rpc");
@@csharpNamespace(Qyl.Domains.Workflow, "Qyl.Domains.Workflow");
@@csharpNamespace(Qyl.Domains.Workspace, "Qyl.Domains.Workspace");

@@csharpNamespace(Qyl.OTel, "Qyl.OTel");
@@csharpNamespace(Qyl.OTel.Enums, "Qyl.OTel.Enums");
@@csharpNamespace(Qyl.OTel.Logs, "Qyl.OTel.Logs");
@@csharpNamespace(Qyl.OTel.Metrics, "Qyl.OTel.Metrics");
@@csharpNamespace(Qyl.OTel.Profiles, "Qyl.OTel.Profiles");
@@csharpNamespace(Qyl.OTel.Resource, "Qyl.OTel.Resource");
@@csharpNamespace(Qyl.OTel.Traces, "Qyl.OTel.Traces");

@@csharpNamespace(Qyl.Storage, "Qyl.Storage");

// -----------------------------------------------------------------------------
// TypeScript brands for opaque string IDs. Mandate §Commit-4 exact list plus
// any scalar in core/specs/common/types.tsp with x-csharp-struct extension.
// -----------------------------------------------------------------------------

@@tsBrand(Qyl.Common.TraceId);
@@tsBrand(Qyl.Common.SpanId);
@@tsBrand(Qyl.Common.SessionId);
@@tsBrand(Qyl.Common.UserId);


// ===== Flattened from: core/specs/qyl-emit-duckdb.tsp =====
// Generated by /tmp/gen-duckdb-augments.mjs — do not edit by hand.
// Sparse: emitter derives defaults from scalars; overrides only where
// openapi x-duckdb-type deviates. PK/index/@@duckdbRowTimestamp always.



// Qyl.Domains.Alerting.AlertFiringEntity -> alert_firings
@@duckdbTable(Qyl.Domains.Alerting.AlertFiringEntity, "alert_firings");
@@duckdbRowTimestamp(Qyl.Domains.Alerting.AlertFiringEntity);
@@duckdbType(Qyl.Domains.Alerting.AlertFiringEntity.contextJson, "JSON");

// Qyl.Domains.Alerting.AlertRuleEntity -> alert_rules
@@duckdbTable(Qyl.Domains.Alerting.AlertRuleEntity, "alert_rules");
@@duckdbRowTimestamp(Qyl.Domains.Alerting.AlertRuleEntity);
@@duckdbType(Qyl.Domains.Alerting.AlertRuleEntity.conditionJson, "JSON");
@@duckdbType(Qyl.Domains.Alerting.AlertRuleEntity.thresholdJson, "JSON");
@@duckdbType(Qyl.Domains.Alerting.AlertRuleEntity.targetFilterJson, "JSON");
@@duckdbType(Qyl.Domains.Alerting.AlertRuleEntity.notificationChannelsJson, "JSON");

// Qyl.Domains.Alerting.FixRunEntity -> fix_runs
@@duckdbTable(Qyl.Domains.Alerting.FixRunEntity, "fix_runs");
@@duckdbRowTimestamp(Qyl.Domains.Alerting.FixRunEntity);

// Qyl.Domains.Configurator.GenerationJobEntity -> generation_jobs
@@duckdbTable(Qyl.Domains.Configurator.GenerationJobEntity, "generation_jobs");
@@duckdbRowTimestamp(Qyl.Domains.Configurator.GenerationJobEntity);

// Qyl.Domains.Configurator.GenerationProfileEntity -> generation_profiles
@@duckdbTable(Qyl.Domains.Configurator.GenerationProfileEntity, "generation_profiles");
@@duckdbRowTimestamp(Qyl.Domains.Configurator.GenerationProfileEntity);
@@duckdbType(Qyl.Domains.Configurator.GenerationProfileEntity.featuresJson, "JSON");
@@duckdbType(Qyl.Domains.Configurator.GenerationProfileEntity.templateOverridesJson, "JSON");

// Qyl.Domains.Configurator.GenerationSelectionEntity -> generation_selections
@@duckdbTable(Qyl.Domains.Configurator.GenerationSelectionEntity, "generation_selections");
@@duckdbRowTimestamp(Qyl.Domains.Configurator.GenerationSelectionEntity);
@@duckdbType(Qyl.Domains.Configurator.GenerationSelectionEntity.configJson, "JSON");

// Qyl.Domains.Issues.ErrorBreadcrumbEntity -> error_breadcrumbs
@@duckdbTable(Qyl.Domains.Issues.ErrorBreadcrumbEntity, "error_breadcrumbs");
@@duckdbRowTimestamp(Qyl.Domains.Issues.ErrorBreadcrumbEntity);
@@duckdbType(Qyl.Domains.Issues.ErrorBreadcrumbEntity.dataJson, "JSON");

// Qyl.Domains.Issues.ErrorIssueEntity -> error_issues
@@duckdbTable(Qyl.Domains.Issues.ErrorIssueEntity, "error_issues");
@@duckdbRowTimestamp(Qyl.Domains.Issues.ErrorIssueEntity);
@@duckdbType(Qyl.Domains.Issues.ErrorIssueEntity.tagsJson, "JSON");
@@duckdbType(Qyl.Domains.Issues.ErrorIssueEntity.metadataJson, "JSON");

// Qyl.Domains.Issues.ErrorIssueEventEntity -> error_issue_events
@@duckdbTable(Qyl.Domains.Issues.ErrorIssueEventEntity, "error_issue_events");
@@duckdbRowTimestamp(Qyl.Domains.Issues.ErrorIssueEventEntity);
@@duckdbType(Qyl.Domains.Issues.ErrorIssueEventEntity.stackFramesJson, "JSON");
@@duckdbType(Qyl.Domains.Issues.ErrorIssueEventEntity.contextJson, "JSON");
@@duckdbType(Qyl.Domains.Issues.ErrorIssueEventEntity.tagsJson, "JSON");

// Qyl.Domains.Observe.Error.ErrorEntity -> errors
@@duckdbTable(Qyl.Domains.Observe.Error.ErrorEntity, "errors");
@@duckdbRowTimestamp(Qyl.Domains.Observe.Error.ErrorEntity);

// Qyl.Domains.Observe.Session.SessionEntity -> session_entities
@@duckdbTable(Qyl.Domains.Observe.Session.SessionEntity, "session_entities");
@@duckdbRowTimestamp(Qyl.Domains.Observe.Session.SessionEntity);

// Qyl.Domains.Ops.Deployment.DeploymentEntity -> deployments
@@duckdbTable(Qyl.Domains.Ops.Deployment.DeploymentEntity, "deployments");
@@duckdbRowTimestamp(Qyl.Domains.Ops.Deployment.DeploymentEntity);

// Qyl.Domains.Workflow.WorkflowEventEntity -> workflow_events
@@duckdbTable(Qyl.Domains.Workflow.WorkflowEventEntity, "workflow_events");
@@duckdbRowTimestamp(Qyl.Domains.Workflow.WorkflowEventEntity);
@@duckdbType(Qyl.Domains.Workflow.WorkflowEventEntity.payloadJson, "JSON");

// Qyl.Domains.Workflow.WorkflowNodeEntity -> workflow_nodes
@@duckdbTable(Qyl.Domains.Workflow.WorkflowNodeEntity, "workflow_nodes");
@@duckdbRowTimestamp(Qyl.Domains.Workflow.WorkflowNodeEntity);
@@duckdbType(Qyl.Domains.Workflow.WorkflowNodeEntity.inputJson, "JSON");
@@duckdbType(Qyl.Domains.Workflow.WorkflowNodeEntity.outputJson, "JSON");

// Qyl.Domains.Workflow.WorkflowRunEntity -> workflow_runs
@@duckdbTable(Qyl.Domains.Workflow.WorkflowRunEntity, "workflow_runs");
@@duckdbRowTimestamp(Qyl.Domains.Workflow.WorkflowRunEntity);
@@duckdbType(Qyl.Domains.Workflow.WorkflowRunEntity.inputJson, "JSON");
@@duckdbType(Qyl.Domains.Workflow.WorkflowRunEntity.outputJson, "JSON");

// Qyl.Domains.Workspace.HandshakeSessionEntity -> handshake_sessions
@@duckdbTable(Qyl.Domains.Workspace.HandshakeSessionEntity, "handshake_sessions");
@@duckdbRowTimestamp(Qyl.Domains.Workspace.HandshakeSessionEntity);

// Qyl.Domains.Workspace.ProjectEntity -> projects
@@duckdbTable(Qyl.Domains.Workspace.ProjectEntity, "projects");
@@duckdbRowTimestamp(Qyl.Domains.Workspace.ProjectEntity);

// Qyl.Domains.Workspace.ProjectEnvironmentEntity -> project_environments
@@duckdbTable(Qyl.Domains.Workspace.ProjectEnvironmentEntity, "project_environments");
@@duckdbRowTimestamp(Qyl.Domains.Workspace.ProjectEnvironmentEntity);

// Qyl.Domains.Workspace.WorkspaceEnvelopeEntity -> workspace_envelopes
@@duckdbTable(Qyl.Domains.Workspace.WorkspaceEnvelopeEntity, "workspace_envelopes");
@@duckdbRowTimestamp(Qyl.Domains.Workspace.WorkspaceEnvelopeEntity);
@@duckdbType(Qyl.Domains.Workspace.WorkspaceEnvelopeEntity.configJson, "JSON");

// Qyl.Storage.ProfileRecord -> profiles
@@duckdbTable(Qyl.Storage.ProfileRecord, "profiles");
@@duckdbRowTimestamp(Qyl.Storage.ProfileRecord);
@@duckdbPrimaryKey(Qyl.Storage.ProfileRecord.profileId);
@@duckdbIndex(Qyl.Storage.ProfileRecord.traceId, "idx_profiles_trace_id");
@@duckdbIndex(Qyl.Storage.ProfileRecord.sessionId, "idx_profiles_session_id");
@@duckdbIndex(Qyl.Storage.ProfileRecord.timeUnixNano, "idx_profiles_time");
@@duckdbIndex(Qyl.Storage.ProfileRecord.sampleType, "idx_profiles_sample_type");
@@duckdbIndex(Qyl.Storage.ProfileRecord.serviceName, "idx_profiles_service");
@@duckdbType(Qyl.Storage.ProfileRecord.schemaUrl, "VARCHAR(256)");
@@duckdbType(Qyl.Storage.ProfileRecord.createdAt, "TIMESTAMP DEFAULT CURRENT_TIMESTAMP");

// Qyl.Storage.SpanRecord -> spans
@@duckdbTable(Qyl.Storage.SpanRecord, "spans");
@@duckdbRowTimestamp(Qyl.Storage.SpanRecord);
@@duckdbPrimaryKey(Qyl.Storage.SpanRecord.spanId);
@@duckdbIndex(Qyl.Storage.SpanRecord.traceId, "idx_spans_trace_id");
@@duckdbIndex(Qyl.Storage.SpanRecord.sessionId, "idx_spans_session_id");
@@duckdbIndex(Qyl.Storage.SpanRecord.startTimeUnixNano, "idx_spans_start_time");
@@duckdbIndex(Qyl.Storage.SpanRecord.serviceName, "idx_spans_service_name");
@@duckdbIndex(Qyl.Storage.SpanRecord.genAiProviderName, "idx_spans_gen_ai_provider_name");
@@duckdbIndex(Qyl.Storage.SpanRecord.genAiRequestModel, "idx_spans_gen_ai_request_model");
@@duckdbType(Qyl.Storage.SpanRecord.schemaUrl, "VARCHAR(256)");
@@duckdbType(Qyl.Storage.SpanRecord.createdAt, "TIMESTAMP DEFAULT now()");



// ===== Flattened from: core/specs/api/routes.tsp =====
// =============================================================================
// QYL v2.0 - API Routes
// =============================================================================
// RESTful API routes with proper pagination, streaming, and error handling.
// =============================================================================



// New domain models



// =============================================================================
// API Versioning
// =============================================================================

@versioned(ApiVersions)
@service(#{
  title: "QYL Observability API",
})
@server("https://api.qyl.dev", "QYL Production API")
@server("https://api.staging.qyl.dev", "QYL Staging API")
namespace Qyl.Api;

enum ApiVersions {
  @doc("Initial API version")
  v1: "2025-12-01",

  @doc("GenAI observability additions")
  v2: "2026-01-15",

  @doc("Full OTel v1.40 compliance")
  v3: "2026-01-26",
}

// =============================================================================
// Common Response Models
// =============================================================================

@doc("Health status for component-level reporting (HealthUiResponse, dashboard).")
enum HealthStatus {
  healthy: "healthy",
  degraded: "degraded",
  unhealthy: "unhealthy",
}

// =============================================================================
// Traces API
// =============================================================================

@route("/api/v1/traces")
@tag("Traces")
interface TracesApi {
  @doc("List traces with pagination")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Minimum duration in ms") minDurationMs?: int64,
    @query @doc("Maximum duration in ms") maxDurationMs?: int64,
    @query @doc("Status filter") status?: SpanStatusCode,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor for pagination") cursor?: string,
  ): CursorPage<Trace> | NotFoundError | ValidationError | InternalServerError;

  @doc("Get a specific trace by ID")
  @get
  @route("/{traceId}")
  get(@path traceId: TraceId): Trace | NotFoundError | InternalServerError;

  @doc("Get spans for a trace")
  @get
  @route("/{traceId}/spans")
  getSpans(
    @path traceId: TraceId,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<SpanRecord> | NotFoundError | InternalServerError;

  @doc("Search traces")
  @post
  @route("/search")
  search(@body query: TraceQuery): CursorPage<Trace> | ValidationError | InternalServerError;
}

@doc("Trace search query")
model TraceQuery {
  @doc("Free text search")
  query?: string;

  @doc("Service name filter")
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Operation name filter")
  @encodedName("application/json", "operation_name")
  operationName?: string;

  @doc("Minimum duration in milliseconds")
  @encodedName("application/json", "min_duration_ms")
  minDurationMs?: int64;

  @doc("Maximum duration in milliseconds")
  @encodedName("application/json", "max_duration_ms")
  maxDurationMs?: int64;

  @doc("Status filter")
  status?: SpanStatusCode;

  @doc("Time range start")
  @encodedName("application/json", "start_time")
  startTime?: utcDateTime;

  @doc("Time range end")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Tag filters")
  tags?: Record<string>;

  @doc("Page size")
  @minValue(1)
  @maxValue(1000)
  limit?: int32 = 100;

  @doc("Cursor")
  cursor?: string;
}

// =============================================================================
// Logs API
// =============================================================================

@route("/api/v1/logs")
@tag("Logs")
interface LogsApi {
  @doc("Query logs with filtering and pagination")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Minimum severity") severityMin?: SeverityNumber,
    @query @doc("Maximum severity") severityMax?: SeverityNumber,
    @query @doc("Trace ID filter") traceId?: TraceId,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Free text search") query?: string,
    @query @doc("Page size") @minValue(1) @maxValue(10000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
    @query @doc("Order") orderBy?: LogOrderBy,
  ): CursorPage<LogRecord> | ValidationError | InternalServerError;

  @doc("Search logs with complex query")
  @post
  @route("/search")
  search(@body query: LogQuery): CursorPage<LogRecord> | ValidationError | InternalServerError;

  @doc("Aggregate logs")
  @post
  @route("/aggregate")
  aggregate(@body request: LogAggregationRequest): LogAggregationResponse | ValidationError | InternalServerError;

  @doc("Get log patterns")
  @get
  @route("/patterns")
  getPatterns(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Minimum occurrences") minCount?: int32,
  ): LogPattern[] | InternalServerError;

  @doc("Get log statistics")
  @get
  @route("/stats")
  getStats(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): Qyl.OTel.Logs.LogStats | InternalServerError;
}

@doc("Log aggregation request")
model LogAggregationRequest {
  @doc("Query filters")
  query?: LogQuery;

  @doc("Aggregation specification")
  aggregation: LogAggregation;
}

@doc("Log aggregation response")
model LogAggregationResponse {
  @doc("Aggregation results")
  results: LogAggregationBucket[];

  @doc("Total matching logs")
  @encodedName("application/json", "total_count")
  totalCount: Count;
}

@doc("Log aggregation bucket")
model LogAggregationBucket {
  @doc("Bucket key (group by value)")
  key: string;

  @doc("Aggregated value")
  value: float64;

  @doc("Document count")
  count: Count;

  @doc("Timestamp (for time series)")
  timestamp?: utcDateTime;
}

// =============================================================================
// Metrics API
// =============================================================================

@route("/api/v1/metrics")
@tag("Metrics")
interface MetricsApi {
  @doc("List available metrics")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Metric name pattern") namePattern?: string,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<MetricMetadata> | InternalServerError;

  @doc("Query metric data points")
  @post
  @route("/query")
  query(@body request: MetricQueryRequest): MetricQueryResponse | ValidationError | InternalServerError;

  @doc("Get metric metadata")
  @get
  @route("/{metricName}")
  getMetadata(@path metricName: string): MetricMetadata | NotFoundError | InternalServerError;
}

@doc("Metric metadata")
model MetricMetadata {
  @doc("Metric name")
  name: string;

  @doc("Metric description")
  description?: string;

  @doc("Metric unit")
  unit?: string;

  @doc("Metric type")
  type: MetricType;

  @doc("Available label keys")
  @encodedName("application/json", "label_keys")
  labelKeys: string[];

  @doc("Services reporting this metric")
  services: string[];
}

@doc("Metric query request")
model MetricQueryRequest {
  @doc("Metric name")
  @encodedName("application/json", "metric_name")
  metricName: string;

  @doc("Label filters")
  filters?: Record<string>;

  @doc("Start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("End time")
  @encodedName("application/json", "end_time")
  endTime: utcDateTime;

  @doc("Step interval")
  step?: Qyl.Common.Pagination.TimeBucket;

  @doc("Aggregation function")
  aggregation?: Qyl.OTel.Metrics.AggregationFunction;

  @doc("Group by labels")
  @encodedName("application/json", "group_by")
  groupBy?: string[];
}

@doc("Metric query response")
model MetricQueryResponse {
  @doc("Metric name")
  @encodedName("application/json", "metric_name")
  metricName: string;

  @doc("Time series data")
  series: MetricTimeSeries[];
}

@doc("Metric time series")
model MetricTimeSeries {
  @doc("Labels")
  labels: Record<string>;

  @doc("Data points")
  points: MetricDataPoint[];
}

@doc("Metric data point")
model MetricDataPoint {
  @doc("Timestamp")
  timestamp: utcDateTime;

  @doc("Value")
  value: float64;
}

// =============================================================================
// Profiles API (v1development, Development stability)
// =============================================================================

@route("/api/v1/profiles")
@tag("Profiles")
interface ProfilesApi {
  @doc("List profiles with filtering")
  @get
  list(
    @query @doc("Session ID filter") sessionId?: SessionId,
    @query @doc("Trace ID filter") traceId?: TraceId,
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Sample type filter (e.g., cpu, alloc_objects, wall)") sampleType?: string,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
  ): ProfileRecord[] | ValidationError | InternalServerError;

  @doc("Get a specific profile by ID")
  @get
  @route("/{profileId}")
  get(@path profileId: string): ProfileRecord | NotFoundError | InternalServerError;

  @doc("Get profiles for a trace")
  @get
  @route("/by-trace/{traceId}")
  getByTrace(
    @path traceId: TraceId,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
  ): ProfileRecord[] | InternalServerError;

  @doc("Get profiles for a span")
  @get
  @route("/by-span/{spanId}")
  getBySpan(
    @path spanId: SpanId,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
  ): ProfileRecord[] | InternalServerError;
}

// =============================================================================
// Sessions API
// =============================================================================

@route("/api/v1/sessions")
@tag("Sessions")
interface SessionsApi {
  @doc("List sessions")
  @get
  list(
    @query @doc("User ID filter") userId?: UserId,
    @query @doc("Is active filter") isActive?: boolean,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<SessionEntity> | ValidationError | InternalServerError;

  @doc("Get session by ID")
  @get
  @route("/{sessionId}")
  get(@path sessionId: string): SessionEntity | NotFoundError | InternalServerError;

  @doc("Get session traces")
  @get
  @route("/{sessionId}/traces")
  getTraces(
    @path sessionId: string,
    @query @doc("Page size") limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<Trace> | NotFoundError | InternalServerError;

  @doc("Get session statistics")
  @get
  @route("/stats")
  getStats(
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): SessionStats | InternalServerError;
}

// =============================================================================
// Errors API
// =============================================================================

@route("/api/v1/errors")
@tag("Errors")
interface ErrorsApi {
  @doc("List error groups")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Status filter") status?: ErrorStatus,
    @query @doc("Category filter") category?: ErrorCategory,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ErrorEntity> | ValidationError | InternalServerError;

  @doc("Get error by ID")
  @get
  @route("/{errorId}")
  get(@path errorId: string): ErrorEntity | NotFoundError | InternalServerError;

  @doc("Update error status")
  @patch(#{ implicitOptionality: true })
  @route("/{errorId}")
  update(
    @path errorId: string,
    @body update: ErrorUpdate,
  ): ErrorEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Get error statistics")
  @get
  @route("/stats")
  getStats(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): ErrorStats | InternalServerError;

  @doc("Get error correlations")
  @get
  @route("/{errorId}/correlations")
  getCorrelations(@path errorId: string): ErrorCorrelation | NotFoundError | InternalServerError;
}

@doc("Error update request")
model ErrorUpdate {
  @doc("New status")
  status?: ErrorStatus;

  @doc("Assignee")
  @encodedName("application/json", "assigned_to")
  assignedTo?: string;

  @doc("Issue URL")
  @encodedName("application/json", "issue_url")
  issueUrl?: UrlString;
}

// =============================================================================
// Deployments API
// =============================================================================

@route("/api/v1/deployments")
@tag("Deployments")
interface DeploymentsApi {
  @doc("List deployments")
  @get
  list(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Environment filter") environment?: DeploymentEnvironment,
    @query @doc("Status filter") status?: DeploymentStatus,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<DeploymentEntity> | ValidationError | InternalServerError;

  @doc("Get deployment by ID")
  @get
  @route("/{deploymentId}")
  get(@path deploymentId: string): DeploymentEntity | NotFoundError | InternalServerError;

  @doc("Record new deployment")
  @post
  create(@body deployment: DeploymentCreate): DeploymentEntity | ValidationError | InternalServerError;

  @doc("Update deployment status")
  @patch(#{ implicitOptionality: true })
  @route("/{deploymentId}")
  update(
    @path deploymentId: string,
    @body update: DeploymentUpdate,
  ): DeploymentEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Get DORA metrics")
  @get
  @route("/metrics/dora")
  getDoraMetrics(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Environment filter") environment?: DeploymentEnvironment,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
  ): DoraMetrics | InternalServerError;
}

@doc("Deployment creation request")
model DeploymentCreate {
  @doc("Service name")
  @encodedName("application/json", "service_name")
  serviceName: string;

  @doc("Service version")
  @encodedName("application/json", "service_version")
  serviceVersion: SemVer;

  @doc("Environment")
  environment: DeploymentEnvironment;

  @doc("Strategy")
  strategy: DeploymentStrategy;

  @doc("Deployed by")
  @encodedName("application/json", "deployed_by")
  deployedBy?: string;

  @doc("Git commit SHA")
  @encodedName("application/json", "git_commit")
  gitCommit?: string;

  @doc("Git branch")
  @encodedName("application/json", "git_branch")
  gitBranch?: string;
}

@doc("Deployment update request")
model DeploymentUpdate {
  @doc("New status")
  status?: DeploymentStatus;

  @doc("Healthy replicas")
  @encodedName("application/json", "healthy_replicas")
  healthyReplicas?: int32;

  @doc("Error message")
  @encodedName("application/json", "error_message")
  errorMessage?: string;
}

@doc("DORA metrics response")
model DoraMetrics {
  @doc("Deployment frequency (per day)")
  @encodedName("application/json", "deployment_frequency")
  deploymentFrequency: float64;

  @doc("Lead time for changes (hours)")
  @encodedName("application/json", "lead_time_hours")
  leadTimeHours: float64;

  @doc("Change failure rate")
  @encodedName("application/json", "change_failure_rate")
  changeFailureRate: Ratio;

  @doc("Mean time to recovery (hours)")
  @encodedName("application/json", "mttr_hours")
  mttrHours: float64;

  @doc("Performance level")
  @encodedName("application/json", "performance_level")
  performanceLevel: DoraPerformanceLevel;
}

@doc("DORA performance levels")
enum DoraPerformanceLevel {
  @doc("Elite performer")
  elite: "elite",

  @doc("High performer")
  high: "high",

  @doc("Medium performer")
  medium: "medium",

  @doc("Low performer")
  low: "low",
}

// =============================================================================
// Services API
// =============================================================================

@route("/api/v1/services")
@tag("Services")
interface ServicesApi {
  @doc("List discovered services")
  @get
  list(
    @query @doc("Namespace filter") namespaceName?: string,
    @query @doc("Page size") @minValue(1) @maxValue(1000) limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ServiceInfo> | InternalServerError;

  @doc("Get service details")
  @get
  @route("/{serviceName}")
  get(@path serviceName: string): ServiceDetails | NotFoundError | InternalServerError;

  @doc("Get service dependencies")
  @get
  @route("/{serviceName}/dependencies")
  getDependencies(@path serviceName: string): ServiceDependency[] | NotFoundError | InternalServerError;

  @doc("Get service operations")
  @get
  @route("/{serviceName}/operations")
  getOperations(
    @path serviceName: string,
    @query @doc("Page size") limit?: int32 = 100,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<OperationInfo> | NotFoundError | InternalServerError;
}

@doc("Service information")
model ServiceInfo {
  @doc("Service name")
  name: string;

  @doc("Service namespace")
  @encodedName("application/json", "namespace_name")
  namespaceName?: string;

  @doc("Service version")
  version?: SemVer;

  @doc("Instance count")
  @encodedName("application/json", "instance_count")
  instanceCount: int32;

  @doc("Last seen")
  @encodedName("application/json", "last_seen")
  lastSeen: utcDateTime;
}

@doc("Service details")
model ServiceDetails {
  ...ServiceInfo;

  @doc("Resource attributes")
  @encodedName("application/json", "resource_attributes")
  resourceAttributes: Attribute[];

  @doc("Instrumentation libraries")
  @encodedName("application/json", "instrumentation_libraries")
  instrumentationLibraries: InstrumentationScope[];

  @doc("Request rate (per second)")
  @encodedName("application/json", "request_rate")
  requestRate: float64;

  @doc("Error rate")
  @encodedName("application/json", "error_rate")
  errorRate: Ratio;

  @doc("Average latency in milliseconds")
  @encodedName("application/json", "avg_latency_ms")
  avgLatencyMs: float64;

  @doc("P99 latency in milliseconds")
  @encodedName("application/json", "p99_latency_ms")
  p99LatencyMs: float64;
}

@doc("Operation information")
model OperationInfo {
  @doc("Operation name")
  name: string;

  @doc("Span kind")
  @encodedName("application/json", "span_kind")
  spanKind: SpanKind;

  @doc("Request count")
  @encodedName("application/json", "request_count")
  requestCount: Count;

  @doc("Error count")
  @encodedName("application/json", "error_count")
  errorCount: Count;

  @doc("Average duration in milliseconds")
  @encodedName("application/json", "avg_duration_ms")
  avgDurationMs: float64;

  @doc("P99 duration in milliseconds")
  @encodedName("application/json", "p99_duration_ms")
  p99DurationMs: float64;
}

// =============================================================================
// Health API — Aspire-shape probes. /alive is cheap liveness (live-tagged checks),
// /health runs all ready-tagged checks. Collector exposes a rich /health/ui DTO for
// the dashboard, modelled separately as HealthUiResponse.
// =============================================================================

@tag("Health")
interface HealthApi {
  @doc("Liveness probe — runs live-tagged health checks.")
  @route("/alive")
  @get
  @operationId("health_alive")
  alive(): {
    @statusCode statusCode: 200;
  } | {
    @statusCode statusCode: 503;
  };

  @doc("Readiness probe — runs all ready-tagged health checks.")
  @route("/health")
  @get
  @operationId("health_ready")
  ready(): {
    @statusCode statusCode: 200;
  } | {
    @statusCode statusCode: 503;
  };
}

// =============================================================================
// Workspace API
// =============================================================================

@route("/api/v1/workspaces")
@tag("Workspaces")
interface WorkspacesApi {
  @doc("Get current workspace envelope")
  @get
  @route("/current")
  getCurrent(): WorkspaceEnvelopeEntity | NotFoundError | InternalServerError;

  @doc("Register workspace heartbeat")
  @post
  @route("/current/heartbeat")
  heartbeat(): WorkspaceEnvelopeEntity | NotFoundError | InternalServerError;

  @doc("List projects")
  @get
  @route("/projects")
  listProjects(
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ProjectEntity> | InternalServerError;

  @doc("Get project by ID")
  @get
  @route("/projects/{projectId}")
  getProject(@path projectId: string): ProjectEntity | NotFoundError | InternalServerError;

  @doc("Create a new project")
  @post
  @route("/projects")
  createProject(@body project: ProjectCreateRequest): ProjectEntity | ValidationError | InternalServerError;

  @doc("List environments for a project")
  @get
  @route("/projects/{projectId}/environments")
  listEnvironments(@path projectId: string): ProjectEnvironmentEntity[] | NotFoundError | InternalServerError;
}

@doc("Project creation request")
model ProjectCreateRequest {
  @doc("Project name")
  name: string;

  @doc("Project slug (URL-safe)")
  slug: string;

  @doc("Project description")
  description?: string;
}

// =============================================================================
// Onboarding API
// =============================================================================

@route("/api/v1/onboarding")
@tag("Onboarding")
interface OnboardingApi {
  @doc("Start a new handshake session")
  @post
  @route("/handshake")
  startHandshake(@body request: HandshakeStartRequest): HandshakeSessionEntity | ValidationError | InternalServerError;

  @doc("Complete handshake verification")
  @post
  @route("/handshake/{sessionId}/verify")
  verifyHandshake(
    @path sessionId: string,
    @body request: HandshakeVerifyRequest,
  ): HandshakeVerifyResponse | NotFoundError | ValidationError | InternalServerError;

  @doc("Get handshake session status")
  @get
  @route("/handshake/{sessionId}")
  getHandshake(@path sessionId: string): HandshakeSessionEntity | NotFoundError | InternalServerError;
}

@doc("Handshake start request")
model HandshakeStartRequest {
  @doc("PKCE code challenge")
  @encodedName("application/json", "code_challenge")
  codeChallenge: string;

  @doc("Client identifier")
  @encodedName("application/json", "client_id")
  clientId: string;
}

@doc("Handshake verification request")
model HandshakeVerifyRequest {
  @doc("PKCE code verifier")
  @encodedName("application/json", "code_verifier")
  codeVerifier: string;

  @doc("Authorization code")
  code: string;
}

@doc("Handshake verification response")
model HandshakeVerifyResponse {
  @doc("Access token")
  @encodedName("application/json", "access_token")
  accessToken: string;

  @doc("Token expiration")
  @encodedName("application/json", "expires_at")
  expiresAt: utcDateTime;

  @doc("Workspace ID")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;
}

// =============================================================================
// Configurator API
// =============================================================================

@route("/api/v1/configurator")
@tag("Configurator")
interface ConfiguratorApi {
  @doc("List generation profiles")
  @get
  @route("/profiles")
  listProfiles(
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<GenerationProfileEntity> | InternalServerError;

  @doc("Get generation profile by ID")
  @get
  @route("/profiles/{profileId}")
  getProfile(@path profileId: string): GenerationProfileEntity | NotFoundError | InternalServerError;

  @doc("Create generation profile")
  @post
  @route("/profiles")
  createProfile(@body profile: GenerationProfileCreateRequest): GenerationProfileEntity | ValidationError | InternalServerError;

  @doc("Get generation selections for a workspace")
  @get
  @route("/selections")
  getSelections(
    @query @doc("Workspace ID") workspaceId: string,
  ): GenerationSelectionEntity[] | InternalServerError;

  @doc("Save generation selections")
  @post
  @route("/selections")
  saveSelections(@body selections: GenerationSelectionSaveRequest): GenerationSelectionEntity | ValidationError | InternalServerError;

  @doc("Create a generation job")
  @post
  @route("/jobs")
  createJob(@body job: GenerationJobCreateRequest): GenerationJobEntity | ValidationError | InternalServerError;

  @doc("Get generation job status")
  @get
  @route("/jobs/{jobId}")
  getJob(@path jobId: string): GenerationJobEntity | NotFoundError | InternalServerError;
}

@doc("Generation profile creation request")
model GenerationProfileCreateRequest {
  @doc("Profile name")
  name: string;

  @doc("Target framework")
  @encodedName("application/json", "target_framework")
  targetFramework: string;

  @doc("Profile description")
  description?: string;

  @doc("Feature flags")
  @encodedName("application/json", "features_json")
  featuresJson?: string;
}

@doc("Save generation selections request")
model GenerationSelectionSaveRequest {
  @doc("Workspace ID")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Profile ID")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Selected semconv keys")
  @encodedName("application/json", "selected_keys_json")
  selectedKeysJson: string;
}

@doc("Generation job creation request")
model GenerationJobCreateRequest {
  @doc("Workspace ID")
  @encodedName("application/json", "workspace_id")
  workspaceId: string;

  @doc("Profile ID")
  @encodedName("application/json", "profile_id")
  profileId: string;

  @doc("Job type")
  @encodedName("application/json", "job_type")
  jobType: GenerationJobType;
}

// =============================================================================
// Issues API (Sentry-style error grouping)
// =============================================================================

@route("/api/v1/issues")
@tag("Issues")
interface IssuesApi {
  @doc("List error issues")
  @get
  list(
    @query @doc("Project ID filter") projectId?: string,
    @query @doc("Status filter") status?: IssueStatus,
    @query @doc("Priority filter") priority?: IssuePriority,
    @query @doc("Level filter") level?: IssueLevel,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ErrorIssueEntity> | ValidationError | InternalServerError;

  @doc("Get issue by ID")
  @get
  @route("/{issueId}")
  get(@path issueId: string): ErrorIssueEntity | NotFoundError | InternalServerError;

  @doc("Update issue status")
  @patch(#{ implicitOptionality: true })
  @route("/{issueId}")
  update(
    @path issueId: string,
    @body update: IssueUpdateRequest,
  ): ErrorIssueEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Get issue events")
  @get
  @route("/{issueId}/events")
  getEvents(
    @path issueId: string,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<ErrorIssueEventEntity> | NotFoundError | InternalServerError;

  @doc("Get issue breadcrumbs")
  @get
  @route("/{issueId}/breadcrumbs")
  getBreadcrumbs(
    @path issueId: string,
    @query @doc("Page size") @minValue(1) @maxValue(50) limit?: int32 = 20,
  ): ErrorBreadcrumbEntity[] | NotFoundError | InternalServerError;
}

@doc("Issue update request")
model IssueUpdateRequest {
  @doc("New status")
  status?: IssueStatus;

  @doc("New priority")
  priority?: IssuePriority;

  @doc("Assignee")
  @encodedName("application/json", "assigned_to")
  assignedTo?: string;
}

// =============================================================================
// Workflow API
// =============================================================================

@route("/api/v1/workflows")
@tag("Workflows")
interface WorkflowsApi {
  @doc("List workflow runs")
  @get
  @route("/runs")
  listRuns(
    @query @doc("Project ID filter") projectId?: string,
    @query @doc("Workflow ID filter") workflowId?: string,
    @query @doc("Status filter") status?: WorkflowRunStatus,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<WorkflowRunEntity> | ValidationError | InternalServerError;

  @doc("Get workflow run by ID")
  @get
  @route("/runs/{runId}")
  getRun(@path runId: string): WorkflowRunEntity | NotFoundError | InternalServerError;

  @doc("Get workflow run nodes")
  @get
  @route("/runs/{runId}/nodes")
  getRunNodes(
    @path runId: string,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 50,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<WorkflowNodeEntity> | NotFoundError | InternalServerError;

  @doc("Get workflow events")
  @get
  @route("/runs/{runId}/events")
  getRunEvents(
    @path runId: string,
    @query @doc("After sequence number") afterSequence?: int64,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 50,
  ): WorkflowEventEntity[] | NotFoundError | InternalServerError;

  @doc("Resume a paused workflow")
  @post
  @route("/runs/{runId}/resume")
  resumeRun(@path runId: string): WorkflowRunEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Approve a pending workflow step")
  @post
  @route("/runs/{runId}/nodes/{nodeId}/approve")
  approveStep(
    @path runId: string,
    @path nodeId: string,
  ): WorkflowNodeEntity | NotFoundError | ValidationError | InternalServerError;
}

// =============================================================================
// Search API
// =============================================================================

@route("/api/v1/search")
@tag("Search")
interface SearchApi {
  @doc("Unified search across all entity types")
  @post
  search(@body request: SearchRequest): SearchResponse | ValidationError | InternalServerError;

  @doc("Get search suggestions")
  @get
  @route("/suggestions")
  getSuggestions(
    @query @doc("Query prefix") query: string,
    @query @doc("Maximum suggestions") @minValue(1) @maxValue(20) limit?: int32 = 5,
  ): string[] | InternalServerError;
}

// =============================================================================
// Alerts API
// =============================================================================

@route("/api/v1/alerts")
@tag("Alerts")
interface AlertsApi {
  @doc("List alert rules")
  @get
  @route("/rules")
  listRules(
    @query @doc("Project ID filter") projectId?: string,
    @query @doc("Enabled filter") enabled?: boolean,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<AlertRuleEntity> | InternalServerError;

  @doc("Get alert rule by ID")
  @get
  @route("/rules/{ruleId}")
  getRule(@path ruleId: string): AlertRuleEntity | NotFoundError | InternalServerError;

  @doc("Create a new alert rule")
  @post
  @route("/rules")
  createRule(@body rule: AlertRuleEntity): AlertRuleEntity | ValidationError | InternalServerError;

  @doc("Update an existing alert rule")
  @put
  @route("/rules/{ruleId}")
  updateRule(
    @path ruleId: string,
    @body rule: AlertRuleEntity,
  ): AlertRuleEntity | NotFoundError | ValidationError | InternalServerError;

  @doc("Delete an alert rule")
  @delete
  @route("/rules/{ruleId}")
  deleteRule(@path ruleId: string): NoContentResponse | NotFoundError | InternalServerError;

  @doc("List alert firings")
  @get
  @route("/firings")
  listFirings(
    @query @doc("Rule ID filter") ruleId?: string,
    @query @doc("Status filter") status?: AlertFiringStatus,
    @query @doc("Start time") startTime?: utcDateTime,
    @query @doc("End time") endTime?: utcDateTime,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<AlertFiringEntity> | ValidationError | InternalServerError;

  @doc("Acknowledge an alert firing")
  @post
  @route("/firings/{firingId}/acknowledge")
  acknowledgeFiring(
    @path firingId: string,
    @body acknowledgement: AlertFiringAcknowledgement,
  ): AlertFiringEntity | NotFoundError | InternalServerError;

  @doc("Resolve an alert firing")
  @post
  @route("/firings/{firingId}/resolve")
  resolveFiring(@path firingId: string): AlertFiringEntity | NotFoundError | InternalServerError;

  @doc("List fix runs")
  @get
  @route("/fixes")
  listFixRuns(
    @query @doc("Issue ID filter") issueId?: string,
    @query @doc("Status filter") status?: FixRunStatus,
    @query @doc("Page size") @minValue(1) @maxValue(100) limit?: int32 = 20,
    @query @doc("Cursor") cursor?: string,
  ): CursorPage<FixRunEntity> | ValidationError | InternalServerError;

  @doc("Get fix run by ID")
  @get
  @route("/fixes/{fixId}")
  getFixRun(@path fixId: string): FixRunEntity | NotFoundError | InternalServerError;
}

@doc("Acknowledge request body for an alert firing")
model AlertFiringAcknowledgement {
  @doc("User or service that acknowledges the firing")
  @encodedName("application/json", "acknowledged_by")
  acknowledgedBy: string;
}


// ===== Flattened from: core/specs/api/streaming.tsp =====
// =============================================================================
// QYL v2.0 - Streaming API Routes
// =============================================================================
// Server-Sent Events (SSE) streaming endpoints for real-time telemetry.
// =============================================================================





namespace Qyl.Api.Streaming;

// =============================================================================
// Stream Event Types
// =============================================================================

@doc("Trace stream event")
model TraceStreamEvent {
  @doc("Event type")
  type: "trace";

  @doc("Trace data")
  data: Trace;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Span stream event")
model SpanStreamEvent {
  @doc("Event type")
  type: "span";

  @doc("Span data")
  data: Span;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Log stream event")
model LogStreamEvent {
  @doc("Event type")
  type: "log";

  @doc("Log data")
  data: LogRecord;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Metric stream event")
model MetricStreamEvent {
  @doc("Event type")
  type: "metric";

  @doc("Metric data")
  data: Metric;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Deployment stream event")
model DeploymentStreamEvent {
  @doc("Event type")
  type: "deployment";

  @doc("Deployment event data")
  data: DeploymentEvent;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("Heartbeat event for connection keep-alive")
model HeartbeatEvent {
  @doc("Event type")
  type: "heartbeat";

  @doc("Server timestamp")
  timestamp: utcDateTime;
}

// =============================================================================
// Combined Stream Event Union
// =============================================================================

@doc("All possible stream event types")
@events
union TelemetryStreamEvent {
  trace: TraceStreamEvent,
  span: SpanStreamEvent,
  log: LogStreamEvent,
  metric: MetricStreamEvent,
  deployment: DeploymentStreamEvent,
  heartbeat: HeartbeatEvent,
}

@doc("Trace stream events")
@events
union TraceEvents {
  trace: TraceStreamEvent,
  heartbeat: HeartbeatEvent,
}

@doc("Span stream events")
@events
union SpanEvents {
  span: SpanStreamEvent,
  heartbeat: HeartbeatEvent,
}

@doc("Log stream events")
@events
union LogEvents {
  log: LogStreamEvent,
  heartbeat: HeartbeatEvent,
}

@doc("Metric stream events")
@events
union MetricEvents {
  metric: MetricStreamEvent,
  heartbeat: HeartbeatEvent,
}

@doc("Deployment stream events")
@events
union DeploymentEvents {
  deployment: DeploymentStreamEvent,
  heartbeat: HeartbeatEvent,
}

// =============================================================================
// Stream Subscription Models
// =============================================================================

@doc("Stream subscription request")
model StreamSubscription {
  @doc("Event types to subscribe to")
  @encodedName("application/json", "event_types")
  eventTypes: StreamEventType[];

  @doc("Service name filter")
  @encodedName("application/json", "service_name")
  serviceName?: string;

  @doc("Trace ID filter (for specific trace)")
  @encodedName("application/json", "trace_id")
  traceId?: TraceId;

  @doc("Minimum severity for logs (1-24)")
  @encodedName("application/json", "min_severity")
  @minValue(1)
  @maxValue(24)
  minSeverity?: int32;

  @doc("Attribute filters")
  filters?: Record<string>;

  @doc("Sample rate (0.0-1.0)")
  @encodedName("application/json", "sample_rate")
  @minValue(0)
  @maxValue(1)
  sampleRate?: float64;
}

@doc("Stream event types")
enum StreamEventType {
  @doc("Trace events")
  traces: "traces",

  @doc("Span events")
  spans: "spans",

  @doc("Log events")
  logs: "logs",

  @doc("Metric events")
  metrics: "metrics",

  @doc("Deployment events")
  deployments: "deployments",

  @doc("All events")
  all: "all",
}

// =============================================================================
// Streaming API Interfaces
// =============================================================================

@route("/api/v1/stream")
@tag("Streaming")
interface StreamingApi {
  @doc("Stream all telemetry events (SSE)")
  @get
  @route("/events")
  streamEvents(
    @query @doc("Event types") types?: StreamEventType[],
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Sample rate") sampleRate?: float64,
  ): SSEStream<TelemetryStreamEvent>;

  @doc("Stream traces in real-time")
  @get
  @route("/traces")
  streamTraces(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Minimum duration filter") minDurationMs?: int64,
  ): SSEStream<TraceEvents>;

  @doc("Stream spans for a specific trace")
  @get
  @route("/traces/{traceId}/spans")
  streamTraceSpans(@path traceId: TraceId): SSEStream<SpanEvents>;

  @doc("Stream logs in real-time")
  @get
  @route("/logs")
  streamLogs(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Minimum severity") minSeverity?: int32,
    @query @doc("Query filter") query?: string,
  ): SSEStream<LogEvents>;

  @doc("Stream metrics in real-time")
  @get
  @route("/metrics")
  streamMetrics(
    @query @doc("Metric name filter") metricName?: string,
    @query @doc("Service name filter") serviceName?: string,
  ): SSEStream<MetricEvents>;

  @doc("Stream deployment events")
  @get
  @route("/deployments")
  streamDeployments(
    @query @doc("Service name filter") serviceName?: string,
    @query @doc("Environment filter") environment?: string,
  ): SSEStream<DeploymentEvents>;
}

// =============================================================================
// WebSocket Streaming (Alternative)
// =============================================================================

@doc("WebSocket message wrapper")
model WebSocketMessage<T> {
  @doc("Message ID")
  id: string;

  @doc("Message type")
  type: WebSocketMessageType;

  @doc("Message payload")
  payload?: T;

  @doc("Timestamp")
  timestamp: utcDateTime;
}

@doc("WebSocket message types")
enum WebSocketMessageType {
  @doc("Subscribe to events")
  subscribe: "subscribe",

  @doc("Unsubscribe from events")
  unsubscribe: "unsubscribe",

  @doc("Data event")
  data: "data",

  @doc("Error message")
  error: "error",

  @doc("Acknowledgment")
  ack: "ack",

  @doc("Ping")
  ping: "ping",

  @doc("Pong")
  pong: "pong",
}

// =============================================================================
// Tail-based Sampling Configuration
// =============================================================================

@doc("Tail-based sampling configuration for streaming")
model TailSamplingConfig {
  @doc("Enable tail-based sampling")
  enabled: boolean;

  @doc("Sample error traces")
  @encodedName("application/json", "sample_errors")
  sampleErrors: boolean;

  @doc("Sample slow traces (above threshold)")
  @encodedName("application/json", "sample_slow")
  sampleSlow: boolean;

  @doc("Slow trace threshold in milliseconds")
  @encodedName("application/json", "slow_threshold_ms")
  slowThresholdMs?: int64;

  @doc("Random sample rate for remaining traces")
  @encodedName("application/json", "random_rate")
  @minValue(0)
  @maxValue(1)
  randomRate: float64;
}


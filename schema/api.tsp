// =============================================================================
// QYL God Schema - API Routes
// =============================================================================
// HTTP endpoints for the QYL Collector REST API.
// =============================================================================

import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";

import "./primitives.tsp";
import "./models.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.OpenAPI;
using Qyl.Primitives;
using Qyl.Models;

namespace Qyl.Api;

// =============================================================================
// Common Response Types
// =============================================================================

@doc("Paginated list response")
model PagedResponse<T> {
  @doc("Items in current page")
  items: T[];

  @doc("Total count across all pages")
  total: int64;

  @doc("Current page offset")
  offset: int64;

  @doc("Page size limit")
  limit: int64;

  @doc("Whether more items exist")
  hasMore: boolean;
}

@doc("Error response")
@error
model ApiError {
  @doc("Error code")
  code: string;

  @doc("Error message")
  message: string;

  @doc("Additional details")
  details?: string;
}

// =============================================================================
// Query Parameters
// =============================================================================

@doc("Common pagination parameters")
model PaginationParams {
  @query
  @doc("Number of items to skip")
  offset?: int64 = 0;

  @query
  @doc("Maximum items to return")
  limit?: int64 = 100;
}

@doc("Time range filter parameters")
model TimeRangeParams {
  @query
  @doc("Start time (Unix nanoseconds)")
  since?: int64;

  @query
  @doc("End time (Unix nanoseconds)")
  until?: int64;
}

// =============================================================================
// Sessions API
// =============================================================================

@route("/api/v1/sessions")
@tag("Sessions")
interface Sessions {
  @doc("List all sessions with optional filters")
  @get
  list(
    ...PaginationParams,
    ...TimeRangeParams,

    @query
    @doc("Filter by service name")
    serviceName?: string,

    @query
    @doc("Filter by GenAI provider")
    genAiSystem?: string,
  ): PagedResponse<SessionSummary> | ApiError;

  @doc("Get session by ID")
  @get
  @route("{sessionId}")
  get(@path sessionId: SessionId): SessionSummary | ApiError;

  @doc("Get all spans for a session")
  @get
  @route("{sessionId}/spans")
  getSpans(
    @path sessionId: SessionId,
    ...PaginationParams,
  ): PagedResponse<SpanRecord> | ApiError;
}

// =============================================================================
// Traces API
// =============================================================================

@route("/api/v1/traces")
@tag("Traces")
interface Traces {
  @doc("Get trace tree by trace ID")
  @get
  @route("{traceId}")
  get(@path traceId: TraceId): TraceNode | ApiError;

  @doc("Get all spans for a trace")
  @get
  @route("{traceId}/spans")
  getSpans(@path traceId: TraceId): SpanRecord[] | ApiError;
}

// =============================================================================
// Spans API
// =============================================================================

@route("/api/v1/spans")
@tag("Spans")
interface Spans {
  @doc("Query spans with filters")
  @get
  list(
    ...PaginationParams,
    ...TimeRangeParams,

    @query
    @doc("Filter by trace ID")
    traceId?: string,

    @query
    @doc("Filter by service name")
    serviceName?: string,

    @query
    @doc("Filter by span name (supports wildcards)")
    name?: string,

    @query
    @doc("Filter by status (ok, error)")
    status?: string,

    @query
    @doc("Filter by GenAI provider")
    genAiSystem?: string,

    @query
    @doc("Filter by model")
    genAiModel?: string,

    @query
    @doc("Minimum duration in nanoseconds")
    minDurationNs?: int64,
  ): PagedResponse<SpanRecord> | ApiError;

  @doc("Get span by ID")
  @get
  @route("{spanId}")
  get(@path spanId: SpanId): SpanRecord | ApiError;
}

// =============================================================================
// Live Streaming API
// =============================================================================

@route("/api/v1/live")
@tag("Streaming")
interface Live {
  @doc("Server-Sent Events stream for real-time span updates")
  @get
  stream(
    @query
    @doc("Filter by session ID")
    sessionId?: string,

    @query
    @doc("Filter by service name")
    serviceName?: string,
  ): {
    @header contentType: "text/event-stream";
    @body body: string;
  };
}

// =============================================================================
// Health API
// =============================================================================

@route("/health")
@tag("Health")
interface Health {
  @doc("Health check endpoint")
  @get
  check(): {
    @statusCode statusCode: 200;
    @body body: {
      status: "healthy";
      version: string;
    };
  };
}

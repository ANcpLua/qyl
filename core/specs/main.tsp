// =============================================================================
// QYL Collector API - TypeSpec Definition
// Single Source of Truth for API Types
// =============================================================================

import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/openapi3";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.OpenAPI;

@service
@info(#{
  title: "QYL Collector API",
  version: "1.0.0",
})
@server("http://localhost:5100", "QYL Collector")
namespace Qyl;

// =============================================================================
// Primitives
// =============================================================================

/** 32-character hex trace ID (OTel W3C format) */
scalar TraceId extends string;

/** 16-character hex span ID */
scalar SpanId extends string;

/** Session identifier (GUID format) */
scalar SessionId extends string;

/** ISO 8601 timestamp */
scalar Timestamp extends string;

// =============================================================================
// Enums
// =============================================================================

/** OpenTelemetry span kind */
enum SpanKind {
  internal: "internal",
  server: "server",
  client: "client",
  producer: "producer",
  consumer: "consumer",
}

/** OpenTelemetry span status */
enum SpanStatus {
  unset: "unset",
  ok: "ok",
  error: "error",
}

/** Console log level */
enum ConsoleLevel {
  debug: "debug",
  log: "log",
  info: "info",
  warn: "warn",
  error: "error",
}

// =============================================================================
// Core Models (camelCase to match C# JSON serialization)
// =============================================================================

/** Key-value attributes map */
model Attributes {
  ...Record<unknown>;
}

/** Span event (log attached to span) */
model SpanEvent {
  name: string;
  timestamp: Timestamp;
  attributes?: Attributes;
}

/** Span link (cross-trace reference) */
model SpanLink {
  traceId: TraceId;
  spanId: SpanId;
  attributes?: Attributes;
}

/** GenAI-specific span data (OTel semconv 1.38) */
model GenAiSpanData {
  providerName?: string;
  operationName?: string;
  requestModel?: string;
  responseModel?: string;
  inputTokens?: int64;
  outputTokens?: int64;
  totalTokens?: int64;
  costUsd?: float64;
  temperature?: float64;
  maxTokens?: int32;
  finishReason?: string;
  toolName?: string;
  toolCallId?: string;
}

/** OpenTelemetry span */
model Span {
  traceId: TraceId;
  spanId: SpanId;
  parentSpanId?: SpanId;
  sessionId?: SessionId;
  name: string;
  kind: SpanKind;
  status: SpanStatus;
  statusMessage?: string;
  startTime: Timestamp;
  endTime: Timestamp;
  durationMs: float64;
  serviceName: string;
  serviceVersion?: string;
  attributes?: Attributes;
  events?: SpanEvent[];
  links?: SpanLink[];
  /** GenAI-specific data if this is an AI span */
  genai?: GenAiSpanData;
}

/** GenAI statistics aggregated at session level */
model SessionGenAiStats {
  totalInputTokens: int64;
  totalOutputTokens: int64;
  totalTokens: int64;
  totalCostUsd: float64;
  requestCount: int64;
  toolCallCount: int64;
  models: string[];
  providers: string[];
  primaryModel?: string;
}

/** AI conversation session with aggregated metrics */
model Session {
  sessionId: SessionId;
  startTime: Timestamp;
  lastActivity: Timestamp;
  durationMs: float64;
  spanCount: int64;
  traceCount: int64;
  errorCount: int64;
  errorRate: float64;
  services: string[];
  traceIds: string[];
  isActive?: boolean;
  genaiStats: SessionGenAiStats;
  attributes?: Attributes;
}

// =============================================================================
// Request/Response Models
// =============================================================================

/** Login request */
model LoginRequest {
  token: string;
}

/** Login response */
model LoginResponse {
  success: boolean;
  error?: string;
}

/** Session list response */
model SessionListResponse {
  sessions: Session[];
  total: int32;
  hasMore: boolean;
}

/** Span list response */
model SpanListResponse {
  spans: Span[];
}

/** Trace response with hierarchy */
model TraceResponse {
  traceId?: TraceId;
  spans: Span[];
  rootSpan?: Span;
  durationMs?: float64;
  status?: string;
}

/** Span batch for ingestion */
model SpanBatch {
  spans: Span[];
}

/** Console log entry */
model ConsoleLogEntry {
  level?: ConsoleLevel;
  message?: string;
  sessionId?: string;
  url?: string;
  stack?: string;
  timestamp: Timestamp;
}

/** Console log ingest request */
model ConsoleIngestRequest {
  level?: string;
  message?: string;
  sessionId?: string;
  url?: string;
  stack?: string;
}

/** Console log batch */
model ConsoleIngestBatch {
  logs: ConsoleIngestRequest[];
}

/** MCP tool definition */
model McpTool {
  name: string;
  description: string;
  inputSchema?: unknown;
}

/** MCP manifest */
model McpManifest {
  name: string;
  version: string;
  description: string;
  tools: McpTool[];
}

/** MCP tool call */
model McpToolCall {
  name: string;
  arguments?: unknown;
}

/** MCP content */
model McpContent {
  type: string;
  text?: string;
}

/** MCP response */
model McpResponse {
  content?: McpContent;
  error?: string;
  isError: boolean;
}

/** Storage statistics */
model StorageStats {
  spanCount: int64;
  sessionCount: int64;
  feedbackCount: int64;
  oldestSpan?: Timestamp;
  newestSpan?: Timestamp;
}

/** Error response */
model ErrorResponse {
  error: string;
  message?: string;
}

/** Health response */
model HealthResponse {
  status: string;
}

/** Auth check response */
model AuthCheckResponse {
  authenticated: boolean;
}

/** Feedback response (placeholder) */
model FeedbackResponse {
  feedback: unknown[];
}

// =============================================================================
// SSE Event Models
// =============================================================================

/** SSE connected event */
model SseConnectedEvent {
  connectionId: string;
}

/** SSE error data */
model SseErrorData {
  errorType: string;
  message: string;
}

// =============================================================================
// API Routes
// =============================================================================

@route("/api")
namespace Api {
  // ─────────────────────────────────────────────────────────────────────────
  // Authentication
  // ─────────────────────────────────────────────────────────────────────────

  @route("/login")
  @post
  @tag("Auth")
  op login(@body request: LoginRequest): LoginResponse;

  @route("/logout")
  @post
  @tag("Auth")
  op logout(): { success: boolean };

  @route("/auth/check")
  @get
  @tag("Auth")
  op authCheck(): AuthCheckResponse;

  // ─────────────────────────────────────────────────────────────────────────
  // Sessions
  // ─────────────────────────────────────────────────────────────────────────

  @route("/v1/sessions")
  @get
  @tag("Sessions")
  op listSessions(
    @query limit?: int32,
    @query serviceName?: string,
  ): SessionListResponse;

  @route("/v1/sessions/{sessionId}")
  @get
  @tag("Sessions")
  op getSession(@path sessionId: SessionId): Session | NotFoundResponse;

  @route("/v1/sessions/{sessionId}/spans")
  @get
  @tag("Sessions")
  op getSessionSpans(@path sessionId: SessionId): SpanListResponse;

  @route("/v1/sessions/{sessionId}/feedback")
  @get
  @tag("Sessions")
  op getSessionFeedback(@path sessionId: SessionId): FeedbackResponse;

  // ─────────────────────────────────────────────────────────────────────────
  // Traces
  // ─────────────────────────────────────────────────────────────────────────

  @route("/v1/traces/{traceId}")
  @get
  @tag("Traces")
  op getTrace(@path traceId: TraceId): TraceResponse | NotFoundResponse;

  // ─────────────────────────────────────────────────────────────────────────
  // Ingestion
  // ─────────────────────────────────────────────────────────────────────────

  @route("/v1/ingest")
  @post
  @tag("Ingestion")
  op ingestSpans(@body batch: SpanBatch): AcceptedResponse | ErrorResponse;

  @route("/v1/feedback")
  @post
  @tag("Ingestion")
  op submitFeedback(): AcceptedResponse;

  // ─────────────────────────────────────────────────────────────────────────
  // Console Logs
  // ─────────────────────────────────────────────────────────────────────────

  @route("/v1/console")
  @post
  @tag("Console")
  op ingestConsoleLogs(@body batch: ConsoleIngestBatch): AcceptedResponse;

  @route("/v1/console")
  @get
  @tag("Console")
  op getConsoleLogs(
    @query session?: string,
    @query level?: string,
    @query limit?: int32,
  ): ConsoleLogEntry[];

  @route("/v1/console/errors")
  @get
  @tag("Console")
  op getConsoleErrors(@query limit?: int32): ConsoleLogEntry[];

  // ─────────────────────────────────────────────────────────────────────────
  // SSE Live Streams
  // ─────────────────────────────────────────────────────────────────────────

  @route("/v1/live")
  @get
  @tag("Streaming")
  op liveStream(@query session?: string): SseConnectedEvent;

  @route("/v1/console/live")
  @get
  @tag("Streaming")
  op liveConsole(@query session?: string): SseConnectedEvent;
}

// ─────────────────────────────────────────────────────────────────────────────
// MCP Endpoints
// ─────────────────────────────────────────────────────────────────────────────

@route("/mcp")
namespace Mcp {
  @route("/manifest")
  @get
  @tag("MCP")
  op getManifest(): McpManifest;

  @route("/tools/call")
  @post
  @tag("MCP")
  op callTool(@body call: McpToolCall): McpResponse;
}

// ─────────────────────────────────────────────────────────────────────────────
// OTLP Endpoint
// ─────────────────────────────────────────────────────────────────────────────

@route("/v1/traces")
@post
@tag("OTLP")
op otlpIngest(@body data: unknown): AcceptedResponse | ErrorResponse;

// ─────────────────────────────────────────────────────────────────────────────
// Health Endpoints
// ─────────────────────────────────────────────────────────────────────────────

@route("/health")
@get
@tag("Health")
op health(): HealthResponse;

@route("/ready")
@get
@tag("Health")
op ready(): HealthResponse;

// =============================================================================
// Standard Response Models
// =============================================================================

model NotFoundResponse {
  @statusCode statusCode: 404;
}

model AcceptedResponse {
  @statusCode statusCode: 202;
}

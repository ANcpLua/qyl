// =============================================================================
// QYL v2.0 - Storage Models
// =============================================================================
// Flattened storage models with DuckDB table generation extensions.
// These are optimized for query performance, not wire format fidelity.
// =============================================================================

import "@typespec/openapi";
import "@typespec/json-schema";

import "../common/types.tsp";
import "./enums.tsp";

using TypeSpec.OpenAPI;
using TypeSpec.JsonSchema;
using Qyl.Common;
using Qyl.OTel.Enums;

namespace Qyl.Storage;

// =============================================================================
// SpanRecord - Primary Storage Model
// =============================================================================

@jsonSchema
@doc("OpenTelemetry span record for storage and query")
@extension("x-duckdb-table", "spans")
@extension("x-csharp-type", "SpanRecord")
model SpanRecord {
  // === Identifiers (Primary Key) ===

  @doc("Unique span identifier")
  @key
  @extension("x-duckdb-column", "span_id")
  @extension("x-duckdb-primary-key", true)
  spanId: SpanId;

  @doc("Trace identifier")
  @extension("x-duckdb-column", "trace_id")
  @extension("x-duckdb-index", "idx_spans_trace_id")
  traceId: TraceId;

  @doc("Parent span identifier (null for root spans)")
  @extension("x-duckdb-column", "parent_span_id")
  parentSpanId?: SpanId;

  // === Session Grouping ===

  @doc("Session identifier for grouping related traces")
  @extension("x-duckdb-column", "session_id")
  @extension("x-duckdb-index", "idx_spans_session_id")
  @extension("x-promoted", true)
  sessionId?: SessionId;

  // === Core Span Fields ===

  @doc("Human-readable span name")
  @minLength(1)
  @extension("x-duckdb-column", "name")
  name: string;

  @doc("Span kind")
  @extension("x-duckdb-column", "kind")
  kind: SpanKind;

  @doc("Start timestamp in nanoseconds since epoch")
  @extension("x-duckdb-column", "start_time_unix_nano")
  @extension("x-duckdb-index", "idx_spans_start_time")
  startTimeUnixNano: int64;

  @doc("End timestamp in nanoseconds since epoch")
  @extension("x-duckdb-column", "end_time_unix_nano")
  endTimeUnixNano: int64;

  @doc("Duration in nanoseconds")
  @extension("x-duckdb-column", "duration_ns")
  durationNs: DurationNs;

  // === Status ===

  @doc("Span status code")
  @extension("x-duckdb-column", "status_code")
  statusCode: SpanStatusCode;

  @doc("Status message (only for ERROR status)")
  @extension("x-duckdb-column", "status_message")
  statusMessage?: string;

  // === Service Info ===

  @doc("Service name from resource attributes")
  @extension("x-duckdb-column", "service_name")
  @extension("x-duckdb-index", "idx_spans_service_name")
  @extension("x-promoted", true)
  serviceName?: string;

  // === GenAI Promoted Columns (Fast Access) ===

  @doc("GenAI provider name (e.g., openai, anthropic)")
  @extension("x-duckdb-column", "gen_ai_provider_name")
  @extension("x-promoted", true)
  genAiProviderName?: string;

  @doc("Requested model name")
  @extension("x-duckdb-column", "gen_ai_request_model")
  @extension("x-promoted", true)
  genAiRequestModel?: string;

  @doc("Actual response model name")
  @extension("x-duckdb-column", "gen_ai_response_model")
  @extension("x-promoted", true)
  genAiResponseModel?: string;

  @doc("Input/prompt tokens")
  @extension("x-duckdb-column", "gen_ai_input_tokens")
  @extension("x-promoted", true)
  genAiInputTokens?: TokenCount;

  @doc("Output/completion tokens")
  @extension("x-duckdb-column", "gen_ai_output_tokens")
  @extension("x-promoted", true)
  genAiOutputTokens?: TokenCount;

  @doc("Request temperature")
  @extension("x-duckdb-column", "gen_ai_temperature")
  @extension("x-promoted", true)
  genAiTemperature?: Temperature;

  @doc("Response finish reason")
  @extension("x-duckdb-column", "gen_ai_stop_reason")
  @extension("x-promoted", true)
  genAiStopReason?: string;

  @doc("Tool name for tool calls")
  @extension("x-duckdb-column", "gen_ai_tool_name")
  @extension("x-promoted", true)
  genAiToolName?: string;

  @doc("Tool call ID")
  @extension("x-duckdb-column", "gen_ai_tool_call_id")
  @extension("x-promoted", true)
  genAiToolCallId?: string;

  @doc("Estimated cost in USD")
  @extension("x-duckdb-column", "gen_ai_cost_usd")
  @extension("x-promoted", true)
  genAiCostUsd?: CostUsd;

  // === Attributes (JSON Blob) ===

  @doc("All span attributes as JSON")
  @extension("x-duckdb-column", "attributes_json")
  @extension("x-duckdb-type", "VARCHAR")
  attributesJson?: string;

  @doc("Resource attributes as JSON")
  @extension("x-duckdb-column", "resource_json")
  @extension("x-duckdb-type", "VARCHAR")
  resourceJson?: string;

  // === Internal Storage Fields ===

  @doc("Row creation timestamp")
  @extension("x-duckdb-column", "created_at")
  @extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @extension("x-internal", true)
  createdAt?: utcDateTime;
}

// =============================================================================
// SessionSummary - Aggregated View
// =============================================================================

@jsonSchema
@doc("Session summary with aggregated metrics")
@extension("x-duckdb-table", "sessions")
@extension("x-csharp-type", "SessionSummary")
model SessionSummary {
  @doc("Session identifier")
  @key
  @extension("x-duckdb-column", "session_id")
  @extension("x-duckdb-primary-key", true)
  sessionId: SessionId;

  @doc("First span timestamp")
  @extension("x-duckdb-column", "start_time")
  startTime: int64;

  @doc("Last span timestamp")
  @extension("x-duckdb-column", "end_time")
  endTime: int64;

  @doc("Total span count")
  @extension("x-duckdb-column", "span_count")
  spanCount: Count;

  @doc("Error span count")
  @extension("x-duckdb-column", "error_count")
  errorCount: Count;

  @doc("Total input tokens")
  @extension("x-duckdb-column", "total_input_tokens")
  totalInputTokens: TokenCount;

  @doc("Total output tokens")
  @extension("x-duckdb-column", "total_output_tokens")
  totalOutputTokens: TokenCount;

  @doc("Total cost in USD")
  @extension("x-duckdb-column", "total_cost_usd")
  totalCostUsd: CostUsd;

  @doc("Primary service name")
  @extension("x-duckdb-column", "service_name")
  serviceName?: string;

  @doc("Primary GenAI provider")
  @extension("x-duckdb-column", "gen_ai_provider_name")
  genAiProviderName?: string;

  @doc("Primary model used")
  @extension("x-duckdb-column", "gen_ai_model")
  genAiModel?: string;
}

// =============================================================================
// LogRecordStorage - Log Storage Model
// =============================================================================

@jsonSchema
@doc("OpenTelemetry log record for storage and query")
@extension("x-duckdb-table", "logs")
@extension("x-csharp-type", "LogRecordStorage")
model LogRecordStorage {
  // === Identifiers ===

  @doc("Unique log record identifier")
  @key
  @extension("x-duckdb-column", "log_id")
  @extension("x-duckdb-primary-key", true)
  logId: string;

  @doc("Associated trace ID")
  @extension("x-duckdb-column", "trace_id")
  @extension("x-duckdb-index", "idx_logs_trace_id")
  traceId?: TraceId;

  @doc("Associated span ID")
  @extension("x-duckdb-column", "span_id")
  spanId?: SpanId;

  // === Session Grouping ===

  @doc("Session identifier for grouping related logs")
  @extension("x-duckdb-column", "session_id")
  @extension("x-duckdb-index", "idx_logs_session_id")
  sessionId?: SessionId;

  // === Timing ===

  @doc("Timestamp when the event occurred (nanoseconds since epoch)")
  @extension("x-duckdb-column", "time_unix_nano")
  @extension("x-duckdb-index", "idx_logs_time")
  timeUnixNano: int64;

  @doc("Timestamp when the log was observed/collected")
  @extension("x-duckdb-column", "observed_time_unix_nano")
  observedTimeUnixNano?: int64;

  // === Severity ===

  @doc("Severity number (1-24)")
  @extension("x-duckdb-column", "severity_number")
  @extension("x-duckdb-index", "idx_logs_severity")
  severityNumber: SeverityNumber;

  @doc("Severity text (TRACE, DEBUG, INFO, WARN, ERROR, FATAL)")
  @extension("x-duckdb-column", "severity_text")
  severityText?: string;

  // === Content ===

  @doc("Log body - the main content")
  @extension("x-duckdb-column", "body")
  body?: string;

  // === Service Info ===

  @doc("Service name from resource attributes")
  @extension("x-duckdb-column", "service_name")
  @extension("x-duckdb-index", "idx_logs_service_name")
  serviceName?: string;

  // === Attributes (JSON Blob) ===

  @doc("Log attributes as JSON")
  @extension("x-duckdb-column", "attributes_json")
  @extension("x-duckdb-type", "VARCHAR")
  attributesJson?: string;

  @doc("Resource attributes as JSON")
  @extension("x-duckdb-column", "resource_json")
  @extension("x-duckdb-type", "VARCHAR")
  resourceJson?: string;

  // === Internal Storage Fields ===

  @doc("Row creation timestamp")
  @extension("x-duckdb-column", "created_at")
  @extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @extension("x-internal", true)
  createdAt?: utcDateTime;
}

// =============================================================================
// TraceNode - For Hierarchical Display
// =============================================================================

@jsonSchema
@doc("Trace tree node for hierarchical span display")
@extension("x-csharp-type", "TraceNode")
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
@extension("x-csharp-type", "GenAiSpanData")
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

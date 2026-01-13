// =============================================================================
// QYL God Schema - Domain Models
// =============================================================================
// Core models with multi-target extensions for storage and API generation.
// =============================================================================

import "@typespec/openapi";
import "@typespec/json-schema";

import "./primitives.tsp";
import "./enums.tsp";

using TypeSpec.JsonSchema;
using Qyl.Primitives;
using Qyl.Enums;

namespace Qyl.Models;

// =============================================================================
// SpanRecord - Primary Storage Model
// =============================================================================

@jsonSchema
@doc("OpenTelemetry span record for storage and query")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "spans")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SpanRecord")
model SpanRecord {
  // === Identifiers (Primary Key) ===

  @doc("Unique span identifier")
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
  startTimeUnixNano: UnixNano;

  @doc("End timestamp in nanoseconds since epoch")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time_unix_nano")
  endTimeUnixNano: UnixNano;

  @doc("Duration in nanoseconds")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "duration_ns")
  durationNs: DurationNs;

  // === Status ===

  @doc("Span status code")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "status_code")
  statusCode: StatusCode;

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

  @doc("GenAI provider/system (e.g., openai, anthropic)")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_system")
  @TypeSpec.OpenAPI.extension("x-promoted", true)
  genAiSystem?: string;

  @doc("Requested model name")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_request_model")
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

  // === Internal Storage Fields ===

  @doc("Row creation timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "created_at")
  @TypeSpec.OpenAPI.extension("x-duckdb-type", "TIMESTAMP DEFAULT now()")
  @TypeSpec.OpenAPI.extension("x-internal", true)
  createdAt?: utcDateTime;
}

// =============================================================================
// Session Summary - Aggregated View
// =============================================================================

@jsonSchema
@doc("Session summary with aggregated metrics")
@TypeSpec.OpenAPI.extension("x-duckdb-table", "sessions")
@TypeSpec.OpenAPI.extension("x-csharp-type", "SessionSummary")
model SessionSummary {
  @doc("Session identifier")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "session_id")
  @TypeSpec.OpenAPI.extension("x-duckdb-primary-key", true)
  sessionId: SessionId;

  @doc("First span timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "start_time")
  startTime: UnixNano;

  @doc("Last span timestamp")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "end_time")
  endTime: UnixNano;

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
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_system")
  genAiSystem?: string;

  @doc("Primary model used")
  @TypeSpec.OpenAPI.extension("x-duckdb-column", "gen_ai_model")
  genAiModel?: string;
}

// =============================================================================
// Trace Tree Node - For Hierarchical Display
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
// GenAI Span Data - Extracted GenAI Attributes
// =============================================================================

@jsonSchema
@doc("Extracted GenAI attributes from a span")
@TypeSpec.OpenAPI.extension("x-csharp-type", "GenAiSpanData")
model GenAiSpanData {
  @doc("Provider/system name")
  system?: string;

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

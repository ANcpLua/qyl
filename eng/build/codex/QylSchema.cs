using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Components.Theory;

/// <summary>
///     Canonical schema definition for qyl.
///     This is the SINGLE SOURCE OF TRUTH for all generated code.
///     Defines:
///     - Primitives (SessionId, UnixNano, etc.)
///     - Models (SpanRecord, GenAiSpanData, SessionSummary, TraceNode)
///     - DuckDB tables with column mappings
///     - OTel gen_ai.* semantic convention attributes
/// </summary>
public sealed class QylSchema
{
    static readonly Lazy<QylSchema> LazyInstance = new(() => new QylSchema());

    QylSchema()
    {
        Primitives = BuildPrimitives();
        Models = BuildModels();
        Tables = BuildTables();
        GenAiAttributes = BuildGenAiAttributes();
    }

    public static QylSchema Instance => LazyInstance.Value;

    // ════════════════════════════════════════════════════════════════════════
    // Collections
    // ════════════════════════════════════════════════════════════════════════

    public FrozenSet<PrimitiveDefinition> Primitives { get; }

    public FrozenSet<ModelDefinition> Models { get; }

    public FrozenSet<TableDefinition> Tables { get; }

    public FrozenDictionary<string, GenAiAttributeDefinition> GenAiAttributes { get; }

    // ════════════════════════════════════════════════════════════════════════
    // Primitive Definitions
    // ════════════════════════════════════════════════════════════════════════

    static FrozenSet<PrimitiveDefinition> BuildPrimitives() =>
    [
        new(
            "SessionId",
            "Guid",
            "Unique identifier for an AI conversation session",
            ["ISpanParsable<SessionId>", "IUtf8SpanParsable<SessionId>"],
            "Guid.Parse",
            "ToString(\"N\")",
            "Guid.Empty",
            "SessionIdJsonConverter"),

        new(
            "UnixNano",
            "ulong",
            "Unix timestamp in nanoseconds (OTel wire format)",
            ["ISpanParsable<UnixNano>", "IUtf8SpanParsable<UnixNano>"],
            "ulong.Parse",
            "ToString()",
            "0UL",
            null),

        new(
            "TraceId",
            "UInt128",
            "128-bit trace identifier (OTel W3C format)",
            ["ISpanParsable<TraceId>", "IUtf8SpanParsable<TraceId>"],
            "UInt128.Parse",
            "ToString(\"x32\")",
            "UInt128.Zero",
            "TraceIdJsonConverter"),

        new(
            "SpanId",
            "ulong",
            "64-bit span identifier",
            ["ISpanParsable<SpanId>", "IUtf8SpanParsable<SpanId>"],
            "ulong.Parse",
            "ToString(\"x16\")",
            "0UL",
            "SpanIdJsonConverter")
    ];

    // ════════════════════════════════════════════════════════════════════════
    // Model Definitions
    // ════════════════════════════════════════════════════════════════════════

    static FrozenSet<ModelDefinition> BuildModels() =>
    [
        // SpanRecord - Flattened span for DuckDB storage
        new(
            "SpanRecord",
            "Flattened span record optimized for DuckDB columnar storage",
            true,
            [
                new PropertyDefinition("TraceId", "TraceId", "W3C trace identifier", true, DuckDbColumn: "trace_id",
                    DuckDbType: "VARCHAR"),
                new PropertyDefinition("SpanId", "SpanId", "Unique span identifier", true, DuckDbColumn: "span_id",
                    DuckDbType: "VARCHAR"),
                new PropertyDefinition("ParentSpanId", "SpanId?", "Parent span identifier (null for root)", false,
                    DuckDbColumn: "parent_span_id", DuckDbType: "VARCHAR"),
                new PropertyDefinition("Name", "string", "Span operation name", true, DuckDbColumn: "name",
                    DuckDbType: "VARCHAR"),
                new PropertyDefinition("Kind", "SpanKind", "Span kind (Client, Server, Producer, Consumer, Internal)",
                    true, DuckDbColumn: "kind", DuckDbType: "TINYINT"),
                new PropertyDefinition("StartTimeUnixNano", "UnixNano", "Start timestamp in nanoseconds", true,
                    DuckDbColumn: "start_time_unix_nano", DuckDbType: "UBIGINT"),
                new PropertyDefinition("EndTimeUnixNano", "UnixNano", "End timestamp in nanoseconds", true,
                    DuckDbColumn: "end_time_unix_nano", DuckDbType: "UBIGINT"),
                new PropertyDefinition("DurationMs", "double", "Duration in milliseconds (computed)", true,
                    DuckDbColumn: "duration_ms", DuckDbType: "DOUBLE"),
                new PropertyDefinition("StatusCode", "StatusCode", "Span status (Unset, Ok, Error)", true,
                    DuckDbColumn: "status_code", DuckDbType: "TINYINT"),
                new PropertyDefinition("StatusMessage", "string?", "Status message for errors", false,
                    DuckDbColumn: "status_message", DuckDbType: "VARCHAR"),
                new PropertyDefinition("ServiceName", "string", "Service name from resource", true,
                    DuckDbColumn: "service_name", DuckDbType: "VARCHAR"),
                new PropertyDefinition("ServiceVersion", "string?", "Service version", false,
                    DuckDbColumn: "service_version", DuckDbType: "VARCHAR"),
                // Promoted gen_ai.* attributes
                new PropertyDefinition("GenAiSystem", "string?", "AI provider (openai, anthropic, etc.)", false,
                    DuckDbColumn: "gen_ai_system", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("GenAiRequestModel", "string?", "Requested model name", false,
                    DuckDbColumn: "gen_ai_request_model", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("GenAiResponseModel", "string?", "Actual model used", false,
                    DuckDbColumn: "gen_ai_response_model", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("GenAiInputTokens", "int?", "Input/prompt token count", false,
                    DuckDbColumn: "gen_ai_input_tokens", DuckDbType: "INTEGER", IsPromoted: true),
                new PropertyDefinition("GenAiOutputTokens", "int?", "Output/completion token count", false,
                    DuckDbColumn: "gen_ai_output_tokens", DuckDbType: "INTEGER", IsPromoted: true),
                new PropertyDefinition("GenAiTotalTokens", "int?", "Total tokens (input + output)", false,
                    DuckDbColumn: "gen_ai_total_tokens", DuckDbType: "INTEGER", IsPromoted: true),
                new PropertyDefinition("GenAiCostUsd", "decimal?", "Computed cost in USD", false,
                    DuckDbColumn: "gen_ai_cost_usd", DuckDbType: "DECIMAL(18,8)", IsPromoted: true),
                new PropertyDefinition("GenAiTemperature", "double?", "Sampling temperature", false,
                    DuckDbColumn: "gen_ai_temperature", DuckDbType: "DOUBLE", IsPromoted: true),
                new PropertyDefinition("GenAiStopReason", "string?", "Stop/finish reason", false,
                    DuckDbColumn: "gen_ai_stop_reason", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("GenAiToolName", "string?", "Tool/function name", false,
                    DuckDbColumn: "gen_ai_tool_name", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("GenAiToolCallId", "string?", "Tool call identifier", false,
                    DuckDbColumn: "gen_ai_tool_call_id", DuckDbType: "VARCHAR", IsPromoted: true),
                // Session tracking
                new PropertyDefinition("SessionId", "SessionId?", "Session/conversation identifier", false,
                    DuckDbColumn: "session_id", DuckDbType: "VARCHAR"),
                // Raw attributes as JSON
                new PropertyDefinition("AttributesJson", "string?", "Raw attributes as JSON", false,
                    DuckDbColumn: "attributes_json", DuckDbType: "JSON"),
                new PropertyDefinition("EventsJson", "string?", "Span events as JSON", false,
                    DuckDbColumn: "events_json", DuckDbType: "JSON"),
                new PropertyDefinition("LinksJson", "string?", "Span links as JSON", false, DuckDbColumn: "links_json",
                    DuckDbType: "JSON"),
                new PropertyDefinition("ResourceAttributesJson", "string?", "Resource attributes as JSON", false,
                    DuckDbColumn: "resource_attributes_json", DuckDbType: "JSON"),
                // Metadata
                new PropertyDefinition("CreatedAt", "DateTimeOffset", "Record creation timestamp", true,
                    DuckDbColumn: "created_at", DuckDbType: "TIMESTAMPTZ")
            ]),

        // GenAiSpanData - Extracted gen_ai.* attributes
        new(
            "GenAiSpanData",
            "Extracted gen_ai.* semantic convention attributes",
            true,
            [
                new PropertyDefinition("System", "string?", "AI provider (gen_ai.system / gen_ai.provider.name)",
                    false),
                new PropertyDefinition("OperationName", "string?", "Operation type (chat, text_completion, embeddings)",
                    false),
                new PropertyDefinition("RequestModel", "string?", "Requested model name", false),
                new PropertyDefinition("ResponseModel", "string?", "Actual model used in response", false),
                new PropertyDefinition("InputTokens", "int?", "Input/prompt tokens", false),
                new PropertyDefinition("OutputTokens", "int?", "Output/completion tokens", false),
                new PropertyDefinition("TotalTokens", "int?", "Total tokens (computed: input + output)", false),
                new PropertyDefinition("Temperature", "double?", "Sampling temperature", false),
                new PropertyDefinition("TopP", "double?", "Top-p sampling parameter", false),
                new PropertyDefinition("MaxTokens", "int?", "Max tokens limit", false),
                new PropertyDefinition("StopReason", "string?", "Stop/finish reason", false),
                new PropertyDefinition("ToolName", "string?", "Tool/function name if tool call", false),
                new PropertyDefinition("ToolCallId", "string?", "Tool call identifier", false),
                new PropertyDefinition("CostUsd", "decimal?", "Computed cost in USD", false),
                new PropertyDefinition("IsToolCall", "bool", "Whether this is a tool/function call", true, "false")
            ]),

        // SessionSummary - Aggregated session statistics
        new(
            "SessionSummary",
            "Aggregated statistics for an AI conversation session",
            true,
            [
                new PropertyDefinition("SessionId", "SessionId", "Session identifier", true),
                new PropertyDefinition("ServiceName", "string", "Primary service name", true),
                new PropertyDefinition("StartTime", "DateTimeOffset", "Session start time", true),
                new PropertyDefinition("LastActivity", "DateTimeOffset", "Most recent activity", true),
                new PropertyDefinition("DurationMinutes", "double", "Session duration in minutes", true),
                new PropertyDefinition("SpanCount", "int", "Total span count", true),
                new PropertyDefinition("ErrorCount", "int", "Error span count", true),
                new PropertyDefinition("ErrorRate", "double", "Error rate (0.0 - 1.0)", true),
                new PropertyDefinition("TotalInputTokens", "long", "Sum of input tokens", true),
                new PropertyDefinition("TotalOutputTokens", "long", "Sum of output tokens", true),
                new PropertyDefinition("TotalTokens", "long", "Total tokens (input + output)", true),
                new PropertyDefinition("TotalCostUsd", "decimal", "Total estimated cost", true),
                new PropertyDefinition("ToolCallCount", "int", "Number of tool calls", true),
                new PropertyDefinition("PrimaryModel", "string?", "Most frequently used model", false),
                new PropertyDefinition("Models", "IReadOnlyList<string>", "All models used", true, "[]"),
                new PropertyDefinition("IsActive", "bool", "Session still active (recent activity)", true)
            ]),

        // TraceNode - Hierarchical trace tree
        new(
            "TraceNode",
            "Hierarchical trace tree node for visualization",
            true,
            [
                new PropertyDefinition("TraceId", "TraceId", "Trace identifier", true),
                new PropertyDefinition("SpanId", "SpanId", "Span identifier", true),
                new PropertyDefinition("ParentSpanId", "SpanId?", "Parent span identifier", false),
                new PropertyDefinition("Name", "string", "Operation name", true),
                new PropertyDefinition("ServiceName", "string", "Service name", true),
                new PropertyDefinition("StartTime", "DateTimeOffset", "Start timestamp", true),
                new PropertyDefinition("DurationMs", "double", "Duration in milliseconds", true),
                new PropertyDefinition("Status", "StatusCode", "Span status", true),
                new PropertyDefinition("GenAi", "GenAiSpanData?", "Extracted gen_ai data", false),
                new PropertyDefinition("Children", "IReadOnlyList<TraceNode>", "Child nodes", true, "[]"),
                new PropertyDefinition("Depth", "int", "Tree depth (0 for root)", true)
            ]),

        // SpanKind enum
        new(
            "SpanKind",
            "OpenTelemetry span kind",
            false,
            IsEnum: true,
            EnumValues:
            [
                new EnumValueDefinition("Unspecified", 0, "Unspecified span kind"),
                new EnumValueDefinition("Internal", 1, "Internal operation"),
                new EnumValueDefinition("Server", 2, "Server-side handling"),
                new EnumValueDefinition("Client", 3, "Client-side request"),
                new EnumValueDefinition("Producer", 4, "Message producer"),
                new EnumValueDefinition("Consumer", 5, "Message consumer")
            ]),

        // StatusCode enum
        new(
            "StatusCode",
            "OpenTelemetry status code",
            false,
            IsEnum: true,
            EnumValues:
            [
                new EnumValueDefinition("Unset", 0, "Status not set"),
                new EnumValueDefinition("Ok", 1, "Operation succeeded"),
                new EnumValueDefinition("Error", 2, "Operation failed")
            ])
    ];

    // ════════════════════════════════════════════════════════════════════════
    // Table Definitions
    // ════════════════════════════════════════════════════════════════════════

    static FrozenSet<TableDefinition> BuildTables() =>
    [
        new(
            "spans",
            "Primary span storage table with promoted gen_ai.* columns",
            "SpanRecord",
            "span_id",
            [
                new IndexDefinition("idx_spans_trace_id", ["trace_id"]),
                new IndexDefinition("idx_spans_service", ["service_name"]),
                new IndexDefinition("idx_spans_start_time", ["start_time_unix_nano"], true),
                new IndexDefinition("idx_spans_session", ["session_id"]),
                new IndexDefinition("idx_spans_gen_ai_system", ["gen_ai_system"]),
                new IndexDefinition("idx_spans_gen_ai_model", ["gen_ai_request_model"])
            ]),

        new(
            "sessions_agg",
            "Materialized view for session aggregation",
            "SessionSummary",
            IsView: true,
            ViewSql: """
                     SELECT
                         COALESCE(session_id, trace_id) as session_id,
                         service_name,
                         MIN(start_time_unix_nano) as start_time,
                         MAX(end_time_unix_nano) as last_activity,
                         COUNT(*) as span_count,
                         COUNT(*) FILTER (WHERE status_code = 2) as error_count,
                         SUM(COALESCE(gen_ai_input_tokens, 0)) as total_input_tokens,
                         SUM(COALESCE(gen_ai_output_tokens, 0)) as total_output_tokens,
                         SUM(COALESCE(gen_ai_cost_usd, 0)) as total_cost_usd,
                         COUNT(*) FILTER (WHERE gen_ai_tool_name IS NOT NULL) as tool_call_count,
                         MODE(gen_ai_request_model) as primary_model,
                         LIST(DISTINCT gen_ai_request_model) FILTER (WHERE gen_ai_request_model IS NOT NULL) as models
                     FROM spans
                     GROUP BY COALESCE(session_id, trace_id), service_name
                     """)
    ];

    // ════════════════════════════════════════════════════════════════════════
    // OTel gen_ai.* Semantic Conventions (v1.38)
    // ════════════════════════════════════════════════════════════════════════

    static FrozenDictionary<string, GenAiAttributeDefinition> BuildGenAiAttributes() =>
        new Dictionary<string, GenAiAttributeDefinition>
        {
            // Current attributes (v1.38)
            ["gen_ai.operation.name"] = new("gen_ai.operation.name", "string", "Operation type",
                ["chat", "text_completion", "embeddings", "image_generation"]),
            ["gen_ai.provider.name"] = new("gen_ai.provider.name", "string", "AI provider name",
                ["anthropic", "openai", "google", "azure", "cohere", "mistral"]),
            ["gen_ai.request.model"] = new("gen_ai.request.model", "string", "Requested model name"),
            ["gen_ai.response.model"] = new("gen_ai.response.model", "string", "Actual model in response"),
            ["gen_ai.usage.input_tokens"] = new("gen_ai.usage.input_tokens", "int", "Input token count"),
            ["gen_ai.usage.output_tokens"] = new("gen_ai.usage.output_tokens", "int", "Output token count"),
            ["gen_ai.request.temperature"] = new("gen_ai.request.temperature", "double", "Sampling temperature"),
            ["gen_ai.request.top_p"] = new("gen_ai.request.top_p", "double", "Top-p sampling parameter"),
            ["gen_ai.request.max_tokens"] = new("gen_ai.request.max_tokens", "int", "Maximum tokens to generate"),
            ["gen_ai.response.finish_reason"] = new("gen_ai.response.finish_reason", "string", "Stop reason",
                ["stop", "length", "content_filter", "tool_calls", "end_turn"]),
            ["gen_ai.response.id"] = new("gen_ai.response.id", "string", "Provider response ID"),
            ["gen_ai.tool.name"] = new("gen_ai.tool.name", "string", "Tool/function name"),
            ["gen_ai.tool.call.id"] = new("gen_ai.tool.call.id", "string", "Tool call identifier"),

            // Deprecated attributes (for migration)
            ["gen_ai.system"] = new("gen_ai.system", "string", "DEPRECATED: Use gen_ai.provider.name",
                IsDeprecated: true, ReplacedBy: "gen_ai.provider.name"),
            ["gen_ai.usage.prompt_tokens"] = new("gen_ai.usage.prompt_tokens", "int",
                "DEPRECATED: Use gen_ai.usage.input_tokens", IsDeprecated: true,
                ReplacedBy: "gen_ai.usage.input_tokens"),
            ["gen_ai.usage.completion_tokens"] = new("gen_ai.usage.completion_tokens", "int",
                "DEPRECATED: Use gen_ai.usage.output_tokens", IsDeprecated: true,
                ReplacedBy: "gen_ai.usage.output_tokens"),

            // qyl.* extension attributes
            ["qyl.session.id"] = new("qyl.session.id", "string", "Session/conversation identifier (qyl extension)"),
            ["qyl.cost.usd"] = new("qyl.cost.usd", "decimal", "Computed cost in USD (qyl extension)"),
            ["qyl.feedback.score"] = new("qyl.feedback.score", "int", "User feedback score -1/0/1 (qyl extension)"),
            ["qyl.agent.id"] = new("qyl.agent.id", "string", "Agent identifier (qyl extension)")
        }.ToFrozenDictionary();
}

// ════════════════════════════════════════════════════════════════════════════
// Schema Definition Records
// ════════════════════════════════════════════════════════════════════════════

public sealed record PrimitiveDefinition(
    string Name,
    string UnderlyingType,
    string Description,
    string[] Implements,
    string ParseMethod,
    string FormatMethod,
    string DefaultValue,
    string? JsonConverter);

public sealed record ModelDefinition(
    string Name,
    string Description,
    bool IsRecord,
    IReadOnlyList<PropertyDefinition>? Properties = null,
    bool IsEnum = false,
    IReadOnlyList<EnumValueDefinition>? EnumValues = null);

public sealed record PropertyDefinition(
    string Name,
    string Type,
    string Description,
    bool IsRequired,
    string? DefaultValue = null,
    string? DuckDbColumn = null,
    string? DuckDbType = null,
    bool IsPromoted = false);

public sealed record EnumValueDefinition(
    string Name,
    int Value,
    string Description);

public sealed record TableDefinition(
    string Name,
    string Description,
    string ModelName,
    string? PrimaryKey = null,
    IReadOnlyList<IndexDefinition>? Indexes = null,
    bool IsView = false,
    string? ViewSql = null);

public sealed record IndexDefinition(
    string Name,
    string[] Columns,
    bool IsDescending = false,
    bool IsUnique = false);

public sealed record GenAiAttributeDefinition(
    string Key,
    string Type,
    string Description,
    string[]? AllowedValues = null,
    bool IsDeprecated = false,
    string? ReplacedBy = null);
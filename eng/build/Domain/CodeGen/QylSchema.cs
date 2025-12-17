using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Domain.CodeGen;

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
                // Promoted gen_ai.* attributes (OTel 1.38)
                new PropertyDefinition("ProviderName", "string?", "gen_ai.provider.name - AI provider", false,
                    DuckDbColumn: "\"gen_ai.provider.name\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("OperationName", "string?", "gen_ai.operation.name - Operation type", false,
                    DuckDbColumn: "\"gen_ai.operation.name\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("RequestModel", "string?", "gen_ai.request.model - Requested model", false,
                    DuckDbColumn: "\"gen_ai.request.model\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("ResponseModel", "string?", "gen_ai.response.model - Actual model", false,
                    DuckDbColumn: "\"gen_ai.response.model\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("InputTokens", "long?", "gen_ai.usage.input_tokens", false,
                    DuckDbColumn: "\"gen_ai.usage.input_tokens\"", DuckDbType: "BIGINT", IsPromoted: true),
                new PropertyDefinition("OutputTokens", "long?", "gen_ai.usage.output_tokens", false,
                    DuckDbColumn: "\"gen_ai.usage.output_tokens\"", DuckDbType: "BIGINT", IsPromoted: true),
                new PropertyDefinition("Temperature", "double?", "gen_ai.request.temperature", false,
                    DuckDbColumn: "\"gen_ai.request.temperature\"", DuckDbType: "DOUBLE", IsPromoted: true),
                new PropertyDefinition("MaxTokens", "long?", "gen_ai.request.max_tokens", false,
                    DuckDbColumn: "\"gen_ai.request.max_tokens\"", DuckDbType: "BIGINT", IsPromoted: true),
                new PropertyDefinition("ResponseId", "string?", "gen_ai.response.id", false,
                    DuckDbColumn: "\"gen_ai.response.id\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("FinishReasons", "string[]?", "gen_ai.response.finish_reasons", false,
                    DuckDbColumn: "\"gen_ai.response.finish_reasons\"", DuckDbType: "VARCHAR[]", IsPromoted: true),
                new PropertyDefinition("AgentId", "string?", "gen_ai.agent.id", false,
                    DuckDbColumn: "\"gen_ai.agent.id\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("AgentName", "string?", "gen_ai.agent.name", false,
                    DuckDbColumn: "\"gen_ai.agent.name\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("ToolName", "string?", "gen_ai.tool.name", false,
                    DuckDbColumn: "\"gen_ai.tool.name\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("ToolCallId", "string?", "gen_ai.tool.call.id", false,
                    DuckDbColumn: "\"gen_ai.tool.call.id\"", DuckDbType: "VARCHAR", IsPromoted: true),
                new PropertyDefinition("ConversationId", "string?", "gen_ai.conversation.id", false,
                    DuckDbColumn: "\"gen_ai.conversation.id\"", DuckDbType: "VARCHAR", IsPromoted: true),
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

        // GenAiSpanData - Extracted gen_ai.* attributes (OTel 1.38)
        new(
            "GenAiSpanData",
            "Extracted gen_ai.* semantic convention attributes (OTel 1.38)",
            true,
            [
                // Core Required (OTel 1.38)
                new PropertyDefinition("ProviderName", "string?", "gen_ai.provider.name - AI provider", false),
                new PropertyDefinition("OperationName", "string?", "gen_ai.operation.name - Operation type", false),
                new PropertyDefinition("RequestModel", "string?", "gen_ai.request.model - Requested model", false),
                new PropertyDefinition("ResponseModel", "string?", "gen_ai.response.model - Actual model", false),

                // Usage (OTel 1.38)
                new PropertyDefinition("InputTokens", "long?", "gen_ai.usage.input_tokens", false),
                new PropertyDefinition("OutputTokens", "long?", "gen_ai.usage.output_tokens", false),

                // Request Parameters (OTel 1.38)
                new PropertyDefinition("Temperature", "double?", "gen_ai.request.temperature", false),
                new PropertyDefinition("TopP", "double?", "gen_ai.request.top_p", false),
                new PropertyDefinition("TopK", "double?", "gen_ai.request.top_k", false),
                new PropertyDefinition("MaxTokens", "long?", "gen_ai.request.max_tokens", false),
                new PropertyDefinition("Seed", "long?", "gen_ai.request.seed", false),
                new PropertyDefinition("FrequencyPenalty", "double?", "gen_ai.request.frequency_penalty", false),
                new PropertyDefinition("PresencePenalty", "double?", "gen_ai.request.presence_penalty", false),
                new PropertyDefinition("ChoiceCount", "int?", "gen_ai.request.choice.count", false),

                // Response (OTel 1.38)
                new PropertyDefinition("ResponseId", "string?", "gen_ai.response.id", false),
                new PropertyDefinition("FinishReasons", "IReadOnlyList<string>?", "gen_ai.response.finish_reasons",
                    false),

                // Agent (OTel 1.38)
                new PropertyDefinition("AgentId", "string?", "gen_ai.agent.id", false),
                new PropertyDefinition("AgentName", "string?", "gen_ai.agent.name", false),

                // Tool (OTel 1.38)
                new PropertyDefinition("ToolName", "string?", "gen_ai.tool.name", false),
                new PropertyDefinition("ToolCallId", "string?", "gen_ai.tool.call.id", false),
                new PropertyDefinition("ToolType", "string?", "gen_ai.tool.type", false),

                // Conversation (OTel 1.38)
                new PropertyDefinition("ConversationId", "string?", "gen_ai.conversation.id", false),

                // Computed (qyl extension)
                new PropertyDefinition("TotalTokens", "long?", "Total tokens (computed: input + output)", false),
                new PropertyDefinition("CostUsd", "decimal?", "Computed cost in USD (qyl extension)", false),
                new PropertyDefinition("IsToolCall", "bool", "Whether this is a tool/function call", true, "false"),
                new PropertyDefinition("IsGenAi", "bool", "Has GenAI attributes", true, "false")
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
                new IndexDefinition("idx_spans_session", ["\"session.id\""]),
                new IndexDefinition("idx_spans_provider", ["\"gen_ai.provider.name\""]),
                new IndexDefinition("idx_spans_model", ["\"gen_ai.request.model\""])
            ]),

        new(
            "sessions_agg",
            "Materialized view for session aggregation",
            "SessionSummary",
            IsView: true,
            ViewSql: """
                     SELECT
                         COALESCE("session.id", trace_id) as session_id,
                         "service.name" as service_name,
                         MIN(start_time_unix_nano) as start_time,
                         MAX(end_time_unix_nano) as last_activity,
                         COUNT(*) as span_count,
                         COUNT(*) FILTER (WHERE status_code = 2) as error_count,
                         SUM(COALESCE("gen_ai.usage.input_tokens", 0)) as total_input_tokens,
                         SUM(COALESCE("gen_ai.usage.output_tokens", 0)) as total_output_tokens,
                         COUNT(*) FILTER (WHERE "gen_ai.tool.name" IS NOT NULL) as tool_call_count,
                         MODE("gen_ai.request.model") as primary_model,
                         LIST(DISTINCT "gen_ai.request.model") FILTER (WHERE "gen_ai.request.model" IS NOT NULL) as models
                     FROM spans
                     GROUP BY COALESCE("session.id", trace_id), "service.name"
                     """)
    ];

    // ════════════════════════════════════════════════════════════════════════
    // OTel gen_ai.* Semantic Conventions (v1.38)
    // ════════════════════════════════════════════════════════════════════════

    static FrozenDictionary<string, GenAiAttributeDefinition> BuildGenAiAttributes() =>
        new Dictionary<string, GenAiAttributeDefinition>
        {
            // === OTel 1.38 Required ===
            ["gen_ai.operation.name"] = new("gen_ai.operation.name", "string", "Operation type",
            [
                "chat", "text_completion", "embeddings", "generate_content", "create_agent", "invoke_agent",
                "execute_tool"
            ]),
            ["gen_ai.provider.name"] = new("gen_ai.provider.name", "string", "AI provider name",
            [
                "anthropic", "openai", "aws.bedrock", "azure.ai.openai", "cohere", "deepseek", "gcp.gemini",
                "gcp.vertex_ai", "groq", "ibm.watsonx.ai", "mistral_ai", "perplexity", "x_ai"
            ]),

            // === OTel 1.38 Request ===
            ["gen_ai.request.model"] = new("gen_ai.request.model", "string", "Requested model name"),
            ["gen_ai.request.temperature"] = new("gen_ai.request.temperature", "double", "Sampling temperature"),
            ["gen_ai.request.top_p"] = new("gen_ai.request.top_p", "double", "Top-p sampling parameter"),
            ["gen_ai.request.top_k"] = new("gen_ai.request.top_k", "double", "Top-k sampling parameter"),
            ["gen_ai.request.max_tokens"] = new("gen_ai.request.max_tokens", "int", "Maximum tokens to generate"),
            ["gen_ai.request.frequency_penalty"] =
                new("gen_ai.request.frequency_penalty", "double", "Frequency penalty"),
            ["gen_ai.request.presence_penalty"] = new("gen_ai.request.presence_penalty", "double", "Presence penalty"),
            ["gen_ai.request.stop_sequences"] = new("gen_ai.request.stop_sequences", "string[]", "Stop sequences"),
            ["gen_ai.request.seed"] = new("gen_ai.request.seed", "int", "Random seed"),
            ["gen_ai.request.choice.count"] = new("gen_ai.request.choice.count", "int", "Number of choices"),

            // === OTel 1.38 Response ===
            ["gen_ai.response.model"] = new("gen_ai.response.model", "string", "Actual model in response"),
            ["gen_ai.response.id"] = new("gen_ai.response.id", "string", "Provider response ID"),
            ["gen_ai.response.finish_reasons"] = new("gen_ai.response.finish_reasons", "string[]", "Stop reasons",
                ["stop", "length", "content_filter", "tool_calls", "end_turn"]),

            // === OTel 1.38 Usage ===
            ["gen_ai.usage.input_tokens"] = new("gen_ai.usage.input_tokens", "int", "Input token count"),
            ["gen_ai.usage.output_tokens"] = new("gen_ai.usage.output_tokens", "int", "Output token count"),
            ["gen_ai.token.type"] = new("gen_ai.token.type", "string", "Token type", ["input", "output"]),

            // === OTel 1.38 Agent ===
            ["gen_ai.agent.id"] = new("gen_ai.agent.id", "string", "Agent identifier"),
            ["gen_ai.agent.name"] = new("gen_ai.agent.name", "string", "Agent name"),
            ["gen_ai.agent.description"] = new("gen_ai.agent.description", "string", "Agent description"),

            // === OTel 1.38 Tool ===
            ["gen_ai.tool.name"] = new("gen_ai.tool.name", "string", "Tool/function name"),
            ["gen_ai.tool.call.id"] = new("gen_ai.tool.call.id", "string", "Tool call identifier"),
            ["gen_ai.tool.type"] =
                new("gen_ai.tool.type", "string", "Tool type", ["function", "extension", "datastore"]),
            ["gen_ai.tool.description"] = new("gen_ai.tool.description", "string", "Tool description"),
            ["gen_ai.tool.call.arguments"] = new("gen_ai.tool.call.arguments", "any", "Tool call arguments"),
            ["gen_ai.tool.call.result"] = new("gen_ai.tool.call.result", "any", "Tool call result"),

            // === OTel 1.38 Content (Opt-In) ===
            ["gen_ai.input.messages"] = new("gen_ai.input.messages", "any", "Input messages"),
            ["gen_ai.output.messages"] = new("gen_ai.output.messages", "any", "Output messages"),
            ["gen_ai.system_instructions"] = new("gen_ai.system_instructions", "any", "System instructions"),
            ["gen_ai.tool.definitions"] = new("gen_ai.tool.definitions", "any", "Tool definitions"),
            ["gen_ai.output.type"] = new("gen_ai.output.type", "string", "Output type",
                ["text", "json", "image", "speech"]),

            // === OTel 1.38 Conversation ===
            ["gen_ai.conversation.id"] = new("gen_ai.conversation.id", "string", "Conversation/session ID"),
            ["gen_ai.data_source.id"] = new("gen_ai.data_source.id", "string", "Data source ID for RAG"),

            // === OTel 1.38 Embeddings ===
            ["gen_ai.embeddings.dimension.count"] =
                new("gen_ai.embeddings.dimension.count", "int", "Embedding dimensions"),
            ["gen_ai.request.encoding_formats"] =
                new("gen_ai.request.encoding_formats", "string[]", "Encoding formats"),

            // === OTel 1.38 Evaluation ===
            ["gen_ai.evaluation.name"] = new("gen_ai.evaluation.name", "string", "Evaluation metric name"),
            ["gen_ai.evaluation.score.value"] = new("gen_ai.evaluation.score.value", "double", "Evaluation score"),
            ["gen_ai.evaluation.score.label"] = new("gen_ai.evaluation.score.label", "string", "Evaluation label"),
            ["gen_ai.evaluation.explanation"] =
                new("gen_ai.evaluation.explanation", "string", "Evaluation explanation"),

            // === Deprecated (for migration) ===
            ["gen_ai.system"] = new("gen_ai.system", "string", "DEPRECATED: Use gen_ai.provider.name",
                IsDeprecated: true, ReplacedBy: "gen_ai.provider.name"),
            ["gen_ai.prompt"] = new("gen_ai.prompt", "string", "DEPRECATED: Use gen_ai.input.messages",
                IsDeprecated: true, ReplacedBy: "gen_ai.input.messages"),
            ["gen_ai.completion"] = new("gen_ai.completion", "string", "DEPRECATED: Use gen_ai.output.messages",
                IsDeprecated: true, ReplacedBy: "gen_ai.output.messages"),
            ["gen_ai.usage.prompt_tokens"] = new("gen_ai.usage.prompt_tokens", "int",
                "DEPRECATED: Use gen_ai.usage.input_tokens", IsDeprecated: true,
                ReplacedBy: "gen_ai.usage.input_tokens"),
            ["gen_ai.usage.completion_tokens"] = new("gen_ai.usage.completion_tokens", "int",
                "DEPRECATED: Use gen_ai.usage.output_tokens", IsDeprecated: true,
                ReplacedBy: "gen_ai.usage.output_tokens"),
            ["gen_ai.openai.request.seed"] = new("gen_ai.openai.request.seed", "int",
                "DEPRECATED: Use gen_ai.request.seed", IsDeprecated: true,
                ReplacedBy: "gen_ai.request.seed"),

            // Legacy agents.* prefix (non-standard, migrated to gen_ai.*)
            ["agents.agent.id"] = new("agents.agent.id", "string", "LEGACY: Use gen_ai.agent.id",
                IsDeprecated: true, ReplacedBy: "gen_ai.agent.id"),
            ["agents.agent.name"] = new("agents.agent.name", "string", "LEGACY: Use gen_ai.agent.name",
                IsDeprecated: true, ReplacedBy: "gen_ai.agent.name"),
            ["agents.tool.name"] = new("agents.tool.name", "string", "LEGACY: Use gen_ai.tool.name",
                IsDeprecated: true, ReplacedBy: "gen_ai.tool.name"),
            ["agents.tool.call_id"] = new("agents.tool.call_id", "string", "LEGACY: Use gen_ai.tool.call.id",
                IsDeprecated: true, ReplacedBy: "gen_ai.tool.call.id")
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
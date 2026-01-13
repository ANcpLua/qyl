using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

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
    static readonly Lazy<QylSchema> LazyInstance = new(static () => new QylSchema());

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
        // ALIGNED with src/qyl.protocol/Models/SpanRecord.cs
        new(
            "SpanRecord",
            "Represents a span record stored in DuckDB and transmitted via API",
            true,
            [
                // Core span identifiers (string-based for JSON/API compatibility)
                new PropertyDefinition("TraceId", "string", "Trace ID (32 hex chars)", true, DuckDbColumn: "trace_id",
                    DuckDbType: "VARCHAR"),
                new PropertyDefinition("SpanId", "string", "Span ID (16 hex chars)", true, DuckDbColumn: "span_id",
                    DuckDbType: "VARCHAR"),
                new PropertyDefinition("ParentSpanId", "string?", "Parent span ID (null for root spans)", false,
                    DuckDbColumn: "parent_span_id", DuckDbType: "VARCHAR"),
                new PropertyDefinition("SessionId", "string?", "Session ID for grouping related traces", false,
                    DuckDbColumn: "session_id", DuckDbType: "VARCHAR"),
                new PropertyDefinition("Name", "string", "Span name/operation", true, DuckDbColumn: "name",
                    DuckDbType: "VARCHAR"),
                new PropertyDefinition("ServiceName", "string?", "Service name from resource attributes", false,
                    DuckDbColumn: "service_name", DuckDbType: "VARCHAR"),
                new PropertyDefinition("Kind", "int", "Span kind (0=Unspecified, 1=Internal, 2=Server, 3=Client, 4=Producer, 5=Consumer)",
                    true, "0", DuckDbColumn: "kind", DuckDbType: "TINYINT"),
                new PropertyDefinition("StartTimeUnixNano", "UnixNano", "Start time in Unix nanoseconds", true,
                    DuckDbColumn: "start_time_unix_nano", DuckDbType: "UBIGINT"),
                new PropertyDefinition("EndTimeUnixNano", "UnixNano", "End time in Unix nanoseconds", true,
                    DuckDbColumn: "end_time_unix_nano", DuckDbType: "UBIGINT"),
                // Note: DurationNs is computed in runtime as (long)EndTimeUnixNano.Value - (long)StartTimeUnixNano.Value
                new PropertyDefinition("StatusCode", "int", "Status code (0=Unset, 1=Ok, 2=Error)", true, "0",
                    DuckDbColumn: "status_code", DuckDbType: "TINYINT"),
                new PropertyDefinition("StatusMessage", "string?", "Status message (for errors)", false,
                    DuckDbColumn: "status_message", DuckDbType: "VARCHAR"),
                // GenAI sub-object (null if not a GenAI span)
                new PropertyDefinition("GenAi", "GenAiSpanData?", "GenAI-specific data (null if not a GenAI span)", false),
                // Additional attributes
                new PropertyDefinition("Attributes", "IReadOnlyDictionary<string, string>?", "Additional attributes as key-value pairs", false),
                new PropertyDefinition("EventsJson", "string?", "Span events as JSON", false,
                    DuckDbColumn: "events_json", DuckDbType: "JSON"),
                new PropertyDefinition("LinksJson", "string?", "Span links as JSON", false, DuckDbColumn: "links_json",
                    DuckDbType: "JSON")
            ]),

        // GenAiSpanData - Extracted gen_ai.* attributes (OTel 1.38)
        // ALIGNED with src/qyl.protocol/Models/GenAiSpanData.cs
        new(
            "GenAiSpanData",
            "GenAI-specific span data extracted from gen_ai.* semantic conventions (OTel 1.38)",
            true,
            [
                // Core Required (OTel 1.38)
                new PropertyDefinition("ProviderName", "string?", "gen_ai.provider.name - Provider name (openai, anthropic, etc.)", false),
                new PropertyDefinition("OperationName", "string?", "gen_ai.operation.name - Operation type (chat, text_completion, etc.)", false),
                new PropertyDefinition("RequestModel", "string?", "gen_ai.request.model - The model ID requested", false),
                new PropertyDefinition("ResponseModel", "string?", "gen_ai.response.model - The model that served the request", false),

                // Usage (OTel 1.38)
                new PropertyDefinition("InputTokens", "long?", "gen_ai.usage.input_tokens - Number of input tokens", false),
                new PropertyDefinition("OutputTokens", "long?", "gen_ai.usage.output_tokens - Number of output tokens", false),
                // Note: TotalTokens is computed in runtime as (InputTokens ?? 0) + (OutputTokens ?? 0)

                // Request Parameters (OTel 1.38)
                new PropertyDefinition("Temperature", "double?", "gen_ai.request.temperature - Sampling temperature", false),
                new PropertyDefinition("TopP", "double?", "gen_ai.request.top_p - Top-p sampling parameter", false),
                new PropertyDefinition("TopK", "double?", "gen_ai.request.top_k - Top-k sampling parameter", false),
                new PropertyDefinition("MaxTokens", "long?", "gen_ai.request.max_tokens - Maximum tokens requested", false),
                new PropertyDefinition("Seed", "long?", "gen_ai.request.seed - Random seed for deterministic output", false),
                new PropertyDefinition("FrequencyPenalty", "double?", "gen_ai.request.frequency_penalty - Frequency penalty", false),
                new PropertyDefinition("PresencePenalty", "double?", "gen_ai.request.presence_penalty - Presence penalty", false),
                new PropertyDefinition("ChoiceCount", "int?", "gen_ai.request.choice.count - Number of choices requested", false),

                // Response (OTel 1.38)
                new PropertyDefinition("ResponseId", "string?", "gen_ai.response.id - Response identifier", false),
                new PropertyDefinition("FinishReasons", "IReadOnlyList<string>?", "gen_ai.response.finish_reasons - Finish reasons", false),

                // Agent (OTel 1.38)
                new PropertyDefinition("AgentId", "string?", "gen_ai.agent.id - Agent identifier", false),
                new PropertyDefinition("AgentName", "string?", "gen_ai.agent.name - Agent name (for agent operations)", false),

                // Tool (OTel 1.38)
                new PropertyDefinition("ToolName", "string?", "gen_ai.tool.name - Tool name (for tool operations)", false),
                new PropertyDefinition("ToolCallId", "string?", "gen_ai.tool.call.id - Tool call identifier", false),
                new PropertyDefinition("ToolType", "string?", "gen_ai.tool.type - Tool type (function, extension, datastore)", false),

                // Conversation (OTel 1.38)
                new PropertyDefinition("ConversationId", "string?", "gen_ai.conversation.id - Conversation/session identifier", false)
                // Note: IsGenAi, IsToolCall, IsAgentOperation are computed properties in runtime
            ]),

        // SessionSummary - Aggregated session statistics
        // ALIGNED with src/qyl.protocol/Models/SessionSummary.cs
        new(
            "SessionSummary",
            "Summary of a session with aggregated metrics",
            true,
            [
                new PropertyDefinition("SessionId", "string", "Session identifier", true),
                new PropertyDefinition("ServiceName", "string?", "Primary service name for this session", false),
                new PropertyDefinition("StartTime", "UnixNano", "Session start time", true),
                new PropertyDefinition("EndTime", "UnixNano", "Session end time (last span)", true),
                // Note: DurationNs is computed in runtime as (long)EndTime.Value - (long)StartTime.Value
                new PropertyDefinition("SpanCount", "int", "Total number of spans in the session", true),
                new PropertyDefinition("GenAiSpanCount", "int", "Number of GenAI spans in the session", true),
                new PropertyDefinition("TotalInputTokens", "long", "Total input tokens across all GenAI spans", true),
                new PropertyDefinition("TotalOutputTokens", "long", "Total output tokens across all GenAI spans", true),
                // Note: TotalTokens is computed in runtime as TotalInputTokens + TotalOutputTokens
                new PropertyDefinition("TraceCount", "int", "Number of distinct trace IDs in the session", true),
                new PropertyDefinition("ErrorCount", "int", "Number of error spans in the session", true),
                // Note: HasErrors is computed in runtime as ErrorCount > 0
                new PropertyDefinition("PrimaryProvider", "string?", "Primary GenAI provider used in the session", false),
                new PropertyDefinition("PrimaryModel", "string?", "Primary model used in the session", false)
            ]),

        // TraceNode - Hierarchical trace tree
        // ALIGNED with src/qyl.protocol/Models/TraceNode.cs
        new(
            "TraceNode",
            "A node in a trace tree. Represents a span with its children",
            true,
            [
                new PropertyDefinition("Span", "SpanRecord", "The span at this node", true),
                new PropertyDefinition("Children", "IReadOnlyList<TraceNode>", "Child nodes (spans that have this span as parent)", true, "[]"),
                new PropertyDefinition("Depth", "int", "Depth in the trace tree (0 for root)", true)
                // Note: IsRoot, HasChildren, DescendantCount are computed properties in runtime
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

    // NOTE: Table definitions are kept for DuckDB DDL generation.
    // The runtime SpanRecord model stores GenAi data as a sub-object,
    // but the DuckDB storage layer may choose to denormalize this.
    // This schema represents the LOGICAL model, not necessarily the physical storage.
    static FrozenSet<TableDefinition> BuildTables() =>
    [
        new(
            "spans",
            "Primary span storage table",
            "SpanRecord",
            "span_id",
            [
                new IndexDefinition("idx_spans_trace_id", ["trace_id"]),
                new IndexDefinition("idx_spans_service", ["service_name"]),
                new IndexDefinition("idx_spans_start_time", ["start_time_unix_nano"], true),
                new IndexDefinition("idx_spans_session", ["session_id"])
            ]),

        new(
            "sessions_agg",
            "Materialized view for session aggregation",
            "SessionSummary",
            IsView: true,
            ViewSql: """
                     SELECT
                         COALESCE(session_id, trace_id) as session_id,
                         service_name as service_name,
                         MIN(start_time_unix_nano) as start_time,
                         MAX(end_time_unix_nano) as end_time,
                         COUNT(*) as span_count,
                         COUNT(*) as gen_ai_span_count,
                         0 as total_input_tokens,
                         0 as total_output_tokens,
                         COUNT(DISTINCT trace_id) as trace_count,
                         COUNT(*) FILTER (WHERE status_code = 2) as error_count,
                         NULL as primary_provider,
                         NULL as primary_model
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
    string Name,
    string Type,
    string Description,
    ImmutableArray<string> AllowedValues = default,
    bool IsDeprecated = false,
    string? ReplacedBy = null);
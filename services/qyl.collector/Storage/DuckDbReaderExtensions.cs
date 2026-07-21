namespace Qyl.Collector.Storage;

internal sealed record SpanBatch(IReadOnlyList<SpanStorageRow> Spans);

[DuckDbTable("spans",
    Indexes = "ProjectId;ProjectId,TraceId;ProjectId,SessionId;ProjectId,StartTimeUnixNano;ProjectId,TraceId,ParentSpanId;ProjectId,ServiceName;ProjectId,GenAiProviderName;ProjectId,GenAiRequestModel;ProjectId,GenAiResponseModel;ProjectId,GenAiOperationName;TraceId;SessionId;StartTimeUnixNano;EndTimeUnixNano;ServiceName;GenAiProviderName;GenAiRequestModel;GenAiResponseModel;GenAiOperationName",
    OnConflict = """
    ON CONFLICT (project_id, trace_id, span_id) DO UPDATE SET
        end_time_unix_nano = EXCLUDED.end_time_unix_nano,
        duration_ns = EXCLUDED.duration_ns,
        status_code = EXCLUDED.status_code,
        service_name = COALESCE(EXCLUDED.service_name, service_name),
        gen_ai_provider_name = EXCLUDED.gen_ai_provider_name,
        gen_ai_operation_name = EXCLUDED.gen_ai_operation_name,
        gen_ai_output_type = EXCLUDED.gen_ai_output_type,
        gen_ai_request_model = EXCLUDED.gen_ai_request_model,
        gen_ai_response_model = EXCLUDED.gen_ai_response_model,
        gen_ai_input_tokens = EXCLUDED.gen_ai_input_tokens,
        gen_ai_output_tokens = EXCLUDED.gen_ai_output_tokens,
        gen_ai_cache_read_input_tokens = EXCLUDED.gen_ai_cache_read_input_tokens,
        gen_ai_cache_creation_input_tokens = EXCLUDED.gen_ai_cache_creation_input_tokens,
        gen_ai_reasoning_tokens = EXCLUDED.gen_ai_reasoning_tokens,
        attributes_json = EXCLUDED.attributes_json,
        resource_json = EXCLUDED.resource_json,
        resource_entity_refs_json = EXCLUDED.resource_entity_refs_json,
        schema_url = EXCLUDED.schema_url,
        status_message = EXCLUDED.status_message,
        events_json = EXCLUDED.events_json,
        links_json = EXCLUDED.links_json
    """)]
internal sealed partial record SpanStorageRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string SpanId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }

    public required string Name { get; init; }
    public required byte Kind { get; init; }
    public required ulong StartTimeUnixNano { get; init; }
    public required ulong EndTimeUnixNano { get; init; }
    public required ulong DurationNs { get; init; }
    public required byte StatusCode { get; init; }

    public string? ServiceName { get; init; }

    public string? GenAiProviderName { get; init; }
    public string? GenAiOperationName { get; init; }
    public string? GenAiOutputType { get; init; }
    public string? GenAiRequestModel { get; init; }
    public string? GenAiResponseModel { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public long? GenAiCacheReadInputTokens { get; init; }
    public long? GenAiCacheCreationInputTokens { get; init; }
    public long? GenAiReasoningTokens { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? AttributesJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceEntityRefsJson { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(256)")]
    public string? SchemaUrl { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR")]
    public string? StatusMessage { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? EventsJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? LinksJson { get; init; }
}

internal sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
}

internal sealed record SessionStatsRow
{
    public long ActiveSessions { get; init; }
    public long TotalSessions { get; init; }
    public double AvgDurationMs { get; init; }
    public long SessionsWithErrors { get; init; }
    public long SessionsWithGenAi { get; init; }
    public double BounceRate { get; init; }
}

[DuckDbTable("logs",
    Indexes = "ProjectId,TimeUnixNano;ProjectId,IngestSequence;ProjectId,TraceId;ProjectId,SessionId;TimeUnixNano",
    OnConflict = """
    ON CONFLICT (project_id, log_id) DO UPDATE SET
        trace_id = EXCLUDED.trace_id,
        span_id = EXCLUDED.span_id,
        event_name = EXCLUDED.event_name,
        session_id = EXCLUDED.session_id,
        time_unix_nano = EXCLUDED.time_unix_nano,
        observed_time_unix_nano = EXCLUDED.observed_time_unix_nano,
        severity_number = EXCLUDED.severity_number,
        severity_text = EXCLUDED.severity_text,
        body = EXCLUDED.body,
        service_name = EXCLUDED.service_name,
        attributes_json = EXCLUDED.attributes_json,
        resource_json = EXCLUDED.resource_json,
        resource_entity_refs_json = EXCLUDED.resource_entity_refs_json
    """)]
internal sealed partial record LogStorageRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string LogId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? EventName { get; init; }
    public string? SessionId { get; init; }

    public required ulong TimeUnixNano { get; init; }
    public ulong? ObservedTimeUnixNano { get; init; }

    public required byte SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public string? Body { get; init; }

    public string? ServiceName { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? AttributesJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceEntityRefsJson { get; init; }

    // Monotonic collector arrival order. Event timestamps are supplied by producers and may arrive
    // late or out of order, so they cannot be used as a lossless live-stream cursor.
    [DuckDbColumn(ExcludeFromInsert = true, DefaultSql = "nextval('logs_ingest_sequence')")]
    public long IngestSequence { get; init; }
}

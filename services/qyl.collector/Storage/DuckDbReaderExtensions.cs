namespace Qyl.Collector.Storage;

internal sealed record SpanBatch(IReadOnlyList<SpanStorageRow> Spans);

[DuckDbTable("spans",
    Indexes = "ProjectId;ProjectId,TraceId;ProjectId,SessionId;ProjectId,StartTimeUnixNano;ProjectId,ServiceName;ProjectId,GenAiProviderName;ProjectId,GenAiRequestModel;ProjectId,GenAiResponseModel;ProjectId,GenAiOperationName;TraceId;SessionId;StartTimeUnixNano;ServiceName;GenAiProviderName;GenAiRequestModel;GenAiResponseModel;GenAiOperationName",
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
    Indexes = "ProjectId,TimeUnixNano;ProjectId,IngestSequence;ProjectId,TraceId;ProjectId,SessionId",
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

[DuckDbTable("metrics",
    Indexes = "ProjectId,TimeUnixNano;ProjectId,MetricName;ProjectId,ServiceName",
    OnConflict = """
    ON CONFLICT (project_id, metric_id) DO UPDATE SET
        metric_name = EXCLUDED.metric_name,
        metric_type = EXCLUDED.metric_type,
        unit = EXCLUDED.unit,
        description = EXCLUDED.description,
        metadata_json = EXCLUDED.metadata_json,
        resource_schema_url = EXCLUDED.resource_schema_url,
        resource_dropped_attributes_count = EXCLUDED.resource_dropped_attributes_count,
        has_instrumentation_scope = EXCLUDED.has_instrumentation_scope,
        scope_name = EXCLUDED.scope_name,
        scope_version = EXCLUDED.scope_version,
        scope_attributes_json = EXCLUDED.scope_attributes_json,
        scope_dropped_attributes_count = EXCLUDED.scope_dropped_attributes_count,
        scope_schema_url = EXCLUDED.scope_schema_url,
        time_unix_nano = EXCLUDED.time_unix_nano,
        start_time_unix_nano = EXCLUDED.start_time_unix_nano,
        int_value = EXCLUDED.int_value,
        double_value = EXCLUDED.double_value,
        flags = EXCLUDED.flags,
        exemplars_json = EXCLUDED.exemplars_json,
        count = EXCLUDED.count,
        sum = EXCLUDED.sum,
        min = EXCLUDED.min,
        max = EXCLUDED.max,
        buckets_json = EXCLUDED.buckets_json,
        exponential_histogram_scale = EXCLUDED.exponential_histogram_scale,
        exponential_histogram_zero_count = EXCLUDED.exponential_histogram_zero_count,
        exponential_histogram_zero_threshold = EXCLUDED.exponential_histogram_zero_threshold,
        exponential_histogram_buckets_json = EXCLUDED.exponential_histogram_buckets_json,
        summary_quantiles_json = EXCLUDED.summary_quantiles_json,
        is_monotonic = EXCLUDED.is_monotonic,
        aggregation_temporality = EXCLUDED.aggregation_temporality,
        service_name = EXCLUDED.service_name,
        attributes_json = EXCLUDED.attributes_json,
        resource_json = EXCLUDED.resource_json,
        resource_entity_refs_json = EXCLUDED.resource_entity_refs_json
    """)]
internal sealed partial record MetricStorageRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string MetricId { get; init; }

    public required string MetricName { get; init; }
    public required byte MetricType { get; init; }
    public string? Unit { get; init; }
    public string? Description { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? MetadataJson { get; init; }
    public string? ResourceSchemaUrl { get; init; }
    public long? ResourceDroppedAttributesCount { get; init; }
    public byte? HasInstrumentationScope { get; init; }
    public string? ScopeName { get; init; }
    public string? ScopeVersion { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ScopeAttributesJson { get; init; }
    public long? ScopeDroppedAttributesCount { get; init; }
    public string? ScopeSchemaUrl { get; init; }

    public required ulong TimeUnixNano { get; init; }
    public ulong? StartTimeUnixNano { get; init; }

    public long? IntValue { get; init; }
    public double? DoubleValue { get; init; }
    public long? Flags { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ExemplarsJson { get; init; }
    public ulong? Count { get; init; }
    public double? Sum { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? BucketsJson { get; init; }
    public int? ExponentialHistogramScale { get; init; }
    public ulong? ExponentialHistogramZeroCount { get; init; }
    public double? ExponentialHistogramZeroThreshold { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ExponentialHistogramBucketsJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? SummaryQuantilesJson { get; init; }
    // 0/1 flag: the storage generator has no boolean mapping, matching Kind/StatusCode style.
    public byte? IsMonotonic { get; init; }
    public byte? AggregationTemporality { get; init; }

    public string? ServiceName { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? AttributesJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceEntityRefsJson { get; init; }
}

[DuckDbTable("profiles",
    Indexes = "ProjectId;ProjectId,ProfileId;ProjectId,TraceId;ProjectId,SpanId;ProjectId,SessionId;ProjectId,TimeUnixNano;ProjectId,ServiceName;ProjectId,SampleType;TraceId;SessionId;TimeUnixNano;ServiceName;SampleType",
    OnConflict = """
    ON CONFLICT (project_id, profile_id) DO UPDATE SET
        trace_id = EXCLUDED.trace_id,
        span_id = EXCLUDED.span_id,
        session_id = EXCLUDED.session_id,
        time_unix_nano = EXCLUDED.time_unix_nano,
        duration_nano = EXCLUDED.duration_nano,
        sample_count = EXCLUDED.sample_count,
        sample_type = EXCLUDED.sample_type,
        sample_unit = EXCLUDED.sample_unit,
        original_payload_format = EXCLUDED.original_payload_format,
        service_name = EXCLUDED.service_name,
        attributes_json = EXCLUDED.attributes_json,
        resource_json = EXCLUDED.resource_json,
        resource_entity_refs_json = EXCLUDED.resource_entity_refs_json,
        schema_url = EXCLUDED.schema_url
    """)]
internal sealed partial record ProfileStorageRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }

    public required ulong TimeUnixNano { get; init; }
    public required ulong DurationNano { get; init; }
    public required int SampleCount { get; init; }

    public string? SampleType { get; init; }
    public string? SampleUnit { get; init; }
    public string? OriginalPayloadFormat { get; init; }

    public string? ServiceName { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? AttributesJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceJson { get; init; }
    [DuckDbColumn(SqlType = "JSON")]
    public string? ResourceEntityRefsJson { get; init; }
    [DuckDbColumn(SqlType = "VARCHAR(256)")]
    public string? SchemaUrl { get; init; }
}

[DuckDbTable("profile_functions",
    OnConflict = """
    ON CONFLICT (project_id, profile_id, ordinal) DO UPDATE SET
        name = EXCLUDED.name,
        system_name = EXCLUDED.system_name,
        filename = EXCLUDED.filename,
        start_line = EXCLUDED.start_line
    """)]
internal sealed partial record ProfileFunctionRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public string? Name { get; init; }
    public string? SystemName { get; init; }
    public string? Filename { get; init; }
    public long? StartLine { get; init; }
}

[DuckDbTable("profile_locations",
    OnConflict = """
    ON CONFLICT (project_id, profile_id, ordinal) DO UPDATE SET
        mapping_ordinal = EXCLUDED.mapping_ordinal,
        address = EXCLUDED.address,
        lines_json = EXCLUDED.lines_json
    """)]
internal sealed partial record ProfileLocationRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public int? MappingOrdinal { get; init; }
    public ulong? Address { get; init; }
    public string? LinesJson { get; init; }
}

[DuckDbTable("profile_mappings",
    OnConflict = """
    ON CONFLICT (project_id, profile_id, ordinal) DO UPDATE SET
        filename = EXCLUDED.filename,
        memory_start = EXCLUDED.memory_start,
        memory_limit = EXCLUDED.memory_limit,
        file_offset = EXCLUDED.file_offset
    """)]
internal sealed partial record ProfileMappingRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public string? Filename { get; init; }
    public ulong? MemoryStart { get; init; }
    public ulong? MemoryLimit { get; init; }
    public ulong? FileOffset { get; init; }
}

[DuckDbTable("profile_samples",
    Indexes = "LinkTraceId",
    OnConflict = """
    ON CONFLICT (project_id, profile_id, ordinal) DO UPDATE SET
        stack_ordinal = EXCLUDED.stack_ordinal,
        link_trace_id = EXCLUDED.link_trace_id,
        link_span_id = EXCLUDED.link_span_id,
        values_json = EXCLUDED.values_json,
        timestamps_json = EXCLUDED.timestamps_json
    """)]
internal sealed partial record ProfileSampleRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public int? StackOrdinal { get; init; }
    public string? LinkTraceId { get; init; }
    public string? LinkSpanId { get; init; }
    public string? ValuesJson { get; init; }
    public string? TimestampsJson { get; init; }
}

[DuckDbTable("profile_stacks",
    OnConflict = """
    ON CONFLICT (project_id, profile_id, ordinal) DO UPDATE SET
        location_ordinals_json = EXCLUDED.location_ordinals_json
    """)]
internal sealed partial record ProfileStackRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public string? LocationOrdinalsJson { get; init; }
}

internal sealed record ProfileDetail
{
    public required ProfileStorageRow Profile { get; init; }
    public required IReadOnlyList<ProfileFunctionRow> Functions { get; init; }
    public required IReadOnlyList<ProfileLocationRow> Locations { get; init; }
    public required IReadOnlyList<ProfileMappingRow> Mappings { get; init; }
    public required IReadOnlyList<ProfileSampleRow> Samples { get; init; }
    public required IReadOnlyList<ProfileStackRow> Stacks { get; init; }
}

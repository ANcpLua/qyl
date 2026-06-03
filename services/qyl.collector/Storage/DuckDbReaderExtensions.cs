namespace Qyl.Collector.Storage;

internal sealed record SpanBatch(IReadOnlyList<SpanStorageRow> Spans);

[DuckDbTable("spans",
    Indexes = "ProjectId;ProjectId,TraceId;ProjectId,SessionId;ProjectId,StartTimeUnixNano;ProjectId,ServiceName;ProjectId,GenAiProviderName;ProjectId,GenAiRequestModel;TraceId;SessionId;StartTimeUnixNano;ServiceName;GenAiProviderName;GenAiRequestModel",
    OnConflict = """
    ON CONFLICT (project_id, trace_id, span_id) DO UPDATE SET
        end_time_unix_nano = EXCLUDED.end_time_unix_nano,
        duration_ns = EXCLUDED.duration_ns,
        status_code = EXCLUDED.status_code,
        service_name = COALESCE(EXCLUDED.service_name, service_name),
        gen_ai_input_tokens = EXCLUDED.gen_ai_input_tokens,
        gen_ai_output_tokens = EXCLUDED.gen_ai_output_tokens,
        gen_ai_cost_usd = EXCLUDED.gen_ai_cost_usd,
        attributes_json = EXCLUDED.attributes_json,
        resource_json = EXCLUDED.resource_json,
        schema_url = EXCLUDED.schema_url
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
    [DuckDbColumn(IsUBigInt = true)] public required ulong StartTimeUnixNano { get; init; }
    [DuckDbColumn(IsUBigInt = true)] public required ulong EndTimeUnixNano { get; init; }
    [DuckDbColumn(IsUBigInt = true)] public required ulong DurationNs { get; init; }
    public required byte StatusCode { get; init; }

    public string? ServiceName { get; init; }

    public string? GenAiProviderName { get; init; }
    public string? GenAiRequestModel { get; init; }
    public string? GenAiResponseModel { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public double? GenAiTemperature { get; init; }
    public string? GenAiStopReason { get; init; }
    public string? GenAiToolName { get; init; }
    public double? GenAiCostUsd { get; init; }

    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(256)")]
    public string? SchemaUrl { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, DefaultSql = "CURRENT_TIMESTAMP")]
    public DateTimeOffset? CreatedAt { get; init; }
}

internal sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
    public long LogCount { get; init; }
    public long DroppedSpanCount { get; init; }
    public long DroppedJobCount { get; init; }
    public double WriteQueueUtilization { get; init; }
    public ulong? OldestSpanTime { get; init; }
    public ulong? NewestSpanTime { get; init; }
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

[DuckDbTable("model_pricing", OnConflict = "ON CONFLICT DO NOTHING")]
internal sealed partial record ModelPricingRow
{
    [JsonPropertyName("provider")]
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string Provider { get; init; }

    [JsonPropertyName("model")]
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string Model { get; init; }

    [JsonPropertyName("input_cost")] public required decimal InputCost { get; init; }

    [JsonPropertyName("output_cost")] public required decimal OutputCost { get; init; }

    [JsonPropertyName("reasoning_cost")] public decimal? ReasoningCost { get; init; }

    [JsonPropertyName("cache_read_cost")] public decimal? CacheReadCost { get; init; }

    [JsonPropertyName("cache_write_cost")] public decimal? CacheWriteCost { get; init; }

    [JsonIgnore]
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public DateTimeOffset ValidFrom { get; init; }

    [JsonIgnore]
    public DateTimeOffset? ValidTo { get; init; }
}

[DuckDbTable("logs", Indexes = "ProjectId,TimeUnixNano;ProjectId,TraceId;ProjectId,SessionId")]
internal sealed partial record LogStorageRow
{
    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }
    public required string LogId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }

    [DuckDbColumn(IsUBigInt = true)]
    public required ulong TimeUnixNano { get; init; }
    [DuckDbColumn(IsUBigInt = true)]
    public ulong? ObservedTimeUnixNano { get; init; }

    public required byte SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public string? Body { get; init; }

    public string? ServiceName { get; init; }
    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, DefaultSql = "CURRENT_TIMESTAMP")]
    public DateTimeOffset? CreatedAt { get; init; }
}

[DuckDbTable("profiles",
    Indexes = "ProjectId;ProjectId,ProfileId;ProjectId,TraceId;ProjectId,SpanId;ProjectId,SessionId;ProjectId,TimeUnixNano;ProjectId,ServiceName;ProjectId,SampleType;TraceId;SessionId;TimeUnixNano;ServiceName;SampleType")]
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

    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }
    [DuckDbColumn(SqlType = "VARCHAR(256)")]
    public string? SchemaUrl { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, DefaultSql = "CURRENT_TIMESTAMP")]
    public DateTimeOffset? CreatedAt { get; init; }
}

[DuckDbTable("profile_functions")]
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

[DuckDbTable("profile_locations")]
internal sealed partial record ProfileLocationRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public int? MappingOrdinal { get; init; }
    [DuckDbColumn(IsUBigInt = true)]
    public ulong? Address { get; init; }
    public string? LinesJson { get; init; }
}

[DuckDbTable("profile_mappings")]
internal sealed partial record ProfileMappingRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ProfileId { get; init; }
    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required int Ordinal { get; init; }
    public string? Filename { get; init; }
    [DuckDbColumn(IsUBigInt = true)]
    public ulong? MemoryStart { get; init; }
    [DuckDbColumn(IsUBigInt = true)]
    public ulong? MemoryLimit { get; init; }
    [DuckDbColumn(IsUBigInt = true)]
    public ulong? FileOffset { get; init; }
}

[DuckDbTable("profile_samples", Indexes = "LinkTraceId")]
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

[DuckDbTable("profile_stacks")]
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

internal sealed record ProfileConversionResult
{
    public required ProfileStorageRow Profile { get; init; }
    public required List<ProfileFunctionRow> Functions { get; init; }
    public required List<ProfileLocationRow> Locations { get; init; }
    public required List<ProfileMappingRow> Mappings { get; init; }
    public required List<ProfileSampleRow> Samples { get; init; }
    public required List<ProfileStackRow> Stacks { get; init; }
}

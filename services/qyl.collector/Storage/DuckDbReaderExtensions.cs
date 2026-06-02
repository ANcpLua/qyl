namespace Qyl.Collector.Storage;

public sealed record SpanBatch(IReadOnlyList<SpanStorageRow> Spans);

[DuckDbTable("spans")]
public sealed partial record SpanStorageRow
{
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }

    public required string Name { get; init; }
    public required byte Kind { get; init; }
    [DuckDbColumn(IsUBigInt = true)] public required ulong StartTimeUnixNano { get; init; }
    [DuckDbColumn(IsUBigInt = true)] public required ulong EndTimeUnixNano { get; init; }
    [DuckDbColumn(IsUBigInt = true)] public required ulong DurationNs { get; init; }
    public required byte StatusCode { get; init; }
    public string? StatusMessage { get; init; }

    public string? ServiceName { get; init; }

    public string? GenAiProviderName { get; init; }
    public string? GenAiRequestModel { get; init; }
    public string? GenAiResponseModel { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public double? GenAiTemperature { get; init; }
    public string? GenAiStopReason { get; init; }
    public string? GenAiToolName { get; init; }
    public string? GenAiToolCallId { get; init; }
    public double? GenAiCostUsd { get; init; }

    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }

    public string? BaggageJson { get; init; }

    public string? SchemaUrl { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true)]
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
    public long LogCount { get; init; }
    public ulong? OldestSpanTime { get; init; }
    public ulong? NewestSpanTime { get; init; }
}

public sealed record LogStorageRow
{
    public required string LogId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }

    public required ulong TimeUnixNano { get; init; }
    public ulong? ObservedTimeUnixNano { get; init; }

    public required byte SeverityNumber { get; init; }
    public string? SeverityText { get; init; }
    public string? Body { get; init; }

    public string? ServiceName { get; init; }
    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }
    public string? SourceFile { get; init; }
    public int? SourceLine { get; init; }
    public int? SourceColumn { get; init; }
    public string? SourceMethod { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

[DuckDbTable("profiles")]
public sealed partial record ProfileStorageRow
{
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
    public string? ProfileFrameType { get; init; }

    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }
    public string? ProfileDataJson { get; init; }
    public string? SchemaUrl { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true)]
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed record ProfileFunctionRow
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public string? Name { get; init; }
    public string? SystemName { get; init; }
    public string? Filename { get; init; }
    public long? StartLine { get; init; }
}

public sealed record ProfileLocationRow
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public int? MappingOrdinal { get; init; }
    public ulong? Address { get; init; }
    public string? LinesJson { get; init; }
}

public sealed record ProfileMappingRow
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public string? Filename { get; init; }
    public ulong? MemoryStart { get; init; }
    public ulong? MemoryLimit { get; init; }
    public ulong? FileOffset { get; init; }
}

public sealed record ProfileSampleRow
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public int? StackOrdinal { get; init; }
    public string? LinkTraceId { get; init; }
    public string? LinkSpanId { get; init; }
    public string? ValuesJson { get; init; }
    public string? TimestampsJson { get; init; }
}

public sealed record ProfileStackRow
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public string? LocationOrdinalsJson { get; init; }
}

public sealed record ProfileDetail
{
    public required ProfileStorageRow Profile { get; init; }
    public required IReadOnlyList<ProfileFunctionRow> Functions { get; init; }
    public required IReadOnlyList<ProfileLocationRow> Locations { get; init; }
    public required IReadOnlyList<ProfileMappingRow> Mappings { get; init; }
    public required IReadOnlyList<ProfileSampleRow> Samples { get; init; }
    public required IReadOnlyList<ProfileStackRow> Stacks { get; init; }
}

public sealed record ProfileConversionResult
{
    public required ProfileStorageRow Profile { get; init; }
    public required List<ProfileFunctionRow> Functions { get; init; }
    public required List<ProfileLocationRow> Locations { get; init; }
    public required List<ProfileMappingRow> Mappings { get; init; }
    public required List<ProfileSampleRow> Samples { get; init; }
    public required List<ProfileStackRow> Stacks { get; init; }
}

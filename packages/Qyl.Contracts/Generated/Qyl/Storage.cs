#nullable enable

namespace Qyl.Storage;

public sealed class SpanRecord
{
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }
    public required string Name { get; init; }
    public required Qyl.OTel.Enums.SpanKind Kind { get; init; }
    public required long StartTimeUnixNano { get; init; }
    public required long EndTimeUnixNano { get; init; }
    public required long DurationNs { get; init; }
    public required Qyl.OTel.Enums.SpanStatusCode StatusCode { get; init; }
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
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed class SessionSummary
{
    public required string SessionId { get; init; }
    public required long StartTime { get; init; }
    public required long EndTime { get; init; }
    public required long SpanCount { get; init; }
    public required long ErrorCount { get; init; }
    public required long TotalInputTokens { get; init; }
    public required long TotalOutputTokens { get; init; }
    public required double TotalCostUsd { get; init; }
    public string? ServiceName { get; init; }
    public string? GenAiProviderName { get; init; }
    public string? GenAiModel { get; init; }
}

public sealed class LogRecordStorage
{
    public required string LogId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }
    public required long TimeUnixNano { get; init; }
    public long? ObservedTimeUnixNano { get; init; }
    public required Qyl.OTel.Enums.SeverityNumber SeverityNumber { get; init; }
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

public sealed class TraceNode
{
    public required Qyl.Storage.SpanRecord Span { get; init; }
    public required IReadOnlyList<Qyl.Storage.TraceNode> Children { get; init; }
    public required int Depth { get; init; }
}

public sealed class GenAiSpanData
{
    public string? ProviderName { get; init; }
    public string? RequestModel { get; init; }
    public string? ResponseModel { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public double? Temperature { get; init; }
    public string? StopReason { get; init; }
    public string? ToolName { get; init; }
    public string? ToolCallId { get; init; }
    public double? CostUsd { get; init; }
}

public sealed class ProfileRecord
{
    public required string ProfileId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }
    public required long TimeUnixNano { get; init; }
    public required long DurationNano { get; init; }
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
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed class ProfileFunctionRecord
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public string? Name { get; init; }
    public string? SystemName { get; init; }
    public string? Filename { get; init; }
    public long? StartLine { get; init; }
}

public sealed class ProfileLocationRecord
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public int? MappingOrdinal { get; init; }
    public long? Address { get; init; }
    public string? LinesJson { get; init; }
}

public sealed class ProfileMappingRecord
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public string? Filename { get; init; }
    public long? MemoryStart { get; init; }
    public long? MemoryLimit { get; init; }
    public long? FileOffset { get; init; }
}

public sealed class ProfileSampleRecord
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public int? StackOrdinal { get; init; }
    public string? LinkTraceId { get; init; }
    public string? LinkSpanId { get; init; }
    public string? ValuesJson { get; init; }
    public string? TimestampsJson { get; init; }
}

public sealed class ProfileStackRecord
{
    public required string ProfileId { get; init; }
    public required int Ordinal { get; init; }
    public string? LocationOrdinalsJson { get; init; }
}

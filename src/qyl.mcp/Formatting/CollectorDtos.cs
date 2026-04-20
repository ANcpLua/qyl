namespace qyl.mcp.Formatting;

using System.Text.Json.Serialization;

internal sealed record TraceSummaryDto(
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("duration_ms")]
    double DurationMs,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("root_span")]
    string RootSpan,
    [property: JsonPropertyName("service")]
    string Service,
    [property: JsonPropertyName("span_count")]
    int SpanCount,
    [property: JsonPropertyName("start_time")]
    string StartTime);

internal sealed record SpanDetailDto(
    [property: JsonPropertyName("span_id")]
    string SpanId,
    [property: JsonPropertyName("trace_id")]
    string TraceId,
    [property: JsonPropertyName("parent_span_id")]
    string? ParentSpanId,
    [property: JsonPropertyName("span_name")]
    string SpanName,
    [property: JsonPropertyName("service_name")]
    string ServiceName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("start_time")]
    string StartTime,
    [property: JsonPropertyName("end_time")]
    string EndTime,
    [property: JsonPropertyName("duration_ms")]
    double DurationMs,
    [property: JsonPropertyName("attributes")]
    Dictionary<string, string>? Attributes = null);

internal sealed record LogSummaryDto(
    [property: JsonPropertyName("log_id")] string LogId,
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("severity")]
    string Severity,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("service_name")]
    string ServiceName);

internal sealed record LogDetailDto(
    [property: JsonPropertyName("log_id")] string LogId,
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("severity")]
    string Severity,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("service_name")]
    string ServiceName,
    [property: JsonPropertyName("trace_id")]
    string? TraceId = null,
    [property: JsonPropertyName("span_id")]
    string? SpanId = null,
    [property: JsonPropertyName("attributes")]
    Dictionary<string, string>? Attributes = null);

internal sealed record SessionSummaryDto(
    [property: JsonPropertyName("session_id")]
    string SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")]
    string CreatedAt,
    [property: JsonPropertyName("span_count")]
    int SpanCount,
    [property: JsonPropertyName("service_name")]
    string ServiceName);

internal sealed record SessionDetailDto(
    [property: JsonPropertyName("session_id")]
    string SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")]
    string CreatedAt,
    [property: JsonPropertyName("span_count")]
    int SpanCount,
    [property: JsonPropertyName("service_name")]
    string ServiceName,
    [property: JsonPropertyName("traces")] List<TraceSummaryDto>? Traces = null);

internal sealed record MetricInfoDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")]
    string? Description = null,
    [property: JsonPropertyName("unit")] string? Unit = null);

internal sealed record TimeSeriesDto(
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("points")] List<DataPointDto> Points);

internal sealed record DataPointDto(
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("value")] double Value);

internal sealed record ServiceInfoDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("instance_count")]
    int InstanceCount,
    [property: JsonPropertyName("last_seen")]
    string LastSeen);

internal sealed record ServiceMapDto(
    [property: JsonPropertyName("nodes")] List<ServiceNodeDto> Nodes,
    [property: JsonPropertyName("edges")] List<ServiceEdgeDto> Edges);

internal sealed record ServiceNodeDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type);

internal sealed record ServiceEdgeDto(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("call_count")]
    int CallCount);

internal sealed record ProjectInfoDto(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")]
    string? Description = null,
    [property: JsonPropertyName("retention_days")]
    int? RetentionDays = null);

internal sealed record AnnotationRequestDto(
    [property: JsonPropertyName("note")] string Note,
    [property: JsonPropertyName("tags")] List<string>? Tags = null);

internal sealed record AnnotationResponseDto(
    [property: JsonPropertyName("resource_id")]
    string ResourceId,
    [property: JsonPropertyName("message")]
    string Message);

internal sealed record ApiKeyResponseDto(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("prefix")] string Prefix,
    [property: JsonPropertyName("name")] string Name);

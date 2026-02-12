using System.Text.Json;
using System.Text.Json.Serialization;

namespace qyl.watch;

/// <summary>
///     Batch of spans from SSE stream. Matches collector's SpanBatch shape (camelCase serialization).
/// </summary>
internal sealed record SpanBatchDto
{
    [JsonPropertyName("spans")]
    public IReadOnlyList<SpanDto>? Spans { get; init; }
}

/// <summary>
///     Flattened span from collector storage. Matches SpanStorageRow JSON serialization (camelCase).
/// </summary>
internal sealed record SpanDto
{
    [JsonPropertyName("spanId")]
    public string? SpanId { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("kind")]
    public byte Kind { get; init; }

    [JsonPropertyName("startTimeUnixNano")]
    public ulong StartTimeUnixNano { get; init; }

    [JsonPropertyName("endTimeUnixNano")]
    public ulong EndTimeUnixNano { get; init; }

    [JsonPropertyName("durationNs")]
    public ulong DurationNs { get; init; }

    [JsonPropertyName("statusCode")]
    public byte StatusCode { get; init; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }

    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    [JsonPropertyName("genAiProviderName")]
    public string? GenAiProviderName { get; init; }

    [JsonPropertyName("genAiRequestModel")]
    public string? GenAiRequestModel { get; init; }

    [JsonPropertyName("genAiResponseModel")]
    public string? GenAiResponseModel { get; init; }

    [JsonPropertyName("genAiInputTokens")]
    public long? GenAiInputTokens { get; init; }

    [JsonPropertyName("genAiOutputTokens")]
    public long? GenAiOutputTokens { get; init; }

    [JsonPropertyName("genAiCostUsd")]
    public double? GenAiCostUsd { get; init; }

    [JsonPropertyName("genAiToolName")]
    public string? GenAiToolName { get; init; }

    [JsonPropertyName("attributesJson")]
    public string? AttributesJson { get; init; }

    /// <summary>Duration in milliseconds, derived from DurationNs.</summary>
    public double DurationMs => DurationNs / 1_000_000.0;

    /// <summary>Whether this is a GenAI span.</summary>
    public bool IsGenAi => GenAiProviderName is not null || GenAiRequestModel is not null;

    /// <summary>Whether this span has an error status (StatusCode 2 = ERROR).</summary>
    public bool IsError => StatusCode == 2;

    /// <summary>HTTP status code extracted from attributes, if present.</summary>
    public int? HttpStatusCode => TryGetIntAttribute("http.response.status_code") ?? TryGetIntAttribute("http.status_code");

    /// <summary>HTTP method extracted from attributes, if present.</summary>
    public string? HttpMethod => TryGetStringAttribute("http.request.method") ?? TryGetStringAttribute("http.method");

    /// <summary>HTTP route/path extracted from attributes, if present.</summary>
    public string? HttpRoute => TryGetStringAttribute("http.route") ?? TryGetStringAttribute("url.path");

    /// <summary>Database system from attributes, if present.</summary>
    public string? DbSystem => TryGetStringAttribute("db.system.name") ?? TryGetStringAttribute("db.system");

    /// <summary>Database operation from attributes, if present.</summary>
    public string? DbOperation => TryGetStringAttribute("db.operation.name") ?? TryGetStringAttribute("db.operation");

    private string? TryGetStringAttribute(string key)
    {
        if (AttributesJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(AttributesJson);
            if (doc.RootElement.TryGetProperty(key, out var val))
                return val.GetString();
        }
        catch
        {
            // Malformed JSON
        }
        return null;
    }

    private int? TryGetIntAttribute(string key)
    {
        if (AttributesJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(AttributesJson);
            if (doc.RootElement.TryGetProperty(key, out var val) && val.TryGetInt32(out var result))
                return result;
        }
        catch
        {
            // Malformed JSON
        }
        return null;
    }
}

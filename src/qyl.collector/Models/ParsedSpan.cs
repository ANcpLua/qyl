// =============================================================================
// qyl Collector Models - ParsedSpan
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.38.0
// =============================================================================

using qyl.collector.Primitives;

namespace qyl.collector.Models;

/// <summary>
///     Parsed span data with promoted GenAI attributes.
/// </summary>
public sealed class ParsedSpan
{
    public TraceId TraceId { get; set; }
    public SpanId SpanId { get; set; }
    public SpanId ParentSpanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpanKind Kind { get; set; }
    public UnixNano StartTime { get; set; }
    public UnixNano EndTime { get; set; }
    public StatusCode Status { get; set; }
    public string? StatusMessage { get; set; }

    // GenAI-specific extracted attributes (OTel 1.38)
    public string? ProviderName { get; set; }
    public string? RequestModel { get; set; }
    public string? ResponseModel { get; set; }
    public string? OperationName { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public double? Temperature { get; set; }
    public long? MaxTokens { get; set; }

    // Session tracking
    public SessionId? SessionId { get; set; }

    // Raw attributes for non-promoted fields
    public List<KeyValuePair<string, object?>>? Attributes { get; set; }

    public TimeSpan Duration => (EndTime - StartTime).ToTimeSpan();
    public long TotalTokens => InputTokens + OutputTokens;
    public bool IsGenAiSpan => ProviderName is not null || RequestModel is not null;
}

/// <summary>
///     OTel span kind enumeration.
/// </summary>
public enum SpanKind : byte
{
    Unspecified = 0,
    Internal = 1,
    Server = 2,
    Client = 3,
    Producer = 4,
    Consumer = 5
}

/// <summary>
///     OTel span status code enumeration.
/// </summary>
public enum StatusCode : byte
{
    Unset = 0,
    Ok = 1,
    Error = 2
}

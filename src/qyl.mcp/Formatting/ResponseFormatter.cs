// src/qyl.mcp/Formatting/ResponseFormatter.cs

using System.Text.Json;
using System.Text.Json.Serialization;
using Qyl.Contracts.Models;

namespace qyl.mcp.Formatting;

/// <summary>
///     Formats collector API responses for AI consumption.
///     Rules: summary before detail, backtick IDs, suggest next tools,
///     paginate by default (25 items), 20k token budget per response.
///     Structured responses separate facts/analysis/actions per spec section 5.2.
/// </summary>
public static class ResponseFormatter
{
    /// <summary>
    ///     Formats a paged result set as a Markdown list with navigation hints.
    /// </summary>
    /// <param name="result">Paged result from the collector API.</param>
    /// <param name="title">Heading for the result section.</param>
    /// <param name="rowFormatter">Converts each item to a Markdown row.</param>
    /// <param name="searchToolName">Tool name to suggest for narrowing the query.</param>
    /// <param name="detailToolName">Tool name to suggest for viewing item details.</param>
    /// <param name="detailIdParam">Parameter name for the detail tool's ID argument.</param>
    public static string FormatPagedList<T>(
        PagedResult<T> result,
        string title,
        Func<T, string> rowFormatter,
        string searchToolName,
        string detailToolName,
        string detailIdParam)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {title} ({result.Items.Count} of {result.TotalCount})");
        sb.AppendLine();

        foreach (var item in result.Items)
            sb.AppendLine(rowFormatter(item));

        sb.AppendLine();
        sb.AppendLine("## Next steps");

        if (result.Items.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- Use `{detailToolName}({detailIdParam}: '<id>')` for full details");
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- Refine with `{searchToolName}(query: '<narrower query>')`");

        if (result.Cursor is not null)
        {
            var remaining = result.TotalCount - result.Items.Count;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- {remaining} more results available — pass `cursor: '{result.Cursor}'` for next page");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Formats a detail view as a Markdown heading followed by labeled fields.
    /// </summary>
    /// <param name="title">Heading for the detail section.</param>
    /// <param name="fields">Label-value pairs to render; null values are omitted.</param>
    public static string FormatDetail(string title, IEnumerable<(string Label, string? Value)> fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {title}");
        sb.AppendLine();

        foreach (var (label, value) in fields)
        {
            if (value is not null)
                sb.AppendLine(CultureInfo.InvariantCulture, $"**{label}:** {value}");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Returns a "Not Found" message directing the caller to use a search tool.
    /// </summary>
    /// <param name="resourceType">The type of resource that was not found.</param>
    public static string FormatNotFound(string resourceType) =>
        $"**Not Found:** {resourceType} not found. Use the corresponding search tool to find valid IDs.";

    /// <summary>
    ///     Returns a formatted error message.
    /// </summary>
    /// <param name="message">Error description to display.</param>
    public static string FormatError(string message) =>
        $"**Error:** {message}";

    /// <summary>
    ///     Returns a formatted success message.
    /// </summary>
    /// <param name="message">Success description to display.</param>
    public static string FormatSuccess(string message) =>
        $"**Done:** {message}";

    /// <summary>
    ///     Serializes a value to JSON using System.Text.Json.
    /// </summary>
    /// <param name="value">Object to serialize.</param>
    /// <param name="options">Optional serializer options; defaults to <see cref="JsonSerializerOptions.Default" />.</param>
    public static string FormatJson<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(value, options ?? JsonSerializerOptions.Default);

    /// <summary>
    ///     Formats a structured response with facts/analysis/actions separation
    ///     per spec section 5.2 and tool contract section 8.
    /// </summary>
    public static string FormatStructured(StructuredResponse response) =>
        JsonSerializer.Serialize(response, StructuredResponseJsonContext.Default.StructuredResponse);

    /// <summary>
    ///     Formats a tool call trace for debugging use_qyl investigations.
    /// </summary>
    /// <param name="toolCallCounts">Map of tool name to invocation count.</param>
    /// <param name="totalCalls">Total tool calls made so far.</param>
    /// <param name="maxCalls">Maximum tool call budget for the investigation.</param>
    public static string FormatToolCallTrace(
        IReadOnlyDictionary<string, int> toolCallCounts,
        int totalCalls,
        int maxCalls)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Investigation Trace ({totalCalls}/{maxCalls} tool calls)");
        sb.AppendLine();

        foreach (var (tool, count) in toolCallCounts.OrderByDescending(static kv => kv.Value))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `{tool}`: {count} call{(count != 1 ? "s" : "")}");

        return sb.ToString();
    }
}

/// <summary>
///     Structured response envelope per spec section 8 tool contract.
///     Separates raw telemetry facts from AI analysis and proposed actions.
/// </summary>
public sealed class StructuredResponse
{
    /// <summary>Raw telemetry data returned by the tool.</summary>
    [JsonPropertyName("facts")] public required object Facts { get; init; }

    /// <summary>AI-generated analysis derived from the facts.</summary>
    [JsonPropertyName("analysis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Analysis { get; init; }

    /// <summary>Proposed next-step tool calls the caller can execute.</summary>
    [JsonPropertyName("actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SuggestedAction>? Actions { get; init; }

    /// <summary>Cursor-based pagination metadata for multi-page results.</summary>
    [JsonPropertyName("pagination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationInfo? Pagination { get; init; }

    /// <summary>Provenance information linking facts to their telemetry sources.</summary>
    [JsonPropertyName("evidence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EvidenceInfo? Evidence { get; init; }
}

/// <summary>
///     Represents a tool call the AI can execute as a follow-up action.
/// </summary>
public sealed class SuggestedAction
{
    /// <summary>MCP tool name to invoke.</summary>
    [JsonPropertyName("tool")] public required string Tool { get; init; }

    /// <summary>Human-readable explanation of what this action accomplishes.</summary>
    [JsonPropertyName("description")] public required string Description { get; init; }

    /// <summary>Parameter key-value pairs to pass to the tool.</summary>
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Parameters { get; init; }
}

/// <summary>
///     Cursor-based pagination state for multi-page result sets.
/// </summary>
public sealed class PaginationInfo
{
    /// <summary>Opaque cursor to pass for retrieving the next page.</summary>
    [JsonPropertyName("cursor")] public string? Cursor { get; init; }

    /// <summary>Indicates whether additional pages are available.</summary>
    [JsonPropertyName("has_more")] public bool HasMore { get; init; }
}

/// <summary>
///     Provenance metadata linking structured response facts to their telemetry sources.
/// </summary>
public sealed class EvidenceInfo
{
    /// <summary>Identifiers of the telemetry sources that produced the facts.</summary>
    [JsonPropertyName("sources")] public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Time window the evidence spans.</summary>
    [JsonPropertyName("time_range")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeRangeInfo? TimeRange { get; init; }
}

/// <summary>
///     Represents a time window with ISO 8601 from/to boundaries.
/// </summary>
public sealed class TimeRangeInfo
{
    /// <summary>Start of the time range (ISO 8601).</summary>
    [JsonPropertyName("from")] public required string From { get; init; }

    /// <summary>End of the time range (ISO 8601).</summary>
    [JsonPropertyName("to")] public required string To { get; init; }
}

[JsonSerializable(typeof(StructuredResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class StructuredResponseJsonContext : JsonSerializerContext;

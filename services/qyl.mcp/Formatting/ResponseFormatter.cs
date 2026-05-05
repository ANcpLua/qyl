
using System.Text.Json;
using System.Text.Json.Serialization;
using Qyl.Contracts.Models;

namespace qyl.mcp.Formatting;

public static class ResponseFormatter
{
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

    public static string FormatNotFound(string resourceType) =>
        $"**Not Found:** {resourceType} not found. Use the corresponding search tool to find valid IDs.";

    public static string FormatError(string message) =>
        $"**Error:** {message}";

    public static string FormatSuccess(string message) =>
        $"**Done:** {message}";

    public static string FormatJson<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(value, options ?? JsonSerializerOptions.Default);

    public static string FormatStructured(StructuredResponse response) =>
        JsonSerializer.Serialize(response, StructuredResponseJsonContext.Default.StructuredResponse);

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

public sealed class StructuredResponse
{
    [JsonPropertyName("facts")]
    public required object Facts { get; init; }

    [JsonPropertyName("analysis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Analysis { get; init; }

    [JsonPropertyName("actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SuggestedAction>? Actions { get; init; }

    [JsonPropertyName("pagination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationInfo? Pagination { get; init; }

    [JsonPropertyName("evidence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EvidenceInfo? Evidence { get; init; }
}

public sealed class SuggestedAction
{
    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Parameters { get; init; }
}

public sealed class PaginationInfo
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

public sealed class EvidenceInfo
{
    [JsonPropertyName("sources")]
    public IReadOnlyList<string> Sources { get; init; } = [];

    [JsonPropertyName("time_range")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeRangeInfo? TimeRange { get; init; }
}

public sealed class TimeRangeInfo
{
    [JsonPropertyName("from")]
    public required string From { get; init; }

    [JsonPropertyName("to")]
    public required string To { get; init; }
}

[JsonSerializable(typeof(StructuredResponse))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class StructuredResponseJsonContext : JsonSerializerContext;

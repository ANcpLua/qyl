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

    /// <summary>
    ///     Formats a structured response with facts/analysis/actions separation
    ///     per spec section 5.2 and tool contract section 8.
    /// </summary>
    public static string FormatStructured(StructuredResponse response) =>
        JsonSerializer.Serialize(response, StructuredResponseJsonContext.Default.StructuredResponse);

}

/// <summary>
///     Structured response envelope per spec section 8 tool contract.
///     Separates raw telemetry facts from AI analysis and proposed actions.
/// </summary>
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

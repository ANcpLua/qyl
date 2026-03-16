// src/qyl.mcp/Formatting/ResponseFormatter.cs

using System.Text.Json;
using Qyl.Contracts.Models;

namespace qyl.mcp.Formatting;

/// <summary>
///     Formats collector API responses as markdown for AI consumption.
///     Rules: summary before detail, backtick IDs, suggest next tools,
///     paginate by default (25 items), 20k token budget per response.
/// </summary>
public static class ResponseFormatter
{
    private const int MaxTokenBudget = 20_000;

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
}

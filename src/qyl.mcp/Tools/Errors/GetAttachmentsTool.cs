
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Errors;

/// <summary>
/// MCP tool that lists attachments (screenshots, logs, minidumps) for an error event.
/// </summary>
/// <param name="client">The HTTP client used to communicate with the qyl API.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed class GetAttachmentsTool(HttpClient client)
{
    /// <summary>
    /// Retrieves the list of attachments for an error event, optionally scoped to a specific event.
    /// </summary>
    /// <param name="issueId">The error issue ID to retrieve attachments for.</param>
    /// <param name="eventId">Optional event ID to scope attachments to a specific event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A formatted markdown table of attachments with download links.</returns>
    [McpServerTool(Name = "get_attachments", Title = "Get Event Attachments",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List attachments (screenshots, logs, minidumps) for an error event.")]
    public async Task<string> GetAttachmentsAsync(
        [Description("The error issue ID to retrieve attachments for")]
        string issueId,
        [Description("Optional event ID to scope attachments to a specific event")]
        string? eventId = null,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/attachments";
        if (!string.IsNullOrEmpty(eventId))
            url += $"?eventId={Uri.EscapeDataString(eventId)}";

        var response = await client.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();

        var attachments = await response.Content
            .ReadFromJsonAsync<List<AttachmentDto>>(ct).ConfigureAwait(false);

        if (attachments is null or { Count: 0 })
        {
            var scope = eventId is not null ? $" for event `{eventId}`" : "";
            return $"No attachments found for issue `{issueId}`{scope}.";
        }

        var fields = new List<(string Label, string? Value)>
        {
            ("Issue ID", $"`{issueId}`"),
            ("Event ID", eventId is not null ? $"`{eventId}`" : null),
            ("Attachment Count", attachments.Count.ToString(CultureInfo.InvariantCulture))
        };

        var result = new StringBuilder(
            ResponseFormatter.FormatDetail("Event Attachments", fields));

        result.AppendLine();
        result.AppendLine("| # | Name | Type | Size | Download |");
        result.AppendLine("|---|------|------|------|----------|");

        for (var i = 0; i < attachments.Count; i++)
        {
            var a = attachments[i];
            result.AppendLine(CultureInfo.InvariantCulture,
                $"| {i + 1} | {a.Name} | {a.MimeType} | {FormatSize(a.Size)} | [link]({a.DownloadUrl}) |");
        }

        return result.ToString();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };
}

internal sealed record AttachmentDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mime_type")]
    string MimeType,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("download_url")]
    string DownloadUrl);

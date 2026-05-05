using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

/// <summary>
///     Snoozes an error issue for a specified duration, temporarily suppressing it from triage views.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class SnoozeErrorTool(HttpClient client)
{
    private static readonly HashSet<string> s_validDurations = ["1h", "6h", "24h", "7d", "30d"];

    [McpServerTool(
        Name = "snooze_error",
        Title = "Snooze Error Issue",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    public async partial Task<string> SnoozeError(
        string issueId,
        string duration,
        string? reason = null,
        CancellationToken ct = default)
    {
        if (!s_validDurations.Contains(duration))
            throw new QylQueryException($"Invalid duration '{duration}'. Must be one of: 1h, 6h, 24h, 7d, 30d.");

        var body = new SnoozeErrorRequestDto(duration, reason);
        using var response = await client.PostAsJsonAsync(
                $"/api/v1/mcp/errors/{Uri.EscapeDataString(issueId)}/snooze", body, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound)
            throw new QylNotFoundException("Error issue");

        response.EnsureSuccessStatusCode();
        return ResponseFormatter.FormatSuccess($"Error issue `{issueId}` snoozed for `{duration}`.");
    }
}

internal sealed record SnoozeErrorRequestDto(
    [property: JsonPropertyName("duration")]
    string Duration,
    [property: JsonPropertyName("reason")] string? Reason = null);

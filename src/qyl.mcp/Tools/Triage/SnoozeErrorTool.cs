
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Errors;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Triage;

/// <summary>
/// Snoozes an error issue for a specified duration, temporarily suppressing it from triage views.
/// </summary>
/// <param name="client">The HTTP client for backend API communication.</param>
[McpServerToolType]
public sealed class SnoozeErrorTool(HttpClient client)
{
    private static readonly HashSet<string> ValidDurations = ["1h", "6h", "24h", "7d", "30d"];

    [McpServerTool(
        Name = "snooze_error",
        Title = "Snooze Error Issue",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Snooze (temporarily ignore) an error issue for a specified duration. Valid durations: 1h, 6h, 24h, 7d, 30d.")]
    /// <summary>
    /// Validates the duration and snoozes the specified error issue with an optional reason.
    /// </summary>
    /// <param name="issueId">The error issue ID to snooze.</param>
    /// <param name="duration">Snooze duration: 1h, 6h, 24h, 7d, or 30d.</param>
    /// <param name="reason">Optional reason for snoozing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A success confirmation message with the snooze duration.</returns>
    public async Task<string> SnoozeError(
        [Description("Error issue ID to snooze")] string issueId,
        [Description("Snooze duration: 1h, 6h, 24h, 7d, or 30d")]
        string duration,
        [Description("Optional reason for snoozing")]
        string? reason = null,
        CancellationToken ct = default)
    {
        if (!ValidDurations.Contains(duration))
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
    [property: JsonPropertyName("reason")]
    string? Reason = null);

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Agents;

/// <summary>
///     Agent provider that proxies to the collector's /api/v1/copilot/chat endpoint.
///     Sends a ChatRequest with the system prompt and streams SSE responses back.
/// </summary>
internal sealed partial class HttpAgentProvider(HttpClient client, ILogger<HttpAgentProvider> logger) : IAgentProvider
{
    public bool IsAvailable { get; private set; } = true;

    public async IAsyncEnumerable<AgentStreamChunk> InvestigateAsync(
        string question,
        string systemPrompt,
        string? context = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new AgentChatRequest
        {
            Prompt = question,
            SystemPrompt = systemPrompt,
            Context = context is not null ? new AgentChatContext { AdditionalContext = context } : null
        };

        // Send request to collector — collect error outside catch to avoid yield-in-catch
        HttpResponseMessage? response = null;
        string? connectionError = null;
        try
        {
            response = await client.PostAsJsonAsync(
                "/api/v1/copilot/chat",
                request,
                AgentJsonContext.Default.AgentChatRequest,
                ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogConnectionFailed(ex);
            IsAvailable = false;
            connectionError = $"Agent unavailable: {ex.Message}";
        }

        if (connectionError is not null)
        {
            yield return new AgentStreamChunk { Error = connectionError };
            yield break;
        }

        if (!response!.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            LogAgentError(response.StatusCode, body);

            if (response.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable)
                IsAvailable = false;

            yield return new AgentStreamChunk { Error = $"Agent error ({response.StatusCode}): {body}" };
            yield break;
        }

        // Parse SSE stream — same pattern as CopilotTools.CopilotChatAsync
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (!line.StartsWithOrdinal("data: "))
                continue;

            var json = line["data: ".Length..];
            AgentStreamUpdateDto? update;
            try
            {
                update = JsonSerializer.Deserialize(json, AgentJsonContext.Default.AgentStreamUpdateDto);
            }
            catch (JsonException)
            {
                continue; // Skip malformed SSE data
            }

            if (update is null) continue;

            if (string.Equals(update.Kind, "content", StringComparison.OrdinalIgnoreCase))
            {
                if (update.Content is not null)
                    yield return new AgentStreamChunk { Content = update.Content };
            }
            else if (string.Equals(update.Kind, "tool_call", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(update.Kind, "TOOLCALL", StringComparison.OrdinalIgnoreCase))
            {
                yield return new AgentStreamChunk { ToolName = update.ToolName };
            }
            else if (string.Equals(update.Kind, "tool_result", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(update.Kind, "TOOLRESULT", StringComparison.OrdinalIgnoreCase))
            {
                yield return new AgentStreamChunk { ToolName = update.ToolName, ToolResult = update.ToolResult };
            }
            else if (string.Equals(update.Kind, "completed", StringComparison.OrdinalIgnoreCase))
            {
                yield return new AgentStreamChunk { IsCompleted = true, OutputTokens = update.OutputTokens };
            }
            else if (string.Equals(update.Kind, "error", StringComparison.OrdinalIgnoreCase))
            {
                yield return new AgentStreamChunk { Error = update.Error };
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to connect to collector agent endpoint")]
    private partial void LogConnectionFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent returned {StatusCode}: {Body}")]
    private partial void LogAgentError(System.Net.HttpStatusCode statusCode, string body);
}

// ═══════════════════════════════════════════════════════════════════════════════
// DTOs for the agent HTTP proxy (separate from CopilotTools DTOs for isolation)
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed record AgentChatRequest
{
    [JsonPropertyName("prompt")] public required string Prompt { get; init; }
    [JsonPropertyName("systemPrompt")] public string? SystemPrompt { get; init; }
    [JsonPropertyName("context")] public AgentChatContext? Context { get; init; }
}

internal sealed record AgentChatContext
{
    [JsonPropertyName("additionalContext")] public string? AdditionalContext { get; init; }
}

internal sealed record AgentStreamUpdateDto
{
    [JsonPropertyName("kind")] public string? Kind { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }
    [JsonPropertyName("toolName")] public string? ToolName { get; init; }
    [JsonPropertyName("toolResult")] public string? ToolResult { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("outputTokens")] public long? OutputTokens { get; init; }
}

[JsonSerializable(typeof(AgentChatRequest))]
[JsonSerializable(typeof(AgentChatContext))]
[JsonSerializable(typeof(AgentStreamUpdateDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class AgentJsonContext : JsonSerializerContext;

using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for interacting with GitHub Copilot via the qyl collector.
/// </summary>
[McpServerToolType]
public sealed class CopilotTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.copilot_status")]
    [Description("""
                 Check if GitHub Copilot authentication is available.

                 Returns authentication status including:
                 - Whether Copilot is authenticated
                 - Authentication method (env var, gh CLI, PAT, OAuth)
                 - GitHub username if available
                 - Available capabilities (chat, workflow, tools)

                 Use this to verify Copilot is ready before sending chat messages.

                 Returns: Authentication status summary
                 """)]
    public Task<string> GetCopilotStatusAsync() =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var status = await client.GetFromJsonAsync<CopilotStatusDto>(
                "/api/v1/copilot/status",
                CopilotJsonContext.Default.CopilotStatusDto).ConfigureAwait(false);

            if (status is null)
                return "Unable to retrieve Copilot status.";

            var sb = new StringBuilder();
            sb.AppendLine("# Copilot Status");
            sb.AppendLine();
            sb.AppendLine($"- **Authenticated:** {(status.IsAuthenticated ? "Yes" : "No")}");

            if (status.IsAuthenticated)
            {
                if (!string.IsNullOrEmpty(status.AuthMethod))
                    sb.AppendLine($"- **Method:** {status.AuthMethod}");
                if (!string.IsNullOrEmpty(status.Username))
                    sb.AppendLine($"- **Username:** {status.Username}");
                if (status.Capabilities is { Count: > 0 })
                    sb.AppendLine($"- **Capabilities:** {string.Join(", ", status.Capabilities)}");
            }
            else if (!string.IsNullOrEmpty(status.Error))
            {
                sb.AppendLine($"- **Error:** {status.Error}");
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.copilot_chat")]
    [Description("""
                 Send a chat message to GitHub Copilot and return the response.

                 Sends the prompt to Copilot and collects the full streamed response.
                 The response includes content text and token usage information.

                 Example queries:
                 - Simple question: copilot_chat(prompt="Explain this error trace")
                 - With context: copilot_chat(prompt="Why is latency high?", context="Service: api-gateway")

                 Returns: Copilot's complete response text
                 """)]
    public Task<string> CopilotChatAsync(
        [Description("The prompt to send to Copilot")]
        string prompt,
        [Description("Additional context to include (e.g., telemetry data)")]
        string? context = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var request = new CopilotChatRequestDto
            {
                Prompt = prompt,
                Context = context is not null ? new CopilotContextDto { AdditionalContext = context } : null
            };

            using var response = await client.PostAsJsonAsync(
                "/api/v1/copilot/chat",
                request,
                CopilotJsonContext.Default.CopilotChatRequestDto).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return $"Copilot chat failed with status {response.StatusCode}";

            // Read SSE stream and collect content
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            var contentBuilder = new StringBuilder();
            long outputTokens = 0;

            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.StartsWithOrdinal("data: "))
                {
                    var json = line["data: ".Length..];
                    try
                    {
                        var update =
                            JsonSerializer.Deserialize(json, CopilotJsonContext.Default.CopilotStreamUpdateDto);
                        if (update is null) continue;

                        if (string.Equals(update.Kind, "content", StringComparison.OrdinalIgnoreCase) &&
                            update.Content is not null)
                        {
                            contentBuilder.Append(update.Content);
                        }
                        else if (string.Equals(update.Kind, "completed", StringComparison.OrdinalIgnoreCase))
                        {
                            outputTokens = update.OutputTokens ?? 0;
                        }
                        else if (string.Equals(update.Kind, "error", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"Copilot error: {update.Error}";
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed SSE data
                    }
                }
            }

            var result = contentBuilder.ToString();
            if (string.IsNullOrEmpty(result))
                return "Copilot returned an empty response.";

            if (outputTokens > 0)
                result += $"\n\n---\n*Tokens: {outputTokens}*";

            return result;
        });

    [McpServerTool(Name = "qyl.copilot_run_workflow")]
    [Description("""
                 Execute a named Copilot workflow.

                 Workflows are declarative automation scripts defined in .qyl/workflows/*.md.
                 They can analyze telemetry, investigate errors, and more.

                 Use copilot_status first to check if workflows are available.

                 Example:
                 - copilot_run_workflow(name="analyze-errors")
                 - copilot_run_workflow(name="summarize-session", context="Session: abc123")

                 Returns: Workflow execution result
                 """)]
    public Task<string> RunCopilotWorkflowAsync(
        [Description("Name of the workflow to execute")]
        string name,
        [Description("Additional context for the workflow")]
        string? context = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var request = new CopilotWorkflowRunDto
            {
                WorkflowName = name,
                Context = context is not null ? new CopilotContextDto { AdditionalContext = context } : null
            };

            using var response = await client.PostAsJsonAsync(
                $"/api/v1/copilot/workflows/{Uri.EscapeDataString(name)}/run",
                request,
                CopilotJsonContext.Default.CopilotWorkflowRunDto).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return $"Workflow execution failed with status {response.StatusCode}";

            // Read SSE stream and collect content
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            var contentBuilder = new StringBuilder();

            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.StartsWithOrdinal("data: "))
                {
                    var json = line["data: ".Length..];
                    try
                    {
                        var update =
                            JsonSerializer.Deserialize(json, CopilotJsonContext.Default.CopilotStreamUpdateDto);
                        if (update is null) continue;

                        if (string.Equals(update.Kind, "content", StringComparison.OrdinalIgnoreCase) &&
                            update.Content is not null)
                        {
                            contentBuilder.Append(update.Content);
                        }
                        else if (string.Equals(update.Kind, "error", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"Workflow error: {update.Error}";
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed SSE data
                    }
                }
            }

            var result = contentBuilder.ToString();
            return string.IsNullOrEmpty(result)
                ? "Workflow completed with no output."
                : result;
        });
}

#region DTOs

internal sealed record CopilotStatusDto(
    [property: JsonPropertyName("isAuthenticated")]
    bool IsAuthenticated,
    [property: JsonPropertyName("authMethod")]
    string? AuthMethod,
    [property: JsonPropertyName("username")]
    string? Username,
    [property: JsonPropertyName("capabilities")]
    List<string>? Capabilities,
    [property: JsonPropertyName("error")] string? Error);

internal sealed record CopilotChatRequestDto
{
    [JsonPropertyName("prompt")] public required string Prompt { get; init; }

    [JsonPropertyName("context")] public CopilotContextDto? Context { get; init; }
}

internal sealed record CopilotContextDto
{
    [JsonPropertyName("additionalContext")]
    public string? AdditionalContext { get; init; }
}

internal sealed record CopilotStreamUpdateDto
{
    [JsonPropertyName("kind")] public string? Kind { get; init; }

    [JsonPropertyName("content")] public string? Content { get; init; }

    [JsonPropertyName("error")] public string? Error { get; init; }

    [JsonPropertyName("outputTokens")] public long? OutputTokens { get; init; }
}

internal sealed record CopilotWorkflowRunDto
{
    [JsonPropertyName("workflowName")] public required string WorkflowName { get; init; }

    [JsonPropertyName("context")] public CopilotContextDto? Context { get; init; }
}

#endregion

[JsonSerializable(typeof(CopilotStatusDto))]
[JsonSerializable(typeof(CopilotChatRequestDto))]
[JsonSerializable(typeof(CopilotContextDto))]
[JsonSerializable(typeof(CopilotStreamUpdateDto))]
[JsonSerializable(typeof(CopilotWorkflowRunDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CopilotJsonContext : JsonSerializerContext;

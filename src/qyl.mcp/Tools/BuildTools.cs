using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

[McpServerToolType]
public sealed class BuildTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_build_failures")]
    [Description("List captured build failures with summary, property issues, and source hints.")]
    public Task<string> ListBuildFailuresAsync(
        [Description("Maximum number of failures to return (default: 10)")]
        int limit = 10) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<BuildFailuresResponse>(
                $"/api/v1/build-failures?limit={Math.Clamp(limit, 1, 100)}",
                BuildJsonContext.Default.BuildFailuresResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count == 0)
                return "No captured build failures.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Build Failures ({response.Items.Count} entries)");
            sb.AppendLine();

            foreach (var item in response.Items)
            {
                sb.AppendLine($"**{item.Timestamp:yyyy-MM-dd HH:mm:ss}** [{item.Target}] Exit code: {item.ExitCode}");
                if (!string.IsNullOrWhiteSpace(item.ErrorSummary))
                    sb.AppendLine($"Error: {item.ErrorSummary}");

                if (!string.IsNullOrWhiteSpace(item.PropertyIssuesJson))
                    sb.AppendLine($"Property tracking: {Truncate(item.PropertyIssuesJson, 200)}");

                if (!string.IsNullOrWhiteSpace(item.CallStackJson))
                    sb.AppendLine($"Call stack: {Truncate(item.CallStackJson, 200)}");

                if (!string.IsNullOrWhiteSpace(item.BinlogPath))
                    sb.AppendLine($"Binlog: {item.BinlogPath}");

                sb.AppendLine($"ID: `{item.Id}`");
                sb.AppendLine();
            }

            return sb.ToString();
        });

    [McpServerTool(Name = "qyl.get_build_failure")]
    [Description("Get full details for a single captured build failure by id.")]
    public Task<string> GetBuildFailureAsync(
        [Description("Build failure id")] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult("Error: id is required");

        return CollectorHelper.ExecuteAsync(async () =>
        {
            var item = await client.GetFromJsonAsync<BuildFailureDto>(
                $"/api/v1/build-failures/{Uri.EscapeDataString(id)}",
                BuildJsonContext.Default.BuildFailureDto).ConfigureAwait(false);

            if (item is null)
                return $"Build failure '{id}' was not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Build Failure {item.Id}");
            sb.AppendLine();
            sb.AppendLine($"Timestamp: {item.Timestamp:O}");
            sb.AppendLine($"Target: {item.Target}");
            sb.AppendLine($"Exit code: {item.ExitCode}");

            if (!string.IsNullOrWhiteSpace(item.BinlogPath))
                sb.AppendLine($"Binlog: {item.BinlogPath}");

            if (!string.IsNullOrWhiteSpace(item.ErrorSummary))
                sb.AppendLine($"Error summary: {item.ErrorSummary}");

            if (!string.IsNullOrWhiteSpace(item.PropertyIssuesJson))
            {
                sb.AppendLine();
                sb.AppendLine("## Property Issues");
                sb.AppendLine(item.PropertyIssuesJson);
            }

            if (!string.IsNullOrWhiteSpace(item.EnvReadsJson))
            {
                sb.AppendLine();
                sb.AppendLine("## Environment Reads");
                sb.AppendLine(item.EnvReadsJson);
            }

            if (!string.IsNullOrWhiteSpace(item.CallStackJson))
            {
                sb.AppendLine();
                sb.AppendLine("## Call Stack");
                sb.AppendLine(item.CallStackJson);
            }

            return sb.ToString();
        });
    }

    [McpServerTool(Name = "qyl.search_build_failures")]
    [Description("Search captured build failures by error text, property tracking details, or call stack text.")]
    public Task<string> SearchBuildFailuresAsync(
        [Description("Search pattern")] string pattern,
        [Description("Maximum results (default: 20)")]
        int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return Task.FromResult("Error: pattern is required");

        return CollectorHelper.ExecuteAsync(async () =>
        {
            var response = await client.GetFromJsonAsync<BuildFailuresResponse>(
                $"/api/v1/build-failures/search?pattern={Uri.EscapeDataString(pattern)}&limit={Math.Clamp(limit, 1, 100)}",
                BuildJsonContext.Default.BuildFailuresResponse).ConfigureAwait(false);

            if (response?.Items is null || response.Items.Count == 0)
                return $"No build failures matched '{pattern}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Build Failures Matching '{pattern}' ({response.Items.Count})");
            sb.AppendLine();

            foreach (var item in response.Items)
            {
                sb.AppendLine(
                    $"- {item.Timestamp:yyyy-MM-dd HH:mm:ss} [{item.Target}] ({item.ExitCode}) {item.ErrorSummary}");
                sb.AppendLine($"  id: `{item.Id}`");
            }

            return sb.ToString();
        });
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
            return text;

        return text[..max] + "...";
    }
}

internal sealed record BuildFailuresResponse(
    [property: JsonPropertyName("items")] List<BuildFailureDto>? Items,
    [property: JsonPropertyName("total")] int Total);

internal sealed record BuildFailureDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("timestamp")]
    DateTimeOffset Timestamp,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("exitCode")]
    int ExitCode,
    [property: JsonPropertyName("binlogPath")]
    string? BinlogPath,
    [property: JsonPropertyName("errorSummary")]
    string? ErrorSummary,
    [property: JsonPropertyName("propertyIssuesJson")]
    string? PropertyIssuesJson,
    [property: JsonPropertyName("envReadsJson")]
    string? EnvReadsJson,
    [property: JsonPropertyName("callStackJson")]
    string? CallStackJson,
    [property: JsonPropertyName("durationMs")]
    int? DurationMs,
    [property: JsonPropertyName("createdAt")]
    DateTimeOffset? CreatedAt);

[JsonSerializable(typeof(BuildFailureDto))]
[JsonSerializable(typeof(BuildFailuresResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class BuildJsonContext : JsonSerializerContext;

using System.ComponentModel;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
public sealed class CreateApiKeyTool(HttpClient client)
{
    [McpServerTool(
        Name = "create_api_key",
        Title = "Create API Key",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false)]
    [Description("Create a new API key for programmatic access.")]
    public async Task<string> CreateApiKeyAsync(
        [Description("Name for the API key (e.g. 'ci-pipeline', 'dev-local')")]
        string name,
        CancellationToken ct = default)
    {
        var body = new { name };
        var response = await client.PostAsJsonAsync("/api/v1/mcp/api-keys", body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiKeyResponseDto>(ct).ConfigureAwait(false);

        return ResponseFormatter.FormatDetail(
            "API Key Created",
            [
                ("Name", result!.Name),
                ("Prefix", $"`{result.Prefix}`"),
                ("Key", $"`{result.Key}` (save this — it won't be shown again)")
            ]);
    }
}

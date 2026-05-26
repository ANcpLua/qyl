using System.Net.Http.Json;
using ANcpLua.Roslyn.Utilities;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools.Management;

[McpServerToolType]
[QylSkill(QylSkillKind.Build)]
public sealed partial class CreateApiKeyTool(HttpClient client)
{
    [McpServerTool(
        Name = "create_api_key",
        Title = "Create API Key",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false)]
    public async partial Task<string> CreateApiKeyAsync(
        string name,
        CancellationToken ct = default)
    {
        var body = new { name };
        var response = await client.PostAsJsonAsync("/api/v1/mcp/api-keys", body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiKeyResponseDto>(ct).ConfigureAwait(false);
        result = Guard.NotNull(result);

        return ResponseFormatter.FormatDetail(
            "API Key Created",
            [
                ("Name", result.Name),
                ("Prefix", $"`{result.Prefix}`"),
                ("Key", $"`{result.Key}` (save this — it won't be shown again)")
            ]);
    }
}

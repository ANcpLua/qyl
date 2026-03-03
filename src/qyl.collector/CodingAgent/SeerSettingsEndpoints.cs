using qyl.collector.Storage;

namespace qyl.collector.CodingAgent;

public static class SeerSettingsEndpoints
{
    public static void MapSeerSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/seer/settings", static async (
            DuckDbStore store, CancellationToken ct) =>
        {
            var settings = await store.GetSeerSettingsAsync(ct);
            return Results.Ok(settings);
        });

        app.MapPut("/api/v1/seer/settings", static async (
            UpdateSeerSettingsRequest request, DuckDbStore store, CancellationToken ct) =>
        {
            if (request.DefaultCodingAgent is { } agent &&
                !Enum.TryParse<CodingAgentProvider>(agent, true, out _))
                return Results.BadRequest(new { error = $"Unknown provider: {agent}" });

            var current = await store.GetSeerSettingsAsync(ct);
            var updated = current with
            {
                DefaultCodingAgent = request.DefaultCodingAgent ?? current.DefaultCodingAgent,
                DefaultCodingAgentIntegrationId =
                    request.DefaultCodingAgentIntegrationId ?? current.DefaultCodingAgentIntegrationId,
                AutomationTuning = request.AutomationTuning ?? current.AutomationTuning
            };

            await store.UpsertSeerSettingsAsync(updated, ct);
            return Results.Ok(updated);
        });
    }
}

public sealed record UpdateSeerSettingsRequest(
    string? DefaultCodingAgent = null,
    string? DefaultCodingAgentIntegrationId = null,
    string? AutomationTuning = null);

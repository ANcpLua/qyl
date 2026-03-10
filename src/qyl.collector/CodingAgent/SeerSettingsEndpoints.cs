using Qyl.Collector.Storage;

namespace Qyl.Collector.CodingAgent;

public static class LoomSettingsEndpoints
{
    public static void MapLoomSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/Loom/settings", static async (
            DuckDbStore store, CancellationToken ct) =>
        {
            var settings = await store.GetLoomSettingsAsync(ct);
            return Results.Ok(settings);
        });

        app.MapPut("/api/v1/Loom/settings", static async (
            UpdateLoomSettingsRequest request, DuckDbStore store, CancellationToken ct) =>
        {
            if (request.DefaultCodingAgent is { } agent &&
                !CodingAgentProviderNames.TryParse(agent, out _))
                return Results.BadRequest(new { error = $"Unknown provider: {agent}" });

            var current = await store.GetLoomSettingsAsync(ct);
            var updated = current with
            {
                DefaultCodingAgent = request.DefaultCodingAgent is { } defaultCodingAgent
                    ? CodingAgentProviderNames.NormalizeSlug(defaultCodingAgent)
                    : current.DefaultCodingAgent,
                DefaultCodingAgentIntegrationId =
                    request.DefaultCodingAgentIntegrationId ?? current.DefaultCodingAgentIntegrationId,
                AutomationTuning = request.AutomationTuning ?? current.AutomationTuning
            };

            await store.UpsertLoomSettingsAsync(updated, ct);
            return Results.Ok(updated);
        });
    }
}

public sealed record UpdateLoomSettingsRequest(
    string? DefaultCodingAgent = null,
    string? DefaultCodingAgentIntegrationId = null,
    string? AutomationTuning = null);

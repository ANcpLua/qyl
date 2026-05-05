namespace Qyl.Collector.Autofix;

public static class LoomSettingsEndpoints
{
    [QylMapEndpoints]
    public static void MapLoomSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/loom/settings/{orgId}", static async Task<Ok<LoomSettingsRecord>> (
            string orgId,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var settings = await store.GetLoomSettingsAsync(orgId, ct).ConfigureAwait(false);
            return TypedResults.Ok(settings);
        });

        app.MapPut("/api/v1/loom/settings/{orgId}", static async Task<Ok<LoomSettingsRecord>> (
            string orgId,
            LoomSettingsRecord settings,
            DuckDbStore store,
            CancellationToken ct) =>
        {
            var normalized = settings with
            {
                Id = orgId, DefaultCodingAgent = CodingAgentProviderNames.NormalizeSlug(settings.DefaultCodingAgent)
            };

            await store.UpsertLoomSettingsAsync(normalized, ct).ConfigureAwait(false);
            var persisted = await store.GetLoomSettingsAsync(orgId, ct).ConfigureAwait(false);
            return TypedResults.Ok(persisted);
        });
    }
}

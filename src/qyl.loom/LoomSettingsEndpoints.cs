namespace Qyl.Loom;

/// <summary>
///     REST endpoints for managing organization-level Loom configuration.
/// </summary>
public static class LoomSettingsEndpoints
{
    public static void MapLoomSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/loom/settings/{orgId}", static (string orgId) =>
        {
            // TODO: Retrieve from DuckDB storage
            var settings = new LoomSettingsRecord
            {
                Id = orgId,
                DefaultCodingAgent = "Loom",
                AutomationTuning = "medium",
                UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
            };

            return Results.Ok(settings);
        });

        app.MapPut("/api/v1/loom/settings/{orgId}", static (string orgId, LoomSettingsRecord settings) =>
        {
            // TODO: Persist to DuckDB storage
            var updatedSettings = settings with
            {
                Id = orgId,
                UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime
            };

            return Results.Ok(updatedSettings);
        });
    }
}

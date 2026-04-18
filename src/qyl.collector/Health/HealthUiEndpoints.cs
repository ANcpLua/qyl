using Qyl.Contracts.Primitives;

namespace Qyl.Collector.Health;

/// <summary>
///     Dashboard-only rich health endpoint (<c>/health/ui</c>).
///     Unlike <c>/alive</c> and <c>/health</c> — which come from <c>QylServiceDefaults</c> and
///     return the canonical 200/503 probe shape — this endpoint streams the full
///     <see cref="HealthUiResponse" /> (DuckDB stats, disk, memory, ingestion buffer) that
///     the dashboard TopBar indicator binds to.
/// </summary>
public static class HealthUiEndpoints
{
    /// <summary>Start time used to compute uptime in <see cref="HealthUiService" /> responses.</summary>
    public static readonly DateTimeOffset StartTime = TimeProvider.System.GetUtcNow();

    /// <summary>Application SemVer used in <see cref="HealthUiResponse" />.</summary>
    public static readonly SemVer AppVersion = ResolveVersion();

    public static IEndpointRouteBuilder MapHealthUiEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/ui", static async (HealthUiService healthUi, CancellationToken ct) =>
                Results.Ok(await healthUi.GetHealthAsync(ct).ConfigureAwait(false)))
            .WithName("HealthUi");

        return app;
    }

    private static SemVer ResolveVersion()
    {
        var version = BuildVersion.InformationalVersion;
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}

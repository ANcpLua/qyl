namespace Qyl.Collector.Health;

public static class HealthUiEndpoints
{
    public static readonly DateTimeOffset StartTime = TimeProvider.System.GetUtcNow();

    public static readonly SemVer AppVersion = ResolveVersion();

    [QylMapEndpoints]
    public static IEndpointRouteBuilder MapHealthUiEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/ui", static async (HealthUiService healthUi, CancellationToken ct) =>
                Results.Ok(await healthUi.GetHealthAsync(ct).ConfigureAwait(false)))
            .WithName("HealthUi");

        return app;
    }

    private static SemVer ResolveVersion()
    {
        const string version = BuildVersion.InformationalVersion;
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}

namespace Qyl.NextDns.Ingester;

/// <summary>
/// Configuration for the NextDNS log ingester. Values come from environment variables
/// (qyl convention: <c>UPPER_SNAKE_CASE</c>). The ingester refuses to start if the
/// API key or profile id is missing, but it will idle cleanly if telemetry endpoint
/// is omitted (running but not exporting is a valid posture for offline triage).
/// </summary>
internal sealed class IngesterOptions
{
    public required string ApiKey { get; init; }

    public required string ProfileId { get; init; }

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(60);

    public string? OtlpEndpoint { get; init; }

    public string BaseUrl { get; init; } = "https://api.nextdns.io";

    public bool DryRun { get; init; }

    public static IngesterOptions? TryFromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("NEXTDNS_API_KEY");
        var profileId = Environment.GetEnvironmentVariable("NEXTDNS_PROFILE_ID");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(profileId))
            return null;

        var pollSecondsRaw = Environment.GetEnvironmentVariable("NEXTDNS_POLL_INTERVAL_SECONDS");
        var pollSeconds = int.TryParse(pollSecondsRaw, out var parsed) && parsed > 0 ? parsed : 60;

        var baseUrl = Environment.GetEnvironmentVariable("NEXTDNS_BASE_URL");

        return new IngesterOptions
        {
            ApiKey = apiKey,
            ProfileId = profileId,
            PollInterval = TimeSpan.FromSeconds(Math.Clamp(pollSeconds, 5, 3600)),
            OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"),
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.nextdns.io" : baseUrl,
            DryRun = string.Equals(
                Environment.GetEnvironmentVariable("NEXTDNS_DRY_RUN"),
                "true",
                StringComparison.OrdinalIgnoreCase)
        };
    }
}

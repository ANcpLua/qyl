namespace qyl.mcp.Sentry;

/// <summary>
///     Authentication options for the Sentry REST API.
///     Uses a User Auth Token sent as Bearer in the Authorization header.
/// </summary>
public sealed class qylAuthOptions
{
    public const string SectionName = "Sentry";
    public const string TokenEnvVar = "SENTRY_AUTH_TOKEN";
    public const string HostEnvVar = "SENTRY_HOST";
    public const string OrgEnvVar = "SENTRY_ORG";

    /// <summary>Sentry User Auth Token (required).</summary>
    public string? AuthToken { get; set; }

    /// <summary>Sentry host — defaults to sentry.io for cloud, override for self-hosted.</summary>
    public string Host { get; set; } = "sentry.io";

    /// <summary>Optional: constrain all tools to a single organization slug.</summary>
    public string? OrgSlug { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(AuthToken);

    public Uri ApiBaseUri => new($"https://{Host}/api/0/");
}

namespace Qyl.Collector.ApiKeys;

internal sealed class OtlpApiKeyOptions
{
    private static readonly string[] s_validAuthModes = ["ApiKey", "Unsecured"];

    public string AuthMode
    {
        get;
        set => field = s_validAuthModes.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value
            : throw new ArgumentException($"AuthMode must be one of: {string.Join(", ", s_validAuthModes)}",
                nameof(value));
    } = "Unsecured";

    // Each credential belongs to exactly one project. Multiple credentials can rotate within a project.
    public IReadOnlyList<ProjectApiKey> Keys { get; set; } = [];

    public bool IsApiKeyMode =>
        string.Equals(AuthMode, "ApiKey", StringComparison.OrdinalIgnoreCase);
}

internal sealed record ProjectApiKey(string ProjectId, string ApiKey, ApiKeyRoles Roles);

/// <summary>
/// What a credential may do (QYL-A2). The roles are orthogonal: a write key that must also read
/// the resources it changes carries <see cref="Read"/> as well, so a leaked ingest key can never
/// change or delete anything.
/// </summary>
[Flags]
internal enum ApiKeyRoles
{
    None = 0,

    /// <summary>OTLP export over HTTP (<c>/v1/*</c>) and gRPC.</summary>
    Ingest = 1,

    /// <summary>Every <c>GET</c> under <c>/api/v1</c>, plus running a saved query.</summary>
    Read = 2,

    /// <summary>Creating and updating annotations and saved queries.</summary>
    Write = 4,

    /// <summary>Deleting annotations and saved queries. Telemetry is never deleted through the API.</summary>
    Delete = 8,

    /// <summary>
    /// The roles of a credential configured as a bare string, which is every credential that
    /// existed before roles: it keeps exactly the surface it had.
    /// </summary>
    IngestAndRead = Ingest | Read
}

internal static class ApiKeyRoleNames
{
    public const string Ingest = "ingest";
    public const string Read = "read";
    public const string Write = "write";
    public const string Delete = "delete";

    public static bool TryParse(string? value, out ApiKeyRoles role)
    {
        role = value switch
        {
            Ingest => ApiKeyRoles.Ingest,
            Read => ApiKeyRoles.Read,
            Write => ApiKeyRoles.Write,
            Delete => ApiKeyRoles.Delete,
            _ => ApiKeyRoles.None
        };
        return role is not ApiKeyRoles.None;
    }

    public static string Name(ApiKeyRoles role) => role switch
    {
        ApiKeyRoles.Ingest => Ingest,
        ApiKeyRoles.Read => Read,
        ApiKeyRoles.Write => Write,
        ApiKeyRoles.Delete => Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Exactly one role is required.")
    };
}

/// <summary>
/// The one role a route requires, attached to every <c>/api/v1</c> endpoint. A route that
/// carries none (the problem+json fallback) requires the role its HTTP method implies.
/// </summary>
internal sealed record ApiKeyRoleRequirement(ApiKeyRoles Role)
{
    public static ApiKeyRoles ForMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            ? ApiKeyRoles.Read
            : HttpMethods.IsDelete(method)
                ? ApiKeyRoles.Delete
                : ApiKeyRoles.Write;
}

internal static class ApiKeyRoleEndpointExtensions
{
    public static RouteHandlerBuilder RequireKeyRole(this RouteHandlerBuilder builder, ApiKeyRoles role) =>
        builder.WithMetadata(new ApiKeyRoleRequirement(role));
}

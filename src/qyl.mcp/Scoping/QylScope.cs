namespace qyl.mcp.Scoping;

/// <summary>
///     Reads QYL_SERVICE and QYL_SESSION environment variables to narrow tool scope.
///     When set, all collector HTTP requests automatically include the scope as query parameters.
/// </summary>
public sealed class QylScope
{
    private QylScope(string? serviceName, string? sessionId)
    {
        ServiceName = serviceName;
        SessionId = sessionId;
    }

    /// <summary>Narrow all queries to a specific service (e.g., "api-gateway").</summary>
    public string? ServiceName { get; }

    /// <summary>Narrow all queries to a specific session ID.</summary>
    public string? SessionId { get; }

    public bool HasScope => ServiceName is not null || SessionId is not null;

    /// <summary>Reads QYL_SERVICE and QYL_SESSION from the process environment.</summary>
    public static QylScope FromEnvironment() => new(
        NullIfEmpty(Environment.GetEnvironmentVariable("QYL_SERVICE")),
        NullIfEmpty(Environment.GetEnvironmentVariable("QYL_SESSION")));

    /// <summary>Creates a scope for testing without environment variable access.</summary>
    internal static QylScope ForTest(string? serviceName = null, string? sessionId = null) =>
        new(serviceName, sessionId);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

namespace qyl.mcp.Scoping;

/// <summary>
///     Reads QYL_SERVICE and QYL_SESSION environment variables to narrow tool scope.
///     When set, all collector HTTP requests automatically include the scope as query parameters.
/// </summary>
public sealed class QylScope
{
    /// <summary>Narrow all queries to a specific service (e.g., "api-gateway").</summary>
    public string? ServiceName { get; }

    /// <summary>Narrow all queries to a specific session ID.</summary>
    public string? SessionId { get; }

    public bool HasScope => ServiceName is not null || SessionId is not null;

    private QylScope(string? serviceName, string? sessionId)
    {
        ServiceName = serviceName;
        SessionId = sessionId;
    }

    public static QylScope FromEnvironment() => new(
        NullIfEmpty(Environment.GetEnvironmentVariable("QYL_SERVICE")),
        NullIfEmpty(Environment.GetEnvironmentVariable("QYL_SESSION")));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

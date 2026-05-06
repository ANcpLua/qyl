namespace qyl.mcp.Scoping;

public sealed class QylScope
{
    private QylScope(string? serviceName, string? sessionId)
    {
        ServiceName = serviceName;
        SessionId = sessionId;
    }

    public string? ServiceName { get; }

    public string? SessionId { get; }

    public bool HasScope => ServiceName is not null || SessionId is not null;

    public static QylScope FromEnvironment() =>
        new(Read("QYL_SERVICE"), Read("QYL_SESSION"));

    internal static QylScope ForTest(string? serviceName = null, string? sessionId = null) =>
        new(serviceName, sessionId);

    private static string? Read(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}

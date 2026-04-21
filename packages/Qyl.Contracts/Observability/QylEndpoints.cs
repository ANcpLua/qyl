namespace Qyl.Contracts.Observability;

/// <summary>
///     Canonical HTTP endpoint paths for qyl service defaults.
///     Shared between <c>qyl.instrumentation</c> (hosts and OTel trace-filter) and standalone
///     services like <c>qyl.mcp</c> that map their own health checks without pulling the full
///     instrumentation stack.
/// </summary>
public static class QylEndpoints
{
    /// <summary>Liveness probe — cheap "is the process responsive" check. Aspire-compatible.</summary>
    public const string Alive = "/alive";

    /// <summary>Readiness probe — runs all <c>ready</c>-tagged health checks. Aspire-compatible.</summary>
    public const string Health = "/health";

    /// <summary>Tag applied to cheap liveness checks (e.g. <c>self</c>).</summary>
    public const string LiveTag = "live";

    /// <summary>Tag applied to dependency readiness checks (e.g. <c>duckdb</c>).</summary>
    public const string ReadyTag = "ready";
}

namespace Qyl.Host;

/// <summary>
/// Decides when a launched resource counts as ready. The orchestrator calls this once per
/// launch (and again after every restart); the implementation owns its whole readiness window —
/// retry cadence and startup deadline included — and returns <c>false</c> when the resource
/// never became ready inside it.
/// </summary>
/// <remarks>
/// The default is <see cref="HttpHealthProbe"/> (GET the resource's health path until 2xx).
/// Other transports plug in per resource via
/// <see cref="QylResourceBuilderExtensions.WithReadinessProbe"/> — e.g. an MCP server is ready
/// when it answers <c>initialize</c> + <c>tools/list</c>, not when an HTTP route appears.
/// Expected unreachability while the resource boots must be handled inside the probe, not
/// thrown; cancellation propagates (it means runner shutdown, not failure).
/// </remarks>
internal interface IReadinessProbe
{
    Task<bool> IsReadyAsync(QylResourceState state, CancellationToken cancellationToken);
}

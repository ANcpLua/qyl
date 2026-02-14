namespace Qyl.ServiceDefaults.Identity;

/// <summary>
///     Holds the workspace identity obtained from the collector handshake.
///     Registered as a singleton so any service can check registration status.
/// </summary>
public sealed class WorkspaceContext
{
    /// <summary>
    ///     The workspace ID assigned by the collector, or null if not yet registered.
    /// </summary>
    public string? WorkspaceId { get; internal set; }

    /// <summary>
    ///     The service name used during handshake.
    /// </summary>
    public string? ServiceName { get; internal set; }

    /// <summary>
    ///     Whether the workspace has been successfully registered with the collector.
    /// </summary>
    public bool IsRegistered => WorkspaceId is not null;
}

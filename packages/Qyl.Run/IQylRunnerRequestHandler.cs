using System.Net;

namespace Qyl.Run;

/// <summary>
/// Extension seam for the runner's loopback HTTP surface. The core API stays read-only
/// (GET, no control verbs); an opt-in package registers a handler to claim additional
/// <c>/runner/*</c> routes — e.g. Qyl.Host.Mcp's <c>/runner/mcp/*</c> passthrough, which
/// needs POST for <c>tools/call</c>. Handlers are consulted before the core routes.
/// </summary>
public interface IQylRunnerRequestHandler
{
    /// <summary>
    /// Returns <see langword="true"/> if this handler owned the request (response written and
    /// closed); <see langword="false"/> hands it to the next handler or the core routes.
    /// </summary>
    Task<bool> TryHandleAsync(HttpListenerContext context, CancellationToken cancellationToken);
}

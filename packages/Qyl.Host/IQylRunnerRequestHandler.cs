using System.Net;

namespace Qyl.Host;

/// <summary>
/// Extension seam for the runner's loopback HTTP surface. An opt-in package registers a handler
/// to claim additional <c>/runner/*</c> routes — e.g. Qyl.Host.Mcp's generated
/// <c>/runner/mcp/*</c> passthrough. Handlers are consulted before the core resource routes.
/// </summary>
internal interface IQylRunnerRequestHandler
{
    /// <summary>
    /// Returns <see langword="true"/> if this handler owned the request (response written and
    /// closed); <see langword="false"/> hands it to the next handler or the core routes.
    /// </summary>
    Task<bool> TryHandleAsync(HttpListenerContext context, CancellationToken cancellationToken);
}

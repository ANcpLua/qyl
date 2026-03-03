// =============================================================================
// qyl.collector - CopilotAguiEndpoints
// Registers and maps the AG-UI SSE endpoint for CopilotKit browser SDKs.
//
// Usage in Program.cs:
//   builder.Services.AddQylAgui();
//   // ... after var app = builder.Build():
//   app.MapQylAguiChat(agent);   // default path: /api/v1/copilot/chat
// =============================================================================

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

namespace qyl.collector.Copilot;

/// <summary>
///     Extension methods that expose a <see cref="AIAgent"/> over the AG-UI SSE
///     protocol, making it consumable by CopilotKit React / Angular / Vanilla JS.
/// </summary>
public static class CopilotAguiEndpoints
{
    /// <summary>
    ///     Registers the AG-UI infrastructure services (serialization, SSE helpers).
    ///     Call this on <c>builder.Services</c> before <c>builder.Build()</c>.
    /// </summary>
    public static IServiceCollection AddQylAgui(this IServiceCollection services)
    {
        Guard.NotNull(services);
        services.AddAGUI();
        return services;
    }

    /// <summary>
    ///     Maps the AG-UI SSE endpoint at <paramref name="path"/>.
    ///     <para>
    ///     POST body: <c>{ threadId, runId, messages: [{role, content}], context? }</c>
    ///     Response: SSE stream (RUN_STARTED … RUN_FINISHED).
    ///     Errors during streaming → RUN_ERROR event (not HTTP 5xx).
    ///     Cancellation → stream closes silently.
    ///     </para>
    /// </summary>
    /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
    /// <param name="agent">The agent to serve at this endpoint.</param>
    /// <param name="path">URL path (default: <c>/api/v1/copilot/chat</c>).</param>
    public static IEndpointRouteBuilder MapQylAguiChat(
        this IEndpointRouteBuilder endpoints,
        AIAgent agent,
        string path = "/api/v1/copilot/chat")
    {
        Guard.NotNull(endpoints);
        Guard.NotNull(agent);
        endpoints.MapAGUI(path, agent);
        return endpoints;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

/// <summary>
///     Minimal-API endpoint exposing the inventory snapshot. Gated behind the
///     <c>QylAdmin</c> authorization policy when configured, falling back to dev-only
///     mapping when no auth scheme is registered. Endpoint surface is recon-relevant
///     (agent keys + instructions hashes) — never expose unprotected in prod.
/// </summary>
public static class QylAgentInventoryEndpoint
{
    public const string AdminPolicy = "QylAdmin";
    public const string Path = "/qyl/inventory/agents";

    public static IEndpointRouteBuilder MapQylAgentInventory(this WebApplication app)
    {
        var hasAuth = app.Services
                          .GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>() is not null
                      && app.Services.GetService<IAuthorizationPolicyProvider>() is not null;

        if (hasAuth)
        {
            app.MapGet(Path, GetInventory).RequireAuthorization(AdminPolicy);
            return app;
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapGet(Path, GetInventory);
            return app;
        }

        // Production with no auth wired: refuse to expose the surface. Inventory entries
        // include agent keys + instructions hashes, which are recon material for prompt
        // injection — never serve them anonymously outside dev.
        app.MapGet(Path, static () => Results.NotFound());
        return app;
    }

    private static Ok<AgentInventoryResponse> GetInventory(IQylAgentInventory inventory) =>
        TypedResults.Ok(new AgentInventoryResponse(inventory.Snapshot(), inventory.Snapshot().Count));
}

public sealed record AgentInventoryResponse(IReadOnlyList<AgentRegistration> Items, int Total);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Qyl.Instrumentation.Instrumentation.Inventory;

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

        app.MapGet(Path, static () => Results.NotFound());
        return app;
    }

    private static Ok<AgentInventoryResponse> GetInventory(IQylAgentInventory inventory)
    {
        var items = inventory.Snapshot();
        return TypedResults.Ok(new AgentInventoryResponse(items, items.Count));
    }
}

public sealed record AgentInventoryResponse(IReadOnlyList<AgentRegistration> Items, int Total);

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using qyl.mcp.Landing;
using qyl.mcp.Scoping;
using Qyl.Contracts.Observability;

namespace qyl.mcp.Hosting;

internal static class QylMcpHttpHost
{
    public static async Task RunAsync(string[] args, SkillConfiguration skills, QylScope scope)
    {
        var builder = WebApplication.CreateBuilder(args);
        QylMcpServiceCollectionExtensions.ConfigureLogging(builder.Logging);

        var hostOptions = McpHostOptions.FromConfiguration(builder.Configuration, McpTransportMode.Http);
        QylMcpServiceCollectionExtensions.ApplyPortFallback(builder.WebHost, builder.Configuration);

        var jsonOptions = builder.Services.AddQylMcpCommonServices(builder.Configuration, skills, scope);
        builder.Services.AddQylMcpHttpAuthentication(hostOptions);
        builder.Services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), [QylEndpoints.LiveTag]);

        IServiceProvider? serviceProvider = null;
        QylMcpServerRegistration.Configure(
            builder.Services,
            skills,
            jsonOptions,
            McpTransportMode.Http,
            hostOptions,
            () => serviceProvider);

        var app = builder.Build();
        serviceProvider = app.Services;

        if (hostOptions.RequiresAuthentication)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapGet("/", (HttpRequest request) =>
        {
            var accept = request.Headers.Accept.ToString();
            if (accept.ContainsIgnoreCase("text/html"))
            {
                return Results.Content(
                    LandingPage.GetHtml(hostOptions.ResolvePublicMcpUrl(request)),
                    "text/html; charset=utf-8");
            }

            return Results.Json(QylMcpManifestBuilder.Create(request, hostOptions, skills));
        });

        app.MapGet("/mcp.json", (HttpRequest request) =>
            Results.Json(QylMcpManifestBuilder.Create(request, hostOptions, skills)));

        app.MapGet("/llms.txt", (HttpRequest request) =>
            Results.Text(QylMcpLlmsTextBuilder.Create(hostOptions, skills, request), "text/plain; charset=utf-8"));

        app.MapHealthChecks(QylEndpoints.Alive,
            new HealthCheckOptions { Predicate = static check => check.Tags.Contains(QylEndpoints.LiveTag) });
        app.MapHealthChecks(QylEndpoints.Health,
            new HealthCheckOptions { Predicate = static check => check.Tags.Contains(QylEndpoints.ReadyTag) });

        var endpoint = app.MapMcp(hostOptions.Path);
        if (hostOptions.RequiresAuthentication)
            endpoint.RequireAuthorization();

        await app.RunAsync().ConfigureAwait(false);
    }
}

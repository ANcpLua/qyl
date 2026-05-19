using ANcpLua.Agents.Mcp.Hosting;
using ANcpLua.Agents.Mcp.Hosting.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qyl.Contracts.Observability;
using qyl.mcp.Landing;
using qyl.mcp.Scoping;

namespace qyl.mcp.Hosting;

internal static class QylMcpHttpHost
{
    public static async Task RunAsync(string[] args, SkillConfiguration skills, QylScope scope)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseQylMcpPortFallback(builder.Configuration);

        var hostOptions = McpHostOptions.FromConfiguration(builder.Configuration, McpTransportMode.Http);

        var jsonOptions = builder.Services.AddQylMcpCommonServices(builder.Configuration, skills, scope);
        builder.Services.AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), [QylEndpoints.LiveTag]);

        var serviceProviderRef = new ServiceProviderRef();
        QylMcpServerRegistration.Configure(
            builder.Services,
            skills,
            jsonOptions,
            McpTransportMode.Http,
            hostOptions,
            () => serviceProviderRef.Value);

        var app = builder.Build();
        serviceProviderRef.Value = app.Services;

        if (hostOptions.RequiresAuthentication)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapGet("/", (HttpRequest request) => Results.Content(
            LandingPage.GetHtml(hostOptions.ResolvePublicMcpUrl(request)),
            "text/html; charset=utf-8"));

        app.MapGet("/llms.txt", (HttpRequest request) =>
            Results.Text(QylMcpLlmsTextBuilder.Create(hostOptions, skills, request), "text/plain; charset=utf-8"));

        var endpoint = app.MapQylMcp(hostOptions.Path);
        if (hostOptions.RequiresAuthentication)
            endpoint.RequireAuthorization();

        await app.RunAsync().ConfigureAwait(false);
    }

    private sealed class ServiceProviderRef
    {
        public IServiceProvider? Value { get; set; }
    }
}

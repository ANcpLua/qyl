using qyl.mcp.Hosting;
using qyl.mcp.Scoping;
using qyl.mcp.Skills;

namespace Qyl.Collector.Hosting;

/// <summary>
/// In-process MCP host wiring for the collector. Shares qyl.mcp's
/// <see cref="QylMcpServiceCollectionExtensions.AddQylMcpCommonServices"/>
/// and <see cref="QylMcpServerRegistration.ConfigureForHttp"/> so the
/// HTTP transport and the qyl-mcp stdio dev host expose an identical
/// tool surface.
/// </summary>
/// <remarks>
/// qyl-PRD Stage E2.a deliverable. The actual route mapping (<c>app.MapMcp(
/// "/mcp/{tenant}")</c>) + Bearer-opaque-token authentication handler + RFC
/// 9728 protected-resource-metadata endpoint are wired by Stage E2.b in
/// <c>QylMcpRouteExtensions</c>; per-request <see cref="QylScope"/>
/// (per-tenant claims → scope injector) is wired by Stage E2.c.
/// </remarks>
public static class CollectorMcpExtensions
{
    /// <summary>
    /// Registers qyl.mcp's full tool surface against the collector's DI
    /// container, configured for HTTP transport in stateless mode.
    /// </summary>
    public static IServiceCollection AddQylCollectorMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var skills = SkillConfiguration.FromEnvironment();
        // Placeholder process-level scope. E2.c replaces this with a per-request
        // ConfigureSessionOptions callback that derives QylScope from the
        // authenticated ClaimsPrincipal's qyl.tenant_id claim.
        var defaultScope = QylScope.FromEnvironment();

        var jsonOptions = services.AddQylMcpCommonServices(configuration, skills, defaultScope);
        QylMcpServerRegistration.ConfigureForHttp(services, skills, jsonOptions);

        return services;
    }
}

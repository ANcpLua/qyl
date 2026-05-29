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
    /// Registration only. The route mapping (<c>app.MapMcp("/mcp/{tenant}")</c>),
    /// JWT bearer authentication, and the RFC 9728 protected-resource-metadata
    /// endpoint are wired by <see cref="CollectorAuthExtensions"/>.
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
        // Placeholder process-level scope, still to be replaced by a per-request
        // QylScope derived from the authenticated ClaimsPrincipal's qyl.tenant_id claim.
        var defaultScope = QylScope.FromEnvironment();

        var jsonOptions = services.AddQylMcpCommonServices(configuration, skills, defaultScope);
        QylMcpServerRegistration.ConfigureForHttp(services, skills, jsonOptions);

        return services;
    }
}

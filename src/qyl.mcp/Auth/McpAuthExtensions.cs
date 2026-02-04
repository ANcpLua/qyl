using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace qyl.mcp.Auth;

/// <summary>
///     Extension methods for configuring MCP authentication.
/// </summary>
public static class McpAuthExtensions
{
    /// <summary>
    ///     Adds MCP authentication services to the service collection.
    ///     Reads token from configuration or QYL_MCP_TOKEN environment variable.
    /// </summary>
    public static IServiceCollection AddMcpAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<McpAuthOptions>(options =>
        {
            // First try configuration section
            configuration.GetSection(McpAuthOptions.SectionName).Bind(options);

            // Environment variable takes precedence
            var envToken = Environment.GetEnvironmentVariable(McpAuthOptions.TokenEnvVar);
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                options.Token = envToken;
            }
        });

        // Register the handler for DI
        services.AddTransient<McpAuthHandler>();

        return services;
    }

    /// <summary>
    ///     Adds the MCP authentication handler to the HTTP client builder.
    ///     This handler adds the x-mcp-api-key header to all requests when auth is enabled.
    /// </summary>
    public static IHttpClientBuilder AddMcpAuthHandler(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<McpAuthHandler>();
    }
}

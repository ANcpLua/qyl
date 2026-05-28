using System.Text.Json;
using ANcpLua.Agents.Mcp.Hosting.Filters;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Generated;
using Qyl.Instrumentation.Instrumentation.Mcp;
using qyl.mcp.Metadata;
using qyl.mcp.Scoping;

namespace qyl.mcp.Hosting;

/// <summary>
/// Entry point for wiring the qyl MCP server into a host's DI container.
/// Public so the collector (HTTP transport) and qyl-mcp's own stdio host can
/// share the same tool/instrumentation/scope-injection setup, varying only
/// the transport.
/// </summary>
public static class QylMcpServerRegistration
{
    /// <summary>
    /// qyl-mcp's stdio dev host wiring. Existing entry point — preserved.
    /// </summary>
    public static void ConfigureForStdio(
        IServiceCollection services,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions)
    {
        var builder = ConfigureCore(services, skills, jsonOptions, transportLabel: "stdio");
        builder.WithStdioServerTransport();
    }

    /// <summary>
    /// HTTP transport wiring for the in-process collector host.
    /// Stateless mode: no <c>Mcp-Session-Id</c> required; safe behind a
    /// load-balanced collector pool.
    /// </summary>
    public static IMcpServerBuilder ConfigureForHttp(
        IServiceCollection services,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions)
    {
        var builder = ConfigureCore(services, skills, jsonOptions, transportLabel: "http");
        builder.WithHttpTransport(static o =>
        {
            o.Stateless = true;
            // ConfigureSessionOptions for per-request claim scoping is not yet wired.
        });
        // Honors [Authorize]/[AllowAnonymous] on tools; required for ASP.NET Core auth integration.
        builder.AddAuthorizationFilters();
        return builder;
    }

    /// <summary>
    /// Transport-agnostic shared setup: task store, server info, instrumentation,
    /// scope injection, capability tools, and the generated tool manifest.
    /// </summary>
    private static IMcpServerBuilder ConfigureCore(
        IServiceCollection services,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions,
        string transportLabel)
    {
        services.AddSingleton<IMcpTaskStore>(_ => new InMemoryMcpTaskStore(
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6),
            TimeSpan.FromSeconds(1),
            maxTasks: 500));

        var builder = services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = QylServerMetadata.Name, Version = QylServerMetadata.Version
            };
            options.ServerInstructions = QylServerMetadata.Instructions;
        });

        services.AddOptions<McpServerOptions>()
            .Configure<IMcpTaskStore>((options, taskStore) => options.TaskStore = taskStore);

        builder
            .UseQylMcpInstrumentation(TelemetryConstants.ActivitySource, options => options.Transport = transportLabel)
            .WithQylScopeInjection<QylScope>()
            .WithAnthropicResultSizeMeta(thresholdChars: 10_000)
            .WithTools<CapabilityTools>(jsonOptions);

        QylToolManifest.RegisterTools(builder, skills, jsonOptions);
        return builder;
    }

    // Extension point: kept for any downstream caller that still wires stdio
    // via the original entry. Forwards to ConfigureForStdio.
    [Obsolete("Use ConfigureForStdio for the qyl-mcp dev host, or ConfigureForHttp for the in-process collector host.", error: false)]
    public static void Configure(
        IServiceCollection services,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions)
        => ConfigureForStdio(services, skills, jsonOptions);
}

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

internal static class QylMcpServerRegistration
{
    public static void Configure(
        IServiceCollection services,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions)
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
            .WithStdioServerTransport()
            .UseQylMcpInstrumentation(TelemetryConstants.ActivitySource, options => options.Transport = "stdio")
            .WithQylScopeInjection<QylScope>()
            .WithAnthropicResultSizeMeta(thresholdChars: 10_000)
            .WithTools<CapabilityTools>(jsonOptions);

        QylToolManifest.RegisterTools(builder, skills, jsonOptions);
    }
}

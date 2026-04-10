using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using qyl.mcp.Scoping;
using qyl.mcp.Skills;

namespace qyl.mcp.Hosting;

internal static class QylMcpStdioHost
{
    public static async Task RunAsync(string[] args, SkillConfiguration skills, QylScope scope)
    {
        var builder = Host.CreateApplicationBuilder(args);
        QylMcpServiceCollectionExtensions.ConfigureLogging(builder.Logging);

        var jsonOptions = builder.Services.AddQylMcpCommonServices(builder.Configuration, skills, scope);

        IServiceProvider? serviceProvider = null;
        QylMcpServerRegistration.Configure(
            builder.Services,
            skills,
            jsonOptions,
            McpTransportMode.Stdio,
            null,
            () => serviceProvider);

        var host = builder.Build();
        serviceProvider = host.Services;
        await host.RunAsync().ConfigureAwait(false);
    }
}

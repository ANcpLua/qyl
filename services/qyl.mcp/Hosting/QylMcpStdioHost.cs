using ANcpLua.Agents.Mcp.Hosting.Logging;
using Microsoft.Extensions.Hosting;
using qyl.mcp.Scoping;

namespace qyl.mcp.Hosting;

internal static class QylMcpStdioHost
{
    public static async Task RunAsync(string[] args, SkillConfiguration skills, QylScope scope)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddQylMcpStdioConsole();

        var jsonOptions = builder.Services.AddQylMcpCommonServices(builder.Configuration, skills, scope);

        QylMcpServerRegistration.Configure(builder.Services, skills, jsonOptions);

        var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}

using Microsoft.Extensions.Hosting;
using qyl.mcp.Scoping;

namespace qyl.mcp.Hosting;

internal static class QylMcpStdioHost
{
    public static async Task RunAsync(string[] args, SkillConfiguration skills, QylScope scope)
    {
        var builder = Host.CreateApplicationBuilder(args);
        QylMcpServiceCollectionExtensions.ConfigureLogging(builder.Logging, true);

        var jsonOptions = builder.Services.AddQylMcpCommonServices(builder.Configuration, skills, scope);

        var serviceProviderRef = new ServiceProviderRef();
        QylMcpServerRegistration.Configure(
            builder.Services,
            skills,
            jsonOptions,
            McpTransportMode.Stdio,
            null,
            () => serviceProviderRef.Value);

        var host = builder.Build();
        serviceProviderRef.Value = host.Services;
        await host.RunAsync().ConfigureAwait(false);
    }

    private sealed class ServiceProviderRef
    {
        public IServiceProvider? Value { get; set; }
    }
}

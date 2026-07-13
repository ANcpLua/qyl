using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Host.Mcp;

namespace Qyl.Host.Tests;

public sealed class McpLifecycleTests
{
    [Fact]
    public async Task In_process_server_completes_a_real_handshake_and_is_owned_by_the_registry()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var builder = QylAppBuilder.Create();
        builder.AddMcpInProcess("tools", transport =>
        {
            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "test-server", Version = "1.0.0" },
                ToolCollection = new McpServerPrimitiveCollection<McpServerTool>()
            };
            return McpServer.Create(
                transport,
                options,
                NullLoggerFactory.Instance,
                services);
        });

        var resource = Assert.Single(builder.Resources);
        var registry = Assert.IsType<McpClientRegistry>(builder.Host.Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(McpClientRegistry) && descriptor.ImplementationInstance is not null)
            .ImplementationInstance);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var state = new QylResourceState
        {
            Name = resource.Name,
            Lifecycle = ResourceLifecycle.Starting,
            Timestamp = DateTimeOffset.UtcNow
        };

        Assert.True(await resource.ReadinessProbe!.IsReadyAsync(state, timeout.Token));
        Assert.True(registry.TryGet(resource.Name, out var client));
        Assert.NotNull(client);

        await registry.StopAsync(timeout.Token);
        Assert.False(registry.TryGet(resource.Name, out _));
        await registry.DisposeAsync();
    }
}

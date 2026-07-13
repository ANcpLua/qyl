using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Host.Mcp;

namespace Qyl.Host.Tests;

public sealed class McpTelemetryTests
{
    [Fact]
    public async Task Official_sdk_correlates_real_tool_calls_without_a_parallel_qyl_span()
    {
        var completed = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                source.Name == QylMcpBuilderExtensions.OfficialDiagnosticsName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = completed.Enqueue
        };
        ActivitySource.AddActivityListener(listener);

        await using var services = new ServiceCollection().BuildServiceProvider();
        var builder = QylAppBuilder.Create();
        builder.AddMcpInProcess("telemetry-server", transport =>
        {
            var tools = new McpServerPrimitiveCollection<McpServerTool>
            {
                McpServerTool.Create(
                    (Func<string, string>)(static value => value),
                    new McpServerToolCreateOptions { Name = "echo" })
            };
            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "telemetry-server", Version = "1.0.0" },
                ToolCollection = tools
            };
            return McpServer.Create(transport, options, NullLoggerFactory.Instance, services);
        });

        var resource = Assert.Single(builder.Resources);
        var registry = Assert.IsType<McpClientRegistry>(builder.Host.Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(McpClientRegistry) && descriptor.ImplementationInstance is not null)
            .ImplementationInstance);
        await using var registryLifetime = registry;
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
        while (completed.TryDequeue(out _))
        {
        }

        var first = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["value"] = "first" },
            cancellationToken: timeout.Token);
        var second = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["value"] = "second" },
            cancellationToken: timeout.Token);

        Assert.NotEqual(true, first.IsError);
        Assert.NotEqual(true, second.IsError);

        var callSpans = completed
            .Where(static activity => Equals(activity.GetTagItem("mcp.method.name"), "tools/call"))
            .ToArray();
        var clientSpans = callSpans.Where(static activity => activity.Kind == ActivityKind.Client).ToArray();
        var serverSpans = callSpans.Where(static activity => activity.Kind == ActivityKind.Server).ToArray();
        Assert.Equal(2, clientSpans.Length);
        Assert.Equal(2, serverSpans.Length);

        Assert.All(clientSpans, static activity =>
        {
            Assert.Equal("echo", activity.GetTagItem("gen_ai.tool.name"));
            Assert.Equal("execute_tool", activity.GetTagItem("gen_ai.operation.name"));
            Assert.False(string.IsNullOrWhiteSpace(activity.GetTagItem("network.transport") as string));
            Assert.False(string.IsNullOrWhiteSpace(activity.GetTagItem("mcp.protocol.version") as string));
            Assert.False(string.IsNullOrWhiteSpace(activity.GetTagItem("jsonrpc.request.id") as string));
        });

        var clientSession = Assert.Single(clientSpans
            .Select(static activity => activity.GetTagItem("mcp.session.id") as string)
            .Distinct());
        var serverSession = Assert.Single(serverSpans
            .Select(static activity => activity.GetTagItem("mcp.session.id") as string)
            .Distinct());
        Assert.False(string.IsNullOrWhiteSpace(clientSession));
        Assert.False(string.IsNullOrWhiteSpace(serverSession));
        Assert.NotEqual("session-correlation", clientSession);
        Assert.Equal(2, clientSpans
            .Select(static activity => activity.GetTagItem("jsonrpc.request.id") as string)
            .Distinct()
            .Count());

        Assert.All(clientSpans, clientActivity =>
        {
            var serverActivity = Assert.Single(
                serverSpans,
                candidate => candidate.ParentSpanId == clientActivity.SpanId);
            Assert.Equal(clientActivity.TraceId, serverActivity.TraceId);
            Assert.Equal(
                clientActivity.GetTagItem("jsonrpc.request.id"),
                serverActivity.GetTagItem("jsonrpc.request.id"));
        });
    }
}

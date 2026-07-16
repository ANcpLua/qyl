using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Host.Internal;
using Qyl.Host.Mcp;

namespace Qyl.Host.Tests;

[Collection(RunnerNetworkTestGroup.Name)]
public sealed class McpVerticalTests
{
    [Fact]
    public async Task Streamable_http_server_crosses_the_runner_contract_for_list_call_and_read()
    {
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(20));
        await using var server = await LiveMcpHttpServer.StartAsync("echo", timeout.Token);

        var builder = QylAppBuilder.Create();
        builder.AddMcpHttp("vertical", server.Endpoint);

        var resource = Assert.Single(builder.Resources);
        var clients = GetRegistry(builder);
        await using var clientLifetime = clients;
        var port = ClaimLoopbackPort();
        var registry = new QylResourceRegistry([resource], TimeProvider.System);
        var api = new QylRunnerApi(
            registry,
            new QylLogStore(),
            new QylResourceActions(),
            new QylAppOptions { RunnerPort = port },
            [new McpPassthroughHandler(clients, [resource])],
            NullLogger<QylRunnerApi>.Instance);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        await api.StartAsync(timeout.Token);
        try
        {
            var toolsUri = new Uri($"http://127.0.0.1:{port}/runner/mcp/vertical/tools");
            using (var notReady = await WaitForResponseAsync(http, toolsUri, timeout.Token))
            {
                Assert.Equal(HttpStatusCode.Conflict, notReady.StatusCode);
                var conflict = await notReady.Content.ReadFromJsonAsync<ConflictError>(timeout.Token);
                Assert.Equal("Conflict", Assert.IsType<ConflictError>(conflict).Title);
            }

            Assert.True(await resource.ReadinessProbe!.IsReadyAsync(
                StartingState(resource.Name),
                timeout.Token));
            Assert.True(clients.TryGet(resource.Name, out var connectedClient));
            var directList = await connectedClient.ListToolsAsync(
                new ListToolsRequestParams(),
                timeout.Token);
            Assert.Single(directList.Tools);
            Assert.NotEmpty(McpSdkJson.Serialize(directList));

            using (var listResponse = await http.GetAsync(toolsUri, timeout.Token))
            {
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
                var list = await listResponse.Content.ReadFromJsonAsync(
                    McpSdkJson.TypeInfo<ListToolsResult>(),
                    timeout.Token);
                var tool = Assert.Single(Assert.IsType<ListToolsResult>(list).Tools);
                Assert.Equal("echo", tool.Name);
                Assert.True(tool.Annotations?.ReadOnlyHint);
            }

            using (var callResponse = await PostMcpAsync(
                       http,
                       $"http://127.0.0.1:{port}/runner/mcp/vertical/tools/call",
                       new CallToolRequestParams
                       {
                           Name = "echo",
                           Arguments = new Dictionary<string, JsonElement>
                           {
                               ["value"] = JsonSerializer.SerializeToElement("live")
                           }
                       },
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.OK, callResponse.StatusCode);
                var call = await callResponse.Content.ReadFromJsonAsync(
                    McpSdkJson.TypeInfo<CallToolResult>(),
                    timeout.Token);
                Assert.Equal(
                    "echo:live",
                    Assert.IsType<TextContentBlock>(
                        Assert.Single(Assert.IsType<CallToolResult>(call).Content)).Text);
            }

            using (var readResponse = await PostMcpAsync(
                       http,
                       $"http://127.0.0.1:{port}/runner/mcp/vertical/resources/read",
                       new ReadResourceRequestParams { Uri = "qyl://resource/static" },
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
                var read = await readResponse.Content.ReadFromJsonAsync(
                    McpSdkJson.TypeInfo<ReadResourceResult>(),
                    timeout.Token);
                Assert.Equal(
                    "resource-body",
                    Assert.IsType<TextResourceContents>(Assert.Single(
                        Assert.IsType<ReadResourceResult>(read).Contents)).Text);
            }

            using (var invalidResponse = await PostJsonAsync(
                       http,
                       $"http://127.0.0.1:{port}/runner/mcp/vertical/resources/read",
                       "{}",
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
                var validation = await invalidResponse.Content.ReadFromJsonAsync<ValidationError>(timeout.Token);
                Assert.Equal("Validation Failed", Assert.IsType<ValidationError>(validation).Title);
            }

            using (var missingResponse = await http.GetAsync(
                       new Uri($"http://127.0.0.1:{port}/runner/mcp/missing/tools"),
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
                var missing = await missingResponse.Content.ReadFromJsonAsync<NotFoundError>(timeout.Token);
                Assert.Equal("Not Found", Assert.IsType<NotFoundError>(missing).Title);
            }
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await api.StopAsync(stopTimeout.Token);
            api.Dispose();
        }
    }

    [Fact]
    public async Task Official_sdk_correlates_streamable_http_tool_calls()
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

        using var timeout = CreateTimeout(TimeSpan.FromSeconds(20));
        var toolName = $"echo_{Guid.NewGuid():N}";
        await using var server = await LiveMcpHttpServer.StartAsync(toolName, timeout.Token);
        var builder = QylAppBuilder.Create();
        builder.AddMcpHttp("telemetry-server", server.Endpoint);

        var resource = Assert.Single(builder.Resources);
        var registry = GetRegistry(builder);
        await using var registryLifetime = registry;

        Assert.True(await resource.ReadinessProbe!.IsReadyAsync(
            StartingState(resource.Name),
            timeout.Token));
        Assert.True(registry.TryGet(resource.Name, out var client));
        while (completed.TryDequeue(out _))
        {
        }

        var first = await client.CallToolAsync(
            toolName,
            new Dictionary<string, object?> { ["value"] = "first" },
            cancellationToken: timeout.Token);
        var second = await client.CallToolAsync(
            toolName,
            new Dictionary<string, object?> { ["value"] = "second" },
            cancellationToken: timeout.Token);

        Assert.NotEqual(true, first.IsError);
        Assert.NotEqual(true, second.IsError);

        var callSpans = completed
            .Where(activity =>
                Equals(activity.GetTagItem("mcp.method.name"), "tools/call") &&
                Equals(activity.GetTagItem("gen_ai.tool.name"), toolName))
            .ToArray();
        var clientSpans = callSpans.Where(static activity => activity.Kind == ActivityKind.Client).ToArray();
        var serverSpans = callSpans.Where(static activity => activity.Kind == ActivityKind.Server).ToArray();
        Assert.Equal(2, clientSpans.Length);
        Assert.Equal(2, serverSpans.Length);

        Assert.All(clientSpans, activity =>
        {
            Assert.Equal(toolName, activity.GetTagItem("gen_ai.tool.name"));
            Assert.Equal("execute_tool", activity.GetTagItem("gen_ai.operation.name"));
            Assert.False(string.IsNullOrWhiteSpace(activity.GetTagItem("network.transport") as string));
            Assert.False(string.IsNullOrWhiteSpace(activity.GetTagItem("mcp.protocol.version") as string));
            Assert.False(string.IsNullOrWhiteSpace(activity.GetTagItem("jsonrpc.request.id") as string));
        });

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

    private static McpClientRegistry GetRegistry(QylAppBuilder builder) =>
        Assert.IsType<McpClientRegistry>(builder.Host.Services.Single(static descriptor =>
                descriptor.ServiceType == typeof(McpClientRegistry) &&
                descriptor.ImplementationInstance is not null)
            .ImplementationInstance);

    private static QylResourceState StartingState(string name) => new()
    {
        Name = name,
        Lifecycle = ResourceLifecycle.Starting,
        Timestamp = DateTimeOffset.UtcNow
    };

    private static CancellationTokenSource CreateTimeout(TimeSpan timeout)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string uri,
        string json,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(uri, content, cancellationToken);
    }

    private static async Task<HttpResponseMessage> PostMcpAsync<T>(
        HttpClient client,
        string uri,
        T body,
        CancellationToken cancellationToken)
    {
        using var content = new ByteArrayContent(McpSdkJson.Serialize(body));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await client.PostAsync(uri, content, cancellationToken);
    }

    private static async Task<HttpResponseMessage> WaitForResponseAsync(
        HttpClient client,
        Uri uri,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await client.GetAsync(uri, cancellationToken);
            }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(25, cancellationToken);
            }
        }
    }

    private static int ClaimLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class LiveMcpHttpServer(WebApplication application, Uri endpoint) : IAsyncDisposable
    {
        public Uri Endpoint { get; } = endpoint;

        public static async Task<LiveMcpHttpServer> StartAsync(
            string toolName,
            CancellationToken cancellationToken)
        {
            var port = ClaimLoopbackPort();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            builder.Logging.ClearProviders();
            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation
                    {
                        Name = "qyl-test-server",
                        Version = "1.0.0"
                    };
                    options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
                    {
                        McpServerTool.Create(
                            (Func<string, string>)(static value => $"echo:{value}"),
                            new McpServerToolCreateOptions
                            {
                                Name = toolName,
                                Description = "Echoes a value.",
                                ReadOnly = true
                            })
                    };
                    options.ResourceCollection = new McpServerResourceCollection
                    {
                        McpServerResource.Create(
                            options: new McpServerResourceCreateOptions
                            {
                                UriTemplate = "qyl://resource/static",
                                Name = "static"
                            },
                            method: (Func<string>)(static () => "resource-body"))
                    };
                })
                .WithHttpTransport(options => options.Stateless = false);

            var app = builder.Build();
            app.MapMcp("/mcp");
            await app.StartAsync(cancellationToken);
            return new LiveMcpHttpServer(app, new Uri($"http://127.0.0.1:{port}/mcp"));
        }

        public async ValueTask DisposeAsync()
        {
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }
}

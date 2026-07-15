using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Real_sdk_server_crosses_the_loopback_runner_contract_for_list_call_and_read()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var builder = QylAppBuilder.Create();
        builder.AddMcpInProcess("vertical", transport =>
        {
            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "vertical-server", Version = "1.0.0" },
                ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
                {
                    McpServerTool.Create(
                        (Func<string, string>)(static value => $"echo:{value}"),
                        new McpServerToolCreateOptions
                        {
                            Name = "echo",
                            Description = "Echoes a value.",
                            ReadOnly = true
                        })
                },
                ResourceCollection = new McpServerResourceCollection
                {
                    McpServerResource.Create(
                        options: new McpServerResourceCreateOptions
                        {
                            UriTemplate = "qyl://resource/static",
                            Name = "static"
                        },
                        method: (Func<string>)(static () => "resource-body"))
                }
            };
            return McpServer.Create(transport, options, NullLoggerFactory.Instance, services);
        });

        var resource = Assert.Single(builder.Resources);
        var clients = Assert.IsType<McpClientRegistry>(builder.Host.Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(McpClientRegistry) && descriptor.ImplementationInstance is not null)
            .ImplementationInstance);
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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
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

            var state = new QylResourceState
            {
                Name = resource.Name,
                Lifecycle = ResourceLifecycle.Starting,
                Timestamp = DateTimeOffset.UtcNow
            };
            Assert.True(await resource.ReadinessProbe!.IsReadyAsync(state, timeout.Token));
            Assert.True(clients.TryGet(resource.Name, out var connectedClient));
            var directList = await connectedClient.ListToolsAsync(new ListToolsRequestParams(), timeout.Token);
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
            timeout.Cancel();
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await api.StopAsync(stopTimeout.Token);
            api.Dispose();
        }
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
}

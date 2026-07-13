using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Runner;
using Qyl.Host.Internal;

namespace Qyl.Host.Tests;

[Collection(RunnerNetworkTestGroup.Name)]
public sealed class RunnerApiTests
{
    [Fact]
    public async Task Unexpected_failure_before_response_returns_the_generated_internal_server_error()
    {
        var port = ClaimLoopbackPort();
        var api = new QylRunnerApi(
            new QylResourceRegistry([], TimeProvider.System),
            new QylLogStore(),
            new QylResourceActions(),
            new QylAppOptions { RunnerPort = port },
            [new ThrowingRunnerRequestHandler()],
            NullLogger<QylRunnerApi>.Instance);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        await api.StartAsync(lifetime.Token);
        try
        {
            using var response = await WaitForResponseAsync(
                client,
                new Uri($"http://127.0.0.1:{port}/runner/resources"),
                lifetime.Token);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<InternalServerError>(lifetime.Token);
            var error = Assert.IsType<InternalServerError>(problem);
            Assert.Equal("Internal Server Error", error.Title);
            Assert.Equal("runner.unhandled_exception", error.ErrorCode);
            Assert.Equal((int)HttpStatusCode.InternalServerError, error.Status);
        }
        finally
        {
            lifetime.Cancel();
            using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            stopTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            await api.StopAsync(stopTimeout.Token);
            api.Dispose();
        }
    }

    [Fact]
    public async Task Runner_is_loopback_only_has_no_wildcard_cors_and_rejects_an_untrusted_host()
    {
        var port = ClaimLoopbackPort();
        var resource = new QylResource
        {
            Name = "demo",
            Kind = QylResourceKind.Command,
            Port = 54321
        };
        var registry = new QylResourceRegistry([resource], TimeProvider.System);
        registry.Publish(
            resource.Name,
            ResourceLifecycle.Ready,
            resource.Port,
            new Uri($"http://127.0.0.1:{resource.Port}"));
        var logs = new QylLogStore();
        var actions = new QylResourceActions();
        logs.Append("demo", isError: false, "one line");
        var api = new QylRunnerApi(
            registry,
            logs,
            actions,
            new QylAppOptions { RunnerPort = port },
            [],
            NullLogger<QylRunnerApi>.Instance);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        await api.StartAsync(lifetime.Token);
        try
        {
            var resourcesUri = new Uri($"http://127.0.0.1:{port}/runner/resources");
            using var response = await WaitForResponseAsync(client, resourcesUri, lifetime.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            await using (var body = await response.Content.ReadAsStreamAsync(lifetime.Token))
            {
                var states = await JsonSerializer.DeserializeAsync<RunnerResourceState[]>(
                    body,
                    cancellationToken: lifetime.Token);
                var state = Assert.Single(Assert.IsType<RunnerResourceState[]>(states));
                Assert.Equal(resource.Name, state.Name);
                Assert.Equal(RunnerResourceLifecycle.Ready, state.Lifecycle);
                Assert.Equal(RunnerResourceKind.Command, state.Kind);
                Assert.Equal(resource.Port, state.AllocatedPort);
            }

            using var logsResponse = await client.GetAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/logs"), lifetime.Token);
            Assert.Equal(HttpStatusCode.OK, logsResponse.StatusCode);
            await using (var body = await logsResponse.Content.ReadAsStreamAsync(lifetime.Token))
            {
                var lines = await JsonSerializer.DeserializeAsync<RunnerLogLine[]>(
                    body,
                    cancellationToken: lifetime.Token);
                var line = Assert.Single(Assert.IsType<RunnerLogLine[]>(lines));
                Assert.Equal("one line", line.Line);
                Assert.Equal(RunnerLogStream.Stdout, line.Stream);
            }

            using var missingLogs = await client.GetAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/missing/logs"), lifetime.Token);
            Assert.Equal(HttpStatusCode.NotFound, missingLogs.StatusCode);
            Assert.Equal(ProblemDetailsMediaType.Value, missingLogs.Content.Headers.ContentType?.MediaType);
            var problem = await missingLogs.Content.ReadFromJsonAsync<NotFoundError>(lifetime.Token);
            var notFound = Assert.IsType<NotFoundError>(problem);
            Assert.Equal("Not Found", notFound.Title);
            Assert.Equal("missing", notFound.ResourceId);

            using (var streamResponse = await client.GetAsync(
                       new Uri($"http://127.0.0.1:{port}/runner/resources/stream"),
                       HttpCompletionOption.ResponseHeadersRead,
                       lifetime.Token))
            {
                Assert.Equal(HttpStatusCode.OK, streamResponse.StatusCode);
                await using var stream = await streamResponse.Content.ReadAsStreamAsync(lifetime.Token);
                using var reader = new StreamReader(stream);
                Assert.Equal("event: message", await reader.ReadLineAsync(lifetime.Token));
                var data = Assert.IsType<string>(await reader.ReadLineAsync(lifetime.Token));
                Assert.StartsWith("data: ", data, StringComparison.Ordinal);
                var streamed = JsonSerializer.Deserialize<RunnerResourceState>(data[6..]);
                Assert.Equal(resource.Name, Assert.IsType<RunnerResourceState>(streamed).Name);
                Assert.Equal(string.Empty, await reader.ReadLineAsync(lifetime.Token));
            }

            using var securityHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false
            };
            using var securityClient = new HttpClient(securityHandler, disposeHandler: false)
            {
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Timeout = TimeSpan.FromSeconds(2)
            };
            using var badHostRequest = new HttpRequestMessage(HttpMethod.Get, resourcesUri);
            badHostRequest.Headers.Host = "attacker.example";
            using var badHostResponse = await securityClient.SendAsync(badHostRequest, lifetime.Token);
            Assert.Contains(
                badHostResponse.StatusCode,
                new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });

            using var crossOrigin = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/stop"));
            crossOrigin.Headers.Add("Origin", "https://attacker.example");
            crossOrigin.Headers.Add("Sec-Fetch-Site", "cross-site");
            crossOrigin.Content = JsonContent.Create(new { });
            using var crossOriginResponse = await securityClient.SendAsync(crossOrigin, lifetime.Token);
            Assert.Equal(HttpStatusCode.Forbidden, crossOriginResponse.StatusCode);
            Assert.Equal(ProblemDetailsMediaType.Value, crossOriginResponse.Content.Headers.ContentType?.MediaType);
            var forbidden = await crossOriginResponse.Content.ReadFromJsonAsync<ForbiddenError>(lifetime.Token);
            var forbiddenError = Assert.IsType<ForbiddenError>(forbidden);
            Assert.Equal("Forbidden", forbiddenError.Title);
            Assert.Equal("same-origin runner control", forbiddenError.RequiredPermission);

            using var simpleForm = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/stop"))
            {
                Content = new FormUrlEncodedContent([])
            };
            using var simpleFormResponse = await securityClient.SendAsync(simpleForm, lifetime.Token);
            Assert.Equal(HttpStatusCode.Forbidden, simpleFormResponse.StatusCode);
        }
        finally
        {
            lifetime.Cancel();
            using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            stopTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            await api.StopAsync(stopTimeout.Token);
            api.Dispose();
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

    private sealed class ThrowingRunnerRequestHandler : IQylRunnerRequestHandler
    {
        public Task<bool> TryHandleAsync(
            HttpListenerContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Programmatic pre-response failure.");
    }
}

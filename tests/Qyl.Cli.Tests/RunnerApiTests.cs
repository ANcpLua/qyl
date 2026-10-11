using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Runner;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[Collection(RunnerNetworkTestGroup.Name)]
public sealed partial class RunnerApiTests
{
    private const string DemoResourceName = "demo";
    private const int DemoResourcePort = 54321;

    // A Host that matches no listener prefix. The HttpListener itself answers such a request (404 on the
    // managed listener, before QylRunnerApi.HandleAsync runs), so the runner's own Host check is a second line.
    private const string UntrustedHost = "attacker.example";

    [Fact]
    public async Task Runner_bind_failure_fails_startup_instead_of_silently_removing_control()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var port = ((IPEndPoint)occupied.LocalEndpoint).Port;
        var api = new QylRunnerApi(
            new QylResourceRegistry([], TimeProvider.System),
            new QylLogStore(),
            new QylResourceActions(),
            new QylAppOptions { RunnerPort = port },
            NullLogger<QylRunnerApi>.Instance);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await Assert.ThrowsAsync<HttpListenerException>(
                () => api.StartAsync(cancellation.Token));
        }
        finally
        {
            api.Dispose();
        }
    }

    [Fact]
    public async Task Runner_is_loopback_only_has_no_wildcard_cors_and_rejects_an_untrusted_host()
    {
        var port = ClaimLoopbackPort();
        var logs = new QylLogStore();
        logs.Append(DemoResourceName, isError: false, "one line");
        using var api = CreateDemoApi(port, logs, new QylResourceActions());
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        await api.StartAsync(lifetime.Token);
        try
        {
            var resourcesUri = new Uri($"http://127.0.0.1:{port}/runner/resources");
            using var response = await WaitForResponseAsync(client, resourcesUri, lifetime.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            await using (var body = await response.Content.ReadAsStreamAsync(lifetime.Token))
            {
                var states = await JsonSerializer.DeserializeAsync(
                    body,
                    QylRunnerJsonContext.Default.RunnerResourceStateArray,
                    lifetime.Token);
                var state = Assert.Single(Assert.IsType<RunnerResourceState[]>(states));
                Assert.Equal(DemoResourceName, state.Name);
                Assert.Equal(RunnerResourceLifecycle.Ready, state.Lifecycle);
                Assert.Equal(RunnerResourceKind.Command, state.Kind);
                Assert.Equal(DemoResourcePort, state.AllocatedPort);
            }

            using var logsResponse = await client.GetAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/logs"), lifetime.Token);
            Assert.Equal(HttpStatusCode.OK, logsResponse.StatusCode);
            await using (var body = await logsResponse.Content.ReadAsStreamAsync(lifetime.Token))
            {
                var lines = await JsonSerializer.DeserializeAsync(
                    body,
                    QylRunnerJsonContext.Default.RunnerLogLineArray,
                    lifetime.Token);
                var line = Assert.Single(Assert.IsType<RunnerLogLine[]>(lines));
                Assert.Equal("one line", line.Line);
                Assert.Equal(RunnerLogStream.Stdout, line.Stream);
            }

            using var missingLogs = await client.GetAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/missing/logs"), lifetime.Token);
            Assert.Equal(HttpStatusCode.NotFound, missingLogs.StatusCode);
            Assert.Equal(ProblemDetailsMediaType.Value, missingLogs.Content.Headers.ContentType?.MediaType);
            var problem = await missingLogs.Content.ReadFromJsonAsync(QylRunnerJsonContext.Default.NotFoundError, lifetime.Token);
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
                var streamed = JsonSerializer.Deserialize(data[6..], QylRunnerJsonContext.Default.RunnerResourceState);
                Assert.Equal(DemoResourceName, Assert.IsType<RunnerResourceState>(streamed).Name);
                Assert.Equal(string.Empty, await reader.ReadLineAsync(lifetime.Token));
            }

            using var securityHandler = new SocketsHttpHandler();
            securityHandler.AllowAutoRedirect = false;
            securityHandler.UseProxy = false;
            using var securityClient = new HttpClient(securityHandler, disposeHandler: false);
            securityClient.DefaultRequestVersion = HttpVersion.Version11;
            securityClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            securityClient.Timeout = TimeSpan.FromSeconds(2);
            using var badHostRequest = new HttpRequestMessage(HttpMethod.Get, resourcesUri);
            badHostRequest.Headers.Host = UntrustedHost;
            // On a keep-alive connection the managed HttpListener follows its 404 for this Host with a stray
            // "200 OK" before it closes the socket (Untrusted_host_probe_leaves_exactly_one_response_on_its_connection).
            // Closing the connection keeps that stray response off the pooled connection the next probe would
            // reuse, where CI read it as the answer to the cross-site POST (QYL-F1).
            badHostRequest.Headers.ConnectionClose = true;
            using var badHostResponse = await securityClient.SendAsync(badHostRequest, lifetime.Token);
            Assert.Contains(
                badHostResponse.StatusCode,
                new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });

            using var crossOrigin = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/stop"));
            crossOrigin.Headers.Add("Origin", $"https://{UntrustedHost}");
            crossOrigin.Headers.Add("Sec-Fetch-Site", "cross-site");
            crossOrigin.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var crossOriginResponse = await securityClient.SendAsync(crossOrigin, lifetime.Token);
            if (crossOriginResponse.StatusCode != HttpStatusCode.Forbidden)
            {
                // Names what answered: the runner's handler always writes application/problem+json, the
                // HttpListener's own responses (prefix mismatch, connection teardown) never do.
                var unexpectedBody = await crossOriginResponse.Content.ReadAsStringAsync(lifetime.Token);
                var producer = crossOriginResponse.Content.Headers.ContentType?.MediaType == ProblemDetailsMediaType.Value
                    ? "QylRunnerApi.HandleAsync"
                    : "the HttpListener itself, not QylRunnerApi.HandleAsync";
                Assert.Fail(
                    $"Cross-site POST to port {port} answered {(int)crossOriginResponse.StatusCode} " +
                    $"({crossOriginResponse.Content.Headers.ContentType}) from {producer}; " +
                    $"headers: {crossOriginResponse.Headers}; body: {unexpectedBody}");
            }
            Assert.Equal(ProblemDetailsMediaType.Value, crossOriginResponse.Content.Headers.ContentType?.MediaType);
            var forbidden = await crossOriginResponse.Content.ReadFromJsonAsync(QylRunnerJsonContext.Default.ForbiddenError, lifetime.Token);
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
            await StopAsync(api);
        }
    }

    [Fact]
    public async Task Untrusted_host_probe_leaves_exactly_one_response_on_its_connection()
    {
        // The wire the untrusted-host probe above relies on, read raw so nothing is pooled or retried. Without
        // "Connection: close" the managed HttpListener answers 404, re-arms the keep-alive connection and then
        // tears it down through a fresh response that goes out as a bare "200 OK" (QYL-F1).
        var port = ClaimLoopbackPort();
        using var api = CreateDemoApi(port, new QylLogStore(), new QylResourceActions());
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));

        await api.StartAsync(lifetime.Token);
        try
        {
            using var connection = new TcpClient();
            await connection.ConnectAsync(IPAddress.Loopback, port, lifetime.Token);
            var stream = connection.GetStream();
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    $"GET /runner/resources HTTP/1.1\r\nHost: {UntrustedHost}\r\nConnection: close\r\n\r\n"),
                lifetime.Token);
            var wire = await ReadUntilClosedAsync(stream, lifetime.Token);

            var statusLines = StatusLine().Matches(wire).Select(static match => match.Value).ToArray();
            var statusLine = Assert.Single(statusLines);
            Assert.DoesNotMatch("^HTTP/1\\.1 2", statusLine);
            Assert.Contains("\r\nConnection: close\r\n", wire, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            lifetime.Cancel();
            await StopAsync(api);
        }
    }

    [Fact]
    public async Task Resource_action_still_pending_at_shutdown_is_answered_503_not_200()
    {
        // No orchestrator reads the action queue here, so the restart stays pending until the runner stops.
        // HttpListener.Stop() alone would end that response as "200 OK": the runner must answer first.
        var port = ClaimLoopbackPort();
        var actions = new QylResourceActions();
        using var api = CreateDemoApi(port, new QylLogStore(), actions);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        await api.StartAsync(lifetime.Token);
        Task<HttpResponseMessage> pending;
        try
        {
            pending = client.PostAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/restart"),
                content: null,
                lifetime.Token);
            while (actions.Reader.Count == 0)
                await Task.Delay(10, lifetime.Token);
        }
        finally
        {
            await StopAsync(api);
        }

        using var response = await pending;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync(QylRunnerJsonContext.Default.ServiceUnavailableError, lifetime.Token);
        var unavailable = Assert.IsType<ServiceUnavailableError>(problem);
        Assert.Equal("Service Unavailable", unavailable.Title);
        Assert.Equal("runner.shutting_down", unavailable.Reason);
    }

    [GeneratedRegex(@"^HTTP/1\.1 \d{3} ", RegexOptions.Multiline)]
    private static partial Regex StatusLine();

    private static QylRunnerApi CreateDemoApi(int port, QylLogStore logs, QylResourceActions actions)
    {
        var resource = new QylResource
        {
            Name = DemoResourceName,
            Kind = QylResourceKind.Command,
            Port = DemoResourcePort,
            Launch = new QylLaunchSpec { Executable = "test" }
        };
        var registry = new QylResourceRegistry([resource], TimeProvider.System);
        registry.Publish(
            resource.Name,
            ResourceLifecycle.Ready,
            resource.Port,
            new Uri($"http://127.0.0.1:{resource.Port}"));
        return new QylRunnerApi(
            registry,
            logs,
            actions,
            new QylAppOptions { RunnerPort = port },
            NullLogger<QylRunnerApi>.Instance);
    }

    private static async Task StopAsync(QylRunnerApi api)
    {
        using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stopTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        await api.StopAsync(stopTimeout.Token);
    }

    private static int ClaimLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> ReadUntilClosedAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var received = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) return Encoding.ASCII.GetString(received.ToArray());
            received.Write(buffer, 0, read);
        }
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
}

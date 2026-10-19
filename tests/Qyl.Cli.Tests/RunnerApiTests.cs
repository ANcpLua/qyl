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

[DoNotParallelize]
[TestClass]
public sealed partial class RunnerApiTests(TestContext testContext)
{

    private static readonly HttpClient s_client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly HttpClient s_securityClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false
    })
    {
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        Timeout = TimeSpan.FromSeconds(2)
    };

    private const string DemoResourceName = "demo";
    private const int DemoResourcePort = 54321;

    // A Host that matches no listener prefix. The HttpListener itself answers such a request (404 on the
    // managed listener, before QylRunnerApi.HandleAsync runs), so the runner's own Host check is a second line.
    private const string UntrustedHost = "attacker.example";

    [TestMethod]
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
            testContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await Assert.ThrowsExactlyAsync<HttpListenerException>(
                () => api.StartAsync(cancellation.Token));
        }
        finally
        {
            api.Dispose();
        }
    }

    [TestMethod]
    public async Task Runner_is_loopback_only_has_no_wildcard_cors_and_rejects_an_untrusted_host()
    {
        var port = ClaimLoopbackPort();
        var logs = new QylLogStore();
        logs.Append(DemoResourceName, isError: false, "one line");
        using var api = CreateDemoApi(port, logs, new QylResourceActions());
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));

        await api.StartAsync(lifetime.Token);
        try
        {
            var resourcesUri = new Uri($"http://127.0.0.1:{port}/runner/resources");
            using var response = await WaitForResponseAsync(s_client, resourcesUri, lifetime.Token);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
            await using (var body = await response.Content.ReadAsStreamAsync(lifetime.Token))
            {
                var states = await JsonSerializer.DeserializeAsync(
                    body,
                    QylRunnerJsonContext.Default.RunnerResourceStateArray,
                    lifetime.Token);
                var state = Assert.ContainsSingle(Assert.IsExactInstanceOfType<RunnerResourceState[]>(states));
                Assert.AreEqual(DemoResourceName, state.Name);
                Assert.AreEqual(RunnerResourceLifecycle.Ready, state.Lifecycle);
                Assert.AreEqual(RunnerResourceKind.Command, state.Kind);
                Assert.AreEqual(DemoResourcePort, state.AllocatedPort);
            }

            using var logsResponse = await s_client.GetAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/logs"), lifetime.Token);
            Assert.AreEqual(HttpStatusCode.OK, logsResponse.StatusCode);
            await using (var body = await logsResponse.Content.ReadAsStreamAsync(lifetime.Token))
            {
                var lines = await JsonSerializer.DeserializeAsync(
                    body,
                    QylRunnerJsonContext.Default.RunnerLogLineArray,
                    lifetime.Token);
                var line = Assert.ContainsSingle(Assert.IsExactInstanceOfType<RunnerLogLine[]>(lines));
                Assert.AreEqual("one line", line.Line);
                Assert.AreEqual(RunnerLogStream.Stdout, line.Stream);
            }

            using var missingLogs = await s_client.GetAsync(
                new Uri($"http://127.0.0.1:{port}/runner/resources/missing/logs"), lifetime.Token);
            Assert.AreEqual(HttpStatusCode.NotFound, missingLogs.StatusCode);
            Assert.AreEqual(ProblemDetailsMediaType.Value, missingLogs.Content.Headers.ContentType?.MediaType);
            var problem = await missingLogs.Content.ReadFromJsonAsync(QylRunnerJsonContext.Default.NotFoundError, lifetime.Token);
            var notFound = Assert.IsExactInstanceOfType<NotFoundError>(problem);
            Assert.AreEqual("Not Found", notFound.Title);
            Assert.AreEqual("missing", notFound.ResourceId);

            using (var streamResponse = await s_client.GetAsync(
                       new Uri($"http://127.0.0.1:{port}/runner/resources/stream"),
                       HttpCompletionOption.ResponseHeadersRead,
                       lifetime.Token))
            {
                Assert.AreEqual(HttpStatusCode.OK, streamResponse.StatusCode);
                await using var stream = await streamResponse.Content.ReadAsStreamAsync(lifetime.Token);
                using var reader = new StreamReader(stream);
                Assert.AreEqual("event: message", await reader.ReadLineAsync(lifetime.Token));
                var data = Assert.IsExactInstanceOfType<string>(await reader.ReadLineAsync(lifetime.Token));
                Assert.StartsWith("data: ", data, StringComparison.Ordinal);
                var streamed = JsonSerializer.Deserialize(data[6..], QylRunnerJsonContext.Default.RunnerResourceState);
                Assert.AreEqual(DemoResourceName, Assert.IsExactInstanceOfType<RunnerResourceState>(streamed).Name);
                Assert.AreEqual(string.Empty, await reader.ReadLineAsync(lifetime.Token));
            }

            using var badHostRequest = new HttpRequestMessage(HttpMethod.Get, resourcesUri);
            badHostRequest.Headers.Host = UntrustedHost;
            // On a keep-alive connection the managed HttpListener follows its 404 for this Host with a stray
            // "200 OK" before it closes the socket (Untrusted_host_probe_leaves_exactly_one_response_on_its_connection).
            // Closing the connection keeps that stray response off the pooled connection the next probe would
            // reuse, where CI read it as the answer to the cross-site POST (QYL-F1).
            badHostRequest.Headers.ConnectionClose = true;
            using var badHostResponse = await s_securityClient.SendAsync(badHostRequest, lifetime.Token);
            Assert.Contains(
                badHostResponse.StatusCode,
                new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });

            using var crossOrigin = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/stop"));
            crossOrigin.Headers.Add("Origin", $"https://{UntrustedHost}");
            crossOrigin.Headers.Add("Sec-Fetch-Site", "cross-site");
            crossOrigin.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var crossOriginResponse = await s_securityClient.SendAsync(crossOrigin, lifetime.Token);
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
            Assert.AreEqual(ProblemDetailsMediaType.Value, crossOriginResponse.Content.Headers.ContentType?.MediaType);
            var forbidden = await crossOriginResponse.Content.ReadFromJsonAsync(QylRunnerJsonContext.Default.ForbiddenError, lifetime.Token);
            var forbiddenError = Assert.IsExactInstanceOfType<ForbiddenError>(forbidden);
            Assert.AreEqual("Forbidden", forbiddenError.Title);
            Assert.AreEqual("same-origin runner control", forbiddenError.RequiredPermission);

            using var simpleForm = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri($"http://127.0.0.1:{port}/runner/resources/demo/stop"));
            simpleForm.Content = new FormUrlEncodedContent([]);
            using var simpleFormResponse = await s_securityClient.SendAsync(simpleForm, lifetime.Token);
            Assert.AreEqual(HttpStatusCode.Forbidden, simpleFormResponse.StatusCode);
        }
        finally
        {
            await lifetime.CancelAsync();
            await StopAsync(api);
        }
    }

    [TestMethod]
    public async Task Untrusted_host_probe_leaves_exactly_one_response_on_its_connection()
    {
        // The wire the untrusted-host probe above relies on, read raw so nothing is pooled or retried. Without
        // "Connection: close" the managed HttpListener answers 404, re-arms the keep-alive connection and then
        // tears it down through a fresh response that goes out as a bare "200 OK" (QYL-F1).
        var port = ClaimLoopbackPort();
        using var api = CreateDemoApi(port, new QylLogStore(), new QylResourceActions());
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
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
            var statusLine = Assert.ContainsSingle(statusLines);
            Assert.DoesNotMatchRegex("^HTTP/1\\.1 2",statusLine);
            Assert.Contains("\r\nConnection: close\r\n", wire, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await lifetime.CancelAsync();
            await StopAsync(api);
        }
    }

    [TestMethod]
    public async Task Resource_action_still_pending_at_shutdown_is_answered_503_not_200()
    {
        // No orchestrator reads the action queue here, so the restart stays pending until the runner stops.
        // HttpListener.Stop() alone would end that response as "200 OK": the runner must answer first.
        var port = ClaimLoopbackPort();
        var actions = new QylResourceActions();
        using var api = CreateDemoApi(port, new QylLogStore(), actions);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));

        await api.StartAsync(lifetime.Token);
        Task<HttpResponseMessage> pending;
        try
        {
            pending = s_client.PostAsync(
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
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync(QylRunnerJsonContext.Default.ServiceUnavailableError, lifetime.Token);
        var unavailable = Assert.IsExactInstanceOfType<ServiceUnavailableError>(problem);
        Assert.AreEqual("Service Unavailable", unavailable.Title);
        Assert.AreEqual("runner.shutting_down", unavailable.Reason);
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

    private async Task StopAsync(QylRunnerApi api)
    {
        using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
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

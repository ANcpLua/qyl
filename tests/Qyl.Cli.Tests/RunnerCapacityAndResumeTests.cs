using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[Collection(RunnerNetworkTestGroup.Name)]
public sealed class RunnerCapacityAndResumeTests
{
    [Fact]
    public async Task Log_stream_resumes_after_last_event_id_without_replaying_delivered_lines()
    {
        var (api, port, logs) = CreateApi();
        using var ownedApi = api;
        logs.Append("worker", isError: false, "first");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        await api.StartAsync(timeout.Token);
        try
        {
            var uri = new Uri($"http://127.0.0.1:{port}/runner/resources/worker/logs/stream");
            using var initial = await SendStreamAsync(client, uri, lastEventId: null, timeout.Token);
            await using var initialBody = await initial.Content.ReadAsStreamAsync(timeout.Token);
            using var initialReader = new StreamReader(initialBody);
            var first = await ReadLogFrameAsync(initialReader, timeout.Token);
            Assert.Equal("1", first.Id);
            Assert.Contains("\"line\":\"first\"", first.Data, StringComparison.Ordinal);

            logs.Append("worker", isError: false, "second");
            logs.Append("worker", isError: true, "third");

            using var resumed = await SendStreamAsync(client, uri, first.Id, timeout.Token);
            await using var resumedBody = await resumed.Content.ReadAsStreamAsync(timeout.Token);
            using var resumedReader = new StreamReader(resumedBody);
            var second = await ReadLogFrameAsync(resumedReader, timeout.Token);
            var third = await ReadLogFrameAsync(resumedReader, timeout.Token);

            Assert.Equal("2", second.Id);
            Assert.Contains("\"line\":\"second\"", second.Data, StringComparison.Ordinal);
            Assert.Equal("3", third.Id);
            Assert.Contains("\"line\":\"third\"", third.Data, StringComparison.Ordinal);
        }
        finally
        {
            await StopAsync(api);
        }
    }

    [Fact]
    public async Task Runner_rejects_excess_long_lived_streams_with_the_generated_503()
    {
        var (api, port, _) = CreateApi();
        using var ownedApi = api;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var admitted = new List<HttpResponseMessage>();

        await api.StartAsync(timeout.Token);
        try
        {
            var uri = new Uri($"http://127.0.0.1:{port}/runner/resources/stream");
            for (var index = 0; index < QylRunnerApi.MaxConcurrentStreams; index++)
            {
                var response = await SendStreamAsync(client, uri, lastEventId: null, timeout.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                admitted.Add(response);
            }

            using var rejected = await SendStreamAsync(client, uri, lastEventId: null, timeout.Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
            Assert.Equal(ProblemDetailsMediaType.Value, rejected.Content.Headers.ContentType?.MediaType);
            var problem = await rejected.Content.ReadFromJsonAsync<ServiceUnavailableError>(timeout.Token);
            Assert.Equal("runner.stream_capacity", Assert.IsType<ServiceUnavailableError>(problem).Reason);
        }
        finally
        {
            foreach (var response in admitted) response.Dispose();
            await StopAsync(api);
        }
    }

    private static (QylRunnerApi Api, int Port, QylLogStore Logs) CreateApi()
    {
        var port = ClaimLoopbackPort();
        var resource = new QylResource
        {
            Name = "worker",
            Kind = QylResourceKind.Command,
            Port = 54321,
            Launch = new QylLaunchSpec { Executable = "test" }
        };
        var registry = new QylResourceRegistry([resource], TimeProvider.System);
        registry.Publish(resource.Name, ResourceLifecycle.Ready, resource.Port, new Uri("http://127.0.0.1:54321"));
        var logs = new QylLogStore();
        var api = new QylRunnerApi(
            registry,
            logs,
            new QylResourceActions(),
            new QylAppOptions { RunnerPort = port },
            NullLogger<QylRunnerApi>.Instance);
        return (api, port, logs);
    }

    private static async Task<HttpResponseMessage> SendStreamAsync(
        HttpClient client,
        Uri uri,
        string? lastEventId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (lastEventId is not null) request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
            try
            {
                return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(25, cancellationToken);
            }
        }
    }

    private static async Task<(string Id, string Data)> ReadLogFrameAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var idLine = Assert.IsType<string>(await reader.ReadLineAsync(cancellationToken));
        var eventLine = Assert.IsType<string>(await reader.ReadLineAsync(cancellationToken));
        var dataLine = Assert.IsType<string>(await reader.ReadLineAsync(cancellationToken));
        Assert.Equal(string.Empty, await reader.ReadLineAsync(cancellationToken));
        Assert.StartsWith("id: ", idLine, StringComparison.Ordinal);
        Assert.Equal("event: message", eventLine);
        Assert.StartsWith("data: ", dataLine, StringComparison.Ordinal);
        return (idLine[4..], dataLine[6..]);
    }

    private static int ClaimLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task StopAsync(QylRunnerApi api)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await api.StopAsync(timeout.Token);
    }

}

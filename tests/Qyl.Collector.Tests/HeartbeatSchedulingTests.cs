using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Qyl.Api.Contracts.Streaming;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class HeartbeatSchedulingTests(TestContext testContext)
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private FakeTimeProvider _fake = null!;
    private TimerObservingTimeProvider _clock = null!;
    private CollectorApiTestHost? _host;

    private CollectorApiTestHost Host => _host ?? throw new InvalidOperationException("The test host has not started.");

    [TestInitialize]
    public async Task StartHostAsync()
    {
        _fake = new FakeTimeProvider(s_now);
        _clock = new TimerObservingTimeProvider(_fake);
        _host = await StartAsync(testContext.CancellationToken, timeProvider: _clock);
    }

    [TestCleanup]
    public async Task StopHostAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Heartbeats_wait_for_the_registered_interval_and_report_the_advanced_time()
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        await using var stream = CreateStream(lifetime.Token);
        Task<bool>? pending = null;
        try
        {
            Assert.IsTrue(await stream.MoveNextAsync());
            Assert.AreEqual("heartbeat", stream.Current.EventType);
            var first = stream.Current.Heartbeat;
            Assert.IsNotNull(first);
            Assert.AreEqual(s_now, first.Timestamp);

            pending = stream.MoveNextAsync().AsTask();
            Assert.AreEqual(TimeSpan.FromSeconds(1), await _clock.WaitForTimerAsync(lifetime.Token));
            _fake.Advance(TimeSpan.FromMilliseconds(999));
            Assert.IsFalse(pending.IsCompleted);

            _fake.Advance(TimeSpan.FromMilliseconds(1));
            Assert.IsTrue(await pending.WaitAsync(lifetime.Token));
            var second = stream.Current.Heartbeat;
            Assert.IsNotNull(second);
            Assert.AreEqual(s_now.AddSeconds(1), second.Timestamp);

            pending = stream.MoveNextAsync().AsTask();
            Assert.AreEqual(TimeSpan.FromSeconds(1), await _clock.WaitForTimerAsync(lifetime.Token));
            _fake.Advance(TimeSpan.FromSeconds(1));
            Assert.IsTrue(await pending.WaitAsync(lifetime.Token));
            var third = stream.Current.Heartbeat;
            Assert.IsNotNull(third);
            Assert.AreEqual(s_now.AddSeconds(2), third.Timestamp);
        }
        finally
        {
            await lifetime.CancelAsync();
            if (pending is not null)
            {
                try { await pending; }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            }
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Cancellation_finishes_a_registered_heartbeat_wait_without_advancing_time()
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        await using var stream = CreateStream(lifetime.Token);
        Task<bool>? pending = null;
        try
        {
            Assert.IsTrue(await stream.MoveNextAsync());
            pending = stream.MoveNextAsync().AsTask();
            await _clock.WaitForTimerAsync(lifetime.Token);
            Assert.IsFalse(pending.IsCompleted);

            await lifetime.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            Assert.IsFalse(_clock.TryReadTimer(out _));
        }
        finally
        {
            await lifetime.CancelAsync();
            if (pending is not null)
            {
                try { await pending; }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            }
        }
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task The_http_stream_uses_the_host_clock_for_heartbeat_timestamps()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/stream/logs");
        request.Headers.Add(OtlpConstants.ApiKeyHeaderName, ReaderApiKey);
        using var response = await Host.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            testContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        await using var body = await response.Content.ReadAsStreamAsync(testContext.CancellationToken);
        using var reader = new StreamReader(body, leaveOpen: true);
        Assert.AreEqual("event: heartbeat", await reader.ReadLineAsync(testContext.CancellationToken));
        var data = await reader.ReadLineAsync(testContext.CancellationToken);
        Assert.IsNotNull(data);
        var parts = data.Split(':', 2);
        Assert.AreEqual("data", parts[0]);
        var heartbeat = JsonSerializer.Deserialize(parts[1], QylSerializerContext.Default.HeartbeatEvent);
        Assert.IsNotNull(heartbeat);
        Assert.AreEqual(s_now, heartbeat.Timestamp);
    }

    private IAsyncEnumerator<(string EventType, long? EventId, LogStreamEvent? Log, HeartbeatEvent? Heartbeat)>
        CreateStream(CancellationToken cancellationToken) =>
        CollectorEndpointExtensions.StreamLogEventsAsync(Host.Store, _clock, AlphaProjectId,
            serviceName: null, minSeverity: null, query: null, afterIngestSequence: null, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
}

using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace Qyl.Cli.Tests;

[TestClass]
public sealed class HttpHealthProbeTests(TestContext testContext) : IAsyncDisposable
{
    private static readonly TimeSpan s_budget = TimeSpan.FromSeconds(2);
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private FakeTimeProvider _fake = null!;
    private TimerObservingTimeProvider _clock = null!;
    private CancellationTokenSource? _lifetime;
    private ProbeHandler? _handler;
    private Task<bool>? _pending;
    private ProbeHandler Handler => _handler ?? throw new InvalidOperationException("The probe has not started.");

    [TestInitialize]
    public void CreateClock()
    {
        _fake = new FakeTimeProvider(s_now);
        _clock = new TimerObservingTimeProvider(_fake);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task The_startup_budget_cancels_and_joins_an_in_flight_request()
    {
        var pending = StartProbe(stall: true);
        await Handler.Started.Task.WaitAsync(testContext.CancellationToken);
        Assert.IsTrue(_clock.TryReadTimer(out var budget), "The request must have a registered startup deadline.");
        Assert.AreEqual(s_budget, budget);
        _fake.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.IsFalse(pending.IsCompleted);
        _fake.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsFalse(await pending.WaitAsync(testContext.CancellationToken));
        Assert.IsTrue(Handler.Finished.Task.IsCompletedSuccessfully);
        Assert.AreEqual(1, Handler.Attempts);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(-1)]
    [DataRow(1)]
    public async Task Wall_clock_adjustments_do_not_change_retry_timing_or_the_elapsed_budget(int days)
    {
        var pending = StartProbe(stall: false);
        Assert.AreEqual(s_budget, await _clock.WaitForTimerAsync(testContext.CancellationToken));
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), await _clock.WaitForTimerAsync(testContext.CancellationToken));
        _fake.AdjustTime(s_now.AddDays(days));
        _fake.Advance(TimeSpan.FromMilliseconds(499));
        Assert.AreEqual(1, Handler.Attempts);
        Assert.IsFalse(pending.IsCompleted);
        _fake.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsTrue(await pending.WaitAsync(testContext.CancellationToken));
        Assert.AreEqual(2, Handler.Attempts);
        Assert.IsTrue(Handler.Responses.All(static response => response.Disposed));
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Caller_cancellation_joins_the_request_or_retry_wait_and_propagates(bool duringRequest)
    {
        var pending = StartProbe(stall: duringRequest);
        await Handler.Started.Task.WaitAsync(testContext.CancellationToken);
        Assert.AreEqual(s_budget, await _clock.WaitForTimerAsync(testContext.CancellationToken));
        if (!duringRequest)
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), await _clock.WaitForTimerAsync(testContext.CancellationToken));

        await _lifetime!.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        Assert.IsTrue(Handler.Finished.Task.IsCompletedSuccessfully);
        Assert.AreEqual(1, Handler.Attempts);
        Assert.IsTrue(Handler.Responses.All(static response => response.Disposed));
    }

    private Task<bool> StartProbe(bool stall)
    {
        _handler = new ProbeHandler(stall);
        return _pending = new HttpHealthProbe(new ClientFactory(_handler), "/health", s_budget, _clock).IsReadyAsync(
            new QylResourceState { Name = "worker", Lifecycle = ResourceLifecycle.Starting,
                Timestamp = s_now, Endpoint = new Uri("http://localhost:54321") }, _lifetime!.Token);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_lifetime is not null) await _lifetime.CancelAsync();
            if (_pending is not null)
            {
                try { await _pending; }
                catch (OperationCanceledException) when (_lifetime?.IsCancellationRequested == true) { }
            }
        }
        finally
        {
            _lifetime?.Dispose();
            _handler?.Dispose();
        }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ProbeHandler(bool stall) : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<TrackedContent> Responses { get; } = [];
        public int Attempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            Started.TrySetResult();
            try
            {
                if (stall) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                var content = new TrackedContent();
                Responses.Add(content);
                return new HttpResponseMessage(Attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = content };
            }
            finally { Finished.TrySetResult(); }
        }
    }

    private sealed class TrackedContent() : StringContent("ready")
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
        }
    }
}

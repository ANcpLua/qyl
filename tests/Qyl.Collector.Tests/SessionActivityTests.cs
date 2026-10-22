using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Qyl.Api.Contracts.Common.Pagination;
using Qyl.Api.Contracts.Domains.Observe.Session;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class SessionActivityTests(TestContext testContext)
{
    private const string SessionId = "boundary";
    // Independent fixtures for 2026-10-17 12:00 UTC and the first still-active nanosecond after 11:55 UTC.
    private const ulong NowUnixNano = 1_792_238_400_000_000_000;
    private const ulong CutoffUnixNano = 1_792_238_100_000_000_001;
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _clock = null!;
    private CollectorApiTestHost? _host;

    private CollectorApiTestHost Host => _host ?? throw new InvalidOperationException("The test host has not started.");

    [TestInitialize]
    public async Task StartHostAsync()
    {
        _clock = new FakeTimeProvider(s_now);
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
    [DataRow(-1L, false)]
    [DataRow(0L, true)]
    [DataRow(1L, true)]
    public async Task Filtering_detail_mapping_and_statistics_agree_at_the_exact_nanosecond_cutoff(
        long offsetNanoseconds, bool expectedActive)
    {
        var lastActivity = checked((ulong)((long)CutoffUnixNano + offsetNanoseconds));
        await SeedAsync(lastActivity);

        var session = await GetSessionAsync();
        Assert.AreEqual(expectedActive ? SessionState.Active : SessionState.Ended, session.State);
        Assert.AreEqual(expectedActive, session.EndTime is null);

        var page = await GetPageAsync();
        var listed = Assert.ContainsSingle(page.Items);
        Assert.AreEqual(SessionId, listed.SessionId);
        Assert.AreEqual(session.State, listed.State);
        Assert.AreEqual(session.EndTime, listed.EndTime);

        var active = await GetPageAsync("?is_active=true");
        var ended = await GetPageAsync("?is_active=false");
        string[] expectedActiveIds = expectedActive ? [SessionId] : [];
        string[] expectedEndedIds = expectedActive ? [] : [SessionId];
        Assert.AreSequenceEqual(expectedActiveIds, active.Items.Select(static item => item.SessionId));
        Assert.AreSequenceEqual(expectedEndedIds, ended.Items.Select(static item => item.SessionId));

        var stats = await GetStatsAsync();
        Assert.AreEqual(1, stats.TotalSessions);
        Assert.AreEqual(expectedActive ? 1 : 0, stats.ActiveSessions);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Advancing_time_expires_a_session_without_rewriting_telemetry()
    {
        await SeedAsync(NowUnixNano);
        Assert.AreEqual(SessionState.Active, (await GetSessionAsync()).State);

        _clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        Assert.AreEqual(SessionState.Active, (await GetSessionAsync()).State);
        Assert.AreEqual(1, (await GetStatsAsync()).ActiveSessions);

        _clock.Advance(TimeSpan.FromTicks(1));
        var expired = await GetSessionAsync();
        Assert.AreEqual(SessionState.Ended, expired.State);
        Assert.AreEqual(s_now, expired.EndTime);
        Assert.IsEmpty((await GetPageAsync("?is_active=true")).Items);
        Assert.AreEqual(SessionId, Assert.ContainsSingle((await GetPageAsync("?is_active=false")).Items).SessionId);
        Assert.AreEqual(0, (await GetStatsAsync()).ActiveSessions);

        var stored = await Host.Store.GetSessionAsync(SessionId, AlphaProjectId, testContext.CancellationToken);
        Assert.IsNotNull(stored);
        Assert.AreEqual(NowUnixNano, stored.LastActivityUnixNano);
        Assert.AreEqual(1L, stored.SpanCount);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Filtering_and_every_item_in_a_response_use_one_clock_snapshot()
    {
        await Host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), CutoffUnixNano - 1, CutoffUnixNano, sessionId: "at-cutoff"),
            Span(TraceId(2), SpanId(2), CutoffUnixNano, CutoffUnixNano + 1, sessionId: "after-cutoff")
        ]), testContext.CancellationToken);
        _clock.AutoAdvanceAmount = TimeSpan.FromTicks(1);

        var page = await GetPageAsync("?is_active=true");

        Assert.AreSequenceEqual(["after-cutoff", "at-cutoff"], page.Items.Select(static item => item.SessionId));
        Assert.AreSequenceEqual([SessionState.Active, SessionState.Active], page.Items.Select(static item => item.State));
        Assert.AreSequenceEqual<DateTimeOffset?>([null, null], page.Items.Select(static item => item.EndTime));
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SessionState_LastSpanTenMinutesAgo_IsEndedInRestAndAnalysis()
    {
        var tenMinutesAgo = TimeConversions.ToUnixNanoUnsigned(_clock.GetUtcNow() - TimeSpan.FromMinutes(10));
        await SeedAsync(tenMinutesAgo);

        var rest = await GetSessionAsync();
        using var response = await Host.SendAsync(HttpMethod.Post, "/api/v1/analysis/list_sessions", ReaderApiKey, "{}");
        var analysis = Assert.ContainsSingle((await Host.JsonAsync(response)).GetProperty("sessions").EnumerateArray());

        Assert.AreEqual(SessionState.Ended, rest.State);
        Assert.AreEqual("ended", analysis.GetProperty("state").GetString());
    }

    private ValueTask SeedAsync(ulong lastActivity) =>
        Host.Store.EnqueueAsync(new SpanBatch(
        [
            Span(TraceId(1), SpanId(1), lastActivity - 1, lastActivity, sessionId: SessionId)
        ]), testContext.CancellationToken);

    private async Task<SessionEntity> GetSessionAsync()
    {
        using var response = await Host.SendAsync(HttpMethod.Get, $"/api/v1/sessions/{SessionId}", ReaderApiKey);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync(
            QylSerializerContext.Default.SessionEntity, testContext.CancellationToken);
        Assert.IsNotNull(session);
        return session;
    }

    private async Task<CursorPageSessionEntity> GetPageAsync(string query = "")
    {
        using var response = await Host.SendAsync(HttpMethod.Get, "/api/v1/sessions" + query, ReaderApiKey);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync(
            QylSerializerContext.Default.CursorPageSessionEntity, testContext.CancellationToken);
        Assert.IsNotNull(page);
        return page;
    }

    private async Task<SessionStats> GetStatsAsync()
    {
        using var response = await Host.SendAsync(HttpMethod.Get, "/api/v1/sessions/stats", ReaderApiKey);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync(
            QylSerializerContext.Default.SessionStats, testContext.CancellationToken);
        Assert.IsNotNull(stats);
        return stats;
    }
}

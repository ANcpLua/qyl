using System.Diagnostics;
using Qyl;

namespace Qyl.Api.Tests;

/// <summary>
/// <c>Activity.Current</c> in a middleware is whatever the pipeline above it left there. Only one of those is
/// the span the collector will receive, and stamping any of the others puts the agent's session on telemetry
/// nobody will see — or on a stranger's activity.
/// </summary>
[Collection(ActivityListeners.Name)]
public sealed class SessionSpanGuardTests : IDisposable
{
    private readonly List<IDisposable> _scopes = [];

    public void Dispose()
    {
        foreach (var scope in _scopes)
            scope.Dispose();
    }

    [Fact]
    public void Nothing_current_is_nothing_to_stamp() => QylSessionBaggageStartupFilter.Stamp(null);

    [Fact]
    public void The_legacy_hosting_activity_is_not_the_server_span()
    {
        // What ASP.NET Core starts when nothing is subscribed to its ActivitySource: a plain Activity, no source.
        using var legacy = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn");
        legacy.SetIdFormat(ActivityIdFormat.W3C);
        legacy.AddBaggage(QylApiContract.SessionIdName, "agent-42");
        legacy.Start();

        QylSessionBaggageStartupFilter.Stamp(legacy);

        Assert.Empty(legacy.Source.Name);
        Assert.Null(legacy.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public void A_propagation_only_activity_records_nothing()
    {
        using var activity = StartServerActivity(ActivitySamplingResult.PropagationData);

        QylSessionBaggageStartupFilter.Stamp(activity);

        Assert.False(activity!.IsAllDataRequested);
        Assert.Null(activity.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public void Someone_elses_activity_is_left_alone()
    {
        // A startup filter registered ahead of AddQylApi, or any library that opened an activity of its own.
        var source = Source("Some.Other.Library", ActivitySamplingResult.AllData);
        using var activity = source.StartActivity("work", ActivityKind.Server);
        activity!.AddBaggage(QylApiContract.SessionIdName, "agent-42");

        QylSessionBaggageStartupFilter.Stamp(activity);

        Assert.Null(activity.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public void A_client_span_is_not_a_server_span()
    {
        using var activity = StartServerActivity(ActivitySamplingResult.AllData, ActivityKind.Client);

        QylSessionBaggageStartupFilter.Stamp(activity);

        Assert.Null(activity!.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public void The_server_span_is_stamped()
    {
        using var activity = StartServerActivity(ActivitySamplingResult.AllData);

        QylSessionBaggageStartupFilter.Stamp(activity);

        Assert.Equal("agent-42", activity!.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public void A_session_already_on_the_span_wins()
    {
        using var activity = StartServerActivity(ActivitySamplingResult.AllData);
        activity!.SetTag(QylApiContract.SessionIdName, "already-here");

        QylSessionBaggageStartupFilter.Stamp(activity);

        Assert.Equal("already-here", activity.GetTagItem(QylApiContract.SessionIdName));
    }

    private Activity? StartServerActivity(
        ActivitySamplingResult sampling,
        ActivityKind kind = ActivityKind.Server)
    {
        var source = Source(QylSessionBaggageStartupFilter.ServerActivitySourceName, sampling);
        var activity = source.StartActivity("GET /todos", kind);
        activity?.AddBaggage(QylApiContract.SessionIdName, "agent-42");
        return activity;
    }

    /// <summary>A source with a listener attached, both alive until the test class is torn down.</summary>
    private ActivitySource Source(string name, ActivitySamplingResult sampling)
    {
        var source = new ActivitySource(name);
        var listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => sampling
        };
        ActivitySource.AddActivityListener(listener);
        _scopes.Add(listener);
        _scopes.Add(source);
        return source;
    }
}

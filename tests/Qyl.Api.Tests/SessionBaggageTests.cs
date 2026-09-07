using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Qyl;

namespace Qyl.Api.Tests;

/// <summary>
/// The agent contract: `baggage: session.id=<id>` on the request, `session.id` on the server span.
/// Qyl.Telemetry.Hosting's session processor takes it from there, so this tag is the whole handover.
/// </summary>
public sealed class SessionBaggageTests
{
    [Theory]
    [InlineData("session.id=agent-1", "agent-1")]
    [InlineData("session.id=agent-1;metadata=x", "agent-1")]
    [InlineData("userId=alice,session.id=agent-1,serverNode=DF%2028", "agent-1")]
    [InlineData("session.id = agent-1 , other=2", "agent-1")]
    [InlineData("session.id=agent%20one", "agent one")]
    [InlineData("other=1,session.id=first,session.id=second", "first")]
    public void Baggage_member_is_read(string header, string expected)
    {
        Assert.True(QylSessionBaggageStartupFilter.TryReadSessionId(new StringValues(header), out var sessionId));
        Assert.Equal(expected, sessionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("userId=alice")]
    [InlineData("session.id=")]
    [InlineData("sessionid=agent-1")]
    [InlineData("Session.Id=agent-1")]
    [InlineData("session.idle=agent-1")]
    [InlineData("session.id")]
    public void Nothing_is_invented(string header)
    {
        Assert.False(QylSessionBaggageStartupFilter.TryReadSessionId(new StringValues(header), out var sessionId));
        Assert.Equal(string.Empty, sessionId);
    }

    [Fact]
    public void An_unbounded_member_is_not_a_session_key()
    {
        var tooLong = new string('a', QylApiContract.MaxSessionIdLength + 1);
        Assert.False(QylSessionBaggageStartupFilter.TryReadSessionId(
            new StringValues($"session.id={tooLong}"), out _));

        var longest = new string('a', QylApiContract.MaxSessionIdLength);
        Assert.True(QylSessionBaggageStartupFilter.TryReadSessionId(
            new StringValues($"session.id={longest}"), out var accepted));
        Assert.Equal(longest, accepted);
    }

    [Fact]
    public void A_control_character_is_not_a_session_key() =>
        Assert.False(QylSessionBaggageStartupFilter.TryReadSessionId(
            new StringValues("session.id=agent%0A1"), out _));

    [Fact]
    public async Task The_server_span_carries_the_session_the_agent_sent()
    {
        var activity = await RunPipelineAsync("baggage", "session.id=agent-42");

        Assert.Equal("agent-42", activity.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public async Task A_request_without_the_header_leaves_the_span_alone()
    {
        var activity = await RunPipelineAsync(headerName: null, headerValue: null);

        Assert.Null(activity.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public async Task A_session_already_on_the_span_wins()
    {
        var activity = await RunPipelineAsync(
            "baggage",
            "session.id=from-the-wire",
            span => span.SetTag(QylApiContract.SessionIdName, "already-here"));

        Assert.Equal("already-here", activity.GetTagItem(QylApiContract.SessionIdName));
    }

    private static async Task<Activity> RunPipelineAsync(
        string? headerName,
        string? headerValue,
        Action<Activity>? beforePipeline = null)
    {
        using var source = new ActivitySource(nameof(SessionBaggageTests));
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        await using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        new QylSessionBaggageStartupFilter()
            .Configure(static inner => inner.Run(static _ => Task.CompletedTask))(app);
        var pipeline = app.Build();

        using var activity = source.StartActivity("GET /todos/{id}", ActivityKind.Server)
                             ?? throw new InvalidOperationException("The listener did not sample the server span");
        beforePipeline?.Invoke(activity);

        var context = new DefaultHttpContext { RequestServices = services };
        if (headerName is not null)
            context.Request.Headers[headerName] = headerValue;

        await pipeline(context);
        return activity;
    }
}

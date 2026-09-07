using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qyl;

namespace Qyl.Api.Tests;

/// <summary>
/// The agent contract, against a running host rather than a parser of our own: an agent sends
/// <c>baggage: session.id=&lt;id&gt;</c>, ASP.NET Core parses the header when it starts the server span, and the
/// filter's job is to decide whether that member is a session id and to stamp it. The header is written by
/// whoever is calling, so the cases that matter most are the ones that are not session ids.
/// </summary>
[Collection(ActivityListeners.Name)]
public sealed class SessionBaggageTests : IAsyncLifetime
{
    private ActivityListener _listener = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private Activity? _serverSpan;
    private string? _forwardedBaggage;

    public async ValueTask InitializeAsync()
    {
        // Without a listener on the ASP.NET Core source the hosting layer starts no server span, and there is
        // nothing to stamp — which is itself one of the states the filter guards against, covered as a unit below.
        _listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                source.Name == QylSessionBaggageStartupFilter.ServerActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IStartupFilter, QylSessionBaggageStartupFilter>();

        _app = builder.Build();

        // Captures the server span the filter saw, then calls the next service in-process over real HTTP so the
        // outbound request carries whatever the runtime propagates.
        _app.MapGet("/probe", async () =>
        {
            _serverSpan = Activity.Current;
            using var client = new HttpClient();
            _forwardedBaggage = await client.GetStringAsync(new Uri(BaseAddress, "/echo"));
            return Results.Ok();
        });
        // Reports what the second hop's own runtime parsed out of the header, not just the header text: the
        // question EXTRA-A asks is whether the session survives the hop, not how the bytes were spelled.
        _app.MapGet("/echo", (HttpContext context) => Results.Text(
            $"{Activity.Current?.GetBaggageItem(QylApiContract.SessionIdName)}|{context.Request.Headers["baggage"]}"));

        _app.MapGet("/propagator", () => Results.Text(DistributedContextPropagator.Current.GetType().Name));

        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = BaseAddress };
    }

    private Uri BaseAddress => new(_app.Urls.First());

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        _listener?.Dispose();
    }

    [Fact]
    public async Task The_server_span_carries_the_session_the_agent_sent()
    {
        await ProbeAsync("session.id=agent-42");

        Assert.Equal("agent-42", _serverSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [Theory]
    // Member properties and a second member: ASP.NET Core's propagator resolves the members, leaves the W3C
    // properties in the value, and the filter cuts at the separator rather than getting a second opinion about
    // the same bytes.
    [InlineData("userId=alice,session.id=agent-1;metadata=x")]
    [InlineData("session.id=agent-1;ttl=1")]
    [InlineData("session.id=agent-1")]
    public async Task The_runtime_owns_the_parsing(string baggage)
    {
        await ProbeAsync(baggage);

        Assert.Equal("agent-1", _serverSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    /// <summary>
    /// The BCL injects baggage into outbound requests through <see cref="DistributedContextPropagator.Current"/>.
    /// A composition that replaces it with the no-output propagator turns the hop below into a silent no-op, so
    /// the type in use is worth naming rather than assuming — and worth failing on.
    /// </summary>
    [Fact]
    public async Task The_propagator_in_use_injects()
    {
        var propagator = await _client.GetStringAsync("/propagator", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("NoOutput", propagator, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DistributedContextPropagator.Current.GetType().Name, propagator, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_request_without_the_header_leaves_the_span_alone()
    {
        await ProbeAsync(baggage: null);

        Assert.Null(_serverSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [Theory]
    // Everything here would become a storage key in the collector and a path segment in its read API.
    [InlineData("session.id=/")]
    [InlineData("session.id=..")]
    [InlineData("session.id=%2e%2e%2f%2e%2e%2fadmin")]
    [InlineData("session.id=%")]
    [InlineData("session.id=%25")]
    [InlineData("session.id=a%E2%80%A8b")]
    [InlineData("session.id=agent 1")]
    [InlineData("userId=alice")]
    public async Task A_member_that_is_not_a_session_id_is_ignored(string baggage)
    {
        await ProbeAsync(baggage);

        Assert.Null(_serverSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public async Task Optional_whitespace_around_the_member_is_the_spec_s_business()
    {
        // W3C permits OWS around `=` and the runtime strips it, so this is the same session as `session.id=agent-1`.
        // The filter adds no trimming of its own: a value that *decodes* to spaces is a different value and is rejected.
        await ProbeAsync("session.id = agent-1 ");

        Assert.Equal("agent-1", _serverSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public async Task An_unbounded_member_is_not_a_session_key()
    {
        await ProbeAsync($"session.id={new string('a', QylApiContract.MaxSessionIdLength + 1)}");
        Assert.Null(_serverSpan?.GetTagItem(QylApiContract.SessionIdName));

        var longest = new string('a', QylApiContract.MaxSessionIdLength);
        await ProbeAsync($"session.id={longest}");
        Assert.Equal(longest, _serverSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    /// <summary>
    /// The session is not stuck in one process. The value stays in the activity's baggage, and
    /// <c>System.Net.Http</c> injects the current activity's baggage into every outbound request by default, so a
    /// Qyl API calling another Qyl API hands the session on without either of them writing a line of code.
    /// </summary>
    [Fact]
    public async Task The_session_travels_to_the_next_service()
    {
        await ProbeAsync("session.id=agent-42");

        Assert.NotNull(_forwardedBaggage);
        var (parsed, header) = _forwardedBaggage.Split('|') is [var first, .. var rest]
            ? (first, string.Join('|', rest))
            : (_forwardedBaggage, string.Empty);

        // The next service parsed the session out of the request it received, without either service
        // forwarding anything by hand. The header is asserted as the BCL actually emitted it — it spells the
        // member `session.id = agent-42`, with the optional whitespace the spec allows — never as a
        // hand-written form no runtime produces.
        Assert.Equal("agent-42", parsed);
        Assert.Contains(QylApiContract.SessionIdName, header, StringComparison.Ordinal);
        Assert.Contains("agent-42", header, StringComparison.Ordinal);
    }

    private async Task ProbeAsync(string? baggage)
    {
        _serverSpan = null;
        _forwardedBaggage = null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        if (baggage is not null)
            request.Headers.TryAddWithoutValidation("baggage", baggage);

        // No ambient activity in the test: the outbound request must carry only what the header said.
        Activity.Current = null;
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}

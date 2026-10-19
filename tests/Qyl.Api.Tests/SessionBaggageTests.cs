namespace Qyl.Api.Tests;

/// <summary>
/// The agent contract, against a running host rather than a parser of our own: an agent sends
/// <c>baggage: session.id=&lt;id&gt;</c>, ASP.NET Core parses the header when it starts the server span, and
/// <c>AddQyl</c>'s startup filter decides whether that member is a session id and stamps it, under the rule
/// <see cref="QylApiContract.SessionIdPattern"/> states. The header is written by whoever is calling, so the
/// cases that matter most are the ones that are not session ids.
/// </summary>
[DoNotParallelize]
[TestClass]
public sealed class SessionBaggageTests(TestContext testContext)
{

    private SessionBaggageHost _host = null!;

    [TestInitialize]
    public async ValueTask InitializeAsync() => _host = await SessionBaggageHost.StartAsync(testContext.CancellationToken);

    [TestCleanup]
    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [TestMethod]
    public async Task The_server_span_carries_the_session_the_agent_sent()
    {
        await _host.ProbeAsync("session.id=agent-42");

        Assert.AreEqual("agent-42", _host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [TestMethod]
    // Member properties and a second member: ASP.NET Core's propagator resolves the members, leaves the W3C
    // properties in the value, and the filter cuts at the separator rather than getting a second opinion about
    // the same bytes.
    [DataRow("userId=alice,session.id=agent-1;metadata=x")]
    [DataRow("session.id=agent-1;ttl=1")]
    [DataRow("session.id=agent-1")]
    public async Task The_runtime_owns_the_parsing(string baggage)
    {
        await _host.ProbeAsync(baggage);

        Assert.AreEqual("agent-1", _host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    /// <summary>
    /// The BCL injects baggage into outbound requests through <see cref="DistributedContextPropagator.Current"/>.
    /// A composition that replaces it with the no-output propagator turns the hop below into a silent no-op, so
    /// the type in use is worth naming rather than assuming — and worth failing on.
    /// </summary>
    [TestMethod]
    public async Task The_propagator_in_use_injects()
    {
        var propagator = await _host.Client.GetStringAsync(new Uri("/propagator", UriKind.Relative), testContext.CancellationToken);

        Assert.DoesNotContain("NoOutput", propagator, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(DistributedContextPropagator.Current.GetType().Name, propagator, StringComparer.Ordinal);
    }

    [TestMethod]
    public async Task A_request_without_the_header_leaves_the_span_alone()
    {
        await _host.ProbeAsync(baggage: null);

        Assert.IsNull(_host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [TestMethod]
    // Everything here would become a storage key in the collector and a path segment in its read API.
    [DataRow("session.id=/")]
    [DataRow("session.id=..")]
    [DataRow("session.id=%2e%2e%2f%2e%2e%2fadmin")]
    [DataRow("session.id=%")]
    [DataRow("session.id=%25")]
    [DataRow("session.id=a%E2%80%A8b")]
    [DataRow("session.id=agent 1")]
    [DataRow("userId=alice")]
    public async Task A_member_that_is_not_a_session_id_is_ignored(string baggage)
    {
        await _host.ProbeAsync(baggage);

        Assert.IsNull(_host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [TestMethod]
    public async Task Optional_whitespace_around_the_member_is_the_spec_s_business()
    {
        // W3C permits OWS around `=` and the runtime strips it, so this is the same session as `session.id=agent-1`.
        // The filter adds no trimming of its own: a value that *decodes* to spaces is a different value and is rejected.
        await _host.ProbeAsync("session.id = agent-1 ");

        Assert.AreEqual("agent-1", _host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    [TestMethod]
    public async Task An_unbounded_member_is_not_a_session_key()
    {
        await _host.ProbeAsync($"session.id={new string('a', QylApiContract.MaxSessionIdLength + 1)}");
        Assert.IsNull(_host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));

        var longest = new string('a', QylApiContract.MaxSessionIdLength);
        await _host.ProbeAsync($"session.id={longest}");
        Assert.AreEqual(longest, _host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }

    /// <summary>
    /// The session is not stuck in one process. The value stays in the activity's baggage, and
    /// <c>System.Net.Http</c> injects the current activity's baggage into every outbound request by default, so a
    /// Qyl API calling another Qyl API hands the session on without either of them writing a line of code.
    /// </summary>
    [TestMethod]
    public async Task The_session_travels_to_the_next_service()
    {
        await _host.ProbeAsync("session.id=agent-42");

        var forwarded = Assert.IsExactInstanceOfType<string>(_host.ForwardedBaggage);
        var (parsed, header) = forwarded.Split('|') is [var first, .. var rest]
            ? (first, string.Join('|', rest))
            : (forwarded, string.Empty);

        // The next service parsed the session out of the request it received, without either service
        // forwarding anything by hand. The header is asserted as the BCL actually emitted it — it spells the
        // member `session.id = agent-42`, with the optional whitespace the spec allows — never as a
        // hand-written form no runtime produces.
        Assert.AreEqual("agent-42", parsed);
        Assert.Contains(QylApiContract.SessionIdName, header, StringComparison.Ordinal);
        Assert.Contains("agent-42", header, StringComparison.Ordinal);
    }
}

namespace Qyl.Api.Tests;

/// <summary>
/// The session belongs on the one span the collector receives for a request: the hosting layer's recording
/// server span. <c>AddQyl</c>'s startup filter stamps that span and nothing else; these are the guards a Qyl
/// API can observe from outside.
/// </summary>
[DoNotParallelize]
[TestClass]
public sealed class SessionSpanGuardTests(TestContext testContext)
{

    private const string SampledParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
    private const string UnsampledParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00";

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
    public async Task The_hosting_layer_s_server_span_is_stamped()
    {
        await _host.ProbeAsync("session.id=agent-42", SampledParent);

        var span = Assert.IsExactInstanceOfType<Activity>(_host.ServerSpan);
        Assert.AreEqual("Microsoft.AspNetCore", span.Source.Name);
        Assert.AreEqual(ActivityKind.Server, span.Kind);
        Assert.AreEqual("agent-42", span.GetTagItem(QylApiContract.SessionIdName));
    }

    [TestMethod]
    public async Task A_propagation_only_server_span_records_nothing()
    {
        // The caller did not sample (flags 00) and AddQyl's parent-based sampler follows it: the server
        // activity only propagates context, and nothing is written onto it, the session included.
        await _host.ProbeAsync("session.id=agent-42", UnsampledParent);

        Assert.IsFalse(_host.ServerSpan?.IsAllDataRequested ?? false);
        Assert.IsNull(_host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }
}

namespace Qyl.Api.Tests;

/// <summary>
/// The session belongs on the one span the collector receives for a request: the hosting layer's recording
/// server span. <c>AddQyl</c>'s startup filter stamps that span and nothing else; these are the guards a Qyl
/// API can observe from outside.
/// </summary>
[Collection(ActivityListeners.Name)]
public sealed class SessionSpanGuardTests : IAsyncLifetime
{
    private const string SampledParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
    private const string UnsampledParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00";

    private SessionBaggageHost _host = null!;

    public async ValueTask InitializeAsync() => _host = await SessionBaggageHost.StartAsync();

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
            await _host.DisposeAsync();
    }

    [Fact]
    public async Task The_hosting_layer_s_server_span_is_stamped()
    {
        await _host.ProbeAsync("session.id=agent-42", SampledParent);

        var span = Assert.IsType<Activity>(_host.ServerSpan);
        Assert.Equal("Microsoft.AspNetCore", span.Source.Name);
        Assert.Equal(ActivityKind.Server, span.Kind);
        Assert.Equal("agent-42", span.GetTagItem(QylApiContract.SessionIdName));
    }

    [Fact]
    public async Task A_propagation_only_server_span_records_nothing()
    {
        // The caller did not sample (flags 00) and AddQyl's parent-based sampler follows it: the server
        // activity only propagates context, and nothing is written onto it, the session included.
        await _host.ProbeAsync("session.id=agent-42", UnsampledParent);

        Assert.False(_host.ServerSpan?.IsAllDataRequested ?? false);
        Assert.Null(_host.ServerSpan?.GetTagItem(QylApiContract.SessionIdName));
    }
}

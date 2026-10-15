using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Qyl.Api.Tests;

/// <summary>
/// A host composed the way <c>AddQylApi</c> composes telemetry: <c>AddQyl</c>, whose ASP.NET Core startup
/// filter stamps the <c>baggage</c> member <c>session.id</c> onto the server span. Nothing is exported: no
/// endpoint is configured and discovery is off. <c>/probe</c> records the server span it ran under and calls
/// <c>/echo</c> over real HTTP, which reports what the second hop's runtime parsed out of the header.
/// </summary>
internal sealed class SessionBaggageHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private SessionBaggageHost(WebApplication app) => _app = app;

    /// <summary>A client of the started host.</summary>
    public HttpClient Client { get; private set; } = null!;

    public Activity? ServerSpan { get; private set; }

    public string? ForwardedBaggage { get; private set; }

    private Uri BaseAddress => new(_app.Urls.First());

    public static async Task<SessionBaggageHost> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.AddQyl(static options =>
        {
            options.EnableCollectorDiscovery = false;
            options.RequireConfiguredEndpoint = true;
            options.EnableLogExport = false;
            options.EnableMetricsExport = false;
        });

        var host = new SessionBaggageHost(builder.Build());
        await host.MapAndStartAsync();
        return host;
    }

    private async Task MapAndStartAsync()
    {
        _app.MapGet("/probe", async () =>
        {
            ServerSpan = Activity.Current;
            ForwardedBaggage = await Client.GetStringAsync(new Uri(BaseAddress, "/echo"));
            return Results.Ok();
        });
        _app.MapGet("/echo", static (HttpContext context) => Results.Text(
            $"{Activity.Current?.GetBaggageItem(QylApiContract.SessionIdName)}|{context.Request.Headers["baggage"]}"));
        _app.MapGet("/propagator", static () => Results.Text(DistributedContextPropagator.Current.GetType().Name));
        await _app.StartAsync();
        Client = new HttpClient { BaseAddress = BaseAddress };
    }

    /// <summary>Calls <c>/probe</c> with the given <c>baggage</c> (and <c>traceparent</c>) header and no ambient activity.</summary>
    public async Task ProbeAsync(string? baggage, string? traceparent = null)
    {
        ServerSpan = null;
        ForwardedBaggage = null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        if (baggage is not null)
            request.Headers.TryAddWithoutValidation("baggage", baggage);
        if (traceparent is not null)
            request.Headers.TryAddWithoutValidation("traceparent", traceparent);

        Activity.Current = null;
        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}

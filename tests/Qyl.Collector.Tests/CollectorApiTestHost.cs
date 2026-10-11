using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Primitives;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// The collector's real HTTP composition — core services, ApiKey auth parsed from
/// <c>QYL_OTLP_PROJECT_KEYS</c>, the role-enforcing middleware and every mapped route — over an
/// in-memory store, on a loopback port.
/// </summary>
internal sealed class CollectorApiTestHost : IAsyncDisposable
{
    // One credential per role combination the tests need, in both configuration forms.
    internal const string ProjectKeys = """
        {
          "alpha": [
            "legacy",
            { "key": "reader", "roles": ["read"] },
            { "key": "writer", "roles": ["read", "write"] },
            { "key": "deleter", "roles": ["delete"] },
            { "key": "ingester", "roles": ["ingest"] }
          ],
          "beta": [
            { "key": "beta-all", "roles": ["ingest", "read", "write", "delete"] }
          ]
        }
        """;

    private readonly WebApplication _app;

    private CollectorApiTestHost(WebApplication app, DuckDbStore store, HttpClient client)
    {
        _app = app;
        Store = store;
        Client = client;
    }

    public DuckDbStore Store { get; }

    public HttpClient Client { get; }

    public static async Task<CollectorApiTestHost> StartAsync(CancellationToken ct)
    {
        var store = new DuckDbStore(":memory:");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_OTLP_AUTH_MODE"] = "ApiKey",
                ["QYL_OTLP_PROJECT_KEYS"] = ProjectKeys
            })
            .Build();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddQylCollectorCore(config);
        builder.Services.AddQylCollectorAuth(config, builder.Environment);
        builder.Services.AddSingleton<IQylStore>(store);
        var app = builder.Build();
        app.UseMiddleware<CollectorApiKeyMiddleware>(app.Services.GetRequiredService<OtlpApiKeyOptions>());
        app.MapQylCollectorEndpoints();
        await app.StartAsync(ct);
        return new CollectorApiTestHost(app, store, new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) });
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string key,
        string? json = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? project = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(OtlpConstants.ApiKeyHeaderName, key);
        if (project is not null)
            request.Headers.Add(ProjectIdentity.HeaderName, project);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            request.Headers.TryAddWithoutValidation(name, value);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>One span row; a trace is the spans sharing a trace id.</summary>
    public static SpanStorageRow Span(
        string traceId,
        string spanId,
        ulong start,
        ulong end,
        string project = "alpha",
        string? parentSpanId = null,
        string service = "checkout",
        byte status = 0,
        string? sessionId = null) =>
        new()
        {
            ProjectId = project,
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            SessionId = sessionId,
            Name = $"span-{spanId}",
            Kind = 1,
            StartTimeUnixNano = start,
            EndTimeUnixNano = end,
            DurationNs = end - start,
            StatusCode = status,
            ServiceName = service
        };

    public static string TraceId(int value) => value.ToString("x32", CultureInfo.InvariantCulture);

    public static string SpanId(int value) => value.ToString("x16", CultureInfo.InvariantCulture);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await Store.DisposeAsync();
    }
}

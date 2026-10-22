using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Collector.Trace.V1;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// The collector's real HTTP composition — core services, ApiKey auth parsed from
/// <c>QYL_OTLP_PROJECT_KEYS</c>, the role-enforcing middleware and every mapped route — over an
/// in-memory store, on two loopback ports: HTTP/1.1 for the routes and, as the collector's own
/// gRPC port, an HTTP/2-only one for OTLP/gRPC (cleartext Kestrel serves HTTP/1.1 unless the
/// endpoint is HTTP/2-only).
/// </summary>
internal sealed class CollectorApiTestHost : IAsyncDisposable
{
    internal const string ReaderApiKey = "reader";
    internal const string AlphaProjectId = "alpha";
    internal static readonly DateTimeOffset FixtureUtcNow = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    internal const ulong FixtureUnixNano = 1_792_238_400_000_000_000;

    // One explicitly scoped credential per role combination the tests need.
    internal const string ProjectKeys = $$"""
        {
          "{{AlphaProjectId}}": [
            { "key": "ingest-reader", "roles": ["ingest", "read"] },
            { "key": "{{ReaderApiKey}}", "roles": ["read"] },
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
    private readonly CancellationToken _cancellationToken;

    private CollectorApiTestHost(WebApplication app, DuckDbStore store, HttpClient client, string grpcAddress, CancellationToken cancellationToken)
    {
        _app = app;
        _cancellationToken = cancellationToken;
        Store = store;
        Client = client;
        GrpcAddress = grpcAddress;
    }

    public DuckDbStore Store { get; }

    public HttpClient Client { get; }

    /// <summary>The HTTP/2-only loopback address for <c>GrpcChannel.ForAddress</c>.</summary>
    public string GrpcAddress { get; }

    public static async Task<CollectorApiTestHost> StartAsync(
        CancellationToken ct,
        string databasePath = ":memory:",
        TimeProvider? timeProvider = null)
    {
        var store = new DuckDbStore(databasePath);
        WebApplication? app = null;
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QYL_OTLP_AUTH_MODE"] = "ApiKey",
                    ["QYL_OTLP_PROJECT_KEYS"] = ProjectKeys,
                    ["QYL_ANALYSIS_SIGNING_KEY"] = "analysis-test-key-independent-of-api-keys"
                })
                .Build();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.ConfigureKestrel(static options =>
            {
                options.Listen(IPAddress.Loopback, 0, static listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
                options.Listen(IPAddress.Loopback, 0, static listen => listen.Protocols = HttpProtocols.Http2);
            });
            // Every invocation owns its clock; telemetry fixtures and relative windows share an epoch.
            builder.Services.AddSingleton<TimeProvider>(timeProvider ?? new FakeTimeProvider(FixtureUtcNow));
            builder.Services.AddQylCollectorCore(config);
            builder.Services.AddQylCollectorAuth(config, builder.Environment);
            builder.Services.AddSingleton<IQylStore>(store);
            app = builder.Build();
            app.UseMiddleware<CollectorApiKeyMiddleware>();
            app.MapQylCollectorEndpoints();
            await app.StartAsync(ct);
            // Kestrel binds the endpoints in the order they were listed.
            var urls = app.Urls.ToArray();
            Assert.AreEqual(2, urls.Length);
            return new CollectorApiTestHost(app, store, new HttpClient { BaseAddress = new Uri(urls[0]) }, urls[1], ct);
        }
        catch (Exception startupFailure)
        {
            try
            {
                await using var failedStore = store;
                if (app is not null)
                {
                    await using var failedApp = app;
                    await app.StopAsync(CancellationToken.None);
                }
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("Collector test-host startup and cleanup failed.", startupFailure, cleanupFailure);
            }

            throw;
        }
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
        return await Client.SendAsync(request, _cancellationToken);
    }

    public async Task<HttpResponseMessage> SendProtobufAsync(string path, IMessage payload, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(OtlpConstants.ApiKeyHeaderName, key);
        request.Content = new ByteArrayContent(payload.ToByteArray());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(OtlpPayloadParser.ProtobufContentType);
        return await Client.SendAsync(request, _cancellationToken);
    }

    /// <summary>Posts an OTLP/protobuf trace export with the ingest-only key, the way an SDK exporter sends it.</summary>
    public async Task IngestTracesAsync(ExportTraceServiceRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/v1/traces");
        message.Headers.Add(OtlpConstants.ApiKeyHeaderName, "ingester");
        message.Content = new ByteArrayContent(request.ToByteArray());
        message.Content.Headers.ContentType = new MediaTypeHeaderValue(OtlpPayloadParser.ProtobufContentType);
        using var response = await Client.SendAsync(message, _cancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    public async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(_cancellationToken);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>One span row; a trace is the spans sharing a trace id.</summary>
    public static SpanStorageRow Span(
        string traceId,
        string spanId,
        ulong start,
        ulong end,
        string project = AlphaProjectId,
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
        await using var store = Store;
        await using var app = _app;
        Client.Dispose();
        // Cleanup must still stop the host and release the database when the test was cancelled.
        await _app.StopAsync(CancellationToken.None);
    }
}

using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class ProjectIsolationTests
{
    [Fact]
    public void Api_key_mode_requires_explicit_unique_project_bindings()
    {
        var environment = new TestEnvironment();
        IServiceCollection services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddQylCollectorAuth(
            Config(("QYL_OTLP_AUTH_MODE", "ApiKey")), environment));

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddQylCollectorAuth(
            Config(("QYL_OTLP_AUTH_MODE", "ApiKey"),
                ("QYL_OTLP_PROJECT_KEYS", "{\"alpha\":[\"same\"],\"beta\":[\"same\"]}")),
            environment));

        services = new ServiceCollection().AddQylCollectorAuth(
            Config(("QYL_OTLP_AUTH_MODE", "ApiKey"),
                ("QYL_OTLP_PROJECT_KEYS", "{\"alpha\":[\"a1\",\"a2\"],\"beta\":[\"b1\"]}")),
            environment);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<OtlpApiKeyOptions>();
        Assert.Equal("alpha", OtlpApiKeyValidator.ResolveProject("a2", options));
        Assert.Equal("beta", OtlpApiKeyValidator.ResolveProject("b1", options));
        Assert.Null(OtlpApiKeyValidator.ResolveProject("unknown", options));
    }

    [Fact]
    public async Task Http_key_selects_its_project_and_rejects_a_conflicting_read_header()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1")]
        };
        string? reachedProject = null;
        var middleware = new CollectorApiKeyMiddleware(
            context =>
            {
                reachedProject = AuthenticatedProjectScope.ForHttpRead(context);
                return Task.CompletedTask;
            }, options);
        var context = Context(options);
        context.Request.Path = "/api/v1/logs";
        context.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";

        await middleware.InvokeAsync(context);
        Assert.Equal("alpha", reachedProject);

        reachedProject = null;
        context = Context(options);
        context.Request.Path = "/api/v1/logs";
        context.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";
        context.Request.Headers["X-Qyl-Project"] = "beta";
        await middleware.InvokeAsync(context);
        Assert.Null(reachedProject);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public void All_signal_mappers_use_the_authenticated_project_and_reject_conflicting_hints()
    {
        var empty = new Dictionary<string, OtlpAttributeValue>();
        var span = new SpanIngestionRecord
        {
            SpanId = "span", TraceId = "trace", Name = "operation", ServiceName = "service",
            StartTimeUnixNano = 1, EndTimeUnixNano = 2,
            Attributes = empty, ResourceAttributes = empty
        };
        var log = new LogIngestionRecord
        {
            TimeUnixNano = 1, SeverityNumber = 9, Body = OtlpAttributeValue.FromString("event"),
            ServiceName = "service", Attributes = empty, ResourceAttributes = empty
        };
        var point = new MetricPointIngestionRecord
        {
            MetricName = "requests", Kind = MetricKind.Gauge, Temporality = MetricTemporality.Unspecified,
            IsMonotonic = false, ServiceName = "service", TimeUnixNano = 1, Value = 1,
            Attributes = empty, ResourceAttributes = empty
        };

        Assert.Equal("alpha", Assert.Single(IngestionStorageMapper.ToSpanStorageRows(
            new TraceIngestionBatch([span]), "alpha")).ProjectId);
        Assert.Equal("alpha", Assert.Single(IngestionStorageMapper.ToLogStorageRows(
            new LogIngestionBatch([log]), "alpha")).ProjectId);
        Assert.Equal("alpha", Assert.Single(MetricStorageMapper.ToStorageRows(
            new MetricIngestionBatch([point], 0, null), "alpha").Points).ProjectId);

        Assert.Throws<InvalidDataException>(() => IngestionStorageMapper.ToSpanStorageRows(
            new TraceIngestionBatch([span with { ProjectIdHint = "beta" }]), "alpha"));
        Assert.Throws<InvalidDataException>(() => IngestionStorageMapper.ToLogStorageRows(
            new LogIngestionBatch([log with { ProjectIdHint = "beta" }]), "alpha"));
        Assert.Throws<InvalidDataException>(() => MetricStorageMapper.ToStorageRows(
            new MetricIngestionBatch([point with { ProjectIdHint = "beta" }], 0, null), "alpha"));
    }

    [Fact]
    public async Task Authenticated_read_endpoint_returns_only_its_projects_logs()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1"), new ProjectApiKey("beta", "b1")]
        };
        var empty = new Dictionary<string, OtlpAttributeValue>();
        var log = new LogIngestionRecord
        {
            TimeUnixNano = 1, SeverityNumber = 9, Body = OtlpAttributeValue.FromString("alpha-only"),
            ServiceName = "service", Attributes = empty, ResourceAttributes = empty
        };
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync(
            [
                .. IngestionStorageMapper.ToLogStorageRows(new LogIngestionBatch([log]), "alpha"),
                .. IngestionStorageMapper.ToLogStorageRows(new LogIngestionBatch(
                    [log with { Body = OtlpAttributeValue.FromString("beta-only") }]), "beta")
            ], TestContext.Current.CancellationToken);

        var middleware = new CollectorApiKeyMiddleware(async context =>
        {
            var result = await CollectorEndpointExtensions.GetLogsAsync(
                context, store, null, null, null, null, null, TestContext.Current.CancellationToken);
            await result.ExecuteAsync(context);
        }, options);
        var request = Context(options);
        request.Request.Path = "/api/v1/logs";
        request.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";
        await middleware.InvokeAsync(request);

        Assert.Equal(StatusCodes.Status200OK, request.Response.StatusCode);
        request.Response.Body.Position = 0;
        using var reader = new StreamReader(request.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("alpha-only", body, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-only", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_export_without_a_project_attribute_writes_to_the_key_project()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1")]
        };
        var export = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = ByteString.CopyFrom(new byte[16]),
                                    SpanId = ByteString.CopyFrom(new byte[8]),
                                    Name = "alpha-trace",
                                    StartTimeUnixNano = 1,
                                    EndTimeUnixNano = 2
                                }
                            }
                        }
                    }
                }
            }
        };
        await using var store = new DuckDbStore(":memory:");
        var middleware = new CollectorApiKeyMiddleware(async context =>
        {
            var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
                context, store, TestContext.Current.CancellationToken);
            await result.ExecuteAsync(context);
        }, options);
        var request = Context(options);
        request.Request.Path = "/v1/traces";
        request.Request.ContentType = OtlpPayloadParser.ProtobufContentType;
        request.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";
        request.Request.Body = new MemoryStream(export.ToByteArray());
        request.Request.ContentLength = request.Request.Body.Length;

        await middleware.InvokeAsync(request);

        Assert.Equal(StatusCodes.Status200OK, request.Response.StatusCode);
        Assert.Single(await store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
        Assert.Empty(await store.GetSpansAsync(ProjectScope.DefaultProjectId,
            ct: TestContext.Current.CancellationToken));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            values.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();

    private static DefaultHttpContext Context(OtlpApiKeyOptions options)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(options);
        services.ConfigureHttpJsonOptions(static json =>
            json.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default));
        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "qyl-tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

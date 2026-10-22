using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ProjectIsolationTests(TestContext testContext)
{
    [TestMethod]
    public void Api_key_mode_requires_explicit_unique_project_bindings()
    {
        Assert.ThrowsExactly<OptionsValidationException>(() => ApiKeyOptions(
            Config(("QYL_OTLP_AUTH_MODE", "ApiKey"))));

        Assert.ThrowsExactly<OptionsValidationException>(() => ApiKeyOptions(
            Config(("QYL_OTLP_AUTH_MODE", "ApiKey"),
                ("QYL_OTLP_PROJECT_KEYS", """{"alpha":[{"key":"same","roles":["read"]}],"beta":[{"key":"same","roles":["read"]}]}"""))));

        var options = ApiKeyOptions(
            Config(("QYL_OTLP_AUTH_MODE", "ApiKey"),
                ("QYL_OTLP_PROJECT_KEYS", """{"alpha":[{"key":"a1","roles":["ingest","read"]},{"key":"a2","roles":["ingest","read"]}],"beta":[{"key":"b1","roles":["ingest","read"]}]}""")));
        Assert.AreEqual("alpha", OtlpApiKeyValidator.Resolve("a2", options)?.ProjectId);
        Assert.AreEqual("beta", OtlpApiKeyValidator.Resolve("b1", options)?.ProjectId);
        Assert.IsNull(OtlpApiKeyValidator.Resolve("unknown", options)?.ProjectId);
    }

    [TestMethod]
    public async Task Http_key_selects_its_project_and_rejects_a_conflicting_read_header()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
        };
        string? reachedProject = null;
        var middleware = new CollectorApiKeyMiddleware(
            context =>
            {
                reachedProject = AuthenticatedProject.ForHttpRead(context);
                return Task.CompletedTask;
            }, Options.Create(options));
        var context = Context(options);
        context.Request.Path = "/api/v1/logs";
        context.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";

        await middleware.InvokeAsync(context);
        Assert.AreEqual("alpha", reachedProject);

        reachedProject = null;
        context = Context(options);
        context.Request.Path = "/api/v1/logs";
        context.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";
        context.Request.Headers["X-Qyl-Project"] = "beta";
        await middleware.InvokeAsync(context);
        Assert.IsNull(reachedProject);
        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [TestMethod]
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
            IsMonotonic = false, ServiceName = "service", TimeUnixNano = 1, Data = new Api.Contracts.OTel.Metrics.MetricNumberPointData
            { Value = new Api.Contracts.Common.AttributeDoubleValue { Value = 1 } },
            Attributes = empty, ResourceAttributes = empty
        };

        Assert.AreEqual("alpha", Assert.ContainsSingle(IngestionStorageMapper.ToSpanStorageRows(
            new TraceIngestionBatch([span]), "alpha")).ProjectId);
        Assert.AreEqual("alpha", Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(
            new LogIngestionBatch([log]), "alpha")).ProjectId);
        Assert.AreEqual("alpha", Assert.ContainsSingle(MetricStorageMapper.ToStorageRows(
            new MetricIngestionBatch([point], 0, null), "alpha").Observations).ProjectId);

        Assert.ThrowsExactly<InvalidDataException>(() => IngestionStorageMapper.ToSpanStorageRows(
            new TraceIngestionBatch([span with { ProjectIdHint = "beta" }]), "alpha"));
        Assert.ThrowsExactly<InvalidDataException>(() => IngestionStorageMapper.ToLogStorageRows(
            new LogIngestionBatch([log with { ProjectIdHint = "beta" }]), "alpha"));
        Assert.ThrowsExactly<InvalidDataException>(() => MetricStorageMapper.ToStorageRows(
            new MetricIngestionBatch([point with { ProjectIdHint = "beta" }], 0, null), "alpha"));
    }

    [TestMethod]
    public async Task Authenticated_read_endpoint_returns_only_its_projects_logs()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read)), new ProjectApiKey("beta", "b1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
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
            ], testContext.CancellationToken);

        var middleware = new CollectorApiKeyMiddleware(async context =>
        {
            var result = await CollectorEndpointExtensions.GetLogsAsync(
                context, store, null, null, null, null, null, testContext.CancellationToken);
            await result.ExecuteAsync(context);
        }, Options.Create(options));
        var request = Context(options);
        request.Request.Path = "/api/v1/logs";
        request.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";
        await middleware.InvokeAsync(request);

        Assert.AreEqual(StatusCodes.Status200OK, request.Response.StatusCode);
        request.Response.Body.Position = 0;
        using var reader = new StreamReader(request.Response.Body);
        var body = await reader.ReadToEndAsync(testContext.CancellationToken);
        Assert.Contains("alpha-only", body, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-only", body, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Http_export_without_a_project_attribute_writes_to_the_key_project()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
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
                                    TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
                                    SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
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
                context, store, testContext.CancellationToken);
            await result.ExecuteAsync(context);
        }, Options.Create(options));
        var request = Context(options);
        request.Request.Path = "/v1/traces";
        request.Request.ContentType = OtlpPayloadParser.ProtobufContentType;
        request.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "a1";
        request.Request.Body = new MemoryStream(export.ToByteArray());
        request.Request.ContentLength = request.Request.Body.Length;

        await middleware.InvokeAsync(request);

        Assert.AreEqual(StatusCodes.Status200OK, request.Response.StatusCode);
        Assert.ContainsSingle(await store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
        Assert.IsEmpty(await store.GetSpansAsync(ProjectScope.DefaultProjectId,
            ct: testContext.CancellationToken));
    }

    private static OtlpApiKeyOptions ApiKeyOptions(IConfiguration config)
    {
        using var provider = new ServiceCollection()
            .AddQylCollectorAuth(config, new TestEnvironment())
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<OtlpApiKeyOptions>>().Value;
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            values.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();

    private static DefaultHttpContext Context(OtlpApiKeyOptions options)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(Options.Create(options));
        services.ConfigureHttpJsonOptions(static json =>
            json.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default));
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Request =
            {
                Method = HttpMethods.Get
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };
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

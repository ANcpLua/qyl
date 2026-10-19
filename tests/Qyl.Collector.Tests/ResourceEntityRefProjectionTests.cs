using Google.Protobuf;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using OtlpResourceLogs = OpenTelemetry.Proto.Logs.V1.ResourceLogs;
using OtlpScopeLogs = OpenTelemetry.Proto.Logs.V1.ScopeLogs;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ResourceEntityRefProjectionTests(TestContext testContext)
{
    [TestMethod]
    public async Task Entity_references_survive_trace_and_log_storage_projection_in_canonical_order()
    {
        var traceRequest = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new OtlpResourceSpans
                {
                    Resource = BuildResource(),
                    ScopeSpans =
                    {
                        new OtlpScopeSpans
                        {
                            Spans =
                            {
                                new OtlpSpan
                                {
                                    TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
                                    SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
                                    Name = "entity-ref-trace",
                                    StartTimeUnixNano = 1,
                                    EndTimeUnixNano = 2
                                }
                            }
                        }
                    }
                }
            }
        };
        var spanRow = Assert.ContainsSingle(IngestionStorageMapper.ToSpanStorageRows(
            OtlpConverter.ConvertTraceRequest(traceRequest)));
        Assert.IsNotNull(spanRow.ResourceEntityRefsJson);
        AssertCanonicalRefs(Assert.ContainsSingle(SpanMapper.ToContracts([spanRow])).Resource.EntityRefs);

        const string EventName = "entity.ref.log";
        var logRequest = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new OtlpResourceLogs
                {
                    Resource = BuildResource(),
                    ScopeLogs =
                    {
                        new OtlpScopeLogs
                        {
                            LogRecords =
                            {
                                new OtlpLogRecord
                                {
                                    EventName = EventName,
                                    TimeUnixNano = 3
                                }
                            }
                        }
                    }
                }
            }
        };
        var logRow = Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(
            OtlpConverter.ConvertLogs(logRequest, new FakeTimeProvider(CollectorApiTestHost.FixtureUtcNow))));
        Assert.AreEqual(EventName, logRow.EventName);
        var log = LogMapper.ToContract(logRow);
        Assert.AreEqual(EventName, log.EventName);
        AssertCanonicalRefs(log.Resource.EntityRefs);

        await using var store = new DuckDbStore(":memory:");
        await store.EnqueueAsync(new SpanBatch([spanRow]), testContext.CancellationToken);
        await store.InsertLogsAsync([logRow], testContext.CancellationToken);

        AssertCanonicalRefs(Assert.ContainsSingle(SpanMapper.ToContracts(await store.GetSpansAsync(
            "default",
            ct: testContext.CancellationToken))).Resource.EntityRefs);
        var storedLog = Assert.ContainsSingle(await store.GetLogsAsync(
            "default",
            ct: testContext.CancellationToken));
        Assert.AreEqual(EventName, storedLog.EventName);
        AssertCanonicalRefs(LogMapper.ToContract(storedLog).Resource.EntityRefs);
    }

    [TestMethod]
    public void Description_metadata_updates_do_not_change_log_identity()
    {
        var request = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new OtlpResourceLogs
                {
                    Resource = BuildResource(),
                    ScopeLogs =
                    {
                        new OtlpScopeLogs
                        {
                            LogRecords = { new OtlpLogRecord { TimeUnixNano = 5 } }
                        }
                    }
                }
            }
        };
        var clock = new FakeTimeProvider(CollectorApiTestHost.FixtureUtcNow);
        var original = Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request, clock)));

        var changed = request.Clone();
        changed.ResourceLogs[0].Resource.EntityRefs
            .Single(static entityRef => entityRef.Type == "service")
            .DescriptionKeys.Clear();
        var updated = Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(changed, clock)));

        Assert.AreEqual(original.LogId, updated.LogId);
        Assert.AreNotEqual(original.ResourceEntityRefsJson, updated.ResourceEntityRefsJson);

        changed = request.Clone();
        changed.ResourceLogs[0].Resource.EntityRefs
            .Single(static entityRef => entityRef.Type == "service")
            .SchemaUrl = "https://opentelemetry.io/schemas/1.39.0";
        updated = Assert.ContainsSingle(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(changed, clock)));

        Assert.AreEqual(original.LogId, updated.LogId);
        Assert.AreNotEqual(original.ResourceEntityRefsJson, updated.ResourceEntityRefsJson);
    }

    [TestMethod]
    public void Entity_references_preserve_only_their_non_denied_custom_resource_attributes()
    {
        var resource = BuildResource();
        resource.Attributes.Add(StringAttribute("custom.entity.id", "custom-1"));
        resource.Attributes.Add(StringAttribute("custom.entity.description", "Custom entity"));
        resource.Attributes.Add(StringAttribute("custom.unreferenced", "retained"));
        resource.EntityRefs.Add(new EntityRef
        {
            Type = "custom.entity",
            IdKeys = { "custom.entity.id" },
            DescriptionKeys = { "custom.entity.description" }
        });

        var span = SpanMapper.ToContracts([ProjectSingleSpan(resource)]).Single();
        var attributes = Assert.IsInstanceOfType<IReadOnlyList<Qyl.Api.Contracts.Common.Attribute>>(
            span.Resource.Attributes);
        Assert.Contains(static attribute =>
            attribute is { Key: "custom.entity.id", Value: Api.Contracts.Common.AttributeValue.StringValue { Value: "custom-1" } },attributes);
        Assert.Contains(static attribute =>
            attribute is { Key: "custom.entity.description", Value: Api.Contracts.Common.AttributeValue.StringValue { Value: "Custom entity" } },attributes);
        Assert.Contains(static attribute =>
            attribute is { Key: "custom.unreferenced", Value: Api.Contracts.Common.AttributeValue.StringValue { Value: "retained" } },attributes);

        var denied = BuildResource();
        denied.Attributes.Add(StringAttribute("custom.api_key", "do-not-store"));
        denied.EntityRefs.Add(new EntityRef
        {
            Type = "custom.entity",
            IdKeys = { "custom.api_key" }
        });
        var rejected = ProjectResource(denied);
        Assert.AreEqual(1L, rejected.RejectedSpans);
        Assert.IsEmpty(rejected.Spans);
    }

    [TestMethod]
    public void An_entity_reference_keeps_its_application_attribute_without_a_prefix_exception()
    {
        var resource = BuildResource();
        resource.Attributes.Add(StringAttribute("qyl.app.tenant", "acme"));
        resource.EntityRefs.Add(new EntityRef
        {
            Type = "custom.entity",
            IdKeys = { "qyl.app.tenant" }
        });

        Assert.IsTrue(AttributeKeySets.IsPersistable("qyl.app.tenant"));
        var projected = ProjectResource(resource);
        Assert.AreEqual(0L, projected.RejectedSpans);
        var span = Assert.ContainsSingle(projected.Spans);
        Assert.AreEqual("acme", span.ResourceAttributes["qyl.app.tenant"].AsString());
        Assert.HasCount(3, span.ResourceEntityRefs);
        Assert.AreSequenceEqual(["qyl.app.tenant"], Assert.ContainsSingle(static reference => reference.Type is "custom.entity", span.ResourceEntityRefs).IdKeys);
    }

    private static SpanStorageRow ProjectSingleSpan(Resource resource) =>
        Assert.ContainsSingle(IngestionStorageMapper.ToSpanStorageRows(ProjectResource(resource)));

    private static TraceIngestionBatch ProjectResource(Resource resource)
    {
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new OtlpResourceSpans
                {
                    Resource = resource,
                    ScopeSpans =
                    {
                        new OtlpScopeSpans
                        {
                            Spans =
                            {
                                new OtlpSpan
                                {
                                    TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
                                    SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
                                    Name = "entity-ref-custom-resource",
                                    StartTimeUnixNano = 1,
                                    EndTimeUnixNano = 2
                                }
                            }
                        }
                    }
                }
            }
        };

        return OtlpConverter.ConvertTraceRequest(request);
    }

    private static Resource BuildResource() =>
        new()
        {
            Attributes =
            {
                StringAttribute("service.name", "entity-ref-service"),
                StringAttribute("service.namespace", "tests"),
                StringAttribute("deployment.environment.name", "test")
            },
            // Deliberately reverse semantic order; ingestion canonicalizes by entity identity.
            EntityRefs =
            {
                new EntityRef
                {
                    SchemaUrl = "https://opentelemetry.io/schemas/1.38.0",
                    Type = "service",
                    IdKeys = { "service.name" },
                    DescriptionKeys = { "service.namespace" }
                },
                new EntityRef
                {
                    Type = "deployment",
                    IdKeys = { "deployment.environment.name" }
                }
            }
        };

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static void AssertCanonicalRefs(IReadOnlyList<Qyl.Api.Contracts.Common.EntityRef>? entityRefs)
    {
        Assert.IsNotNull(entityRefs);
        Assert.HasCount(2, entityRefs);
        var deployment = entityRefs[0];
        Assert.AreEqual("deployment", deployment.Type);
        Assert.AreSequenceEqual(["deployment.environment.name"], deployment.IdKeys);
        Assert.IsNull(deployment.DescriptionKeys);
        var service = entityRefs[1];
        Assert.AreEqual("service", service.Type);
        Assert.AreSequenceEqual(["service.name"], service.IdKeys);
        Assert.AreSequenceEqual(["service.namespace"], service.DescriptionKeys);
    }
}

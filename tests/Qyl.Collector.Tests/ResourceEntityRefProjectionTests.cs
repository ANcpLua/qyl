using Google.Protobuf;
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

public sealed class ResourceEntityRefProjectionTests
{
    [Fact]
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
                                    TraceId = ByteString.CopyFrom(new byte[16]),
                                    SpanId = ByteString.CopyFrom(new byte[8]),
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
        var spanRow = Assert.Single(IngestionStorageMapper.ToSpanStorageRows(
            OtlpConverter.ConvertTraceRequest(traceRequest)));
        Assert.NotNull(spanRow.ResourceEntityRefsJson);
        AssertCanonicalRefs(Assert.Single(SpanMapper.ToContracts([spanRow])).Resource.EntityRefs);

        const string eventName = "entity.ref.log";
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
                                    EventName = eventName,
                                    TimeUnixNano = 3
                                }
                            }
                        }
                    }
                }
            }
        };
        var logRow = Assert.Single(IngestionStorageMapper.ToLogStorageRows(
            OtlpConverter.ConvertLogs(logRequest)));
        Assert.Equal(eventName, logRow.EventName);
        var log = LogMapper.ToContract(logRow);
        Assert.Equal(eventName, log.EventName);
        AssertCanonicalRefs(log.Resource.EntityRefs);

        await using var store = new DuckDbStore(":memory:");
        await store.EnqueueAsync(new SpanBatch([spanRow]), TestContext.Current.CancellationToken);
        await store.InsertLogsAsync([logRow], TestContext.Current.CancellationToken);

        AssertCanonicalRefs(Assert.Single(SpanMapper.ToContracts(await store.GetSpansAsync(
            "default",
            ct: TestContext.Current.CancellationToken))).Resource.EntityRefs);
        var storedLog = Assert.Single(await store.GetLogsAsync(
            "default",
            ct: TestContext.Current.CancellationToken));
        Assert.Equal(eventName, storedLog.EventName);
        AssertCanonicalRefs(LogMapper.ToContract(storedLog).Resource.EntityRefs);
    }

    [Fact]
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
        var original = Assert.Single(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(request)));

        var changed = request.Clone();
        changed.ResourceLogs[0].Resource.EntityRefs
            .Single(static entityRef => entityRef.Type == "service")
            .DescriptionKeys.Clear();
        var updated = Assert.Single(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(changed)));

        Assert.Equal(original.LogId, updated.LogId);
        Assert.NotEqual(original.ResourceEntityRefsJson, updated.ResourceEntityRefsJson);

        changed = request.Clone();
        changed.ResourceLogs[0].Resource.EntityRefs
            .Single(static entityRef => entityRef.Type == "service")
            .SchemaUrl = "https://opentelemetry.io/schemas/1.39.0";
        updated = Assert.Single(IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(changed)));

        Assert.Equal(original.LogId, updated.LogId);
        Assert.NotEqual(original.ResourceEntityRefsJson, updated.ResourceEntityRefsJson);
    }

    [Fact]
    public void Entity_references_preserve_only_their_non_denied_custom_resource_attributes()
    {
        var resource = BuildResource();
        resource.Attributes.Add(StringAttribute("custom.entity.id", "custom-1"));
        resource.Attributes.Add(StringAttribute("custom.entity.description", "Custom entity"));
        resource.Attributes.Add(StringAttribute("custom.unreferenced", "discard me"));
        resource.EntityRefs.Add(new EntityRef
        {
            Type = "custom.entity",
            IdKeys = { "custom.entity.id" },
            DescriptionKeys = { "custom.entity.description" }
        });

        var span = SpanMapper.ToContracts([ProjectSingleSpan(resource)]).Single();
        var attributes = Assert.IsAssignableFrom<IReadOnlyList<Qyl.Api.Contracts.Common.Attribute>>(
            span.Resource.Attributes);
        Assert.Contains(attributes, static attribute =>
            attribute.Key == "custom.entity.id" && Equals(attribute.Value, "custom-1"));
        Assert.Contains(attributes, static attribute =>
            attribute.Key == "custom.entity.description" && Equals(attribute.Value, "Custom entity"));
        Assert.DoesNotContain(attributes, static attribute => attribute.Key == "custom.unreferenced");

        var denied = BuildResource();
        denied.Attributes.Add(StringAttribute("custom.api_key", "do-not-store"));
        denied.EntityRefs.Add(new EntityRef
        {
            Type = "custom.entity",
            IdKeys = { "custom.api_key" }
        });
        Assert.Throws<InvalidDataException>(() => ProjectSingleSpan(denied));
    }

    private static SpanStorageRow ProjectSingleSpan(Resource resource)
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
                                    TraceId = ByteString.CopyFrom(new byte[16]),
                                    SpanId = ByteString.CopyFrom(new byte[8]),
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

        return Assert.Single(IngestionStorageMapper.ToSpanStorageRows(
            OtlpConverter.ConvertTraceRequest(request)));
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
        Assert.Collection(
            Assert.IsAssignableFrom<IReadOnlyList<Qyl.Api.Contracts.Common.EntityRef>>(entityRefs),
            deployment =>
            {
                Assert.Equal("deployment", deployment.Type);
                Assert.Equal(["deployment.environment.name"], deployment.IdKeys);
                Assert.Null(deployment.DescriptionKeys);
            },
            service =>
            {
                Assert.Equal("service", service.Type);
                Assert.Equal(["service.name"], service.IdKeys);
                Assert.Equal(["service.namespace"], service.DescriptionKeys);
            });
    }
}

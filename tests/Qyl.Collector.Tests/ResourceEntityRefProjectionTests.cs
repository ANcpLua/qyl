using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using OtlpProfile = OpenTelemetry.Proto.Profiles.V1Development.Profile;
using OtlpProfilesDictionary = OpenTelemetry.Proto.Profiles.V1Development.ProfilesDictionary;
using OtlpResourceLogs = OpenTelemetry.Proto.Logs.V1.ResourceLogs;
using OtlpResourceProfiles = OpenTelemetry.Proto.Profiles.V1Development.ResourceProfiles;
using OtlpScopeLogs = OpenTelemetry.Proto.Logs.V1.ScopeLogs;
using OtlpScopeProfiles = OpenTelemetry.Proto.Profiles.V1Development.ScopeProfiles;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;

namespace Qyl.Collector.Tests;

public sealed class ResourceEntityRefProjectionTests
{
    [Fact]
    public async Task Entity_references_survive_trace_log_and_profile_storage_projection_in_canonical_order()
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

        var profileRequest = new ExportProfilesServiceRequest
        {
            ResourceProfiles =
            {
                new OtlpResourceProfiles
                {
                    Resource = BuildResource(),
                    ScopeProfiles =
                    {
                        new OtlpScopeProfiles
                        {
                            Profiles =
                            {
                                new OtlpProfile
                                {
                                    ProfileId = ByteString.CopyFrom(new byte[16]),
                                    TimeUnixNano = 4
                                }
                            }
                        }
                    }
                }
            },
            Dictionary = new OtlpProfilesDictionary()
        };
        var profileDetail = Assert.Single(IngestionStorageMapper.ToProfileStorageRows(
            OtlpConverter.ConvertProfiles(profileRequest)));
        Assert.NotNull(profileDetail.Profile.ResourceEntityRefsJson);
        AssertCanonicalRefs(ProfileMapper.ToContract(profileDetail).Resource.EntityRefs);

        await using var store = new DuckDbStore(":memory:");
        await store.EnqueueAsync(new SpanBatch([spanRow]), TestContext.Current.CancellationToken);
        await store.InsertLogsAsync([logRow], TestContext.Current.CancellationToken);
        await store.InsertProfilesAsync([profileDetail], TestContext.Current.CancellationToken);

        AssertCanonicalRefs(Assert.Single(SpanMapper.ToContracts(await store.GetSpansAsync(
            "default",
            ct: TestContext.Current.CancellationToken))).Resource.EntityRefs);
        var storedLog = Assert.Single(await store.GetLogsAsync(
            "default",
            ct: TestContext.Current.CancellationToken));
        Assert.Equal(eventName, storedLog.EventName);
        AssertCanonicalRefs(LogMapper.ToContract(storedLog).Resource.EntityRefs);
        AssertCanonicalRefs(ProfileMapper.ToContract(Assert.Single(await store.GetProfilesAsync(
            "default",
            ct: TestContext.Current.CancellationToken))).Resource.EntityRefs);
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
    public void Empty_entity_references_preserve_pre_feature_stable_storage_ids()
    {
        var log = Assert.Single(IngestionStorageMapper.ToLogStorageRows(new LogIngestionBatch(
        [
            new LogIngestionRecord
            {
                TimeUnixNano = 1,
                SeverityNumber = 0,
                ServiceName = "unknown",
                Attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceEntityRefs = []
            }
        ])));
        Assert.Equal("log_61ff352f12dd8bf7c6aa5b159489ef27", log.LogId);

        var metric = Assert.Single(IngestionStorageMapper.ToMetricStorageRows(new MetricIngestionBatch(
        [
            new MetricIngestionRecord
            {
                MetricName = "legacy.metric",
                MetricType = MetricStorageTypes.Gauge,
                Metadata = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceDroppedAttributesCount = 0,
                HasInstrumentationScope = false,
                ScopeAttributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ScopeDroppedAttributesCount = 0,
                TimeUnixNano = 1,
                StartTimeUnixNano = 0,
                Flags = 0,
                Exemplars = [],
                ServiceName = "unknown",
                Attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceEntityRefs = []
            }
        ])));
        Assert.Equal("metric_7675ae3b4f9225873b3e27c0cfeedf16", metric.MetricId);

        var profile = Assert.Single(IngestionStorageMapper.ToProfileStorageRows(new ProfileIngestionBatch(
        [
            new ProfileIngestionRecord
            {
                ProfileId = "",
                TimeUnixNano = 1,
                DurationNano = 0,
                SampleCount = 0,
                ServiceName = "unknown",
                Attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal),
                ResourceEntityRefs = [],
                Functions = [],
                Locations = [],
                Mappings = [],
                Samples = [],
                Stacks = []
            }
        ])));
        Assert.Equal("profile_5cf620f9c11a610c7e173457e29c2112", profile.Profile.ProfileId);
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

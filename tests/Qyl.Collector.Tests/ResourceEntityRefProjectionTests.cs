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

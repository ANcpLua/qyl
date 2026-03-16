using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using Xunit;

namespace Qyl.Collector.Tests.Ingestion;

public sealed class OtlpConverterCapabilityTests
{
    [Fact]
    public void ExtractServiceInstance_PreservesCapabilityArraysInMetadataJson()
    {
        Dictionary<string, string> resourceAttributes = new(StringComparer.Ordinal)
        {
            ["service.name"] = "planner",
            ["qyl.capability.agents"] = """["planner","executor"]""",
            ["qyl.capability.genai.models"] = """["gpt-4o-mini"]"""
        };

        var instance = Assert.IsType<ServiceInstanceRecord>(
            OtlpConverter.ExtractServiceInstance(resourceAttributes, null, 42));
        var metadataJson = Assert.IsType<string>(instance.MetadataJson);

        using var metadata = JsonDocument.Parse(metadataJson);

        Assert.Equal(
            ["planner", "executor"],
            ReadStringArray(metadata.RootElement.GetProperty("qyl.capability.agents")));
        Assert.Equal(
            ["gpt-4o-mini"],
            ReadStringArray(metadata.RootElement.GetProperty("qyl.capability.genai.models")));
    }

    [Fact]
    public void ExtractServiceInstancesFromJson_ReturnsResourceOnlyServiceInstances()
    {
        var request = new OtlpExportTraceServiceRequest
        {
            ResourceSpans =
            [
                new OtlpResourceSpans
                {
                    Resource = new OtlpResource
                    {
                        Attributes =
                        [
                            new OtlpKeyValue
                            {
                                Key = "service.name", Value = new OtlpAnyValue { StringValue = "planner" }
                            },
                            new OtlpKeyValue
                            {
                                Key = "qyl.capability.agents",
                                Value = new OtlpAnyValue
                                {
                                    ArrayValue = new OtlpArrayValue
                                    {
                                        Values = [new OtlpAnyValue { StringValue = "planner" }]
                                    }
                                }
                            }
                        ]
                    },
                    ScopeSpans = []
                }
            ]
        };

        var instances = OtlpConverter.ExtractServiceInstancesFromJson(request);

        var instance = Assert.Single(instances);
        Assert.Equal("planner", instance.ServiceName);
        var metadataJson = Assert.IsType<string>(instance.MetadataJson);

        using var metadata = JsonDocument.Parse(metadataJson);
        Assert.Equal(
            ["planner"],
            ReadStringArray(metadata.RootElement.GetProperty("qyl.capability.agents")));
    }

    private static string[] ReadStringArray(JsonElement element) =>
        element.EnumerateArray()
            .Select(static value => value.GetString())
            .Where(static value => value is not null)
            .Select(static value => value!)
            .ToArray();
}

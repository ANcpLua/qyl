using System.Text.Json;
using AwesomeAssertions;
using Xunit;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

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

        var instance = OtlpConverter.ExtractServiceInstance(resourceAttributes, null, 42)
            .Should().BeOfType<ServiceInstanceRecord>().Which;
        var metadataJson = instance.MetadataJson.Should().BeOfType<string>().Which;

        using var metadata = JsonDocument.Parse(metadataJson);

        ReadStringArray(metadata.RootElement.GetProperty("qyl.capability.agents"))
            .Should().BeEquivalentTo(["planner", "executor"]);
        ReadStringArray(metadata.RootElement.GetProperty("qyl.capability.genai.models"))
            .Should().BeEquivalentTo(["gpt-4o-mini"]);
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

        var instance = instances.Should().ContainSingle().Which;
        instance.ServiceName.Should().Be("planner");
        var metadataJson = instance.MetadataJson.Should().BeOfType<string>().Which;

        using var metadata = JsonDocument.Parse(metadataJson);
        ReadStringArray(metadata.RootElement.GetProperty("qyl.capability.agents"))
            .Should().BeEquivalentTo(["planner"]);
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(static v => v.GetString())
                .Where(static v => v is not null)
                .Select(static v => v!)
                .ToArray();
        }

        if (element.ValueKind is JsonValueKind.String)
        {
            var raw = element.GetString()!;
            // Capability values may be stored as JSON-encoded arrays (e.g. "[\"a\",\"b\"]")
            if (raw.StartsWith('['))
            {
                using var inner = JsonDocument.Parse(raw);
                return inner.RootElement.EnumerateArray()
                    .Select(static v => v.GetString())
                    .Where(static v => v is not null)
                    .Select(static v => v!)
                    .ToArray();
            }

            return [raw];
        }

        return [];
    }
}

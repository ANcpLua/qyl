using System.Text.Json;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

public sealed class AttributeJsonContextTests
{
    public static TheoryData<string> Shapes => ["empty", "string", "bool", "int", "double", "nan", "bytes", "array", "map"];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Ingestion_and_http_contexts_write_the_same_attribute_json(string shape)
    {
        var value = shape switch
        {
            "empty" => OtlpAttributeValue.Empty,
            "string" => OtlpAttributeValue.FromString("checkout"),
            "bool" => OtlpAttributeValue.FromBool(true),
            "int" => OtlpAttributeValue.FromInt(long.MaxValue),
            "double" => OtlpAttributeValue.FromDouble(0.25),
            "nan" => OtlpAttributeValue.FromDouble(double.NaN),
            "bytes" => OtlpAttributeValue.FromBytes([0x00, 0xff, 0x10]),
            "array" => OtlpAttributeValue.FromArray([OtlpAttributeValue.FromInt(1), OtlpAttributeValue.FromString("two")]),
            "map" => OtlpAttributeValue.FromKeyValueList(new Dictionary<string, OtlpAttributeValue>
            {
                ["b"] = OtlpAttributeValue.FromBool(false),
                ["a"] = OtlpAttributeValue.FromKeyValueList(new Dictionary<string, OtlpAttributeValue>
                {
                    ["nested"] = OtlpAttributeValue.FromDouble(1.5)
                })
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        var contract = value.ToContract();

        Assert.Equal(
            JsonSerializer.Serialize(contract, QylSerializerContext.Default.AttributeValue),
            JsonSerializer.Serialize(contract, OtlpAttributeJsonContext.Default.AttributeValue));
    }

    [Fact]
    public void Ingestion_context_carries_the_http_context_options()
    {
        var http = QylSerializerContext.Default.Options;
        var ingestion = OtlpAttributeJsonContext.Default.Options;

        Assert.Equal(http.PropertyNamingPolicy, ingestion.PropertyNamingPolicy);
        Assert.Equal(http.DefaultIgnoreCondition, ingestion.DefaultIgnoreCondition);
        Assert.Equal(http.NumberHandling, ingestion.NumberHandling);
        Assert.Equal(http.AllowOutOfOrderMetadataProperties, ingestion.AllowOutOfOrderMetadataProperties);
        Assert.Equal(http.WriteIndented, ingestion.WriteIndented);
        Assert.Equal(http.Converters.Count, ingestion.Converters.Count);
    }
}

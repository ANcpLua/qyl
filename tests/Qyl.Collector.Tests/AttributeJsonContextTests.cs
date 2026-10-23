using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// Storage writes every attribute value shape through its own record and context. These are the bytes
/// earlier collectors persisted, and the read boundary hands the API exactly those bytes.
/// </summary>
[TestClass]
public sealed class AttributeJsonContextTests
{
    public static IEnumerable<object[]> Shapes =>
    [
        ["empty", "null"],
        ["string", "\"checkout\""],
        ["bool", "true"],
        ["int", """{"type":"int","value":"9223372036854775807"}"""],
        ["double", """{"type":"double","value":0.25}"""],
        ["nan", """{"type":"double","value":"NaN"}"""],
        ["bytes", """{"type":"bytes","base64":"AP8Q"}"""],
        ["array", """[{"type":"int","value":"1"},"two"]"""],
        ["map", """{"type":"kvlist","values":{"a":{"type":"kvlist","values":{"nested":{"type":"double","value":1.5}}},"b":false}}"""]
    ];

    [TestMethod]
    [DynamicData(nameof(Shapes))]
    public void Storage_and_http_write_the_same_attribute_json(string shape, string expected)
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

        var stored = JsonSerializer.Serialize(StoredRecordMapper.ToStored(value), StorageJsonSerializerContext.Default.StoredAttributeValue);
        var read = StoredValueMapper.ToContract(JsonSerializer.Deserialize(stored, StorageJsonSerializerContext.Default.StoredAttributeValue));

        Assert.AreEqual(expected, stored);
        Assert.AreEqual(expected, JsonSerializer.Serialize(read, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Common.AttributeValue>()));
    }
}

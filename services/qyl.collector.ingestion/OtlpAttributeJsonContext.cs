using Qyl.Api.Contracts.Common;
using ContractAttribute = Qyl.Api.Contracts.Common.Attribute;

namespace Qyl.Collector.Ingestion;

/// <summary>
/// The contract AttributeValue as ingestion and storage write it. The options are the HTTP
/// context's (QylSerializerContext) so a persisted value and an API response are the same JSON;
/// a test holds the two contexts to byte-identical output.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString |
                     JsonNumberHandling.AllowNamedFloatingPointLiterals,
    AllowOutOfOrderMetadataProperties = true,
    WriteIndented = false)]
[JsonSerializable(typeof(AttributeValue))]
[JsonSerializable(typeof(AttributeObjectValue))]
[JsonSerializable(typeof(ContractAttribute))]
[JsonSerializable(typeof(ContractAttribute[]))]
[JsonSerializable(typeof(AttributeBytesValue))]
[JsonSerializable(typeof(AttributeIntValue))]
[JsonSerializable(typeof(AttributeDoubleValue))]
[JsonSerializable(typeof(AttributeKeyValueListValue))]
internal partial class OtlpAttributeJsonContext : JsonSerializerContext;

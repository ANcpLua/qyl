namespace Qyl.Collector.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString |
                     JsonNumberHandling.AllowNamedFloatingPointLiterals,
    WriteIndented = false)]
[JsonSerializable(typeof(List<SpanEventJson>), TypeInfoPropertyName = "SpanEventJsonList")]
[JsonSerializable(typeof(List<SpanLinkJson>), TypeInfoPropertyName = "SpanLinkJsonList")]
[JsonSerializable(typeof(List<ResourceEntityRefIngestionRecord>), TypeInfoPropertyName = "ResourceEntityRefIngestionRecordList")]
internal partial class StorageJsonSerializerContext : JsonSerializerContext;

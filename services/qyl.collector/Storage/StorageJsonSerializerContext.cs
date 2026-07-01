namespace Qyl.Collector.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(List<ProfileLocationLineJson>), TypeInfoPropertyName = "ProfileLocationLineJsonList")]
[JsonSerializable(typeof(List<SpanEventJson>), TypeInfoPropertyName = "SpanEventJsonList")]
[JsonSerializable(typeof(List<SpanLinkJson>), TypeInfoPropertyName = "SpanLinkJsonList")]
[JsonSerializable(typeof(long[]), TypeInfoPropertyName = "Int64Array")]
[JsonSerializable(typeof(ulong[]), TypeInfoPropertyName = "UInt64Array")]
[JsonSerializable(typeof(int[]), TypeInfoPropertyName = "Int32Array")]
internal partial class StorageJsonSerializerContext : JsonSerializerContext;

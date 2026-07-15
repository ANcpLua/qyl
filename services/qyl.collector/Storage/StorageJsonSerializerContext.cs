namespace Qyl.Collector.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString |
                     JsonNumberHandling.AllowNamedFloatingPointLiterals,
    WriteIndented = false)]
[JsonSerializable(typeof(List<ProfileLocationLineJson>), TypeInfoPropertyName = "ProfileLocationLineJsonList")]
[JsonSerializable(typeof(List<SpanEventJson>), TypeInfoPropertyName = "SpanEventJsonList")]
[JsonSerializable(typeof(List<SpanLinkJson>), TypeInfoPropertyName = "SpanLinkJsonList")]
[JsonSerializable(typeof(List<ResourceEntityRefIngestionRecord>), TypeInfoPropertyName = "ResourceEntityRefIngestionRecordList")]
[JsonSerializable(typeof(MetricHistogramBucketsJson))]
[JsonSerializable(typeof(MetricExponentialHistogramBucketsJson))]
[JsonSerializable(typeof(List<MetricExemplarJson>), TypeInfoPropertyName = "MetricExemplarJsonList")]
[JsonSerializable(typeof(List<MetricSummaryQuantileJson>), TypeInfoPropertyName = "MetricSummaryQuantileJsonList")]
[JsonSerializable(typeof(long[]), TypeInfoPropertyName = "Int64Array")]
[JsonSerializable(typeof(ulong[]), TypeInfoPropertyName = "UInt64Array")]
[JsonSerializable(typeof(int[]), TypeInfoPropertyName = "Int32Array")]
internal partial class StorageJsonSerializerContext : JsonSerializerContext;

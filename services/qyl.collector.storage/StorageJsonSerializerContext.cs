namespace Qyl.Collector.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString |
                     JsonNumberHandling.AllowNamedFloatingPointLiterals,
    AllowOutOfOrderMetadataProperties = true,
    WriteIndented = false)]
[JsonSerializable(typeof(List<SpanEventJson>), TypeInfoPropertyName = "SpanEventJsonList")]
[JsonSerializable(typeof(List<SpanLinkJson>), TypeInfoPropertyName = "SpanLinkJsonList")]
[JsonSerializable(typeof(List<ResourceEntityRefIngestionRecord>), TypeInfoPropertyName = "ResourceEntityRefIngestionRecordList")]
[JsonSerializable(typeof(Api.Contracts.Common.AttributeValue))]
[JsonSerializable(typeof(Api.Contracts.Common.AttributeObjectValue))]
[JsonSerializable(typeof(Api.Contracts.Common.AttributeBytesValue))]
[JsonSerializable(typeof(Api.Contracts.Common.AttributeIntValue))]
[JsonSerializable(typeof(Api.Contracts.Common.AttributeDoubleValue))]
[JsonSerializable(typeof(Api.Contracts.Common.AttributeKeyValueListValue))]
[JsonSerializable(typeof(HistoricalMetricProjection))]
[JsonSerializable(typeof(IReadOnlyList<string>), TypeInfoPropertyName = "IReadOnlyListString")]
[JsonSerializable(typeof(IReadOnlyList<double>), TypeInfoPropertyName = "Doubles")]
[JsonSerializable(typeof(IReadOnlyList<ulong>), TypeInfoPropertyName = "Counts")]
[JsonSerializable(typeof(Api.Contracts.OTel.Metrics.MetricPointData), TypeInfoPropertyName = "MetricData")]
[JsonSerializable(typeof(IReadOnlyList<Api.Contracts.OTel.Metrics.MetricExemplar>), TypeInfoPropertyName = "MetricExemplars")]
[JsonSerializable(typeof(IReadOnlyList<Qyl.Api.Contracts.Common.Attribute>), TypeInfoPropertyName = "MetricMetadata")]
internal partial class StorageJsonSerializerContext : JsonSerializerContext;

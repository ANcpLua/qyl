namespace Qyl.Collector.Ingestion;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
[JsonSerializable(typeof(List<ProfileLocationLineJson>), TypeInfoPropertyName = "ProfileLocationLineJsonList")]
internal partial class IngestionJsonSerializerContext : JsonSerializerContext;

internal readonly record struct ProfileLocationLineJson(
    [property: JsonPropertyName("functionOrdinal")] int FunctionOrdinal,
    [property: JsonPropertyName("line")] long Line,
    [property: JsonPropertyName("column")] long Column);

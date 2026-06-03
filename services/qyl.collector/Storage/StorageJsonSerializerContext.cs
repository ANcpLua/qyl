namespace Qyl.Collector.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    WriteIndented = false)]
[JsonSerializable(typeof(List<ProfileLocationLineJson>), TypeInfoPropertyName = "ProfileLocationLineJsonList")]
internal partial class StorageJsonSerializerContext : JsonSerializerContext;

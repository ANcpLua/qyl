#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSpec.Helpers
{
    public class JsonSerializationProvider : IJsonSerializationProvider
    {
        public virtual JsonSerializerOptions Options { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public virtual T? Deserialize<T>(string value)
        {
            return JsonSerializer.Deserialize<T>(value, Options);
        }

        public virtual string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }
    }
}

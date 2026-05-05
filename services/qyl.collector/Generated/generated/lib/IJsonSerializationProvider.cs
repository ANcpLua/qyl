#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSpec.Helpers
{
    public interface IJsonSerializationProvider
    {
        string Serialize<T>(T value);

        T? Deserialize<T>(string value);
    }
}

#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSpec.Helpers.JsonConverters
{
    public class Base64UrlJsonConverter : JsonConverter<byte[]>
    {
        private static string Pad(string input)
        {
            var count = 3 - ((input.Length + 3) % 4);
            if (count == 0)
            {
                return input;
            }
            return $"{input}{new string('=', count)}";
        }

        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert != typeof(byte[])) throw new ArgumentException($"Cannot apply converter {this.GetType().FullName} to type {typeToConvert.FullName}");
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Convert.FromBase64String(Pad(value.Replace('-', '+').Replace('_', '/')));
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
        }
    }
}

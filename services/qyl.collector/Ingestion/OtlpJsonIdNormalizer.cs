using System.Text;

namespace Qyl.Collector.Ingestion;

/// <summary>
/// Rewrites the hex-encoded id fields of an OTLP/JSON payload to base64 so the protojson parser
/// yields the bytes the sender meant.
/// </summary>
/// <remarks>
/// The OTLP spec special-cases trace/span/profile ids in JSON to hex, while proto3's JSON mapping
/// (which <c>Google.Protobuf.JsonParser</c> implements) decodes every <c>bytes</c> field as base64.
/// Without this rewrite a spec-compliant hex id happens to also be valid base64 and silently
/// decodes to the wrong bytes — mangled, unjoinable ids. The contract is strict spec-hex: a
/// non-hex or wrong-length id throws <see cref="InvalidDataException"/>, which the ingest
/// endpoints surface as a 400, never a mangled row.
/// </remarks>
internal static class OtlpJsonIdNormalizer
{
    private const int TraceIdBytes = 16;
    private const int SpanIdBytes = 8;
    private const int ProfileIdBytes = 16;

    public static string NormalizeIdsToProtoJson(ReadOnlySpan<byte> utf8Json)
    {
        // The previous StreamReader-based path tolerated a UTF-8 BOM; keep that tolerance.
        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        if (utf8Json.StartsWith(utf8Bom))
            utf8Json = utf8Json[utf8Bom.Length..];

        var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions { MaxDepth = 128 });
        var buffer = new ArrayBufferWriter<byte>(utf8Json.Length);
        using var writer = new Utf8JsonWriter(buffer);

        // Set when the previous token was a property name that carries an id; only the string
        // value immediately following it is rewritten. OTLP/JSON never nests these names as
        // anything else: attribute entries spell their key inside a "key" property, so an
        // attribute named "traceId" cannot collide.
        var pendingIdBytes = 0;
        string? pendingIdName = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    var name = reader.GetString()!;
                    writer.WritePropertyName(name);
                    pendingIdBytes = ExpectedIdByteLength(name);
                    pendingIdName = pendingIdBytes > 0 ? name : null;
                    continue;

                case JsonTokenType.String:
                    if (pendingIdBytes > 0)
                        WriteIdAsBase64(writer, reader.GetString()!, pendingIdBytes, pendingIdName!);
                    else
                        writer.WriteStringValue(reader.GetString());
                    break;

                case JsonTokenType.Number:
                    writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);
                    break;

                case JsonTokenType.True:
                    writer.WriteBooleanValue(true);
                    break;

                case JsonTokenType.False:
                    writer.WriteBooleanValue(false);
                    break;

                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    break;

                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    break;

                case JsonTokenType.EndObject:
                    writer.WriteEndObject();
                    break;

                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    break;

                case JsonTokenType.EndArray:
                    writer.WriteEndArray();
                    break;
            }

            pendingIdBytes = 0;
            pendingIdName = null;
        }

        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    // protojson accepts both the lowerCamelCase JSON name and the original proto field name.
    private static int ExpectedIdByteLength(string propertyName) => propertyName switch
    {
        "traceId" or "trace_id" => TraceIdBytes,
        "spanId" or "span_id" or "parentSpanId" or "parent_span_id" => SpanIdBytes,
        "profileId" or "profile_id" => ProfileIdBytes,
        _ => 0
    };

    private static void WriteIdAsBase64(Utf8JsonWriter writer, string hex, int expectedBytes, string field)
    {
        // Empty means absent (e.g. a root span's parentSpanId); protojson maps it to empty bytes.
        if (hex.Length is 0)
        {
            writer.WriteStringValue(hex);
            return;
        }

        if (hex.Length != expectedBytes * 2)
        {
            throw new InvalidDataException(
                $"OTLP/JSON field '{field}' must be {expectedBytes * 2} hex characters ({expectedBytes} bytes); got {hex.Length} characters.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            throw new InvalidDataException(
                $"OTLP/JSON field '{field}' must be hex-encoded per the OTLP spec; got a non-hex value.");
        }

        writer.WriteBase64StringValue(bytes);
    }
}

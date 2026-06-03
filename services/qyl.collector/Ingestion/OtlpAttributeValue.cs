using System.Text;

namespace Qyl.Collector.Ingestion;

internal enum OtlpAttributeValueKind
{
    String,
    Bool,
    Int,
    Double,
    Bytes,
    Array,
    KeyValueList
}

internal sealed class OtlpAttributeValue
{
    private readonly object value;

    private OtlpAttributeValue(OtlpAttributeValueKind kind, object value)
    {
        Kind = kind;
        this.value = value;
    }

    public OtlpAttributeValueKind Kind { get; }

    public static OtlpAttributeValue FromString(string value) => new(OtlpAttributeValueKind.String, value);

    public static OtlpAttributeValue FromBool(bool value) => new(OtlpAttributeValueKind.Bool, value);

    public static OtlpAttributeValue FromInt(long value) => new(OtlpAttributeValueKind.Int, value);

    public static OtlpAttributeValue FromDouble(double value) => new(OtlpAttributeValueKind.Double, value);

    public static OtlpAttributeValue FromBytes(byte[] value) => new(OtlpAttributeValueKind.Bytes, value);

    public static OtlpAttributeValue FromArray(IReadOnlyList<OtlpAttributeValue> value) =>
        new(OtlpAttributeValueKind.Array, value);

    public static OtlpAttributeValue FromKeyValueList(IReadOnlyDictionary<string, OtlpAttributeValue> value) =>
        new(OtlpAttributeValueKind.KeyValueList, value);

    public string? AsString() =>
        Kind is OtlpAttributeValueKind.String ? (string)value : null;

    public long? AsInt64() =>
        Kind switch
        {
            OtlpAttributeValueKind.Int => (long)value,
            OtlpAttributeValueKind.String when long.TryParse(
                (string)value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };

    public double? AsDouble() =>
        Kind switch
        {
            OtlpAttributeValueKind.Double => (double)value,
            OtlpAttributeValueKind.Int => (long)value,
            OtlpAttributeValueKind.String when double.TryParse(
                (string)value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };

    public string ToStableString() =>
        Kind switch
        {
            OtlpAttributeValueKind.String => (string)value,
            OtlpAttributeValueKind.Bool => (bool)value ? "true" : "false",
            OtlpAttributeValueKind.Int => ((long)value).ToString(CultureInfo.InvariantCulture),
            OtlpAttributeValueKind.Double => ((double)value).ToString("R", CultureInfo.InvariantCulture),
            OtlpAttributeValueKind.Bytes => Convert.ToBase64String((byte[])value),
            _ => WriteJsonToString()
        };

    public void WriteJsonValue(Utf8JsonWriter writer)
    {
        switch (Kind)
        {
            case OtlpAttributeValueKind.String:
                writer.WriteStringValue((string)value);
                break;
            case OtlpAttributeValueKind.Bool:
                writer.WriteBooleanValue((bool)value);
                break;
            case OtlpAttributeValueKind.Int:
                writer.WriteNumberValue((long)value);
                break;
            case OtlpAttributeValueKind.Double:
                WriteDouble(writer, (double)value);
                break;
            case OtlpAttributeValueKind.Bytes:
                writer.WriteStartObject();
                writer.WriteString("type", "bytes");
                writer.WriteString("base64", Convert.ToBase64String((byte[])value));
                writer.WriteEndObject();
                break;
            case OtlpAttributeValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in (IReadOnlyList<OtlpAttributeValue>)value)
                    item.WriteJsonValue(writer);
                writer.WriteEndArray();
                break;
            case OtlpAttributeValueKind.KeyValueList:
                writer.WriteStartObject();
                foreach (var (key, nestedValue) in (IReadOnlyDictionary<string, OtlpAttributeValue>)value)
                {
                    writer.WritePropertyName(key);
                    nestedValue.WriteJsonValue(writer);
                }
                writer.WriteEndObject();
                break;
            default:
                throw new InvalidOperationException($"Unknown OTLP attribute value kind '{Kind}'.");
        }
    }

    private static void WriteDouble(Utf8JsonWriter writer, double value)
    {
        if (double.IsFinite(value))
        {
            writer.WriteNumberValue(value);
            return;
        }

        writer.WriteStringValue(value.ToString("R", CultureInfo.InvariantCulture));
    }

    private string WriteJsonToString()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteJsonValue(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}

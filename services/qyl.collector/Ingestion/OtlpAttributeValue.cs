using System.Text;
using System.Text.Json;
using ContractValue = Qyl.Api.Contracts.Common.AttributeValue;
using Qyl.Api.Contracts.Common;

namespace Qyl.Collector.Ingestion;

internal enum OtlpAttributeValueKind
{
    Empty,
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

    public static OtlpAttributeValue Empty { get; } = new(OtlpAttributeValueKind.Empty, DBNull.Value);

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

    public string ToStableString() =>
        Kind switch
        {
            OtlpAttributeValueKind.Empty => "null",
            OtlpAttributeValueKind.String => (string)value,
            OtlpAttributeValueKind.Bool => (bool)value ? "true" : "false",
            OtlpAttributeValueKind.Int => ((long)value).ToString(CultureInfo.InvariantCulture),
            OtlpAttributeValueKind.Double => ((double)value).ToString("R", CultureInfo.InvariantCulture),
            OtlpAttributeValueKind.Bytes => Convert.ToBase64String((byte[])value),
            _ => WriteJsonToString()
        };

    public string ToIdentityString()
    {
        var builder = new StringBuilder();
        AppendIdentityTo(builder);
        return builder.ToString();
    }

    /// <summary>
    /// The generated contract value. This is the only encoder: storage and the API both write
    /// it through the contract's own converter, so the persisted bytes are the wire bytes.
    /// </summary>
    public ContractValue? ToContract() =>
        Kind switch
        {
            OtlpAttributeValueKind.Empty => null,
            OtlpAttributeValueKind.String => new ContractValue.StringValue((string)value),
            OtlpAttributeValueKind.Bool => new ContractValue.BoolValue((bool)value),
            OtlpAttributeValueKind.Int => new ContractValue.ObjectValue(new AttributeIntValue { Value = (long)value }),
            OtlpAttributeValueKind.Double => new ContractValue.ObjectValue(new AttributeDoubleValue { Value = (double)value }),
            OtlpAttributeValueKind.Bytes => new ContractValue.ObjectValue(new AttributeBytesValue { Base64 = (byte[])value }),
            OtlpAttributeValueKind.Array => new ContractValue.ArrayValue(
                ((IReadOnlyList<OtlpAttributeValue>)value).Select(static item => item.ToContract()).ToArray()),
            OtlpAttributeValueKind.KeyValueList => new ContractValue.ObjectValue(new AttributeKeyValueListValue
            {
                Values = ((IReadOnlyDictionary<string, OtlpAttributeValue>)value)
                    .OrderBy(static item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(static item => item.Key, static item => item.Value.ToContract(), StringComparer.Ordinal)
            }),
            _ => throw new InvalidOperationException($"Unknown OTLP attribute value kind '{Kind}'.")
        };

    public void WriteJsonValue(Utf8JsonWriter writer) =>
        JsonSerializer.Serialize(writer, ToContract(), QylSerializerContext.Default.AttributeValue);

    private string WriteJsonToString() =>
        JsonSerializer.Serialize(ToContract(), QylSerializerContext.Default.AttributeValue);

    private void AppendIdentityTo(StringBuilder builder)
    {
        builder.Append((int)Kind).Append('{');
        switch (Kind)
        {
            case OtlpAttributeValueKind.Empty:
                break;
            case OtlpAttributeValueKind.String:
                AppendIdentitySegment(builder, (string)value);
                break;
            case OtlpAttributeValueKind.Bool:
                AppendIdentitySegment(builder, (bool)value ? "true" : "false");
                break;
            case OtlpAttributeValueKind.Int:
                AppendIdentitySegment(builder, ((long)value).ToString(CultureInfo.InvariantCulture));
                break;
            case OtlpAttributeValueKind.Double:
                AppendIdentitySegment(builder, ((double)value).ToString("R", CultureInfo.InvariantCulture));
                break;
            case OtlpAttributeValueKind.Bytes:
                AppendIdentitySegment(builder, Convert.ToBase64String((byte[])value));
                break;
            case OtlpAttributeValueKind.Array:
            {
                var items = (IReadOnlyList<OtlpAttributeValue>)value;
                builder.Append(items.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
                foreach (var item in items)
                    AppendIdentitySegment(builder, item.ToIdentityString());
                break;
            }
            case OtlpAttributeValueKind.KeyValueList:
            {
                var items = (IReadOnlyDictionary<string, OtlpAttributeValue>)value;
                builder.Append(items.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
                foreach (var (key, nestedValue) in items.OrderBy(static item => item.Key, StringComparer.Ordinal))
                {
                    AppendIdentitySegment(builder, key);
                    AppendIdentitySegment(builder, nestedValue.ToIdentityString());
                }

                break;
            }
            default:
                throw new InvalidOperationException($"Unknown OTLP attribute value kind '{Kind}'.");
        }

        builder.Append('}');
    }

    private static void AppendIdentitySegment(StringBuilder builder, string segment) =>
        builder
            .Append(segment.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(segment);
}

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

    /// <summary>The canonical text of this value, the contract's own definition, shared with TypeScript.</summary>
    public string ToStableString() => ContractValue.StableString(ToContract());

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

}

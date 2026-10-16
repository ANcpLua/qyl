using Qyl.Api.Contracts.Common;
using ContractValue = Qyl.Api.Contracts.Common.AttributeValue;

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
    private readonly object _value;

    private OtlpAttributeValue(OtlpAttributeValueKind kind, object value)
    {
        Kind = kind;
        _value = value;
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
        Kind is OtlpAttributeValueKind.String ? (string)_value : null;

    public long? AsInt64() =>
        Kind switch
        {
            OtlpAttributeValueKind.Int => (long)_value,
            OtlpAttributeValueKind.String when long.TryParse(
                (string)_value,
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
            OtlpAttributeValueKind.String => new ContractValue.StringValue((string)_value),
            OtlpAttributeValueKind.Bool => new ContractValue.BoolValue((bool)_value),
            OtlpAttributeValueKind.Int => new ContractValue.ObjectValue(new AttributeIntValue { Value = (long)_value }),
            OtlpAttributeValueKind.Double => new ContractValue.ObjectValue(new AttributeDoubleValue { Value = (double)_value }),
            OtlpAttributeValueKind.Bytes => new ContractValue.ObjectValue(new AttributeBytesValue { Base64 = (byte[])_value }),
            OtlpAttributeValueKind.Array => new ContractValue.ArrayValue(
                ((IReadOnlyList<OtlpAttributeValue>)_value).Select(static item => item.ToContract()).ToArray()),
            OtlpAttributeValueKind.KeyValueList => new ContractValue.ObjectValue(new AttributeKeyValueListValue
            {
                Values = ((IReadOnlyDictionary<string, OtlpAttributeValue>)_value)
                    .OrderBy(static item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(static item => item.Key, static item => item.Value.ToContract(), StringComparer.Ordinal)
            }),
            _ => throw new InvalidOperationException($"Unknown OTLP attribute value kind '{Kind}'.")
        };

    public void WriteJsonValue(Utf8JsonWriter writer) =>
        JsonSerializer.Serialize(writer, ToContract(), OtlpAttributeJsonContext.Default.AttributeValue);

}

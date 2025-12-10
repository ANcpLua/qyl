namespace qyl.grpc.Models;

public abstract record AttributeValue
{
    public abstract object RawValue { get; }
}

public sealed record StringValue(string Value) : AttributeValue
{
    public override object RawValue => Value;
}

public sealed record IntValue(long Value) : AttributeValue
{
    public override object RawValue => Value;
}

public sealed record DoubleValue(double Value) : AttributeValue
{
    public override object RawValue => Value;
}

public sealed record BoolValue(bool Value) : AttributeValue
{
    public override object RawValue => Value;
}

public sealed record BytesValue(byte[] Value) : AttributeValue
{
    public override object RawValue => Value;
}

public sealed record ArrayValue(IReadOnlyList<AttributeValue> Values) : AttributeValue
{
    public override object RawValue => Values;
}

public sealed record MapValue(IReadOnlyDictionary<string, AttributeValue> Values) : AttributeValue
{
    public override object RawValue => Values;
}

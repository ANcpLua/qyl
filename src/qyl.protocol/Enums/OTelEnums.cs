namespace qyl.protocol.Enums;

/// <summary>
///     OTel span kind describing the relationship between spans.
///     Values match the OTel protobuf specification.
/// </summary>
public enum SpanKind : byte
{
    Unspecified = 0,
    Internal = 1,
    Server = 2,
    Client = 3,
    Producer = 4,
    Consumer = 5
}

/// <summary>
///     OTel span status code.
///     Values match the OTel protobuf specification.
/// </summary>
public enum SpanStatusCode : byte
{
    Unset = 0,
    Ok = 1,
    Error = 2
}

using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

[JsonConverter(typeof(JsonStringEnumConverter<SignalOperator>))]
public enum SignalOperator
{
    Eq,

    Neq,

    Gt,

    Gte,

    Lt,

    Lte,

    Contains,

    Exists,

    NotExists,

    Matches,

    InSet
}

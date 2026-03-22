using System.Text.Json.Serialization;

namespace Qyl.Contracts.Intelligence;

/// <summary>Comparison operator for signal evaluation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignalOperator>))]
public enum SignalOperator
{
    /// <summary>Equals</summary>
    Eq,

    /// <summary>Not equals</summary>
    Neq,

    /// <summary>Greater than</summary>
    Gt,

    /// <summary>Greater than or equal</summary>
    Gte,

    /// <summary>Less than</summary>
    Lt,

    /// <summary>Less than or equal</summary>
    Lte,

    /// <summary>String contains</summary>
    Contains,

    /// <summary>Attribute is non-null</summary>
    Exists,

    /// <summary>Attribute is null</summary>
    NotExists,

    /// <summary>Regex match</summary>
    Matches,

    /// <summary>Value in set (comma-separated)</summary>
    InSet
}

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1019, IDE0300

namespace ANcpLua.NET.Sdk.Shared.Polyfills.NullabilityAttributes;

/// <summary>
///     Specifies that the method or property will ensure that the listed field and property members have not-null
///     values.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
[ExcludeFromCodeCoverage]
internal sealed class MemberNotNullAttribute : Attribute
{
    public MemberNotNullAttribute(string member) => Members = [member];

    public MemberNotNullAttribute(params string[] members) => Members = members;

    public string[] Members { get; }
}

/// <summary>
///     Specifies that the method or property will ensure that the listed field and property members have not-null
///     values when returning with the specified return value condition.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
[ExcludeFromCodeCoverage]
internal sealed class MemberNotNullWhenAttribute : Attribute
{
    public MemberNotNullWhenAttribute(bool returnValue, string member)
    {
        ReturnValue = returnValue;
        Members = [member];
    }

    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
        ReturnValue = returnValue;
        Members = members;
    }

    public bool ReturnValue { get; }
    public string[] Members { get; }
}

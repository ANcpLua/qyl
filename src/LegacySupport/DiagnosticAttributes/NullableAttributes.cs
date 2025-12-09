// Copyright (c) qyl. All rights reserved.
// Polyfill for nullable attributes on older frameworks.

#pragma warning disable CA1019, IDE0300

#if !NETCOREAPP3_1_OR_GREATER

namespace System.Diagnostics.CodeAnalysis;

/// <summary>Specifies that null is allowed as an input even if the corresponding type disallows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class AllowNullAttribute : Attribute { }

/// <summary>Specifies that null is disallowed as an input even if the corresponding type allows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class DisallowNullAttribute : Attribute { }

/// <summary>Specifies that an output may be null even if the corresponding type disallows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class MaybeNullAttribute : Attribute { }

/// <summary>Specifies that an output will not be null even if the corresponding type allows it.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class NotNullAttribute : Attribute { }

/// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter may be null.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class MaybeNullWhenAttribute : Attribute
{
    public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
    public bool ReturnValue { get; }
}

/// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter will not be null.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class NotNullWhenAttribute : Attribute
{
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
    public bool ReturnValue { get; }
}

/// <summary>Specifies that the output will be non-null if the named parameter is non-null.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class NotNullIfNotNullAttribute : Attribute
{
    public NotNullIfNotNullAttribute(string parameterName) => ParameterName = parameterName;
    public string ParameterName { get; }
}

/// <summary>Applied to a method that will never return under any circumstance.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class DoesNotReturnAttribute : Attribute { }

/// <summary>Specifies that the method will not return if the associated Boolean parameter is passed the specified value.</summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class DoesNotReturnIfAttribute : Attribute
{
    public DoesNotReturnIfAttribute(bool parameterValue) => ParameterValue = parameterValue;
    public bool ParameterValue { get; }
}

#endif

/// <summary>Specifies that the method or property will ensure that the listed field and property members have not-null values.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
[ExcludeFromCodeCoverage]
internal sealed class MemberNotNullAttribute : Attribute
{
    public MemberNotNullAttribute(string member) => Members = new[] { member };
    public MemberNotNullAttribute(params string[] members) => Members = members;
    public string[] Members { get; }
}

/// <summary>Specifies that the method or property will ensure that the listed field and property members have not-null values when returning with the specified return value condition.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
[ExcludeFromCodeCoverage]
internal sealed class MemberNotNullWhenAttribute : Attribute
{
    public MemberNotNullWhenAttribute(bool returnValue, string member)
    {
        ReturnValue = returnValue;
        Members = new[] { member };
    }

    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
        ReturnValue = returnValue;
        Members = members;
    }

    public bool ReturnValue { get; }
    public string[] Members { get; }
}

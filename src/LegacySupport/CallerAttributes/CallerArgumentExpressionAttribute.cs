// Copyright (c) qyl. All rights reserved.
// Polyfill for CallerArgumentExpressionAttribute on older frameworks.

#if !NET6_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Tags parameter that should be filled with specific caller argument expression.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallerArgumentExpressionAttribute"/> class.
    /// </summary>
    /// <param name="parameterName">Function parameter to take the expression from.</param>
    public CallerArgumentExpressionAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    /// <summary>
    /// Gets name of the function parameter that expression should be taken from.
    /// </summary>
    public string ParameterName { get; }
}

#endif

// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

/// <summary>Shared helper that identifies telemetry tag-setter invocations and extracts the key argument.</summary>
internal static class TagMethodMatcher
{
    // Method names that accept (string key, ...) as their first argument
    private static readonly HashSet<string> s_tagMethods = new(StringComparer.Ordinal)
    {
        "SetTag", "AddTag", "SetCustomProperty", "SetAttribute", "AddAttribute",
        "SetBaggage", "RecordException",
    };

    /// <summary>
    /// If <paramref name="invocation"/> is a call to a known tag-setter method,
    /// returns the first string-literal argument; otherwise returns null.
    /// </summary>
    public static LiteralExpressionSyntax? TryGetStringKeyArgument(InvocationExpressionSyntax invocation)
    {
        var methodName = GetMethodName(invocation);
        if (methodName is null || !s_tagMethods.Contains(methodName))
            return null;

        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
            return null;

        return args[0].Expression as LiteralExpressionSyntax;
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            IdentifierNameSyntax id         => id.Identifier.Text,
            _                               => null,
        };
    }
}

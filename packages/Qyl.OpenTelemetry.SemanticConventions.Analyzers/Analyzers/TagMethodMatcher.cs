
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

internal static class TagMethodMatcher
{
    private static readonly HashSet<string> s_tagMethods = new(StringComparer.Ordinal)
    {
        "SetTag", "AddTag", "SetCustomProperty", "SetAttribute", "AddAttribute",
        "SetBaggage", "RecordException"
    };

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
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => null
        };
    }
}

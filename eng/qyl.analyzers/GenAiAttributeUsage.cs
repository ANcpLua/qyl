using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace qyl.analyzers;

internal static class GenAiAttributeUsage
{
    private static readonly ImmutableHashSet<string> GenAiPrefixes =
        ImmutableHashSet.Create("gen_ai.");

    /// <summary>
    /// Tries to get a constant string attribute name from calls like
    /// activity.SetTag("gen_ai.request.model", ...)
    /// or builder.AddTag("gen_ai.usage.input_tokens", ...).
    /// </summary>
    public static bool TryGetAttributeNameLiteral(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string attributeName)
    {
        attributeName = string.Empty;

        if (invocation.ArgumentList.Arguments.Count == 0)
            return false;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;

        var constantValue = semanticModel.GetConstantValue(firstArg, cancellationToken);
        if (!constantValue.HasValue || constantValue.Value is not string s)
            return false;

        if (!GenAiPrefixes.Any(p => s.StartsWith(p, StringComparison.Ordinal)))
            return false;

        attributeName = s;
        return true;
    }
}
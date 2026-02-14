using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Shared helper methods for call site analyzers.
/// </summary>
internal static class AnalyzerHelpers
{
    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be an invocation?
    ///     Shared by all call site analyzers. Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldBeInvocation(SyntaxNode node, CancellationToken _)
    {
        return node.IsKind(SyntaxKind.InvocationExpression);
    }

    /// <summary>
    ///     Checks if a file is generated code based on its file path.
    /// </summary>
    public static bool IsGeneratedFile(string filePath)
    {
        return filePath.EndsWithIgnoreCase(".g.cs") ||
               filePath.EndsWithIgnoreCase(".generated.cs");
    }

    /// <summary>
    ///     Tries to get an invocation operation from the syntax context.
    /// </summary>
    public static bool TryGetInvocationOperation(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out IInvocationOperation? invocation)
    {
        if (context.SemanticModel.GetOperation(context.Node, cancellationToken)
            is IInvocationOperation op)
        {
            invocation = op;
            return true;
        }

        invocation = null;
        return false;
    }

    /// <summary>
    ///     Checks if a call is already being intercepted by another source generator.
    /// </summary>
    /// <seealso href="https://github.com/dotnet/roslyn/issues/72093" />
    public static bool IsAlreadyIntercepted(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not InvocationExpressionSyntax invocationSyntax)
            return false;

        var interceptor = context.SemanticModel.GetInterceptorMethod(invocationSyntax, cancellationToken);
        return interceptor is not null;
    }

    /// <summary>
    ///     Formats a syntax node location as a sort key for deterministic output ordering.
    /// </summary>
    public static string FormatSortKey(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        var start = span.StartLinePosition;
        return $"{node.SyntaxTree.FilePath}:{start.Line}:{start.Character}";
    }

    /// <summary>
    ///     Finds an attribute by its full metadata name in a collection of attributes.
    /// </summary>
    public static AttributeData? FindAttributeByName(
        ImmutableArray<AttributeData> attributes,
        string fullMetadataName)
    {
        foreach (var attr in attributes)
            if (attr.AttributeClass?.ToDisplayString() == fullMetadataName)
                return attr;

        return null;
    }

    /// <summary>
    ///     Checks if a method's return type indicates an async method (Task, ValueTask, or their generic variants).
    /// </summary>
    public static bool IsAsyncReturnType(IMethodSymbol method)
    {
        var returnTypeName = method.ReturnType.ToDisplayString();
        return returnTypeName.StartsWithOrdinal("System.Threading.Tasks.Task") ||
               returnTypeName.StartsWithOrdinal("System.Threading.Tasks.ValueTask");
    }
}

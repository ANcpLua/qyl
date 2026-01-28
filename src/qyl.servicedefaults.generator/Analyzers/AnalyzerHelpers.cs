using System.Diagnostics.CodeAnalysis;
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
    ///     Checks if a file is generated code based on its file path.
    /// </summary>
    public static bool IsGeneratedFile(string filePath) =>
        filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);

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
    ///     Formats a syntax node location as an order key for deterministic output.
    /// </summary>
    public static string FormatOrderKey(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        var start = span.StartLinePosition;
        return $"{node.SyntaxTree.FilePath}:{start.Line}:{start.Character}";
    }
}

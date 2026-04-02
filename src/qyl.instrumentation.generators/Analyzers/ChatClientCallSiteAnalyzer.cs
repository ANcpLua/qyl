using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     Analyzes syntax to find <c>IChatClient.GetResponseAsync</c> and
///     <c>IChatClient.GetStreamingResponseAsync</c> calls on any type that implements
///     <c>Microsoft.Extensions.AI.IChatClient</c>.
/// </summary>
/// <remarks>
///     This is the interface-level interceptor: it catches calls on any concrete class that
///     implements <c>IChatClient</c>, not just the specific SDK types listed in
///     <see cref="GenAiCallSiteAnalyzer" />. This makes the instrumentation SDK-agnostic —
///     any present or future <c>IChatClient</c> implementation is automatically covered.
///     <para>
///         The two analyzers are complementary, not conflicting. <see cref="IsAlreadyIntercepted" />
///         prevents double-wrapping: if <see cref="GenAiCallSiteAnalyzer" /> has already claimed a
///         call site (e.g., for a known OpenAI SDK type), this analyzer skips it.
///     </para>
/// </remarks>
internal static class ChatClientCallSiteAnalyzer
{
    private const string IChatClientMetadataName = "Microsoft.Extensions.AI.IChatClient";

    private static readonly HashSet<string> CandidateMethodNames = new(StringComparer.Ordinal)
    {
        "GetResponseAsync",
        "GetStreamingResponseAsync"
    };

    /// <summary>
    ///     Fast syntactic pre-filter: could this node be a GetResponseAsync / GetStreamingResponseAsync call?
    ///     Runs on every syntax node — no semantic model access allowed here.
    /// </summary>
    public static bool CouldBeChatClientInvocation(SyntaxNode node, CancellationToken _) =>
        AnalyzerHelpers.GetInvokedMethodName(node) is { } name && CandidateMethodNames.Contains(name);

    /// <summary>
    ///     Semantic phase: confirms the receiver's type implements <c>IChatClient</c>.
    ///     Returns null if not applicable, already intercepted, or no interceptable location.
    /// </summary>
    public static ChatClientCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (AnalyzerHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!AnalyzerHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        // Require the receiver's containing type to implement IChatClient
        if (!ImplementsIChatClient(invocation.TargetMethod.ContainingType, context.SemanticModel.Compilation))
            return null;

        // Skip if another generator (e.g. GenAiCallSiteAnalyzer) already claimed this call site
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        if (context.SemanticModel.GetInterceptableLocation(
                (InvocationExpressionSyntax)context.Node, cancellationToken)
            is not { } location)
            return null;

        var method = invocation.TargetMethod;
        var returnTypeName = method.ReturnType.ToDisplayString();
        var isStreaming = returnTypeName.StartsWithOrdinal("System.Collections.Generic.IAsyncEnumerable<");
        var isAsync = isStreaming || AnalyzerHelpers.IsAsyncReturnType(method);

        return new ChatClientCallSite(
            AnalyzerHelpers.FormatSortKey(context.Node),
            method.ContainingType.ToDisplayString(),
            method.Name,
            isAsync,
            isStreaming,
            returnTypeName,
            method.Parameters.Select(static p => p.Type.ToDisplayString()).ToArray().ToEquatableArray(),
            method.Parameters.Select(static p => p.Name).ToArray().ToEquatableArray(),
            location);
    }

    /// <summary>
    ///     Returns true if <paramref name="type" /> implements <c>Microsoft.Extensions.AI.IChatClient</c>,
    ///     either directly or transitively through its interface hierarchy.
    /// </summary>
    private static bool ImplementsIChatClient(INamedTypeSymbol? type, Compilation compilation)
    {
        if (type is null)
            return false;

        var iChatClientSymbol = compilation.GetTypeByMetadataName(IChatClientMetadataName);
        if (iChatClientSymbol is null)
            return false;

        // Direct interface check: the type IS IChatClient
        if (type.IsEqualTo(iChatClientSymbol))
            return true;

        // Transitive check: does the type implement IChatClient in its interface closure?
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.IsEqualTo(iChatClientSymbol))
                return true;
        }

        return false;
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class DbCallSiteAnalyzer
{
    private const string DbCommandTypeName = "System.Data.Common.DbCommand";

    private static readonly (string MethodName, InvocationMatcher Matcher, DbCommandMethod Method, bool IsAsync)[]
        Matchers =
        [
            ("ExecuteReader", Invoke.Method("ExecuteReader"), DbCommandMethod.ExecuteReader, false),
            ("ExecuteReaderAsync", Invoke.Method("ExecuteReaderAsync"), DbCommandMethod.ExecuteReader, true),
            ("ExecuteNonQuery", Invoke.Method("ExecuteNonQuery"), DbCommandMethod.ExecuteNonQuery, false),
            ("ExecuteNonQueryAsync", Invoke.Method("ExecuteNonQueryAsync"), DbCommandMethod.ExecuteNonQuery, true),
            ("ExecuteScalar", Invoke.Method("ExecuteScalar"), DbCommandMethod.ExecuteScalar, false),
            ("ExecuteScalarAsync", Invoke.Method("ExecuteScalarAsync"), DbCommandMethod.ExecuteScalar, true),
            ("ExecuteDbDataReaderAsync", Invoke.Method("ExecuteDbDataReaderAsync"), DbCommandMethod.ExecuteReader, true)
        ];

    private static readonly HashSet<string> s_candidateMethodNames =
    [
        ..Matchers.Select(static matcher => matcher.MethodName)
    ];

    public static bool CouldBeDbInvocation(SyntaxNode node, CancellationToken _) =>
        IncrementalPipelineHelpers.GetInvokedMethodName(node) is { } methodName &&
        s_candidateMethodNames.Contains(methodName);

    public static DbCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (IncrementalPipelineHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!IncrementalPipelineHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!TryMatchDbCommandMethod(invocation, context.SemanticModel.Compilation, out var method, out var isAsync,
                out var concreteType))
            return null;

        if (IncrementalPipelineHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        if (context.SemanticModel.GetInterceptableLocation((InvocationExpressionSyntax)context.Node, cancellationToken)
            is not { } interceptLocation)
            return null;

        return new DbCallSite(
            IncrementalPipelineHelpers.FormatSortKey(context.Node),
            method,
            isAsync,
            concreteType,
            interceptLocation);
    }

    private static bool TryMatchDbCommandMethod(
        IInvocationOperation invocation,
        Compilation compilation,
        out DbCommandMethod method,
        out bool isAsync,
        out string? concreteType)
    {
        method = default;
        isAsync = false;
        concreteType = null;

        (string MethodName, InvocationMatcher Matcher, DbCommandMethod Method, bool IsAsync) matched = default;
        var found = false;

        foreach (var entry in Matchers)
        {
            if (!entry.Matcher.Matches(invocation))
                continue;

            matched = entry;
            found = true;
            break;
        }

        if (!found)
            return false;

        if (invocation.TargetMethod.ContainingType is not { } containingType)
            return false;

        if (compilation.GetTypeByMetadataName(DbCommandTypeName) is not { } dbCommandType)
            return false;

        if (!containingType.IsOrInheritsFrom(dbCommandType))
            return false;

        method = matched.Method;
        isAsync = matched.IsAsync;

        if (!containingType.IsEqualTo(dbCommandType))
            concreteType = containingType.ToDisplayString();

        return true;
    }
}

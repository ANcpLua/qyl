using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class DbCallSiteAnalyzer
{
    private const string DbCommandTypeName = "System.Data.Common.DbCommand";
    private const string NoTraceAttributeName = "Qyl.Instrumentation.QylNoTraceAttribute";
    private const string SampleAttributeName = "Qyl.Instrumentation.QylSampleAttribute";

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

    public static bool CouldBeDbInvocation(SyntaxNode node, CancellationToken _) =>
        IncrementalPipelineHelpers.GetInvokedMethodName(node) is { } methodName &&
        methodName is "ExecuteReader" or
            "ExecuteReaderAsync" or
            "ExecuteNonQuery" or
            "ExecuteNonQueryAsync" or
            "ExecuteScalar" or
            "ExecuteScalarAsync" or
            "ExecuteDbDataReaderAsync";

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

        var (sampling, sampleRatio) = ResolveSampling(invocation, cancellationToken);

        return new DbCallSite(
            IncrementalPipelineHelpers.FormatSortKey(context.Node),
            method,
            isAsync,
            concreteType,
            sampling,
            sampleRatio,
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

    private static (SamplingMode Mode, double Ratio) ResolveSampling(
        IInvocationOperation invocation,
        CancellationToken cancellationToken)
    {
        if (invocation.GetContainingMethod(cancellationToken) is not { } method)
            return (SamplingMode.Always, 1.0);

        // Precedence: method > containing type > assembly. First scope carrying a marker wins.
        foreach (var scope in EnumerateScopes(method))
        {
            if (scope.HasAttribute(NoTraceAttributeName))
                return (SamplingMode.Never, 0.0);

            if (scope.GetAttribute(SampleAttributeName) is { ConstructorArguments: [{ Value: double ratio }, ..] })
                return Classify(ratio);
        }

        return (SamplingMode.Always, 1.0);
    }

    private static IEnumerable<ISymbol> EnumerateScopes(IMethodSymbol method)
    {
        // Walk out through lambda / local-function wrappers so a marker on the enclosing named
        // method also covers DB calls nested in its lambdas / local functions; the ContainingSymbol
        // chain ends at the containing type.
        ISymbol current = method;
        while (current is IMethodSymbol enclosingMethod)
        {
            yield return enclosingMethod;
            current = enclosingMethod.ContainingSymbol;
        }

        if (current is INamedTypeSymbol type)
            yield return type;

        if (method.ContainingAssembly is { } assembly)
            yield return assembly;
    }

    private static (SamplingMode Mode, double Ratio) Classify(double ratio) =>
        // NaN would escape both ratio comparisons below (NaN <= 0 and NaN >= 1 are both false) and
        // emit a bare `NaN` literal -> CS0103 in the consumer build; treat it as the safe default.
        double.IsNaN(ratio)
            ? (SamplingMode.Always, 1.0)
            : ratio <= 0.0
                ? (SamplingMode.Never, 0.0)
                : ratio >= 1.0
                    ? (SamplingMode.Always, 1.0)
                    : (SamplingMode.Ratio, ratio);
}

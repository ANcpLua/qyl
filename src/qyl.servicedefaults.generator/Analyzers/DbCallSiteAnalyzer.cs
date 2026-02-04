using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find ADO.NET DbCommand method invocations to intercept.
/// </summary>
internal static class DbCallSiteAnalyzer
{
    /// <summary>
    ///     The DbCommand base type metadata name.
    /// </summary>
    private const string DbCommandTypeName = "System.Data.Common.DbCommand";

    /// <summary>
    ///     Method patterns to intercept on DbCommand and derived types.
    /// </summary>
    /// <remarks>
    ///     Note: ExecuteReaderAsync has an overload with CommandBehavior parameter,
    ///     but we only intercept the parameterless version to avoid complexity.
    ///     The CommandBehavior overload is typically used internally.
    /// </remarks>
    private static readonly Dictionary<string, (DbCommandMethod Method, bool IsAsync)> MethodPatterns =
        new(StringComparer.Ordinal)
        {
            ["ExecuteReader"] = (DbCommandMethod.ExecuteReader, false),
            ["ExecuteReaderAsync"] = (DbCommandMethod.ExecuteReader, true),
            ["ExecuteNonQuery"] = (DbCommandMethod.ExecuteNonQuery, false),
            ["ExecuteNonQueryAsync"] = (DbCommandMethod.ExecuteNonQuery, true),
            ["ExecuteScalar"] = (DbCommandMethod.ExecuteScalar, false),
            ["ExecuteScalarAsync"] = (DbCommandMethod.ExecuteScalar, true),
            // Protected virtual method exposed by some providers
            ["ExecuteDbDataReaderAsync"] = (DbCommandMethod.ExecuteReader, true)
        };

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a database invocation?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldBeDbInvocation(SyntaxNode node, CancellationToken _)
    {
        return node.IsKind(SyntaxKind.InvocationExpression);
    }

    /// <summary>
    ///     Extracts a database call site from a syntax context if it matches DbCommand patterns.
    ///     Returns null if not a DbCommand call or if already intercepted.
    /// </summary>
    public static DbCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (AnalyzerHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!AnalyzerHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!TryMatchDbCommandMethod(invocation, context.SemanticModel.Compilation, out var method, out var isAsync,
                out var concreteType))
            return null;

        // Skip if already intercepted by another generator
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var interceptLocation = context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node,
            cancellationToken);

        if (interceptLocation is null)
            return null;

        return new DbCallSite(
            AnalyzerHelpers.FormatSortKey(context.Node),
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

        var methodName = invocation.TargetMethod.Name;

        if (!MethodPatterns.TryGetValue(methodName, out var pattern))
            return false;

        var containingType = invocation.TargetMethod.ContainingType;
        if (containingType is null)
            return false;

        var dbCommandType = compilation.GetTypeByMetadataName(DbCommandTypeName);
        if (dbCommandType is null)
            return false;

        if (!AnalyzerHelpers.IsOrDerivesFrom(containingType, dbCommandType))
            return false;

        method = pattern.Method;
        isAsync = pattern.IsAsync;

        if (!SymbolEqualityComparer.Default.Equals(containingType, dbCommandType))
            concreteType = containingType.ToDisplayString();

        return true;
    }
}

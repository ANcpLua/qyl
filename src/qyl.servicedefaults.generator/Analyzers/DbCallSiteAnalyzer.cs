using ANcpLua.Roslyn.Utilities;
using ANcpLua.Roslyn.Utilities.Matching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find ADO.NET DbCommand method invocations to intercept.
/// </summary>
/// <remarks>
///     Uses the <see cref="Invoke" /> DSL from ANcpLua.Roslyn.Utilities for declarative matching.
///     The type inheritance check (is-or-derives-from DbCommand) uses symbol equality via the compilation
///     because the DSL's <see cref="InvocationMatcher.OnTypeInheritingFrom" /> uses name-based matching,
///     and we need precise symbol identity for the concrete type extraction.
/// </remarks>
internal static class DbCallSiteAnalyzer
{
    /// <summary>
    ///     The DbCommand base type metadata name.
    /// </summary>
    private const string DbCommandTypeName = "System.Data.Common.DbCommand";

    /// <summary>
    ///     Declarative DbCommand method matchers.
    ///     Each entry pairs an <see cref="InvocationMatcher" /> (method name only) with the operation metadata.
    /// </summary>
    /// <remarks>
    ///     Note: ExecuteReaderAsync has an overload with CommandBehavior parameter,
    ///     but we only intercept the parameterless version to avoid complexity.
    ///     The CommandBehavior overload is typically used internally.
    ///     <para>
    ///         Type checking against DbCommand is deferred to <see cref="TryMatchDbCommandMethod" />
    ///         because it requires the <see cref="Compilation" /> to resolve the DbCommand symbol.
    ///     </para>
    /// </remarks>
    private static readonly (InvocationMatcher Matcher, DbCommandMethod Method, bool IsAsync)[] Matchers =
    [
        (Invoke.Method("ExecuteReader"), DbCommandMethod.ExecuteReader, false),
        (Invoke.Method("ExecuteReaderAsync"), DbCommandMethod.ExecuteReader, true),
        (Invoke.Method("ExecuteNonQuery"), DbCommandMethod.ExecuteNonQuery, false),
        (Invoke.Method("ExecuteNonQueryAsync"), DbCommandMethod.ExecuteNonQuery, true),
        (Invoke.Method("ExecuteScalar"), DbCommandMethod.ExecuteScalar, false),
        (Invoke.Method("ExecuteScalarAsync"), DbCommandMethod.ExecuteScalar, true),
        // Protected virtual method exposed by some providers
        (Invoke.Method("ExecuteDbDataReaderAsync"), DbCommandMethod.ExecuteReader, true)
    ];

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a database invocation?
    ///     Delegates to <see cref="AnalyzerHelpers.CouldBeInvocation" />.
    /// </summary>
    public static bool CouldBeDbInvocation(SyntaxNode node, CancellationToken ct)
    {
        return AnalyzerHelpers.CouldBeInvocation(node, ct);
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

        // Phase 1: DSL-based method name matching
        (InvocationMatcher Matcher, DbCommandMethod Method, bool IsAsync) matched = default;
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

        // Phase 2: Symbol-based type inheritance check (requires compilation)
        var containingType = invocation.TargetMethod.ContainingType;
        if (containingType is null)
            return false;

        var dbCommandType = compilation.GetTypeByMetadataName(DbCommandTypeName);
        if (dbCommandType is null)
            return false;

        if (!containingType.IsOrInheritsFrom(dbCommandType))
            return false;

        method = matched.Method;
        isAsync = matched.IsAsync;

        if (!SymbolEqualityComparer.Default.Equals(containingType, dbCommandType))
            concreteType = containingType.ToDisplayString();

        return true;
    }
}

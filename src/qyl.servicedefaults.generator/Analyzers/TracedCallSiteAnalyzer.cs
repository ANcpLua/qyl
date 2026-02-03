using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find invocations of methods decorated with [Traced] attribute.
/// </summary>
internal static class TracedCallSiteAnalyzer
{
    private const string TracedAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TracedAttribute";
    private const string TracedTagAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TracedTagAttribute";
    private const string NoTraceAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.NoTraceAttribute";

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a traced method invocation?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldBeTracedInvocation(SyntaxNode node, CancellationToken _) =>
        node.IsKind(SyntaxKind.InvocationExpression);

    /// <summary>
    ///     Extracts a traced call site from a syntax context if the target has [Traced] attribute.
    ///     Returns null if not a traced method or if already intercepted.
    /// </summary>
    public static TracedCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (AnalyzerHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!AnalyzerHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!TryGetTracedAttribute(invocation.TargetMethod, context.SemanticModel.Compilation, out var tracedInfo))
            return null;

        // Skip if already intercepted by another generator
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var interceptLocation = context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node,
            cancellationToken);

        if (interceptLocation is null)
            return null;

        var method = invocation.TargetMethod;
        var tracedTags = ExtractTracedTags(method, context.SemanticModel.Compilation);
        var parameterTypes = method.Parameters.Select(static p => p.Type.ToDisplayString()).ToList();
        var parameterNames = method.Parameters.Select(static p => p.Name).ToList();
        var typeParameters = ExtractTypeParameters(method);

        var isStatic = method.IsStatic;
        var isAsync = AnalyzerHelpers.IsAsyncReturnType(method);

        return new TracedCallSite(
            AnalyzerHelpers.FormatSortKey(context.Node),
            tracedInfo.Value.ActivitySourceName,
            tracedInfo.Value.SpanName ?? method.Name,
            tracedInfo.Value.SpanKind,
            method.ContainingType.ToDisplayString(),
            method.Name,
            isStatic,
            isAsync,
            method.ReturnType.ToDisplayString(),
            parameterTypes,
            parameterNames,
            tracedTags,
            typeParameters,
            interceptLocation);
    }

    private static bool TryGetTracedAttribute(
        ISymbol method,
        Compilation compilation,
        [NotNullWhen(true)] out (string ActivitySourceName, string? SpanName, string SpanKind)? tracedInfo)
    {
        tracedInfo = null;

        var tracedAttributeType = compilation.GetTypeByMetadataName(TracedAttributeFullName);
        if (tracedAttributeType is null)
            return false;

        // Check if method has [NoTrace] - opt-out from class-level tracing
        var noTraceAttributeType = compilation.GetTypeByMetadataName(NoTraceAttributeFullName);
        if (noTraceAttributeType is not null &&
            method.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, noTraceAttributeType)))
            return false;

        // 1. Check method-level [Traced] first (takes priority)
        var methodAttr = GetTracedAttributeData(method.GetAttributes(), tracedAttributeType);
        if (methodAttr is not null)
        {
            tracedInfo = ExtractTracedInfo(methodAttr, method.Name);
            return tracedInfo is not null;
        }

        // 2. Check class-level [Traced] - walk inheritance chain
        var classAttr = GetTracedAttributeFromTypeHierarchy(method.ContainingType, tracedAttributeType);
        if (classAttr is not null)
        {
            // Only trace public methods (not private/internal helpers)
            if (method.DeclaredAccessibility != Accessibility.Public)
                return false;

            tracedInfo = ExtractTracedInfo(classAttr, method.Name);
            return tracedInfo is not null;
        }

        return false;
    }

    private static AttributeData? GetTracedAttributeFromTypeHierarchy(
        INamedTypeSymbol? type,
        ISymbol tracedAttributeType)
    {
        while (type is not null)
        {
            var attr = GetTracedAttributeData(type.GetAttributes(), tracedAttributeType);
            if (attr is not null)
                return attr;
            type = type.BaseType;
        }
        return null;
    }

    private static AttributeData? GetTracedAttributeData(
        ImmutableArray<AttributeData> attributes,
        ISymbol tracedAttributeType)
    {
        foreach (var attribute in attributes)
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, tracedAttributeType))
                return attribute;
        }
        return null;
    }

    private static (string ActivitySourceName, string? SpanName, string SpanKind)? ExtractTracedInfo(
        AttributeData attribute,
        string defaultSpanName)
    {
        // Get ActivitySourceName from constructor argument
        if (attribute.ConstructorArguments is not [{ Value: string { Length: > 0 } activitySourceName }, ..])
            return null;

        string? spanName = null;
        var spanKind = "Internal"; // Default

        // Get named arguments
        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "SpanName":
                    spanName = namedArg.Value.Value as string;
                    break;
                case "Kind":
                    // ActivityKind enum value
                    if (namedArg.Value.Value is int kindValue)
                    {
                        spanKind = kindValue switch
                        {
                            0 => "Internal",
                            1 => "Server",
                            2 => "Client",
                            3 => "Producer",
                            4 => "Consumer",
                            _ => "Internal"
                        };
                    }
                    break;
            }
        }

        return (activitySourceName, spanName, spanKind);
    }

    private static List<TracedTagParameter> ExtractTracedTags(
        IMethodSymbol method,
        Compilation compilation)
    {
        var tracedTagAttributeType = compilation.GetTypeByMetadataName(TracedTagAttributeFullName);
        if (tracedTagAttributeType is null)
            return [];

        var tags = new List<TracedTagParameter>();

        foreach (var parameter in method.Parameters)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, tracedTagAttributeType))
                    continue;

                // Get tag name from constructor argument, fallback to parameter name
                string? tagName = null;
                if (attribute.ConstructorArguments.Length > 0)
                    tagName = attribute.ConstructorArguments[0].Value as string;

                // Use parameter name if no explicit name provided
                tagName ??= parameter.Name;

                if (string.IsNullOrEmpty(tagName))
                    continue;

                var skipIfNull = true; // Default
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg is { Key: "SkipIfNull", Value.Value: bool skipValue })
                        skipIfNull = skipValue;
                }

                var isNullable = parameter.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                                 parameter.Type.IsReferenceType;

                tags.Add(new TracedTagParameter(
                    parameter.Name,
                    tagName,
                    skipIfNull,
                    isNullable));
            }
        }

        return tags;
    }


    private static List<TypeParameterConstraint> ExtractTypeParameters(IMethodSymbol method)
    {
        if (method.TypeParameters.IsEmpty)
            return [];

        var result = new List<TypeParameterConstraint>();

        foreach (var tp in method.TypeParameters)
        {
            var constraints = BuildConstraintClause(tp);
            result.Add(new TypeParameterConstraint(tp.Name, constraints));
        }

        return result;
    }

    private static string? BuildConstraintClause(ITypeParameterSymbol tp)
    {
        var parts = new List<string>();

        if (tp.HasReferenceTypeConstraint)
            parts.Add("class");
        if (tp.HasValueTypeConstraint)
            parts.Add("struct");
        if (tp.HasUnmanagedTypeConstraint)
            parts.Add("unmanaged");
        if (tp.HasNotNullConstraint)
            parts.Add("notnull");

        foreach (var constraintType in tp.ConstraintTypes)
            parts.Add(constraintType.ToDisplayString());

        if (tp.HasConstructorConstraint)
            parts.Add("new()");

        return parts.Count > 0 ? $"where {tp.Name} : {string.Join(", ", parts)}" : null;
    }
}

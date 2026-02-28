using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find invocations of methods decorated with [Traced] attribute.
/// </summary>
internal static class TracedCallSiteAnalyzer
{
    private const string TracedAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TracedAttribute";
    private const string TracedTagAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TracedTagAttribute";
    private const string TracedReturnAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.TracedReturnAttribute";
    private const string NoTraceAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.NoTraceAttribute";

    private const string AsyncEnumerablePrefix =
        "System.Collections.Generic.IAsyncEnumerable<";

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a traced method invocation?
    /// </summary>
    public static bool CouldBeTracedInvocation(SyntaxNode node, CancellationToken ct) =>
        AnalyzerHelpers.CouldBeInvocation(node, ct);

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

        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        if (context.SemanticModel.GetInterceptableLocation((InvocationExpressionSyntax)context.Node, cancellationToken)
            is not { } interceptLocation)
            return null;

        var method = invocation.TargetMethod;
        var compilation = context.SemanticModel.Compilation;

        // Resolve attribute types once
        var tracedTagAttributeType = compilation.GetTypeByMetadataName(TracedTagAttributeFullName);
        var tracedReturnAttributeType = compilation.GetTypeByMetadataName(TracedReturnAttributeFullName);

        var tracedTags = ExtractTracedTags(method, tracedTagAttributeType);
        var tracedTagProperties = ExtractTracedTagProperties(method.ContainingType, tracedTagAttributeType, method.IsStatic);
        var returnCapture = ExtractReturnCapture(method, tracedReturnAttributeType);
        var typeParameters = ExtractTypeParameters(method);
        var parameterTypes =
            method.Parameters.Select(static p => p.Type.ToDisplayString()).ToArray().ToEquatableArray();
        var parameterNames = method.Parameters.Select(static p => p.Name).ToArray().ToEquatableArray();

        var returnTypeName = method.ReturnType.ToDisplayString();
        var isAsyncEnumerable = returnTypeName.StartsWithOrdinal(AsyncEnumerablePrefix);
        var isAsync = !isAsyncEnumerable && AnalyzerHelpers.IsAsyncReturnType(method);

        return new TracedCallSite(
            AnalyzerHelpers.FormatSortKey(context.Node),
            tracedInfo.Value.ActivitySourceName,
            tracedInfo.Value.SpanName,
            tracedInfo.Value.SpanKind,
            tracedInfo.Value.RootSpan,
            method.ContainingType.ToDisplayString(),
            method.Name,
            method.IsStatic,
            isAsync,
            isAsyncEnumerable,
            returnTypeName,
            parameterTypes,
            parameterNames,
            tracedTags,
            tracedTagProperties,
            typeParameters,
            returnCapture,
            interceptLocation);
    }

    // =========================================================================
    // [Traced] attribute extraction
    // =========================================================================

    private static bool TryGetTracedAttribute(
        ISymbol method,
        Compilation compilation,
        [NotNullWhen(true)] out (string ActivitySourceName, string SpanName, string SpanKind, bool RootSpan)? tracedInfo)
    {
        tracedInfo = null;

        if (compilation.GetTypeByMetadataName(TracedAttributeFullName) is not { } tracedAttributeType)
            return false;

        // Check if method has [NoTrace] — opt-out from class-level tracing
        var noTraceAttributeType = compilation.GetTypeByMetadataName(NoTraceAttributeFullName);
        if (noTraceAttributeType is not null && method.HasAttribute(noTraceAttributeType))
            return false;

        // 1. Method-level [Traced] takes priority
        var methodAttr = GetTracedAttributeData(method.GetAttributes(), tracedAttributeType);
        if (methodAttr is not null)
        {
            tracedInfo = ExtractTracedInfo(methodAttr, method.Name);
            return tracedInfo is not null;
        }

        // 2. Class-level [Traced] — walk inheritance chain
        var classAttr = GetTracedAttributeFromTypeHierarchy(method.ContainingType, tracedAttributeType);
        if (classAttr is not null)
        {
            // Only public methods inherit class-level tracing
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
        ISymbol tracedAttributeType) =>
        Enumerable.FirstOrDefault(attributes,
            attribute => attribute.AttributeClass.IsEqualTo(tracedAttributeType));

    private static (string ActivitySourceName, string SpanName, string SpanKind, bool RootSpan)? ExtractTracedInfo(
        AttributeData attribute,
        string defaultSpanName)
    {
        if (attribute.ConstructorArguments.Length < 1 || attribute.ConstructorArguments[0].Value is not string { Length: > 0 } activitySourceName)
            return null;

        string? spanName = null;
        var spanKind = "Internal";
        var rootSpan = false;

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "SpanName":
                    spanName = namedArg.Value.Value as string;
                    break;
                case "Kind":
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
                case "RootSpan":
                    rootSpan = namedArg.Value.Value is true;
                    break;
            }
        }

        return (activitySourceName, spanName ?? defaultSpanName, spanKind, rootSpan);
    }

    // =========================================================================
    // [TracedTag] extraction — parameters (T-006: SkipIfDefault)
    // =========================================================================

    private static EquatableArray<TracedTagParameter> ExtractTracedTags(
        IMethodSymbol method,
        INamedTypeSymbol? tracedTagAttributeType)
    {
        if (tracedTagAttributeType is null)
            return default;

        var tags = new List<TracedTagParameter>();

        foreach (var parameter in method.Parameters)
        foreach (var attribute in parameter.GetAttributes())
        {
            if (!attribute.AttributeClass.IsEqualTo(tracedTagAttributeType))
                continue;

            string? tagName = null;
            if (attribute.ConstructorArguments.Length > 0)
                tagName = attribute.ConstructorArguments[0].Value as string;
            tagName ??= parameter.Name;

            if (string.IsNullOrEmpty(tagName))
                continue;

            var skipIfNull = true;
            var skipIfDefault = false;
            foreach (var namedArg in attribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "SkipIfNull" when namedArg.Value.Value is bool sv:
                        skipIfNull = sv;
                        break;
                    case "SkipIfDefault" when namedArg.Value.Value is bool dv:
                        skipIfDefault = dv;
                        break;
                }
            }

            var isValueType = parameter.Type is { IsValueType: true, OriginalDefinition.SpecialType: not SpecialType.System_Nullable_T };
            var isNullable = !isValueType ||
                             parameter.Type.NullableAnnotation == NullableAnnotation.Annotated;

            tags.Add(new TracedTagParameter(
                parameter.Name,
                parameter.Type.ToDisplayString(),
                tagName,
                skipIfNull,
                skipIfDefault,
                isNullable,
                isValueType));
        }

        return tags.ToArray().ToEquatableArray();
    }

    // =========================================================================
    // T-004: [TracedTag] on properties of the containing type
    // =========================================================================

    private static EquatableArray<TracedTagProperty> ExtractTracedTagProperties(
        INamedTypeSymbol containingType,
        INamedTypeSymbol? tracedTagAttributeType,
        bool methodIsStatic)
    {
        if (tracedTagAttributeType is null)
            return default;

        var properties = new List<TracedTagProperty>();

        // Only scan instance properties for instance methods (static methods have no @this)
        foreach (var member in containingType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                continue;

            // Instance properties are inaccessible from static interceptors
            if (methodIsStatic && !member.IsStatic)
                continue;

            foreach (var attribute in member.GetAttributes())
            {
                if (!attribute.AttributeClass.IsEqualTo(tracedTagAttributeType))
                    continue;

                string? tagName = null;
                if (attribute.ConstructorArguments.Length > 0)
                    tagName = attribute.ConstructorArguments[0].Value as string;
                tagName ??= member.Name;

                if (string.IsNullOrEmpty(tagName))
                    continue;

                var skipIfNull = true;
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg is { Key: "SkipIfNull", Value.Value: bool sv })
                        skipIfNull = sv;
                }

                var isNullable = member.Type.IsReferenceType ||
                                 member.NullableAnnotation == NullableAnnotation.Annotated;

                properties.Add(new TracedTagProperty(
                    member.Name,
                    tagName,
                    skipIfNull,
                    isNullable,
                    member.IsStatic));
            }
        }

        return properties.ToArray().ToEquatableArray();
    }

    // =========================================================================
    // T-007: [return: TracedReturn(...)] extraction
    // =========================================================================

    private static TracedReturnInfo? ExtractReturnCapture(
        IMethodSymbol method,
        INamedTypeSymbol? tracedReturnAttributeType)
    {
        if (tracedReturnAttributeType is null)
            return null;

        // Skip void and Task (no meaningful return value to capture)
        var returnType = method.ReturnType.ToDisplayString();
        if (returnType is "void" ||
            returnType is "System.Threading.Tasks.Task" ||
            returnType.StartsWithOrdinal(AsyncEnumerablePrefix))
            return null;

        var returnAttrs = method.GetReturnTypeAttributes();
        var attr = Enumerable.FirstOrDefault(returnAttrs,
            a => a.AttributeClass.IsEqualTo(tracedReturnAttributeType));

        if (attr is null)
            return null;

        if (attr.ConstructorArguments.Length < 1 || attr.ConstructorArguments[0].Value is not string { Length: > 0 } tagName)
            return null;

        string? propertyPath = null;
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg is { Key: "Property", Value.Value: string path })
                propertyPath = path;
        }

        return new TracedReturnInfo(tagName, propertyPath);
    }

    // =========================================================================
    // Generic type parameter extraction
    // =========================================================================

    private static EquatableArray<TypeParameterConstraint> ExtractTypeParameters(IMethodSymbol method) =>
        method.TypeParameters.IsEmpty
            ? default
            : method.TypeParameters
                .Select(static tp => new TypeParameterConstraint(tp.Name, BuildConstraintClause(tp)))
                .ToArray()
                .ToEquatableArray();

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

        parts.AddRange(tp.ConstraintTypes.Select(static t => t.ToDisplayString()));

        if (tp.HasConstructorConstraint)
            parts.Add("new()");

        return parts.Count > 0 ? $"where {tp.Name} : {string.Join(", ", parts)}" : null;
    }
}

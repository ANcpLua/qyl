using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.CallSites;

internal static class TracedCallSiteAnalyzer
{
    private const string TracedAttributeFullName = "Qyl.Instrumentation.Instrumentation.TracedAttribute";
    private const string TracedTagAttributeFullName = "Qyl.Instrumentation.Instrumentation.TracedTagAttribute";
    private const string TracedReturnAttributeFullName = "Qyl.Instrumentation.Instrumentation.TracedReturnAttribute";
    private const string NoTraceAttributeFullName = "Qyl.Instrumentation.Instrumentation.NoTraceAttribute";

    private const string AsyncEnumerablePrefix =
        "System.Collections.Generic.IAsyncEnumerable<";

    public static bool CouldBeTracedInvocation(SyntaxNode node, CancellationToken ct) =>
        IncrementalPipelineHelpers.CouldBeInvocation(node, ct);

    public static TracedCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (IncrementalPipelineHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!IncrementalPipelineHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!TryGetTracedAttribute(invocation.TargetMethod, context.SemanticModel.Compilation, out var tracedInfo))
            return null;

        if (IncrementalPipelineHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        if (context.SemanticModel.GetInterceptableLocation((InvocationExpressionSyntax)context.Node, cancellationToken)
            is not { } interceptLocation)
            return null;

        var method = invocation.TargetMethod;
        var compilation = context.SemanticModel.Compilation;

        var tracedTagAttributeType = compilation.GetTypeByMetadataName(TracedTagAttributeFullName);
        var tracedReturnAttributeType = compilation.GetTypeByMetadataName(TracedReturnAttributeFullName);

        var tracedTags = ExtractTracedTags(method, tracedTagAttributeType, compilation);
        var tracedTagProperties =
            ExtractTracedTagProperties(method.ContainingType, tracedTagAttributeType, method.IsStatic, compilation);
        var returnCapture = ExtractReturnCapture(method, tracedReturnAttributeType);
        var typeParameters = ExtractTypeParameters(method);
        var parameterTypes = ExtractParameterTypes(method);
        var parameterNames = ExtractParameterNames(method);

        var returnTypeName = method.ReturnType.ToDisplayString();
        var isAsyncEnumerable = returnTypeName.StartsWithOrdinal(AsyncEnumerablePrefix);
        var isAsync = !isAsyncEnumerable && IncrementalPipelineHelpers.IsAsyncReturnType(method);

        string? codeFilePath = null;
        var codeLineNumber = 0;
        if (method.DeclaringSyntaxReferences.FirstOrDefault() is { } syntaxRef)
        {
            codeFilePath = syntaxRef.SyntaxTree.FilePath;
            codeLineNumber =
                syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span, cancellationToken).StartLinePosition.Line + 1;
        }

        var codeNamespace = method.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : null;

        return new TracedCallSite(
            IncrementalPipelineHelpers.FormatSortKey(context.Node),
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
            codeFilePath,
            codeNamespace,
            codeLineNumber,
            interceptLocation);
    }

    private static EquatableArray<string> ExtractParameterTypes(IMethodSymbol method)
    {
        if (method.Parameters.Length is 0)
            return default;

        var parameterTypes = new List<string>(method.Parameters.Length);
        foreach (var parameter in method.Parameters)
            parameterTypes.Add(parameter.Type.ToDisplayString());

        return parameterTypes.ToEquatableArray();
    }

    private static EquatableArray<string> ExtractParameterNames(IMethodSymbol method)
    {
        if (method.Parameters.Length is 0)
            return default;

        var parameterNames = new List<string>(method.Parameters.Length);
        foreach (var parameter in method.Parameters)
            parameterNames.Add(parameter.Name);

        return parameterNames.ToEquatableArray();
    }


    private static bool TryGetTracedAttribute(
        ISymbol method,
        Compilation compilation,
        [NotNullWhen(true)]
        out (string ActivitySourceName, string SpanName, string SpanKind, bool RootSpan)? tracedInfo)
    {
        tracedInfo = null;

        if (compilation.GetTypeByMetadataName(TracedAttributeFullName) is not { } tracedAttributeType)
            return false;

        var noTraceAttributeType = compilation.GetTypeByMetadataName(NoTraceAttributeFullName);
        if (noTraceAttributeType is not null && method.HasAttribute(noTraceAttributeType))
            return false;

        var methodAttr = GetTracedAttributeData(method.GetAttributes(), tracedAttributeType);
        if (methodAttr is not null)
        {
            tracedInfo = ExtractTracedInfo(methodAttr, method.Name);
            return tracedInfo is not null;
        }

        var classAttr = GetTracedAttributeFromTypeHierarchy(method.ContainingType, tracedAttributeType);
        if (classAttr is not null)
        {
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
        if (attribute.GetConstructorArgument<string>(0) is not { Length: > 0 } activitySourceName)
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


    private static EquatableArray<TracedTagParameter> ExtractTracedTags(
        IMethodSymbol method,
        INamedTypeSymbol? tracedTagAttributeType,
        Compilation compilation)
    {
        if (tracedTagAttributeType is null)
            return default;

        var tags = new List<TracedTagParameter>();

        foreach (var parameter in method.Parameters)
        foreach (var attribute in parameter.GetAttributes())
        {
            if (!attribute.AttributeClass.IsEqualTo(tracedTagAttributeType))
                continue;

            attribute.TryGetConstructorArgument<string>(0, out var explicitTagName);

            bool? explicitSkipIfNull = null;
            var skipIfDefault = false;
            foreach (var namedArg in attribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "SkipIfNull" when namedArg.Value.Value is bool sv:
                        explicitSkipIfNull = sv;
                        break;
                    case "SkipIfDefault" when namedArg.Value.Value is bool dv:
                        skipIfDefault = dv;
                        break;
                }
            }

            var tagName = TelemetryTagNameResolver.ResolveName(parameter, compilation, explicitTagName, parameter.Name);
            var skipIfNull = TelemetryTagNameResolver.ResolveSkipIfNull(parameter, compilation, explicitSkipIfNull);

            var isValueType = parameter.Type is
                { IsValueType: true, OriginalDefinition.SpecialType: not SpecialType.System_Nullable_T };
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

        return tags.ToEquatableArray();
    }


    private static EquatableArray<TracedTagProperty> ExtractTracedTagProperties(
        INamedTypeSymbol containingType,
        INamedTypeSymbol? tracedTagAttributeType,
        bool methodIsStatic,
        Compilation compilation)
    {
        if (tracedTagAttributeType is null)
            return default;

        var properties = new List<TracedTagProperty>();

        foreach (var member in containingType.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                continue;

            if (methodIsStatic && !member.IsStatic)
                continue;

            foreach (var attribute in member.GetAttributes())
            {
                if (!attribute.AttributeClass.IsEqualTo(tracedTagAttributeType))
                    continue;

                attribute.TryGetConstructorArgument<string>(0, out var explicitTagName);

                bool? explicitSkipIfNull = null;
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg is { Key: "SkipIfNull", Value.Value: bool sv })
                        explicitSkipIfNull = sv;
                }

                var tagName = TelemetryTagNameResolver.ResolveName(member, compilation, explicitTagName, member.Name);
                var skipIfNull = TelemetryTagNameResolver.ResolveSkipIfNull(member, compilation, explicitSkipIfNull);

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

        return properties.ToEquatableArray();
    }


    private static TracedReturnInfo? ExtractReturnCapture(
        IMethodSymbol method,
        INamedTypeSymbol? tracedReturnAttributeType)
    {
        if (tracedReturnAttributeType is null)
            return null;

        var returnType = method.ReturnType.ToDisplayString();
        if (returnType is "void" ||
            returnType is "System.Threading.Tasks.Task" ||
            returnType.StartsWithOrdinal(AsyncEnumerablePrefix))
            return null;

        var returnAttrs = method.GetReturnTypeAttributes();
        if (Enumerable.FirstOrDefault(returnAttrs,
                a => a.AttributeClass.IsEqualTo(tracedReturnAttributeType)) is not { } attr)
            return null;

        if (attr.GetConstructorArgument<string>(0) is not { Length: > 0 } tagName)
            return null;

        string? propertyPath = null;
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg is { Key: "Property", Value.Value: string path })
                propertyPath = path;
        }

        return new TracedReturnInfo(tagName, propertyPath);
    }


    private static EquatableArray<TypeParameterConstraint> ExtractTypeParameters(IMethodSymbol method)
    {
        if (method.TypeParameters.IsEmpty)
            return default;

        var typeParameters = new List<TypeParameterConstraint>(method.TypeParameters.Length);
        foreach (var typeParameter in method.TypeParameters)
            typeParameters.Add(new TypeParameterConstraint(typeParameter.Name, BuildConstraintClause(typeParameter)));

        return typeParameters.ToEquatableArray();
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

        parts.AddRange(tp.ConstraintTypes.Select(static t => t.ToDisplayString()));

        if (tp.HasConstructorConstraint)
            parts.Add("new()");

        return parts.Count > 0 ? $"where {tp.Name} : {string.Join(", ", parts)}" : null;
    }
}

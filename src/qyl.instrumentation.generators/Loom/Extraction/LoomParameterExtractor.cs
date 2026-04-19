using Microsoft.CodeAnalysis;
using Qyl.Instrumentation.Generators.Loom.Models;

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

internal static class LoomParameterExtractor
{
    private const string DescriptionAttributeFullName = "System.ComponentModel.DescriptionAttribute";
    private const string CancellationTokenTypeName = "global::System.Threading.CancellationToken";
    private const string ServiceProviderTypeName = "global::System.IServiceProvider";
    private const string AIFunctionArgumentsTypeName = "global::Microsoft.Extensions.AI.AIFunctionArguments";

    public static EquatableArray<LoomParameterModel> Extract(
        ImmutableArray<IParameterSymbol> parameters,
        CancellationToken cancellationToken)
    {
        if (parameters.Length is 0) return default;

        var builder = new LoomParameterModel[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var typeFullyQualified = parameter.Type.GetFullyQualifiedName();
            var isCancellationToken = typeFullyQualified == CancellationTokenTypeName;
            var isServiceProvider = typeFullyQualified == ServiceProviderTypeName;
            var isAIFunctionArguments = typeFullyQualified == AIFunctionArgumentsTypeName;
            var isInfrastructureBound = isCancellationToken || isServiceProvider || isAIFunctionArguments;
            builder[i] = new LoomParameterModel(
                parameter.Name,
                typeFullyQualified,
                IsNullable(parameter.Type),
                parameter.HasExplicitDefaultValue,
                parameter.HasExplicitDefaultValue
                    ? LoomLiteralFormatter.GetDefaultValueLiteral(parameter, cancellationToken)
                    : null,
                GetDescription(parameter),
                isCancellationToken,
                !isInfrastructureBound,
                isInfrastructureBound,
                GetEnumValues(parameter.Type));
        }

        return builder.ToEquatableArray();
    }

    private static string? GetDescription(IParameterSymbol parameter)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (!string.Equals(
                    attribute.AttributeClass?.ToDisplayString(),
                    DescriptionAttributeFullName,
                    StringComparison.Ordinal))
                continue;

            return attribute.ConstructorArguments.FirstOrDefault().Value as string;
        }

        return null;
    }

    private static bool IsNullable(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T })
            return true;

        return typeSymbol.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static EquatableArray<string> GetEnumValues(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return default;

        var values = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue)
            .Select(static field => field.Name)
            .ToArray();

        return values.Length is 0 ? default : values.ToEquatableArray();
    }
}

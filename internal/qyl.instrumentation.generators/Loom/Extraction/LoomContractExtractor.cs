using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Instrumentation.Generators.Loom.Models;

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

internal static class LoomContractExtractor
{
    public static LoomContractModel? Extract(GeneratorAttributeSyntaxContext context, CancellationToken _)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type ||
            context.TargetNode is not TypeDeclarationSyntax declaration)
            return null;

        var attribute = context.Attributes[0];
        var name = attribute.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property =>
                !property.IsStatic &&
                property is { IsImplicitlyDeclared: false, DeclaredAccessibility: Accessibility.Public })
            .Select(static property => new LoomContractPropertyModel(
                property.Name,
                property.Type.GetFullyQualifiedName(),
                IsNullable(property.Type),
                !IsNullable(property.Type),
                GetEnumValues(property.Type)))
            .ToArray();

        return new LoomContractModel(
            name!,
            type.GetFullyQualifiedName(),
            LoomDeclarationChainExtractor.Extract(declaration),
            properties.Length is 0 ? default : properties.ToEquatableArray());
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

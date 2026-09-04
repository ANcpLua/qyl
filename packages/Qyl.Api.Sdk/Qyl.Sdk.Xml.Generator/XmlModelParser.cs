using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.Sdk.Xml.Generator;

/// <summary>Turns a <c>[GenerateXml]</c> type into a cacheable <see cref="XmlModelSpec"/>, or into diagnostics.</summary>
internal static class XmlModelParser
{
    private const string XmlRootAttributeName = "System.Xml.Serialization.XmlRootAttribute";
    private const string XmlElementAttributeName = "System.Xml.Serialization.XmlElementAttribute";
    private const string XmlAttributeAttributeName = "System.Xml.Serialization.XmlAttributeAttribute";
    private const string XmlIgnoreAttributeName = "System.Xml.Serialization.XmlIgnoreAttribute";

    public static XmlModelResult Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var model = (INamedTypeSymbol)context.TargetSymbol;
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticSpec>();

        if (!IsSupportedModel(model))
        {
            Report(diagnostics, Diagnostics.InvalidModel, model, model.ToDisplayString());
            return new XmlModelResult(null, new EquatableArray<DiagnosticSpec>(diagnostics.ToImmutable()));
        }

        var hasErrors = false;

        if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            Report(diagnostics, Diagnostics.NotPartial, model, model.ToDisplayString());
            hasErrors = true;
        }

        var rootAttribute = FindSingleAttribute(model, XmlRootAttributeName, out var duplicateRoot);
        if (duplicateRoot)
        {
            Report(diagnostics, Diagnostics.UnsupportedAttributeConfiguration, model, "XmlRoot", model.ToDisplayString());
            hasErrors = true;
        }

        var root = ReadXmlMapping(diagnostics, model, rootAttribute, model.Name, "ElementName", "XmlRoot", ref hasErrors);

        var properties = ImmutableArray.CreateBuilder<XmlPropertySpec>();
        foreach (var property in GetDeclaredPublicProperties(model))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HasAttribute(property, XmlIgnoreAttributeName))
            {
                continue;
            }

            if (property.IsStatic ||
                property.Parameters.Length != 0 ||
                property.GetMethod is null ||
                property.GetMethod.DeclaredAccessibility != Accessibility.Public)
            {
                Report(diagnostics, Diagnostics.InvalidProperty, property, property.Name, model.ToDisplayString());
                hasErrors = true;
                continue;
            }

            var elementAttribute = FindSingleAttribute(property, XmlElementAttributeName, out var duplicateElement);
            var xmlAttribute = FindSingleAttribute(property, XmlAttributeAttributeName, out var duplicateAttribute);

            if (duplicateElement || duplicateAttribute || (elementAttribute is not null && xmlAttribute is not null))
            {
                Report(diagnostics, Diagnostics.ConflictingPropertyMapping, property, property.Name, model.ToDisplayString());
                hasErrors = true;
                continue;
            }

            if (!TryClassify(property.Type, out var valueKind, out var isNullable))
            {
                Report(
                    diagnostics,
                    Diagnostics.UnsupportedPropertyType,
                    property,
                    property.Name,
                    model.ToDisplayString(),
                    property.Type.ToDisplayString());
                hasErrors = true;
                continue;
            }

            var isAttribute = xmlAttribute is not null;
            var mapping = ReadXmlMapping(
                diagnostics,
                property,
                xmlAttribute ?? elementAttribute,
                property.Name,
                isAttribute ? "AttributeName" : "ElementName",
                isAttribute ? "XmlAttribute" : "XmlElement",
                ref hasErrors);

            properties.Add(new XmlPropertySpec(property.Name, mapping.Name, mapping.Namespace, isAttribute, valueKind, isNullable));
        }

        if (hasErrors)
        {
            return new XmlModelResult(null, new EquatableArray<DiagnosticSpec>(diagnostics.ToImmutable()));
        }

        var spec = new XmlModelSpec(
            model.ContainingNamespace.IsGlobalNamespace ? null : model.ContainingNamespace.ToDisplayString(),
            model.IsRecord ? "record" : "class",
            model.Name,
            GetHintName(model),
            root.Name,
            root.Namespace,
            new EquatableArray<XmlPropertySpec>(properties.ToImmutable()));

        return new XmlModelResult(spec, new EquatableArray<DiagnosticSpec>(diagnostics.ToImmutable()));
    }

    private static bool IsSupportedModel(INamedTypeSymbol model)
    {
        return model.TypeKind == TypeKind.Class &&
            model.ContainingType is null &&
            model.Arity == 0 &&
            !model.IsStatic &&
            !model.IsFileLocal;
    }

    private static List<IPropertySymbol> GetDeclaredPublicProperties(INamedTypeSymbol model)
    {
        var properties = new List<IPropertySymbol>();

        foreach (var member in model.GetMembers())
        {
            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsImplicitlyDeclared: false } property)
            {
                properties.Add(property);
            }
        }

        properties.Sort(CompareSourceOrder);
        return properties;
    }

    private static int CompareSourceOrder(IPropertySymbol left, IPropertySymbol right)
    {
        var leftLocation = LocationSpec.From(left);
        var rightLocation = LocationSpec.From(right);

        if (leftLocation is null || rightLocation is null)
        {
            return 0;
        }

        var pathComparison = string.Compare(leftLocation.FilePath, rightLocation.FilePath, StringComparison.Ordinal);
        return pathComparison != 0 ? pathComparison : leftLocation.Span.Start.CompareTo(rightLocation.Span.Start);
    }

    private static bool TryClassify(ITypeSymbol propertyType, out ValueKind valueKind, out bool isNullable)
    {
        var valueType = propertyType;
        isNullable = false;

        if (propertyType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        {
            valueType = named.TypeArguments[0];
            isNullable = true;
        }

        switch (valueType.SpecialType)
        {
            case SpecialType.System_String:
                valueKind = ValueKind.String;
                isNullable = true;
                return true;
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                valueKind = ValueKind.XmlConvertScalar;
                return true;
            case SpecialType.System_DateTime:
                valueKind = ValueKind.DateTime;
                return true;
        }

        if (valueType.TypeKind == TypeKind.Enum)
        {
            valueKind = ValueKind.Enum;
            return true;
        }

        switch (valueType.ToDisplayString())
        {
            case "System.DateTimeOffset":
                valueKind = ValueKind.DateTimeOffset;
                return true;
            case "System.Guid":
                valueKind = ValueKind.Guid;
                return true;
            case "System.TimeSpan":
                valueKind = ValueKind.TimeSpan;
                return true;
            case "System.DateOnly":
                valueKind = ValueKind.DateOnly;
                return true;
            case "System.TimeOnly":
                valueKind = ValueKind.TimeOnly;
                return true;
            default:
                valueKind = default;
                return false;
        }
    }

    private static AttributeData? FindSingleAttribute(ISymbol symbol, string metadataName, out bool duplicate)
    {
        AttributeData? result = null;
        duplicate = false;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != metadataName)
            {
                continue;
            }

            if (result is not null)
            {
                duplicate = true;
            }

            result ??= attribute;
        }

        return result;
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static (string Name, string? Namespace) ReadXmlMapping(
        ImmutableArray<DiagnosticSpec>.Builder diagnostics,
        ISymbol target,
        AttributeData? attribute,
        string fallbackName,
        string nameProperty,
        string attributeShortName,
        ref bool hasErrors)
    {
        var name = fallbackName;
        string? xmlNamespace = null;

        if (attribute is not null)
        {
            if (attribute.ConstructorArguments.Length > 1 ||
                (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is not string))
            {
                Report(diagnostics, Diagnostics.UnsupportedAttributeConfiguration, target, attributeShortName, target.ToDisplayString());
                hasErrors = true;
            }
            else if (attribute.ConstructorArguments.Length == 1 &&
                     attribute.ConstructorArguments[0].Value is string { Length: > 0 } constructorName)
            {
                name = constructorName;
            }

            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key == nameProperty && namedArgument.Value.Value is string namedName)
                {
                    if (namedName.Length != 0)
                    {
                        name = namedName;
                    }
                }
                else if (namedArgument.Key == "Namespace" && namedArgument.Value.Value is string namedNamespace)
                {
                    xmlNamespace = namedNamespace;
                }
                else
                {
                    Report(diagnostics, Diagnostics.UnsupportedAttributeConfiguration, target, attributeShortName, target.ToDisplayString());
                    hasErrors = true;
                }
            }
        }

        if (!IsValidXmlName(name))
        {
            Report(diagnostics, Diagnostics.InvalidXmlName, target, name, target.ToDisplayString());
            hasErrors = true;
        }

        return (name, xmlNamespace);
    }

    private static bool IsValidXmlName(string value)
    {
        try
        {
            return XmlConvert.VerifyNCName(value) == value;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static void Report(
        ImmutableArray<DiagnosticSpec>.Builder diagnostics,
        DiagnosticDescriptor descriptor,
        ISymbol target,
        params string[] arguments)
    {
        diagnostics.Add(new DiagnosticSpec(
            descriptor,
            LocationSpec.From(target),
            new EquatableArray<string>(ImmutableArray.Create(arguments))));
    }

    private static string GetHintName(INamedTypeSymbol model)
    {
        var builder = new StringBuilder();
        foreach (var character in model.ToDisplayString())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Append(".GenerateXml.g.cs").ToString();
    }
}

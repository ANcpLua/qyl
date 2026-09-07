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

/// <summary>
/// Turns a <c>[GenerateXml]</c> type into a cacheable <see cref="XmlModelSpec"/>, or into diagnostics. Every
/// <c>System.Xml.Serialization</c> option is either mapped exactly as <c>XmlSerializer</c> writes it or rejected; nothing is
/// skipped silently.
/// </summary>
internal static class XmlModelParser
{
    public const string GenerateXmlAttributeName = "Qyl.Xml.GenerateXmlAttribute";

    private const string XmlWritableInterfaceName = "Qyl.Xml.IXmlWritable";
    private const string SerializationNamespace = "System.Xml.Serialization";
    private const string XmlRootAttributeName = "System.Xml.Serialization.XmlRootAttribute";
    private const string XmlElementAttributeName = "System.Xml.Serialization.XmlElementAttribute";
    private const string XmlAttributeAttributeName = "System.Xml.Serialization.XmlAttributeAttribute";
    private const string XmlArrayAttributeName = "System.Xml.Serialization.XmlArrayAttribute";
    private const string XmlArrayItemAttributeName = "System.Xml.Serialization.XmlArrayItemAttribute";
    private const string XmlTextAttributeName = "System.Xml.Serialization.XmlTextAttribute";
    private const string XmlEnumAttributeName = "System.Xml.Serialization.XmlEnumAttribute";
    private const string XmlIgnoreAttributeName = "System.Xml.Serialization.XmlIgnoreAttribute";
    private const string DefaultValueAttributeName = "System.ComponentModel.DefaultValueAttribute";
    private const string FlagsAttributeName = "System.FlagsAttribute";

    public static XmlModelResult Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var model = (INamedTypeSymbol)context.TargetSymbol;
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var state = new ParseState(model);

        if (!IsSupportedModel(model))
        {
            state.Report(Diagnostics.InvalidModel, model, state.ModelName);
            return state.ToResult(null);
        }

        if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            state.Report(Diagnostics.NotPartial, model, state.ModelName);
        }

        // XmlRoot is the only type-level serialization attribute that is honoured; XmlType, XmlInclude, ... would change the output.
        AttributeData? rootAttribute = null;
        foreach (var attribute in model.GetAttributes())
        {
            if (IsAttribute(attribute, XmlRootAttributeName))
            {
                if (rootAttribute is not null)
                {
                    state.ReportUnsupportedAttribute(model, attribute);
                }

                rootAttribute ??= attribute;
            }
            else if (IsSerializationAttribute(attribute))
            {
                state.ReportUnsupportedAttribute(model, attribute);
            }
        }

        var root = ToName(ReadNameOptions(state, model, rootAttribute, model.Name, "ElementName", allowIsNullable: false));

        // XmlSerializer writes inherited members first; that order is only known for types declared in this compilation.
        var chain = new List<INamedTypeSymbol> { model };
        var hidesBaseImplementation = false;
        for (var baseType = model.BaseType; baseType is not null && baseType.SpecialType != SpecialType.System_Object; baseType = baseType.BaseType)
        {
            if (baseType.DeclaringSyntaxReferences.Length == 0)
            {
                state.Report(Diagnostics.BaseTypeNotInSource, model, state.ModelName, baseType.ToDisplayString());
                break;
            }

            hidesBaseImplementation |= IsWritableModel(baseType);
            chain.Insert(0, baseType);
        }

        var nodes = ImmutableArray.CreateBuilder<XmlNodeSpec>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var hasText = false;

        foreach (var type in chain)
        {
            foreach (var property in GetDeclaredPublicProperties(type))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // An override is written through its base declaration, which dispatches virtually anyway.
                if (property.IsOverride || !names.Add(property.Name) || HasAttribute(property, XmlIgnoreAttributeName))
                {
                    continue;
                }

                if (ParseProperty(state, type, property, ref hasText) is { } node)
                {
                    nodes.Add(node);
                }
            }
        }

        if (state.HasErrors)
        {
            return state.ToResult(null);
        }

        return state.ToResult(new XmlModelSpec(
            model.ContainingNamespace.IsGlobalNamespace ? null : model.ContainingNamespace.ToDisplayString(),
            model.IsRecord ? "record" : "class",
            model.Name,
            GetHintName(model),
            root,
            hidesBaseImplementation,
            new EquatableArray<XmlNodeSpec>(nodes.ToImmutable()),
            state.Enums));
    }

    private static XmlNodeSpec? ParseProperty(ParseState state, INamedTypeSymbol declaringType, IPropertySymbol property, ref bool hasText)
    {
        if (property.IsStatic || property.Parameters.Length != 0 || property.GetMethod is not { DeclaredAccessibility: Accessibility.Public })
        {
            state.Report(Diagnostics.InvalidProperty, property, property.Name, state.ModelName);
            return null;
        }

        ReportSerializerConventions(state, declaringType, property);

        AttributeData? element = null;
        AttributeData? attribute = null;
        AttributeData? array = null;
        AttributeData? arrayItem = null;
        AttributeData? text = null;
        var duplicate = false;

        foreach (var data in property.GetAttributes())
        {
            switch (data.AttributeClass?.ToDisplayString())
            {
                case XmlElementAttributeName:
                    duplicate |= !TrySet(ref element, data);
                    break;
                case XmlAttributeAttributeName:
                    duplicate |= !TrySet(ref attribute, data);
                    break;
                case XmlArrayAttributeName:
                    duplicate |= !TrySet(ref array, data);
                    break;
                case XmlArrayItemAttributeName:
                    duplicate |= !TrySet(ref arrayItem, data);
                    break;
                case XmlTextAttributeName:
                    duplicate |= !TrySet(ref text, data);
                    break;
                case XmlIgnoreAttributeName:
                    break;
                default:
                    if (IsSerializationAttribute(data))
                    {
                        state.ReportUnsupportedAttribute(property, data);
                    }

                    break;
            }
        }

        var mappings = Count(element) + Count(attribute) + Count(array) + Count(text);
        if (duplicate || mappings > 1 || (arrayItem is not null && (element ?? attribute ?? text) is not null))
        {
            state.Report(Diagnostics.ConflictingPropertyMapping, property, property.Name, state.ModelName);
            return null;
        }

        var type = property.Type;

        if (TryClassifyScalar(state, type, out var scalar))
        {
            if (attribute is not null)
            {
                var name = ReadNameOptions(state, property, attribute, property.Name, "AttributeName", allowIsNullable: false);
                return new XmlAttributeNode(property.Name, ToName(name), scalar);
            }

            if (text is not null)
            {
                ReadNoOptions(state, property, text);
                if (hasText)
                {
                    state.Report(Diagnostics.MappingMismatch, property, "XmlText", property.Name, state.ModelName, "only one property per type can be its text content");
                    return null;
                }

                hasText = true;
                return new XmlTextNode(property.Name, scalar);
            }

            if ((array ?? arrayItem) is { } misplaced)
            {
                state.Report(Diagnostics.MappingMismatch, property, ShortName(misplaced), property.Name, state.ModelName, "the property is a single value, not a collection");
                return null;
            }

            // XmlSerializer writes an absent Nullable<T> as xsi:nil and omits an absent string, unless IsNullable says otherwise.
            var options = ReadNameOptions(state, property, element, property.Name, "ElementName", allowIsNullable: true);
            var writeNil = options.IsNullable ?? (scalar.IsNullable && scalar.Kind != ValueKind.String);
            return new XmlElementNode(property.Name, ToName(options), scalar, writeNil);
        }

        if (IsWritableModel(type))
        {
            if ((attribute ?? text ?? array ?? arrayItem) is { } misplaced)
            {
                state.Report(Diagnostics.MappingMismatch, property, ShortName(misplaced), property.Name, state.ModelName, "a [GenerateXml] model is written as a child element; use [XmlElement]");
                return null;
            }

            var options = ReadNameOptions(state, property, element, property.Name, "ElementName", allowIsNullable: true);
            return new XmlModelNode(property.Name, ToName(options), options.IsNullable ?? false, FullName(type));
        }

        if (TryGetCollectionItem(type, out var itemType))
        {
            if ((attribute ?? text) is { } misplaced)
            {
                state.Report(Diagnostics.MappingMismatch, property, ShortName(misplaced), property.Name, state.ModelName, "a collection is written as repeated elements; use [XmlElement], or [XmlArray] with [XmlArrayItem]");
                return null;
            }

            XmlScalarSpec? itemScalar;
            string? itemTypeName = null;
            if (TryClassifyScalar(state, itemType, out var itemValue))
            {
                itemScalar = itemValue;
            }
            else if (IsWritableModel(itemType))
            {
                itemScalar = null;
                itemTypeName = FullName(itemType);
            }
            else
            {
                state.Report(Diagnostics.UnsupportedPropertyType, property, property.Name, state.ModelName, type.ToDisplayString());
                return null;
            }

            if (element is not null)
            {
                // Repeated elements: XmlSerializer skips absent items unless IsNullable = true.
                var flat = ReadNameOptions(state, property, element, property.Name, "ElementName", allowIsNullable: true);
                return new XmlCollectionNode(property.Name, null, false, ToName(flat), itemScalar, itemTypeName, flat.IsNullable ?? false);
            }

            // Wrapped items: XmlSerializer writes absent items as xsi:nil unless [XmlArrayItem(IsNullable = false)];
            // the wrapper itself is omitted for an absent collection unless [XmlArray(IsNullable = true)].
            var wrapper = ReadNameOptions(state, property, array, property.Name, "ElementName", allowIsNullable: true);
            var item = ReadNameOptions(state, property, arrayItem, GetDefaultItemName(itemType, itemScalar), "ElementName", allowIsNullable: true);
            if (item.Name is null)
            {
                state.Report(Diagnostics.ItemNameRequired, property, property.Name, state.ModelName, itemType.ToDisplayString());
                return null;
            }

            return new XmlCollectionNode(property.Name, ToName(wrapper), wrapper.IsNullable ?? false, ToName(item), itemScalar, itemTypeName, item.IsNullable ?? true);
        }

        state.Report(Diagnostics.UnsupportedPropertyType, property, property.Name, state.ModelName, type.ToDisplayString());
        return null;
    }

    /// <summary>Conventions XmlSerializer honours through reflection; the generator cannot, so each is an error instead of a silent difference.</summary>
    private static void ReportSerializerConventions(ParseState state, INamedTypeSymbol declaringType, IPropertySymbol property)
    {
        foreach (var member in declaringType.GetMembers("ShouldSerialize" + property.Name))
        {
            if (member is IMethodSymbol { Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Boolean })
            {
                state.Report(Diagnostics.UnsupportedSerializerConvention, member, member.Name + "()", state.ModelName);
            }
        }

        foreach (var member in declaringType.GetMembers(property.Name + "Specified"))
        {
            if (member is IPropertySymbol { Type.SpecialType: SpecialType.System_Boolean })
            {
                state.Report(Diagnostics.UnsupportedSerializerConvention, member, member.Name, state.ModelName);
            }
        }

        if (HasAttribute(property, DefaultValueAttributeName))
        {
            state.Report(Diagnostics.UnsupportedSerializerConvention, property, "[DefaultValue] on " + property.Name, state.ModelName);
        }
    }

    private static bool IsSupportedModel(INamedTypeSymbol model)
    {
        return model.TypeKind == TypeKind.Class &&
            model.ContainingType is null &&
            model.Arity == 0 &&
            !model.IsStatic &&
            !model.IsFileLocal;
    }

    private static bool IsWritableModel(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } named)
        {
            return false;
        }

        if (HasAttribute(named, GenerateXmlAttributeName))
        {
            return true;
        }

        foreach (var implemented in named.AllInterfaces)
        {
            if (implemented.ToDisplayString() == XmlWritableInterfaceName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCollectionItem(ITypeSymbol type, out ITypeSymbol itemType)
    {
        if (type is IArrayTypeSymbol { Rank: 1 } array)
        {
            itemType = array.ElementType;
            // XmlSerializer writes byte[] as base64, not as repeated elements.
            return array.ElementType.SpecialType != SpecialType.System_Byte;
        }

        if (type is INamedTypeSymbol { IsReferenceType: true } named)
        {
            if (IsEnumerableOfT(named))
            {
                itemType = named.TypeArguments[0];
                return true;
            }

            foreach (var implemented in named.AllInterfaces)
            {
                if (IsEnumerableOfT(implemented))
                {
                    itemType = implemented.TypeArguments[0];
                    return true;
                }
            }
        }

        itemType = null!;
        return false;
    }

    private static bool IsEnumerableOfT(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;

    private static List<IPropertySymbol> GetDeclaredPublicProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();

        foreach (var member in type.GetMembers())
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
            return string.CompareOrdinal(left.Name, right.Name);
        }

        var pathComparison = string.CompareOrdinal(leftLocation.FilePath, rightLocation.FilePath);
        return pathComparison != 0 ? pathComparison : leftLocation.Span.Start.CompareTo(rightLocation.Span.Start);
    }

    private static bool TryClassifyScalar(ParseState state, ITypeSymbol type, out XmlScalarSpec scalar)
    {
        var valueType = type;
        var isNullable = false;

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            valueType = nullable.TypeArguments[0];
            isNullable = true;
        }

        // The schema type is the Qyl.Xml.XmlScalarType member the shape describes the value with.
        switch (valueType.SpecialType)
        {
            case SpecialType.System_String:
                scalar = new XmlScalarSpec(ValueKind.String, true, null, "String");
                return true;
            case SpecialType.System_Char:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "String");
                return true;
            case SpecialType.System_Boolean:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "Boolean");
                return true;
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "Integer");
                return true;
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "Long");
                return true;
            case SpecialType.System_Single:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "Float");
                return true;
            case SpecialType.System_Double:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "Double");
                return true;
            case SpecialType.System_Decimal:
                scalar = new XmlScalarSpec(ValueKind.XmlConvertScalar, isNullable, null, "Decimal");
                return true;
            case SpecialType.System_DateTime:
                scalar = new XmlScalarSpec(ValueKind.DateTime, isNullable, null, "DateTime");
                return true;
        }

        if (valueType is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            scalar = new XmlScalarSpec(ValueKind.Enum, isNullable, state.RegisterEnum(enumType), "Enum");
            return true;
        }

        (ValueKind Kind, string SchemaType)? known = valueType.ToDisplayString() switch
        {
            "System.DateTimeOffset" => (ValueKind.DateTimeOffset, "DateTime"),
            "System.Guid" => (ValueKind.Guid, "Uuid"),
            "System.TimeSpan" => (ValueKind.TimeSpan, "Duration"),
            "System.DateOnly" => (ValueKind.DateOnly, "Date"),
            "System.TimeOnly" => (ValueKind.TimeOnly, "Time"),
            _ => null,
        };

        if (known is { } value)
        {
            scalar = new XmlScalarSpec(value.Kind, isNullable, null, value.SchemaType);
            return true;
        }

        scalar = null!;
        return false;
    }

    /// <summary>The fully qualified name of a model type, without a nullable annotation, as generated code refers to it.</summary>
    private static string FullName(ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>The item element name XmlSerializer uses without [XmlArrayItem]: the XSD type name for primitives, the type name otherwise.</summary>
    private static string? GetDefaultItemName(ITypeSymbol itemType, XmlScalarSpec? itemScalar)
    {
        if (itemScalar is null)
        {
            return itemType.Name;
        }

        var valueType = itemType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : itemType;

        if (valueType.TypeKind == TypeKind.Enum)
        {
            return valueType.Name;
        }

        return valueType.SpecialType switch
        {
            SpecialType.System_String => "string",
            SpecialType.System_Boolean => "boolean",
            SpecialType.System_Char => "char",
            SpecialType.System_SByte => "byte",
            SpecialType.System_Byte => "unsignedByte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "unsignedShort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "unsignedInt",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "unsignedLong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_DateTime => "dateTime",
            _ => valueType.ToDisplayString() == "System.Guid" ? "guid" : null,
        };
    }

    private static XmlEnumSpec BuildEnum(ParseState state, INamedTypeSymbol enumType, string formatterName)
    {
        var members = ImmutableArray.CreateBuilder<XmlEnumMemberSpec>();
        var values = new HashSet<ulong>();

        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol { IsConst: true, HasConstantValue: true } field || HasAttribute(field, XmlIgnoreAttributeName))
            {
                continue;
            }

            // XmlSerializer writes the first member declared for a value.
            var bits = ToBits(field.ConstantValue!);
            if (!values.Add(bits))
            {
                continue;
            }

            var xmlName = field.Name;
            AttributeData? xmlEnum = null;
            foreach (var attribute in field.GetAttributes())
            {
                if (IsAttribute(attribute, XmlEnumAttributeName))
                {
                    if (xmlEnum is not null)
                    {
                        state.ReportUnsupportedAttribute(field, attribute);
                    }

                    xmlEnum ??= attribute;
                }
                else if (IsSerializationAttribute(attribute))
                {
                    state.ReportUnsupportedAttribute(field, attribute);
                }
            }

            if (xmlEnum is not null)
            {
                xmlName = ReadEnumName(state, field, xmlEnum) ?? xmlName;
            }

            members.Add(new XmlEnumMemberSpec(field.Name, xmlName, bits == 0));
        }

        return new XmlEnumSpec(
            enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            enumType.Name,
            formatterName,
            HasAttribute(enumType, FlagsAttributeName),
            new EquatableArray<XmlEnumMemberSpec>(members.ToImmutable()));
    }

    private static ulong ToBits(object value)
    {
        return value switch
        {
            ulong u64 => u64,
            long i64 => unchecked((ulong)i64),
            uint u32 => u32,
            int i32 => unchecked((ulong)(long)i32),
            ushort u16 => u16,
            short i16 => unchecked((ulong)(long)i16),
            byte u8 => u8,
            sbyte i8 => unchecked((ulong)(long)i8),
            _ => 0,
        };
    }

    private static bool TrySet(ref AttributeData? slot, AttributeData value)
    {
        if (slot is not null)
        {
            return false;
        }

        slot = value;
        return true;
    }

    private static int Count(AttributeData? attribute) => attribute is null ? 0 : 1;

    private static bool IsAttribute(AttributeData attribute, string metadataName) =>
        attribute.AttributeClass?.ToDisplayString() == metadataName;

    private static bool IsSerializationAttribute(AttributeData attribute) =>
        attribute.AttributeClass?.ContainingNamespace?.ToDisplayString() == SerializationNamespace;

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsAttribute(attribute, metadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static string ShortName(AttributeData attribute)
    {
        var name = attribute.AttributeClass?.Name ?? "Attribute";
        const string suffix = "Attribute";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
    }

    private static XmlNameSpec ToName(XmlNameOptions options) => new(options.Name!, options.Namespace);

    /// <summary>
    /// Reads the name, namespace, and (where XmlSerializer honours it) IsNullable of a mapping attribute. The name is
    /// <see langword="null"/> when neither the attribute nor the fallback provides one.
    /// </summary>
    private static XmlNameOptions ReadNameOptions(
        ParseState state,
        ISymbol target,
        AttributeData? attribute,
        string? fallbackName,
        string nameProperty,
        bool allowIsNullable)
    {
        var name = fallbackName;
        string? ns = null;
        bool? isNullable = null;

        if (attribute is not null)
        {
            if (attribute.ConstructorArguments.Length > 1 ||
                (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is not string))
            {
                state.ReportUnsupportedAttribute(target, attribute);
            }
            else if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string { Length: > 0 } constructorName)
            {
                name = constructorName;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == nameProperty && argument.Value.Value is string namedName)
                {
                    if (namedName.Length != 0)
                    {
                        name = namedName;
                    }
                }
                else if (argument.Key == "Namespace" && argument.Value.Value is string namedNamespace)
                {
                    // An empty string stays empty: XmlSerializer writes such a member in the empty namespace (xmlns="").
                    ns = namedNamespace;
                }
                else if (allowIsNullable && argument.Key == "IsNullable" && argument.Value.Value is bool namedIsNullable)
                {
                    isNullable = namedIsNullable;
                }
                else
                {
                    state.ReportUnsupportedAttribute(target, attribute);
                }
            }
        }

        if (name is not null && !IsValidXmlName(name))
        {
            state.Report(Diagnostics.InvalidXmlName, target, name, target.ToDisplayString());
        }

        return new XmlNameOptions(name, ns, isNullable);
    }

    private readonly record struct XmlNameOptions(string? Name, string? Namespace, bool? IsNullable);

    /// <summary>[XmlEnum]: the constructor or Name; the text need not be an XML name.</summary>
    private static string? ReadEnumName(ParseState state, ISymbol target, AttributeData attribute)
    {
        string? name = null;

        if (attribute.ConstructorArguments.Length > 1 ||
            (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is not string))
        {
            state.ReportUnsupportedAttribute(target, attribute);
        }
        else if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string { Length: > 0 } constructorName)
        {
            name = constructorName;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "Name" && argument.Value.Value is string namedName)
            {
                if (namedName.Length != 0)
                {
                    name = namedName;
                }
            }
            else
            {
                state.ReportUnsupportedAttribute(target, attribute);
            }
        }

        return name;
    }

    /// <summary>[XmlText]: only the parameterless form is honoured; a Type or DataType would change the written text.</summary>
    private static void ReadNoOptions(ParseState state, ISymbol target, AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length != 0 || attribute.NamedArguments.Length != 0)
        {
            state.ReportUnsupportedAttribute(target, attribute);
        }
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

    private static string GetHintName(INamedTypeSymbol model)
    {
        var builder = new StringBuilder();
        foreach (var character in model.ToDisplayString())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Append(".GenerateXml.g.cs").ToString();
    }

    /// <summary>Diagnostics, the enum formatters discovered so far, and the model name, for one parse.</summary>
    private sealed class ParseState
    {
        private readonly ImmutableArray<DiagnosticSpec>.Builder _diagnostics = ImmutableArray.CreateBuilder<DiagnosticSpec>();
        private readonly Dictionary<string, XmlEnumSpec> _enumsByType = new(StringComparer.Ordinal);
        private readonly List<XmlEnumSpec> _enums = new();
        private readonly HashSet<string> _formatterNames = new(StringComparer.Ordinal);

        public ParseState(INamedTypeSymbol model)
        {
            ModelName = model.ToDisplayString();
        }

        public string ModelName { get; }

        public bool HasErrors { get; private set; }

        public EquatableArray<XmlEnumSpec> Enums => new(_enums.ToImmutableArray());

        public void Report(DiagnosticDescriptor descriptor, ISymbol target, params string[] arguments)
        {
            HasErrors = true;
            _diagnostics.Add(new DiagnosticSpec(descriptor, LocationSpec.From(target), new EquatableArray<string>(ImmutableArray.Create(arguments))));
        }

        public void ReportUnsupportedAttribute(ISymbol target, AttributeData attribute) =>
            Report(Diagnostics.UnsupportedAttributeConfiguration, target, ShortName(attribute), target.ToDisplayString());

        /// <summary>Registers the enum once per model and returns the name of its formatter.</summary>
        public string RegisterEnum(INamedTypeSymbol enumType)
        {
            var key = enumType.ToDisplayString();
            if (!_enumsByType.TryGetValue(key, out var spec))
            {
                spec = BuildEnum(this, enumType, UniqueFormatterName(enumType.Name));
                _enumsByType.Add(key, spec);
                _enums.Add(spec);
            }

            return spec.FormatterName;
        }

        public XmlModelResult ToResult(XmlModelSpec? spec) =>
            new(spec, new EquatableArray<DiagnosticSpec>(_diagnostics.ToImmutable()));

        private string UniqueFormatterName(string typeName)
        {
            var name = typeName + "ToXmlString";
            var suffix = 1;
            while (!_formatterNames.Add(name))
            {
                suffix++;
                name = typeName + "ToXmlString" + suffix.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }
    }
}

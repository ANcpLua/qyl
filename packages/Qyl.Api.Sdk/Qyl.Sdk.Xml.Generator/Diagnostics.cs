using Microsoft.CodeAnalysis;

namespace Qyl.Sdk.Xml.Generator;

internal static class Diagnostics
{
    private const string Category = "QylXml";

    public static readonly DiagnosticDescriptor InvalidModel = new(
        "QYLXML001",
        "XML model shape is not supported",
        "Type '{0}' must be a top-level, non-generic, non-static class or record class",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidProperty = new(
        "QYLXML002",
        "XML property shape is not supported",
        "Property '{0}' on '{1}' must be a readable public instance property without parameters",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        "QYLXML003",
        "XML property type is not supported",
        "Property '{0}' on '{1}' has unsupported type '{2}'; use a string, enum, or supported scalar, a [GenerateXml] model, or a collection of those",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingPropertyMapping = new(
        "QYLXML004",
        "XML property mapping is ambiguous",
        "Property '{0}' on '{1}' must have at most one of XmlAttribute, XmlElement, XmlArray, or XmlText, each applied once",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidXmlName = new(
        "QYLXML005",
        "XML name is invalid",
        "XML name '{0}' configured for '{1}' is not a valid unqualified XML name",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedAttributeConfiguration = new(
        "QYLXML006",
        "XML serialization option is not supported",
        "Attribute '{0}' on '{1}' is not supported in this form; only the name and namespace of XmlRoot, XmlElement, XmlAttribute, XmlArray, and XmlArrayItem, and the name of XmlEnum, are honoured",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotPartial = new(
        "QYLXML007",
        "XML model must be partial",
        "Type '{0}' is marked [GenerateXml] and must be declared partial so the IXmlWritable implementation can be generated into it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BaseTypeNotInSource = new(
        "QYLXML008",
        "XML model base type is not declared in this compilation",
        "Type '{0}' derives from '{1}', which is not declared in this compilation; inherited properties can only be written in declaration order for source types",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MappingMismatch = new(
        "QYLXML009",
        "XML mapping does not fit the property type",
        "[{0}] on property '{1}' of '{2}' does not fit its type: {3}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ItemNameRequired = new(
        "QYLXML010",
        "XML collection item name is required",
        "Property '{0}' on '{1}' is a collection of '{2}', which has no default XML item name; add [XmlArrayItem(\"...\")] or map the property with [XmlElement(\"...\")]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedSerializerConvention = new(
        "QYLXML011",
        "XmlSerializer convention is not supported",
        "'{0}' on '{1}' would change what XmlSerializer writes but is not honoured by the generator; remove it or mark the property [XmlIgnore]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

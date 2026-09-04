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
        "Property '{0}' on '{1}' has unsupported type '{2}'; use a string, enum, or supported scalar type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingPropertyMapping = new(
        "QYLXML004",
        "XML property mapping is ambiguous",
        "Property '{0}' on '{1}' must have at most one XmlElement or XmlAttribute mapping, not both",
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
        "Attribute '{0}' on '{1}' uses an unsupported constructor or named option; only the XML name and namespace are supported",
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
}

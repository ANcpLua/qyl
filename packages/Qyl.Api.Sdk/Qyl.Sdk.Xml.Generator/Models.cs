using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Qyl.Sdk.Xml.Generator;

/// <summary>How a scalar is turned into XML text.</summary>
internal enum ValueKind
{
    String,
    XmlConvertScalar,
    DateTime,
    DateTimeOffset,
    Guid,
    TimeSpan,
    DateOnly,
    TimeOnly,
    Enum,
}

/// <summary>An XML name. A <see langword="null"/> namespace inherits the enclosing element's namespace; an empty one is the empty namespace.</summary>
internal sealed record XmlNameSpec(string LocalName, string? Namespace);

/// <summary>
/// A scalar value: its kind, whether it can be absent, for enums the name of the generated formatter, and the
/// <c>Qyl.Xml.XmlScalarType</c> member the shape describes it with.
/// </summary>
internal sealed record XmlScalarSpec(ValueKind Kind, bool IsNullable, string? EnumFormatter, string SchemaType);

/// <summary>One property of the model, mapped to a node of the element tree. Attributes are written before all other nodes.</summary>
internal abstract record XmlNodeSpec(string PropertyName);

/// <summary>A scalar written as an attribute of the model's element.</summary>
internal sealed record XmlAttributeNode(string PropertyName, XmlNameSpec Name, XmlScalarSpec Value) : XmlNodeSpec(PropertyName);

/// <summary>A scalar written as a child element with text content; an absent value is either omitted or written as <c>xsi:nil</c>.</summary>
internal sealed record XmlElementNode(string PropertyName, XmlNameSpec Name, XmlScalarSpec Value, bool WriteNil) : XmlNodeSpec(PropertyName);

/// <summary>A scalar written as the text content of the model's element.</summary>
internal sealed record XmlTextNode(string PropertyName, XmlScalarSpec Value) : XmlNodeSpec(PropertyName);

/// <summary>A property whose type is itself a writable model, written as a child element under the parent's chosen name.</summary>
internal sealed record XmlModelNode(string PropertyName, XmlNameSpec Name, bool WriteNil, string TypeName) : XmlNodeSpec(PropertyName);

/// <summary>
/// A collection property. With a wrapper the items nest inside it (<c>[XmlArray]</c>); without one they repeat directly under the
/// parent (<c>[XmlElement]</c>). <see cref="ItemScalar"/> is <see langword="null"/> when the items are writable models. The nil flags
/// say whether an absent collection or item is written as <c>xsi:nil</c> instead of being skipped.
/// </summary>
internal sealed record XmlCollectionNode(
    string PropertyName,
    XmlNameSpec? Wrapper,
    bool WrapperWriteNil,
    XmlNameSpec ItemName,
    XmlScalarSpec? ItemScalar,
    string? ItemTypeName,
    bool ItemWriteNil) : XmlNodeSpec(PropertyName);

/// <summary>One enum member and the text <c>XmlSerializer</c> writes for it.</summary>
internal sealed record XmlEnumMemberSpec(string MemberName, string XmlName, bool IsZero);

/// <summary>An enum type used by the model; the emitter writes one private formatter per spec.</summary>
internal sealed record XmlEnumSpec(string TypeName, string ShortName, string FormatterName, bool IsFlags, EquatableArray<XmlEnumMemberSpec> Members);

internal sealed record XmlModelSpec(
    string? Namespace,
    string TypeKeyword,
    string TypeName,
    string HintName,
    XmlNameSpec Root,
    bool HidesBaseImplementation,
    EquatableArray<XmlNodeSpec> Nodes,
    EquatableArray<XmlEnumSpec> Enums);

/// <summary>A source location that is cheap to compare, so diagnostics do not pin syntax trees in the pipeline cache.</summary>
internal sealed record LocationSpec(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationSpec? From(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
            {
                return new LocationSpec(location.SourceTree!.FilePath, location.SourceSpan, location.GetLineSpan().Span);
            }
        }

        return null;
    }

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

internal sealed record DiagnosticSpec(DiagnosticDescriptor Descriptor, LocationSpec? Location, EquatableArray<string> Arguments)
{
    public Diagnostic ToDiagnostic()
    {
        var arguments = new object[Arguments.Count];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = Arguments[index];
        }

        return Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, arguments);
    }
}

internal sealed record XmlModelResult(XmlModelSpec? Spec, EquatableArray<DiagnosticSpec> Diagnostics);

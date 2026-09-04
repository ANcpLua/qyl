using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Qyl.Sdk.Xml.Generator;

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

internal sealed record XmlPropertySpec(
    string PropertyName,
    string XmlName,
    string? XmlNamespace,
    bool IsAttribute,
    ValueKind Kind,
    bool IsNullable);

internal sealed record XmlModelSpec(
    string? Namespace,
    string TypeKeyword,
    string TypeName,
    string HintName,
    string RootName,
    string? RootNamespace,
    EquatableArray<XmlPropertySpec> Properties);

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

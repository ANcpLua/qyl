using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

internal static class LoomLiteralFormatter
{
    public static string? GetDefaultValueLiteral(IParameterSymbol parameter, CancellationToken cancellationToken)
    {
        var syntax =
            parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) as ParameterSyntax;
        var fromSyntax = syntax?.Default?.Value.ToString().Trim();

        return !string.IsNullOrWhiteSpace(fromSyntax)
            ? fromSyntax
            : FormatConstant(parameter.ExplicitDefaultValue, parameter.Type);
    }

    private static string? FormatConstant(object? value, ITypeSymbol? type)
    {
        if (value is null)
            return "null";

        if (type is INamedTypeSymbol
            {
                TypeKind: TypeKind.Enum,
                EnumUnderlyingType: { } underlyingType
            })
        {
            var converted = ConvertToUnderlyingType(value, underlyingType);
            return converted is null
                ? null
                : $"({type.GetFullyQualifiedName()}){SymbolDisplay.FormatPrimitive(converted, true, false)}";
        }

        return SymbolDisplay.FormatPrimitive(value, true, false);
    }

    private static object? ConvertToUnderlyingType(object value, ITypeSymbol underlyingType)
    {
        try
        {
            return underlyingType.SpecialType switch
            {
                SpecialType.System_Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
                SpecialType.System_SByte => Convert.ToSByte(value, CultureInfo.InvariantCulture),
                SpecialType.System_Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
                SpecialType.System_UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
                SpecialType.System_Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                SpecialType.System_UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
                SpecialType.System_Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                SpecialType.System_UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                SpecialType.System_IntPtr => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                SpecialType.System_UIntPtr => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                SpecialType.System_Single => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                SpecialType.System_Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                SpecialType.System_Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                _ => null
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }
}

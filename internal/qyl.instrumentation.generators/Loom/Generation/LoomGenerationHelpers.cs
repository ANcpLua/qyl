using Microsoft.CodeAnalysis.CSharp;
using Qyl.Instrumentation.Generators.Loom.Models;

namespace Qyl.Instrumentation.Generators.Loom.Generation;

internal static class LoomGenerationHelpers
{
    public static void AppendDeclarationChain(
        IndentedStringBuilder sb,
        EquatableArray<LoomTypeDeclarationModel> declarationChain)
    {
        foreach (var declaration in declarationChain)
        {
            sb.AppendLine(
                $"{declaration.Modifiers} {declaration.Keyword} {declaration.Name}{declaration.TypeParameters}");

            foreach (var clause in declaration.ConstraintClauses)
                sb.AppendLine(clause);

            sb.BeginBlock();
        }
    }

    public static string StringLiteral(string value) => SymbolDisplay.FormatLiteral(value, true);

    public static string NullableStringLiteral(string? value)
        => value is null ? "null" : StringLiteral(value);

    public static string TypeOf(string fullyQualifiedType) => $"typeof({fullyQualifiedType})";

    public static string HintName(string fullyQualifiedType, string suffix)
    {
        var sanitized = fullyQualifiedType
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace(',', '_');

        return sanitized + suffix;
    }

    public static string GetNamespace(string fullyQualifiedType)
    {
        var raw = fullyQualifiedType.Replace("global::", string.Empty, StringComparison.Ordinal);
        var lastDot = raw.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : raw[..lastDot];
    }
}

namespace Qyl.Instrumentation.Generators.Loom.Extraction;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Models;

internal static class LoomDeclarationChainExtractor
{
    public static EquatableArray<LoomTypeDeclarationModel> Extract(TypeDeclarationSyntax declaration)
    {
        var chain = new List<LoomTypeDeclarationModel>();

        for (var current = declaration; current is not null; current = current.Parent as TypeDeclarationSyntax)
        {
            var modifiers = current.Modifiers.Select(static modifier => modifier.ValueText).ToList();
            if (!modifiers.Contains("partial", StringComparer.Ordinal))
                modifiers.Add("partial");

            chain.Add(BuildModel(current, modifiers));
        }

        chain.Reverse();
        return chain.Count is 0 ? default : chain.ToArray().ToEquatableArray();
    }

    public static DiagnosticFlow<EquatableArray<LoomTypeDeclarationModel>> ExtractWithDiagnostics(
        TypeDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var chain = new List<LoomTypeDeclarationModel>();

        for (var current = declaration; current is not null; current = current.Parent as TypeDeclarationSyntax)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modifiers = current.Modifiers.Select(static modifier => modifier.ValueText).ToList();

            if (!current.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    LoomDiagnosticDescriptors.TypeMustBePartial,
                    current.Identifier,
                    current.Identifier.ValueText));
            }

            if (!modifiers.Contains("partial", StringComparer.Ordinal))
                modifiers.Add("partial");

            chain.Add(BuildModel(current, modifiers));
        }

        chain.Reverse();

        return diagnostics.Count > 0
            ? DiagnosticFlow.Fail<EquatableArray<LoomTypeDeclarationModel>>(diagnostics.ToArray())
            : DiagnosticFlow.Ok(chain.Count is 0 ? default : chain.ToArray().ToEquatableArray());
    }

    private static LoomTypeDeclarationModel BuildModel(TypeDeclarationSyntax current, List<string> modifiers)
    {
        var keyword = current.Keyword.ValueText;
        if (current is RecordDeclarationSyntax record)
        {
            if (record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword))
                keyword = "record struct";
            else if (record.ClassOrStructKeyword.IsKind(SyntaxKind.ClassKeyword))
                keyword = "record class";
            else
                keyword = "record";
        }

        return new LoomTypeDeclarationModel(
            current.Identifier.ValueText,
            keyword,
            string.Join(" ", modifiers),
            current.TypeParameterList?.ToString().Trim() ?? string.Empty,
            current.ConstraintClauses
                .Select(static clause => clause.ToString().Trim())
                .ToArray()
                .ToEquatableArray());
    }
}

file static class LoomDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor TypeMustBePartial = new(
        "QLOOM001",
        "Loom: Type must be partial",
        "Type '{0}' must be declared as partial to support Loom code generation",
        "Qyl.Loom",
        DiagnosticSeverity.Error,
        true);
}

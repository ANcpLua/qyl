// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     QYL0136 — Flags inline string literals supplied as a <c>ChatClientAgentOptions.Instructions</c>
///     value or a <c>ChatClientAgent</c> <c>instructions:</c> argument. Only literals over a
///     ~40-char threshold (or containing a newline) are flagged so that short test/debug sentinels
///     stay silent. Exempt under <c>tests/**</c> and <c>samples/**</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InlineSystemPromptAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QYL0136";

    private const int LengthThreshold = 40;
    private const string OptionsTypeName = "Microsoft.Agents.AI.ChatClientAgentOptions";
    private const string InstructionsMember = "Instructions";
    private const string InstructionsParameter = "instructions";

    private static readonly DiagnosticDescriptor SRule = new(
        DiagnosticId,
        "Inline system-prompt literal",
        "System prompt literals must load from `.md` files with `ConcurrentDictionary<string, string>` " +
        "caching (see `Apex.AgenticEntityExtractor.Agents.ExtractorAgentsBuilder.LoadInstructions`). " +
        "Move the prompt to `Data/Instructions/<agent>.md` and load via `LoadInstructions(\"<agent>.md\")`.",
        "Qyl.Instrumentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
        context.RegisterOperationAction(AnalyzeArgument, OperationKind.Argument);
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (IsExempt(context.Operation.Syntax.SyntaxTree.FilePath))
            return;

        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation propertyRef)
            return;

        if (propertyRef.Property.Name != InstructionsMember ||
            propertyRef.Property.ContainingType?.ToDisplayString() != OptionsTypeName)
            return;

        Report(context, assignment.Value);
    }

    private static void AnalyzeArgument(OperationAnalysisContext context)
    {
        if (IsExempt(context.Operation.Syntax.SyntaxTree.FilePath))
            return;

        var argument = (IArgumentOperation)context.Operation;
        if (argument.Parameter?.Name != InstructionsParameter)
            return;

        // Limit to ChatClient*Agent / ChatClientAgentOptions ctors to avoid coincidental parameter names.
        var containing = argument.Parameter.ContainingSymbol?.ContainingType?.ToDisplayString();
        if (containing is not "Microsoft.Agents.AI.ChatClientAgent" and not OptionsTypeName)
            return;

        Report(context, argument.Value);
    }

    private static void Report(OperationAnalysisContext context, IOperation value)
    {
        if (value is not ILiteralOperation literal || literal.ConstantValue.Value is not string text)
            return;

        if (text.Length < LengthThreshold && !text.Contains('\n', StringComparison.Ordinal))
            return;

        context.ReportDiagnostic(Diagnostic.Create(SRule, value.Syntax.GetLocation()));
    }

    private static bool IsExempt(string path)
    {
        // Wrap so startsWith-tests and leading-separator paths both match `/tests/` / `/samples/`.
        var normalized = "/" + path.Replace('\\', '/');
        return normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/samples/", StringComparison.OrdinalIgnoreCase);
    }
}

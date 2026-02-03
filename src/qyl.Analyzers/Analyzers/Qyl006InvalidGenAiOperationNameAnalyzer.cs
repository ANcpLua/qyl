using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL006: Detects GenAI operation names that don't follow semantic conventions.
/// </summary>
/// <remarks>
///     <para>
///         GenAI operation names should be one of the standard values:
///         <list type="bullet">
///             <item>chat - for chat completions</item>
///             <item>text_completion - for text completions</item>
///             <item>embeddings - for embedding generation</item>
///         </list>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl006InvalidGenAiOperationNameAnalyzer : QylAnalyzer {
    private static readonly HashSet<string> ValidOperationNames = new(StringComparer.OrdinalIgnoreCase) {
        "chat", "text_completion", "embeddings"
    };

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL006AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL006AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL006AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.InvalidGenAiOperationName,
        Title, MessageFormat, DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers operation actions to analyze string literals used as operation names.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context) {
        var invocation = (IInvocationOperation)context.Operation;

        // Look for SetTag calls with "gen_ai.operation.name" key
        if (invocation.TargetMethod.Name != "SetTag" || invocation.Arguments.Length < 2) {
            return;
        }

        // Check if the first argument is "gen_ai.operation.name"
        if (invocation.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: string tagName }) {
            return;
        }

        if (!tagName.Equals("gen_ai.operation.name", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        // Check if the second argument is a valid operation name
        if (invocation.Arguments[1].Value.ConstantValue is not { HasValue: true, Value: string operationName }) {
            return;
        }

        if (!ValidOperationNames.Contains(operationName)) {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.Arguments[1].Syntax.GetLocation(),
                operationName));
        }
    }
}

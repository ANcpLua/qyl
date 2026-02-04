using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL003: Detects ActivitySource instances that may not be registered with AddSource().
/// </summary>
/// <remarks>
///     <para>
///         ActivitySources must be registered with AddSource() in the OpenTelemetry tracing
///         configuration to emit spans. Unregistered sources will silently fail to produce traces.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl003UnregisteredActivitySourceAnalyzer : QylAnalyzer
{
    private const string ActivitySourceTypeName = "System.Diagnostics.ActivitySource";

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL003AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL003AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL003AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.UnregisteredActivitySource,
        Title, MessageFormat, DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers operation actions to analyze ActivitySource creation.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;

        // Check if this is ActivitySource creation
        if (objectCreation.Type?.ToDisplayString() != ActivitySourceTypeName)
        {
            return;
        }

        // Get the source name from constructor argument
        if (objectCreation.Arguments.Length == 0 ||
            objectCreation.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: string sourceName })
        {
            return;
        }

        // Check if there's an AddSource call in the compilation with this source name
        // This is a heuristic - we can't do cross-file analysis, so we report a suggestion
        // The user can suppress if they know it's registered elsewhere

        // For now, we report this as a reminder/suggestion
        // In a more sophisticated implementation, we could track AddSource calls
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            objectCreation.Syntax.GetLocation(),
            sourceName));
    }
}

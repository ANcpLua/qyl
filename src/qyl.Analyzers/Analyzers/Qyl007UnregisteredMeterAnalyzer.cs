using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL007: Detects Meter instances that may not be registered with AddMeter().
/// </summary>
/// <remarks>
///     <para>
///         Meters must be registered with AddMeter() in the OpenTelemetry metrics
///         configuration to export metrics. Unregistered meters will silently fail.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl007UnregisteredMeterAnalyzer : QylAnalyzer {
    private const string MeterTypeName = "System.Diagnostics.Metrics.Meter";

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL007AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL007AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL007AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.UnregisteredMeter,
        Title, MessageFormat, DiagnosticCategories.Metrics,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers operation actions to analyze Meter creation.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);

    private static void AnalyzeObjectCreation(OperationAnalysisContext context) {
        var objectCreation = (IObjectCreationOperation)context.Operation;

        // Check if this is Meter creation
        if (objectCreation.Type?.ToDisplayString() != MeterTypeName) {
            return;
        }

        // Get the meter name from constructor argument
        if (objectCreation.Arguments.Length == 0 ||
            objectCreation.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: string meterName }) {
            return;
        }

        // Report as reminder/suggestion to ensure registration
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            objectCreation.Syntax.GetLocation(),
            meterName));
    }
}

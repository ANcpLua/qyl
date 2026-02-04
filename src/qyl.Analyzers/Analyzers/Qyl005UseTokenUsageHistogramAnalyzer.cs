using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL005: Detects token usage metrics that don't use the standard histogram.
/// </summary>
/// <remarks>
///     <para>
///         GenAI token usage should be recorded using the standard histogram:
///         <c>gen_ai.client.token.usage</c>
///     </para>
///     <para>
///         This ensures compatibility with standard GenAI observability dashboards.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl005UseTokenUsageHistogramAnalyzer : QylAnalyzer
{
    private const string CorrectMetricName = "gen_ai.client.token.usage";
    private const string HistogramAttributeFullName = "qyl.ServiceDefaults.Instrumentation.HistogramAttribute";

    private static readonly string[] TokenRelatedPatterns =
    [
        "token", "input_token", "output_token", "prompt_token", "completion_token"
    ];

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL005AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL005AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL005AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.UseTokenUsageHistogram,
        Title, MessageFormat, DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers symbol actions to analyze methods with histogram attributes.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        var histogramType = context.Compilation.GetTypeByMetadataName(HistogramAttributeFullName);
        if (histogramType is null)
        {
            return;
        }

        foreach (var attribute in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, histogramType))
            {
                continue;
            }

            // Get the metric name from constructor argument
            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not string metricName)
            {
                continue;
            }

            // Check if this looks like a token usage metric but uses wrong name
            if (IsTokenRelatedMetric(metricName) &&
                !metricName.Equals(CorrectMetricName, StringComparison.OrdinalIgnoreCase))
            {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)
                    .GetLocation() ?? method.Locations.FirstOrDefault();

                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        location,
                        metricName));
                }
            }
        }
    }

    private static bool IsTokenRelatedMetric(string metricName)
    {
        foreach (var pattern in TokenRelatedPatterns)
        {
            if (metricName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

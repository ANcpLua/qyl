using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL009: Detects incomplete ServiceDefaults configuration.
/// </summary>
/// <remarks>
///     <para>
///         Complete ServiceDefaults configuration should include:
///         <list type="bullet">
///             <item>Tracing configuration (AddTracing/WithTracing)</item>
///             <item>Metrics configuration (AddMetrics/WithMetrics)</item>
///             <item>Logging configuration (AddLogging/WithLogging)</item>
///         </list>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl009IncompleteServiceDefaultsAnalyzer : QylAnalyzer {
    private static readonly string[] TracingMethods = ["AddOpenTelemetry", "WithTracing", "AddTracing"];
    private static readonly string[] MetricsMethods = ["AddOpenTelemetry", "WithMetrics", "AddMetrics"];
    private static readonly string[] LoggingMethods = ["AddOpenTelemetry", "WithLogging", "AddLogging"];

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL009AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL009AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL009AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.IncompleteServiceDefaults,
        Title, MessageFormat, DiagnosticCategories.Configuration,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers syntax tree actions to analyze ServiceDefaults configuration.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Look for ConfigureOpenTelemetry or AddServiceDefaults calls
        var methodName = GetMethodName(invocation);
        if (methodName is not ("ConfigureOpenTelemetry" or "AddServiceDefaults" or "AddOpenTelemetry")) {
            return;
        }

        // Check if this is in a method body (likely configuration code)
        var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod is null) {
            return;
        }

        // Collect all method invocations in the same method
        var allInvocations = new HashSet<string>();
        foreach (var inv in containingMethod.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            var name = GetMethodName(inv);
            if (name is not null) {
                allInvocations.Add(name);
            }
        }

        // Check for tracing configuration
        var hasTracing = TracingMethods.Any(allInvocations.Contains);
        var hasMetrics = MetricsMethods.Any(allInvocations.Contains);
        var hasLogging = LoggingMethods.Any(allInvocations.Contains);

        // Report missing components
        if (!hasTracing) {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                "tracing",
                "WithTracing() or AddTracing()"));
        }

        if (!hasMetrics) {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                "metrics",
                "WithMetrics() or AddMetrics()"));
        }

        // Note: Logging is optional, so we don't report it as missing
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
}

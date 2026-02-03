using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL010: Detects collector endpoint configurations that don't use OTLP protocol.
/// </summary>
/// <remarks>
///     <para>
///         qyl collector endpoints should use the OTLP protocol for standardized telemetry export:
///         <list type="bullet">
///             <item>gRPC: grpc://host:4317</item>
///             <item>HTTP: http://host:4318/v1/traces (or /v1/metrics, /v1/logs)</item>
///         </list>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl010NonOtlpCollectorEndpointAnalyzer : QylAnalyzer {
    private static readonly string[] OtlpPatterns = [
        "4317",      // gRPC default port
        "4318",      // HTTP default port
        "/v1/traces",
        "/v1/metrics",
        "/v1/logs",
        "otlp"
    ];

    private static readonly string[] EndpointPropertyNames = [
        "Endpoint", "CollectorEndpoint", "OtlpEndpoint", "ExporterEndpoint"
    ];

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL010AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL010AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL010AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.NonOtlpCollectorEndpoint,
        Title, MessageFormat, DiagnosticCategories.Configuration,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers operation actions to analyze endpoint assignments.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);

    private static void AnalyzeAssignment(OperationAnalysisContext context) {
        var assignment = (ISimpleAssignmentOperation)context.Operation;

        // Check if this is an endpoint property assignment
        var propertyName = GetPropertyName(assignment.Target);
        if (propertyName is null || !EndpointPropertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase)) {
            return;
        }

        // Get the assigned value
        if (assignment.Value.ConstantValue is not { HasValue: true, Value: string endpoint }) {
            return;
        }

        // Check if the endpoint looks like OTLP
        if (!IsOtlpEndpoint(endpoint)) {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                assignment.Syntax.GetLocation(),
                endpoint));
        }
    }

    private static string? GetPropertyName(IOperation target) =>
        target switch {
            IPropertyReferenceOperation propRef => propRef.Property.Name,
            IMemberReferenceOperation memberRef => memberRef.Member.Name,
            _ => null
        };

    private static bool IsOtlpEndpoint(string endpoint) {
        foreach (var pattern in OtlpPatterns) {
            if (endpoint.Contains(pattern, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}

using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL011: Detects [Meter] classes that are not declared as partial static.
/// </summary>
/// <remarks>
///     <para>
///         The qyl source generator requires [Meter] classes to be partial static because:
///         <list type="bullet">
///             <item>The generator creates static Meter and instrument fields</item>
///             <item>The generator implements partial methods that record metrics</item>
///             <item>Static classes ensure single instance of meter/instruments</item>
///         </list>
///     </para>
///     <para>
///         Example of correct usage:
///         <code>
///         [Meter("MyApp")]
///         public static partial class AppMetrics
///         {
///             [Counter("orders.created")]
///             public static partial void RecordOrderCreated([Tag("status")] string status);
///         }
///         </code>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl011MeterClassMustBePartialStaticAnalyzer : QylAnalyzer {
    private const string MeterAttributeFullName = "qyl.ServiceDefaults.Instrumentation.MeterAttribute";

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL011AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL011AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL011AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.MeterClassMustBePartialStatic,
        Title, MessageFormat, DiagnosticCategories.Metrics,
        DiagnosticSeverities.RequiredFix, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers syntax node actions to analyze class declarations with [Meter] attribute.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context) {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Quick check: skip if no attributes
        if (classDeclaration.AttributeLists.Count == 0) {
            return;
        }

        // Check if class has [Meter] attribute
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken);
        if (classSymbol is null) {
            return;
        }

        if (!HasMeterAttribute(classSymbol, context.SemanticModel.Compilation)) {
            return;
        }

        // Check for partial modifier
        var hasPartial = classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        // Check for static modifier
        var hasStatic = classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword);

        // Report if missing either modifier
        if (!hasPartial || !hasStatic) {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name));
        }
    }

    private static bool HasMeterAttribute(INamedTypeSymbol classSymbol, Compilation compilation) {
        var meterAttributeType = compilation.GetTypeByMetadataName(MeterAttributeFullName);
        if (meterAttributeType is null) {
            return false;
        }

        foreach (var attribute in classSymbol.GetAttributes()) {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, meterAttributeType)) {
                return true;
            }
        }

        return false;
    }
}

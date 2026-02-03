namespace qyl.Analyzers.Core;

/// <summary>
///     Base class for all qyl analyzers.
/// </summary>
public abstract partial class QylAnalyzer : DiagnosticAnalyzer {
    /// <summary>Base URL for diagnostic help links.</summary>
    public const string HelpLinkBase = "https://github.com/ANcpLua/qyl#analyzers";

    /// <summary>Initializes the analyzer and configures execution options.</summary>
    public sealed override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        RegisterActions(context);
    }

    /// <summary>Registers analysis actions to be performed during compilation.</summary>
    /// <param name="context">The analysis context to register actions with.</param>
    protected abstract void RegisterActions(AnalysisContext context);
}

/// <summary>
///     Standard severity levels for analyzers with documentation on when to use each.
/// </summary>
/// <remarks>
///     <para>
///         <b>IMPORTANT:</b> Avoid using <see cref="DiagnosticSeverity.Info"/> for analyzers
///         that should appear in normal build output. Info-level diagnostics are filtered
///         out by MSBuild by default and won't appear in build output or IDE error lists
///         unless explicitly configured.
///     </para>
///     <para>
///         Use <see cref="DiagnosticSeverity.Warning"/> for suggestions and code improvements.
///         Use <see cref="DiagnosticSeverity.Error"/> only for definite bugs or violations.
///     </para>
/// </remarks>
public static partial class DiagnosticSeverities {
    /// <summary>
    ///     Use for suggestions and code style improvements.
    ///     This appears in normal build output and IDE error lists.
    /// </summary>
    public const DiagnosticSeverity Suggestion = DiagnosticSeverity.Warning;

    /// <summary>
    ///     Use for definite bugs, security issues, or violations that must be fixed.
    /// </summary>
    public const DiagnosticSeverity RequiredFix = DiagnosticSeverity.Error;

    /// <summary>
    ///     Use only for diagnostics that should be hidden by default.
    ///     <b>WARNING:</b> Info-level diagnostics are NOT shown in normal build output!
    ///     Users must explicitly enable them via .editorconfig or MSBuild properties.
    /// </summary>
    public const DiagnosticSeverity HiddenByDefault = DiagnosticSeverity.Info;
}

/// <summary>
///     Diagnostic categories for grouping related qyl analyzers.
/// </summary>
public static partial class DiagnosticCategories {
    /// <summary>Category for OpenTelemetry instrumentation diagnostics.</summary>
    public const string OpenTelemetry = "OpenTelemetry";

    /// <summary>Category for Generative AI / LLM observability diagnostics.</summary>
    public const string GenAI = "GenAI";

    /// <summary>Category for metrics and measurement diagnostics.</summary>
    public const string Metrics = "Metrics";

    /// <summary>Category for configuration and setup diagnostics.</summary>
    public const string Configuration = "Configuration";

    /// <summary>Category for qyl protocol diagnostics.</summary>
    public const string Protocol = "Protocol";

    /// <summary>Category for collector and exporter diagnostics.</summary>
    public const string Collector = "Collector";
}

/// <summary>
///     Central registry of all qyl diagnostic IDs following Roslyn naming conventions.
/// </summary>
public static partial class DiagnosticIds {
    // ═══════════════════════════════════════════════════════════════════════════
    // OPENTELEMETRY SEMANTIC CONVENTIONS (QYL001-QYL006)
    // These analyzers enforce OpenTelemetry semantic convention compliance.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL001: Activity/Span missing semantic convention attributes.</summary>
    public const string ActivityMissingSemconv = "QYL001";

    /// <summary>QYL002: Deprecated semantic convention attribute.</summary>
    public const string DeprecatedSemconv = "QYL002";

    /// <summary>QYL003: ActivitySource not registered with AddSource().</summary>
    public const string UnregisteredActivitySource = "QYL003";

    /// <summary>QYL004: GenAI span missing required attributes.</summary>
    public const string GenAiMissingRequiredAttributes = "QYL004";

    /// <summary>QYL005: Use gen_ai.client.token.usage histogram for token metrics.</summary>
    public const string UseTokenUsageHistogram = "QYL005";

    /// <summary>QYL006: GenAI operation name should follow semantic conventions.</summary>
    public const string InvalidGenAiOperationName = "QYL006";

    // ═══════════════════════════════════════════════════════════════════════════
    // METRICS AND CONFIGURATION (QYL007-QYL010)
    // These analyzers enforce metrics configuration and naming conventions.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL007: Meter not registered with AddMeter().</summary>
    public const string UnregisteredMeter = "QYL007";

    /// <summary>QYL008: Metric instrument name should follow naming conventions.</summary>
    public const string InvalidMetricName = "QYL008";

    /// <summary>QYL009: ServiceDefaults configuration incomplete.</summary>
    public const string IncompleteServiceDefaults = "QYL009";

    /// <summary>QYL010: Collector endpoint should use OTLP protocol.</summary>
    public const string NonOtlpCollectorEndpoint = "QYL010";

    // ═══════════════════════════════════════════════════════════════════════════
    // SOURCE GENERATOR REQUIREMENTS (QYL011-QYL012)
    // These analyzers enforce correct attribute usage for qyl source generators.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL011: [Meter] class must be partial static.</summary>
    public const string MeterClassMustBePartialStatic = "QYL011";

    /// <summary>QYL012: [Counter]/[Histogram] method must be partial.</summary>
    public const string MetricMethodMustBePartial = "QYL012";

    // ═══════════════════════════════════════════════════════════════════════════
    // TRACING CONFIGURATION (QYL013)
    // These analyzers enforce correct tracing attribute configuration.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL013: [Traced] attribute must have non-empty ActivitySourceName.</summary>
    public const string TracedActivitySourceNameEmpty = "QYL013";

    // ═══════════════════════════════════════════════════════════════════════════
    // GENAI SEMANTIC CONVENTIONS (QYL014)
    // These analyzers enforce GenAI semantic convention compliance.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL014: Deprecated GenAI semantic convention attribute.</summary>
    public const string DeprecatedGenAiAttribute = "QYL014";

    // ═══════════════════════════════════════════════════════════════════════════
    // METRICS BEST PRACTICES (QYL015)
    // These analyzers enforce metrics cardinality best practices.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL015: High-cardinality tag on metrics (user.id, request.id, etc.).</summary>
    public const string HighCardinalityMetricTag = "QYL015";
}

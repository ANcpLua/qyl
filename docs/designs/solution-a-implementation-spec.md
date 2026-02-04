# Solution A: Implementation Specification

## File 1: Core/QylAnalyzer.cs

**Path:** `/Users/ancplua/qyl/src/qyl.instrumentation.generators/Core/QylAnalyzer.cs`

```csharp
// =============================================================================
// qyl.instrumentation.generators - QYL Analyzer Infrastructure
// Base class and diagnostic constants for all qyl analyzers
// Owner: qyl.instrumentation.generators
// =============================================================================

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace qyl.instrumentation.generators.Core;

/// <summary>
/// Base class for all qyl analyzers.
/// Provides standard configuration for concurrent execution and generated code handling.
/// </summary>
public abstract class QylAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Base URL for diagnostic documentation.</summary>
    public const string HelpLinkBase = "https://github.com/ANcpLua/qyl/blob/main/docs/diagnostics";

    /// <summary>Initializes the analyzer with standard configuration.</summary>
    public sealed override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        RegisterActions(context);
    }

    /// <summary>Registers analysis actions to be performed during compilation.</summary>
    protected abstract void RegisterActions(AnalysisContext context);
}

/// <summary>
/// Central registry of all qyl diagnostic IDs.
/// </summary>
/// <remarks>
/// Numbering convention:
/// - QYL0xxx: Errors and warnings (actionable issues)
/// - QYL1xxx: Informational (success indicators, suggestions)
/// </remarks>
public static class DiagnosticIds
{
    // ═══════════════════════════════════════════════════════════════════════════
    // INTERCEPTOR LIMITATIONS (QYL0001-QYL0003)
    // Diagnostics reported by the GenAiInterceptorGenerator
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0001: Interface call cannot be intercepted.</summary>
    public const string InterfaceCallCannotBeIntercepted = "QYL0001";

    /// <summary>QYL0002: Virtual method call cannot be intercepted.</summary>
    public const string VirtualCallCannotBeIntercepted = "QYL0002";

    /// <summary>QYL0003: Call in compiled assembly cannot be intercepted.</summary>
    public const string CompiledAssemblyCannotBeIntercepted = "QYL0003";

    // ═══════════════════════════════════════════════════════════════════════════
    // INSTRUMENTATION (QYL0004-QYL0010)
    // Diagnostics for OTel instrumentation patterns
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0004: AddQylServiceDefaults called but AddOpenTelemetry is missing.</summary>
    public const string MissingOTelConfiguration = "QYL0004";

    /// <summary>QYL0005: Same method instrumented by both interceptor and manual span.</summary>
    public const string DuplicateInstrumentation = "QYL0005";

    /// <summary>QYL0006: ActivitySource name doesn't follow naming convention.</summary>
    public const string InvalidActivitySourceName = "QYL0006";

    /// <summary>QYL0007: ActivitySource created without schema URL.</summary>
    public const string MissingSchemaUrl = "QYL0007";

    /// <summary>QYL0008: Complex flow detected; manual instrumentation recommended.</summary>
    public const string ManualSpanRecommended = "QYL0008";

    /// <summary>QYL0009: Using deprecated OTel semantic convention attribute.</summary>
    public const string DeprecatedGenAiAttribute = "QYL0009";

    /// <summary>QYL0010: GenAI call without model specification.</summary>
    public const string MissingModelParameter = "QYL0010";

    // ═══════════════════════════════════════════════════════════════════════════
    // SERVICE DEFAULTS (QYL0011-QYL0016)
    // Diagnostics for qyl.servicedefaults patterns
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0011: Conflicting configuration in AddQylServiceDefaults.</summary>
    public const string ServiceDefaultsMisconfiguration = "QYL0011";

    /// <summary>QYL0012: HTTP client without resilience policies.</summary>
    public const string MissingResilienceConfiguration = "QYL0012";

    /// <summary>QYL0013: Service registered without health check endpoint.</summary>
    public const string MissingHealthChecks = "QYL0013";

    /// <summary>QYL0014: Hardcoded connection string; use configuration instead.</summary>
    public const string ConsiderConnectionString = "QYL0014";

    /// <summary>QYL0015: HTTP endpoint used where HTTPS expected.</summary>
    public const string InsecureEndpoint = "QYL0015";

    /// <summary>QYL0016: Direct URL used instead of service discovery.</summary>
    public const string MissingServiceDiscovery = "QYL0016";

    // ═══════════════════════════════════════════════════════════════════════════
    // PROTOCOL/ATTRIBUTES (QYL0017-QYL0022)
    // Diagnostics for OTel semantic convention compliance
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0017: Attribute value violates OTel semantic convention spec.</summary>
    public const string InvalidAttributeValue = "QYL0017";

    /// <summary>QYL0018: Required semantic convention attribute not set.</summary>
    public const string MissingRequiredAttribute = "QYL0018";

    /// <summary>QYL0019: Attribute set with wrong type.</summary>
    public const string IncorrectAttributeType = "QYL0019";

    /// <summary>QYL0020: Use GenAiAttributes.X instead of string literal.</summary>
    public const string PreferConstantAttribute = "QYL0020";

    /// <summary>QYL0021: PII or credential detected in span attribute.</summary>
    public const string SensitiveDataInAttribute = "QYL0021";

    /// <summary>QYL0022: Attribute value may cause cardinality explosion.</summary>
    public const string HighCardinalityAttribute = "QYL0022";

    // ═══════════════════════════════════════════════════════════════════════════
    // COLLECTOR INTEGRATION (QYL0023-QYL0029)
    // Diagnostics for OTLP export and collector configuration
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0023: OTEL_EXPORTER_OTLP_ENDPOINT not configured.</summary>
    public const string MissingOtlpConfiguration = "QYL0023";

    /// <summary>QYL0024: OTLP export without compression configured.</summary>
    public const string UncompressedExport = "QYL0024";

    /// <summary>QYL0025: Malformed OTLP endpoint URL.</summary>
    public const string InvalidOtlpEndpoint = "QYL0025";

    /// <summary>QYL0026: Single-span export reduces performance.</summary>
    public const string BatchExportDisabled = "QYL0026";

    /// <summary>QYL0027: High-volume service without sampling configured.</summary>
    public const string ConsiderSampling = "QYL0027";

    /// <summary>QYL0028: service.name or service.version not configured.</summary>
    public const string MissingResourceAttributes = "QYL0028";

    /// <summary>QYL0029: MCP server running without authentication.</summary>
    public const string McpEndpointExposed = "QYL0029";

    // ═══════════════════════════════════════════════════════════════════════════
    // INFORMATIONAL (QYL1xxx)
    // Success indicators and suggestions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL1001: GenAI call successfully intercepted.</summary>
    public const string GenAiCallIntercepted = "QYL1001";
}

/// <summary>
/// Diagnostic categories matching qyl project domains.
/// </summary>
public static class DiagnosticCategories
{
    /// <summary>Category for instrumentation-related diagnostics.</summary>
    public const string Instrumentation = "qyl.instrumentation";

    /// <summary>Category for service defaults diagnostics.</summary>
    public const string ServiceDefaults = "qyl.servicedefaults";

    /// <summary>Category for protocol/attribute diagnostics.</summary>
    public const string Protocol = "qyl.protocol";

    /// <summary>Category for collector integration diagnostics.</summary>
    public const string Collector = "qyl.collector";
}
```

**Lines:** 156

---

## File 2: Expanded DiagnosticDescriptors.cs

**Path:** `/Users/ancplua/qyl/src/qyl.instrumentation.generators/Diagnostics/DiagnosticDescriptors.cs`

```csharp
// =============================================================================
// qyl.instrumentation.generators - Diagnostic Descriptors
// All diagnostic descriptors for qyl analyzers
// Owner: qyl.instrumentation.generators
// =============================================================================

using Microsoft.CodeAnalysis;
using qyl.instrumentation.generators.Core;

namespace qyl.instrumentation.generators.Diagnostics;

/// <summary>
/// Diagnostic descriptors for all qyl analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    private static string HelpLink(string id) => $"{QylAnalyzer.HelpLinkBase}/{id}.md";

    // ═══════════════════════════════════════════════════════════════════════════
    // INTERCEPTOR LIMITATIONS (QYL0001-QYL0003)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0001: Interface call cannot be intercepted.</summary>
    public static readonly DiagnosticDescriptor InterfaceCallCannotBeIntercepted = new(
        id: DiagnosticIds.InterfaceCallCannotBeIntercepted,
        title: "Interface call cannot be intercepted",
        messageFormat: "The call to '{0}' through interface '{1}' cannot be intercepted. Interceptors only work on concrete type calls.",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "C# interceptors cannot intercept calls made through interfaces because the actual implementation is determined at runtime.",
        helpLinkUri: HelpLink(DiagnosticIds.InterfaceCallCannotBeIntercepted));

    /// <summary>QYL0002: Virtual method call cannot be intercepted.</summary>
    public static readonly DiagnosticDescriptor VirtualCallCannotBeIntercepted = new(
        id: DiagnosticIds.VirtualCallCannotBeIntercepted,
        title: "Virtual method call cannot be intercepted",
        messageFormat: "The virtual call to '{0}' cannot be intercepted",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "C# interceptors cannot intercept virtual method calls because the actual implementation is determined by the runtime type.",
        helpLinkUri: HelpLink(DiagnosticIds.VirtualCallCannotBeIntercepted));

    /// <summary>QYL0003: Call in compiled assembly cannot be intercepted.</summary>
    public static readonly DiagnosticDescriptor CompiledAssemblyCannotBeIntercepted = new(
        id: DiagnosticIds.CompiledAssemblyCannotBeIntercepted,
        title: "Call in compiled assembly cannot be intercepted",
        messageFormat: "The call to '{0}' is in a compiled assembly",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "C# interceptors only work on source code you compile.",
        helpLinkUri: HelpLink(DiagnosticIds.CompiledAssemblyCannotBeIntercepted));

    // ═══════════════════════════════════════════════════════════════════════════
    // INSTRUMENTATION (QYL0004-QYL0010)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0004: Missing OTel configuration.</summary>
    public static readonly DiagnosticDescriptor MissingOTelConfiguration = new(
        id: DiagnosticIds.MissingOTelConfiguration,
        title: "Missing OpenTelemetry configuration",
        messageFormat: "AddQylServiceDefaults() is called but AddOpenTelemetry() is missing. Telemetry will not be exported.",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AddQylServiceDefaults() sets up telemetry instrumentation, but without AddOpenTelemetry() the spans and metrics won't be exported.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingOTelConfiguration));

    /// <summary>QYL0005: Duplicate instrumentation.</summary>
    public static readonly DiagnosticDescriptor DuplicateInstrumentation = new(
        id: DiagnosticIds.DuplicateInstrumentation,
        title: "Duplicate instrumentation detected",
        messageFormat: "Method '{0}' is instrumented by both interceptor and manual span. This creates nested duplicate spans.",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The same method is being traced twice - once by auto-instrumentation and once manually. Remove one to avoid duplicate spans.",
        helpLinkUri: HelpLink(DiagnosticIds.DuplicateInstrumentation));

    /// <summary>QYL0006: Invalid ActivitySource name.</summary>
    public static readonly DiagnosticDescriptor InvalidActivitySourceName = new(
        id: DiagnosticIds.InvalidActivitySourceName,
        title: "Invalid ActivitySource name",
        messageFormat: "ActivitySource name '{0}' doesn't follow naming convention. Use reverse-DNS format (e.g., 'qyl.instrumentation.{1}').",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ActivitySource names should follow reverse-DNS naming convention for consistency and filtering.",
        helpLinkUri: HelpLink(DiagnosticIds.InvalidActivitySourceName));

    /// <summary>QYL0007: Missing schema URL.</summary>
    public static readonly DiagnosticDescriptor MissingSchemaUrl = new(
        id: DiagnosticIds.MissingSchemaUrl,
        title: "ActivitySource missing schema URL",
        messageFormat: "ActivitySource '{0}' created without schema URL. Add schema URL for OTel semantic convention compliance.",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Schema URLs help backends understand which version of semantic conventions the telemetry uses.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingSchemaUrl));

    /// <summary>QYL0008: Manual span recommended.</summary>
    public static readonly DiagnosticDescriptor ManualSpanRecommended = new(
        id: DiagnosticIds.ManualSpanRecommended,
        title: "Complex flow - manual instrumentation recommended",
        messageFormat: "Complex async flow in '{0}' may not be fully captured by auto-instrumentation. Consider manual spans.",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Auto-instrumentation may miss parts of complex async flows. Manual instrumentation gives you full control.",
        helpLinkUri: HelpLink(DiagnosticIds.ManualSpanRecommended));

    /// <summary>QYL0009: Deprecated GenAI attribute.</summary>
    public static readonly DiagnosticDescriptor DeprecatedGenAiAttribute = new(
        id: DiagnosticIds.DeprecatedGenAiAttribute,
        title: "Deprecated OTel semantic convention attribute",
        messageFormat: "Attribute '{0}' is deprecated. Use '{1}' instead (OTel semconv {2}).",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The OpenTelemetry semantic conventions evolve. Using deprecated attributes may cause compatibility issues with backends.",
        helpLinkUri: HelpLink(DiagnosticIds.DeprecatedGenAiAttribute));

    /// <summary>QYL0010: Missing model parameter.</summary>
    public static readonly DiagnosticDescriptor MissingModelParameter = new(
        id: DiagnosticIds.MissingModelParameter,
        title: "GenAI call without model specification",
        messageFormat: "GenAI call to '{0}' doesn't specify gen_ai.request.model. Model attribution will be missing from telemetry.",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The gen_ai.request.model attribute is recommended for proper GenAI telemetry analysis.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingModelParameter));

    // ═══════════════════════════════════════════════════════════════════════════
    // SERVICE DEFAULTS (QYL0011-QYL0016)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0011: Service defaults misconfiguration.</summary>
    public static readonly DiagnosticDescriptor ServiceDefaultsMisconfiguration = new(
        id: DiagnosticIds.ServiceDefaultsMisconfiguration,
        title: "Service defaults misconfiguration",
        messageFormat: "Conflicting configuration: {0}",
        category: DiagnosticCategories.ServiceDefaults,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The service defaults configuration contains conflicting settings that may cause unexpected behavior.",
        helpLinkUri: HelpLink(DiagnosticIds.ServiceDefaultsMisconfiguration));

    /// <summary>QYL0012: Missing resilience configuration.</summary>
    public static readonly DiagnosticDescriptor MissingResilienceConfiguration = new(
        id: DiagnosticIds.MissingResilienceConfiguration,
        title: "HTTP client without resilience policies",
        messageFormat: "HTTP client '{0}' doesn't have resilience policies. Consider adding retry and circuit breaker.",
        category: DiagnosticCategories.ServiceDefaults,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "HTTP clients should have resilience policies (retry, timeout, circuit breaker) for production reliability.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingResilienceConfiguration));

    /// <summary>QYL0013: Missing health checks.</summary>
    public static readonly DiagnosticDescriptor MissingHealthChecks = new(
        id: DiagnosticIds.MissingHealthChecks,
        title: "Service missing health check endpoint",
        messageFormat: "Service '{0}' doesn't expose health check endpoint. Add builder.Services.AddHealthChecks().",
        category: DiagnosticCategories.ServiceDefaults,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Health check endpoints are essential for container orchestrators and load balancers.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingHealthChecks));

    /// <summary>QYL0014: Consider connection string.</summary>
    public static readonly DiagnosticDescriptor ConsiderConnectionString = new(
        id: DiagnosticIds.ConsiderConnectionString,
        title: "Hardcoded connection string detected",
        messageFormat: "Hardcoded connection string for '{0}'. Use builder.Configuration.GetConnectionString() instead.",
        category: DiagnosticCategories.ServiceDefaults,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Connection strings should come from configuration for environment flexibility and security.",
        helpLinkUri: HelpLink(DiagnosticIds.ConsiderConnectionString));

    /// <summary>QYL0015: Insecure endpoint.</summary>
    public static readonly DiagnosticDescriptor InsecureEndpoint = new(
        id: DiagnosticIds.InsecureEndpoint,
        title: "Insecure HTTP endpoint",
        messageFormat: "HTTP endpoint '{0}' used where HTTPS is expected. This may expose sensitive data.",
        category: DiagnosticCategories.ServiceDefaults,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Production services should use HTTPS to protect data in transit.",
        helpLinkUri: HelpLink(DiagnosticIds.InsecureEndpoint));

    /// <summary>QYL0016: Missing service discovery.</summary>
    public static readonly DiagnosticDescriptor MissingServiceDiscovery = new(
        id: DiagnosticIds.MissingServiceDiscovery,
        title: "Direct URL instead of service discovery",
        messageFormat: "Direct URL '{0}' used instead of service discovery. Consider using service name resolution.",
        category: DiagnosticCategories.ServiceDefaults,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Service discovery enables dynamic endpoint resolution and better resilience.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingServiceDiscovery));

    // ═══════════════════════════════════════════════════════════════════════════
    // PROTOCOL/ATTRIBUTES (QYL0017-QYL0022)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0017: Invalid attribute value.</summary>
    public static readonly DiagnosticDescriptor InvalidAttributeValue = new(
        id: DiagnosticIds.InvalidAttributeValue,
        title: "Invalid OTel attribute value",
        messageFormat: "Attribute '{0}' has invalid value '{1}'. Expected: {2}",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Attribute values must conform to OpenTelemetry semantic convention specifications.",
        helpLinkUri: HelpLink(DiagnosticIds.InvalidAttributeValue));

    /// <summary>QYL0018: Missing required attribute.</summary>
    public static readonly DiagnosticDescriptor MissingRequiredAttribute = new(
        id: DiagnosticIds.MissingRequiredAttribute,
        title: "Missing required OTel attribute",
        messageFormat: "Required attribute '{0}' is not set on span '{1}'.",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Some OpenTelemetry semantic conventions have required attributes for proper telemetry analysis.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingRequiredAttribute));

    /// <summary>QYL0019: Incorrect attribute type.</summary>
    public static readonly DiagnosticDescriptor IncorrectAttributeType = new(
        id: DiagnosticIds.IncorrectAttributeType,
        title: "Incorrect OTel attribute type",
        messageFormat: "Attribute '{0}' should be type '{1}' but '{2}' was used.",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Using incorrect types for attributes may cause issues with backends and queries.",
        helpLinkUri: HelpLink(DiagnosticIds.IncorrectAttributeType));

    /// <summary>QYL0020: Prefer constant attribute.</summary>
    public static readonly DiagnosticDescriptor PreferConstantAttribute = new(
        id: DiagnosticIds.PreferConstantAttribute,
        title: "Use constant attribute name",
        messageFormat: "Use GenAiAttributes.{0} instead of literal \"{1}\"",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Using constants from GenAiAttributes ensures correct spelling and enables refactoring.",
        helpLinkUri: HelpLink(DiagnosticIds.PreferConstantAttribute));

    /// <summary>QYL0021: Sensitive data in attribute.</summary>
    public static readonly DiagnosticDescriptor SensitiveDataInAttribute = new(
        id: DiagnosticIds.SensitiveDataInAttribute,
        title: "Sensitive data in span attribute",
        messageFormat: "Potential {0} detected in attribute '{1}'. Consider redacting sensitive information.",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Span attributes should not contain PII, credentials, or other sensitive data.",
        helpLinkUri: HelpLink(DiagnosticIds.SensitiveDataInAttribute));

    /// <summary>QYL0022: High cardinality attribute.</summary>
    public static readonly DiagnosticDescriptor HighCardinalityAttribute = new(
        id: DiagnosticIds.HighCardinalityAttribute,
        title: "High cardinality attribute",
        messageFormat: "Attribute '{0}' may have high cardinality. This can cause performance issues in backends.",
        category: DiagnosticCategories.Protocol,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "High cardinality attributes (unique IDs, timestamps) can overwhelm telemetry backends.",
        helpLinkUri: HelpLink(DiagnosticIds.HighCardinalityAttribute));

    // ═══════════════════════════════════════════════════════════════════════════
    // COLLECTOR INTEGRATION (QYL0023-QYL0029)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL0023: Missing OTLP configuration.</summary>
    public static readonly DiagnosticDescriptor MissingOtlpConfiguration = new(
        id: DiagnosticIds.MissingOtlpConfiguration,
        title: "Missing OTLP endpoint configuration",
        messageFormat: "OTEL_EXPORTER_OTLP_ENDPOINT is not configured. Telemetry will use default endpoint or fail.",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Configure OTLP endpoint to ensure telemetry is exported to your collector.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingOtlpConfiguration));

    /// <summary>QYL0024: Uncompressed export.</summary>
    public static readonly DiagnosticDescriptor UncompressedExport = new(
        id: DiagnosticIds.UncompressedExport,
        title: "OTLP export without compression",
        messageFormat: "OTLP exporter doesn't have compression enabled. This increases bandwidth usage.",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Enable gzip compression for OTLP exports to reduce bandwidth, especially with gen_ai.content attributes.",
        helpLinkUri: HelpLink(DiagnosticIds.UncompressedExport));

    /// <summary>QYL0025: Invalid OTLP endpoint.</summary>
    public static readonly DiagnosticDescriptor InvalidOtlpEndpoint = new(
        id: DiagnosticIds.InvalidOtlpEndpoint,
        title: "Invalid OTLP endpoint URL",
        messageFormat: "OTLP endpoint '{0}' is malformed. Expected format: http(s)://host:port",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "OTLP endpoint must be a valid HTTP or HTTPS URL.",
        helpLinkUri: HelpLink(DiagnosticIds.InvalidOtlpEndpoint));

    /// <summary>QYL0026: Batch export disabled.</summary>
    public static readonly DiagnosticDescriptor BatchExportDisabled = new(
        id: DiagnosticIds.BatchExportDisabled,
        title: "Batch export disabled",
        messageFormat: "Single-span export is configured. Batch export improves performance significantly.",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Batch exporting reduces network overhead and improves throughput.",
        helpLinkUri: HelpLink(DiagnosticIds.BatchExportDisabled));

    /// <summary>QYL0027: Consider sampling.</summary>
    public static readonly DiagnosticDescriptor ConsiderSampling = new(
        id: DiagnosticIds.ConsiderSampling,
        title: "Consider enabling sampling",
        messageFormat: "High-volume service without sampling. Consider tail-based or probabilistic sampling.",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Sampling reduces telemetry volume while preserving statistically significant data.",
        helpLinkUri: HelpLink(DiagnosticIds.ConsiderSampling));

    /// <summary>QYL0028: Missing resource attributes.</summary>
    public static readonly DiagnosticDescriptor MissingResourceAttributes = new(
        id: DiagnosticIds.MissingResourceAttributes,
        title: "Missing resource attributes",
        messageFormat: "Resource attribute '{0}' is not configured. This is required for proper service identification.",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "service.name and service.version are essential for identifying telemetry sources.",
        helpLinkUri: HelpLink(DiagnosticIds.MissingResourceAttributes));

    /// <summary>QYL0029: MCP endpoint exposed.</summary>
    public static readonly DiagnosticDescriptor McpEndpointExposed = new(
        id: DiagnosticIds.McpEndpointExposed,
        title: "MCP endpoint exposed without authentication",
        messageFormat: "MCP server endpoint at '{0}' has no authentication. Add authentication middleware.",
        category: DiagnosticCategories.Collector,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "MCP endpoints allow AI agents to query telemetry. Protect with authentication.",
        helpLinkUri: HelpLink(DiagnosticIds.McpEndpointExposed));

    // ═══════════════════════════════════════════════════════════════════════════
    // INFORMATIONAL (QYL1xxx)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>QYL1001: GenAI call intercepted.</summary>
    public static readonly DiagnosticDescriptor GenAiCallIntercepted = new(
        id: DiagnosticIds.GenAiCallIntercepted,
        title: "GenAI call intercepted",
        messageFormat: "The call to '{0}' will be instrumented with OTel GenAI semantic conventions",
        category: DiagnosticCategories.Instrumentation,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This GenAI SDK call will be wrapped with OpenTelemetry instrumentation.",
        helpLinkUri: HelpLink(DiagnosticIds.GenAiCallIntercepted));
}
```

**Lines:** ~340

---

## File 3: Update AnalyzerReleases.Unshipped.md

**Path:** `/Users/ancplua/qyl/src/qyl.instrumentation.generators/AnalyzerReleases/AnalyzerReleases.Unshipped.md`

```markdown
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
QYL0001 | qyl.instrumentation | Info | Interface call cannot be intercepted
QYL0002 | qyl.instrumentation | Info | Virtual method call cannot be intercepted
QYL0003 | qyl.instrumentation | Info | Call in compiled assembly cannot be intercepted
QYL0004 | qyl.instrumentation | Warning | Missing OpenTelemetry configuration
QYL0005 | qyl.instrumentation | Warning | Duplicate instrumentation detected
QYL0006 | qyl.instrumentation | Error | Invalid ActivitySource name
QYL0007 | qyl.instrumentation | Warning | ActivitySource missing schema URL
QYL0008 | qyl.instrumentation | Info | Complex flow - manual instrumentation recommended
QYL0009 | qyl.protocol | Warning | Deprecated OTel semantic convention attribute
QYL0010 | qyl.instrumentation | Warning | GenAI call without model specification
QYL0011 | qyl.servicedefaults | Error | Service defaults misconfiguration
QYL0012 | qyl.servicedefaults | Warning | HTTP client without resilience policies
QYL0013 | qyl.servicedefaults | Warning | Service missing health check endpoint
QYL0014 | qyl.servicedefaults | Info | Hardcoded connection string detected
QYL0015 | qyl.servicedefaults | Warning | Insecure HTTP endpoint
QYL0016 | qyl.servicedefaults | Warning | Direct URL instead of service discovery
QYL0017 | qyl.protocol | Error | Invalid OTel attribute value
QYL0018 | qyl.protocol | Warning | Missing required OTel attribute
QYL0019 | qyl.protocol | Warning | Incorrect OTel attribute type
QYL0020 | qyl.protocol | Info | Use constant attribute name
QYL0021 | qyl.protocol | Warning | Sensitive data in span attribute
QYL0022 | qyl.protocol | Warning | High cardinality attribute
QYL0023 | qyl.collector | Warning | Missing OTLP endpoint configuration
QYL0024 | qyl.collector | Warning | OTLP export without compression
QYL0025 | qyl.collector | Error | Invalid OTLP endpoint URL
QYL0026 | qyl.collector | Warning | Batch export disabled
QYL0027 | qyl.collector | Info | Consider enabling sampling
QYL0028 | qyl.collector | Warning | Missing resource attributes
QYL0029 | qyl.collector | Warning | MCP endpoint exposed without authentication
QYL1001 | qyl.instrumentation | Info | GenAI call intercepted
```

---

## Example Analyzer Implementation

**Path:** `/Users/ancplua/qyl/src/qyl.instrumentation.generators/Analyzers/Qyl0004MissingOTelConfigurationAnalyzer.cs`

```csharp
// =============================================================================
// qyl.instrumentation.generators - QYL0004 Missing OTel Configuration Analyzer
// Detects AddQylServiceDefaults() without AddOpenTelemetry()
// Owner: qyl.instrumentation.generators
// =============================================================================

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using qyl.instrumentation.generators.Core;
using qyl.instrumentation.generators.Diagnostics;

namespace qyl.instrumentation.generators.Analyzers;

/// <summary>
/// Analyzer that detects when AddQylServiceDefaults() is called without
/// a corresponding AddOpenTelemetry() configuration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0004MissingOTelConfigurationAnalyzer : QylAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.MissingOTelConfiguration];

    protected override void RegisterActions(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var hasServiceDefaults = false;
            var hasOpenTelemetry = false;
            Location? serviceDefaultsLocation = null;

            compilationContext.RegisterOperationAction(operationContext =>
            {
                if (operationContext.Operation is not IInvocationOperation invocation)
                    return;

                var methodName = invocation.TargetMethod.Name;
                var containingType = invocation.TargetMethod.ContainingType?.Name ?? "";

                // Track AddQylServiceDefaults calls
                if (methodName == "AddQylServiceDefaults")
                {
                    hasServiceDefaults = true;
                    serviceDefaultsLocation ??= invocation.Syntax.GetLocation();
                }

                // Track AddOpenTelemetry calls
                if (methodName == "AddOpenTelemetry" ||
                    containingType.Contains("OpenTelemetry"))
                {
                    hasOpenTelemetry = true;
                }
            }, OperationKind.Invocation);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                if (hasServiceDefaults && !hasOpenTelemetry && serviceDefaultsLocation is not null)
                {
                    endContext.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.MissingOTelConfiguration,
                            serviceDefaultsLocation));
                }
            });
        });
    }
}
```

**Lines:** ~72

---

## Summary

| File | Purpose | Lines |
|------|---------|-------|
| `Core/QylAnalyzer.cs` | Base class + DiagnosticIds + Categories | 156 |
| `Diagnostics/DiagnosticDescriptors.cs` | All 30 diagnostic descriptors | 340 |
| `AnalyzerReleases.Unshipped.md` | Release tracking | 35 |
| `Analyzers/Qyl0004*.cs` (example) | Example analyzer implementation | 72 |

**Total infrastructure:** ~600 lines

Each additional analyzer: ~70-120 lines
26 analyzers estimated: ~2,340 lines

**Grand total for Solution A:** ~4,500 lines

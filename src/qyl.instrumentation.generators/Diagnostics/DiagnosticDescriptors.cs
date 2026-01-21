// =============================================================================
// qyl.instrumentation.generators - Diagnostic Descriptors
// Compiler diagnostics for interceptor generator
// Owner: qyl.instrumentation.generators
// =============================================================================

using Microsoft.CodeAnalysis;

namespace qyl.instrumentation.generators.Diagnostics;

/// <summary>
/// Diagnostic descriptors for the GenAI interceptor generator.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "qyl.instrumentation";

    /// <summary>
    /// QYL0001: Interface call cannot be intercepted.
    /// </summary>
    public static readonly DiagnosticDescriptor InterfaceCallCannotBeIntercepted = new(
        id: "QYL0001",
        title: "Interface call cannot be intercepted",
        messageFormat: "The call to '{0}' through interface '{1}' cannot be intercepted. Interceptors only work on concrete type calls.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "C# interceptors cannot intercept calls made through interfaces because the actual implementation is determined at runtime.");

    /// <summary>
    /// QYL0002: Virtual method call cannot be intercepted.
    /// </summary>
    public static readonly DiagnosticDescriptor VirtualCallCannotBeIntercepted = new(
        id: "QYL0002",
        title: "Virtual method call cannot be intercepted",
        messageFormat: "The virtual call to '{0}' cannot be intercepted.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "C# interceptors cannot intercept virtual method calls because the actual implementation is determined by the runtime type.");

    /// <summary>
    /// QYL0003: Call in compiled assembly cannot be intercepted.
    /// </summary>
    public static readonly DiagnosticDescriptor CompiledAssemblyCannotBeIntercepted = new(
        id: "QYL0003",
        title: "Call in compiled assembly cannot be intercepted",
        messageFormat: "The call to '{0}' is in a compiled assembly.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "C# interceptors only work on source code you compile.");

    /// <summary>
    /// QYL1001: Successfully intercepted GenAI call.
    /// </summary>
    public static readonly DiagnosticDescriptor GenAiCallIntercepted = new(
        id: "QYL1001",
        title: "GenAI call intercepted",
        messageFormat: "The call to '{0}' will be instrumented with OTel GenAI semantic conventions.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This GenAI SDK call will be wrapped with OpenTelemetry instrumentation.");
}

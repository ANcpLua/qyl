// =============================================================================
// qyl.instrumentation.generators - Diagnostic Descriptors
// Compiler diagnostics for interceptor generator
// Owner: qyl.instrumentation.generators
// =============================================================================

using Microsoft.CodeAnalysis;

namespace qyl.instrumentation.generators.Diagnostics;

/// <summary>
///     Diagnostic descriptors for the GenAI interceptor generator.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "qyl.instrumentation";

    /// <summary>
    ///     QYL0001: Interface call cannot be intercepted.
    /// </summary>
    public static readonly DiagnosticDescriptor InterfaceCallCannotBeIntercepted = new(
        "QYL0001",
        "Interface call cannot be intercepted",
        "The call to '{0}' through interface '{1}' cannot be intercepted. Interceptors only work on concrete type calls.",
        Category,
        DiagnosticSeverity.Info,
        true,
        "C# interceptors cannot intercept calls made through interfaces because the actual implementation is determined at runtime.");

    /// <summary>
    ///     QYL0002: Virtual method call cannot be intercepted.
    /// </summary>
    public static readonly DiagnosticDescriptor VirtualCallCannotBeIntercepted = new(
        "QYL0002",
        "Virtual method call cannot be intercepted",
        "The virtual call to '{0}' cannot be intercepted",
        Category,
        DiagnosticSeverity.Info,
        true,
        "C# interceptors cannot intercept virtual method calls because the actual implementation is determined by the runtime type.");

    /// <summary>
    ///     QYL0003: Call in compiled assembly cannot be intercepted.
    /// </summary>
    public static readonly DiagnosticDescriptor CompiledAssemblyCannotBeIntercepted = new(
        "QYL0003",
        "Call in compiled assembly cannot be intercepted",
        "The call to '{0}' is in a compiled assembly",
        Category,
        DiagnosticSeverity.Info,
        true,
        "C# interceptors only work on source code you compile.");

    /// <summary>
    ///     QYL1001: Successfully intercepted GenAI call.
    /// </summary>
    public static readonly DiagnosticDescriptor GenAiCallIntercepted = new(
        "QYL1001",
        "GenAI call intercepted",
        "The call to '{0}' will be instrumented with OTel GenAI semantic conventions",
        Category,
        DiagnosticSeverity.Info,
        true,
        "This GenAI SDK call will be wrapped with OpenTelemetry instrumentation.");
}

using System.Diagnostics;
using Microsoft.Shared.Diagnostics;

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
/// Marks a class or method for automatic OpenTelemetry tracing instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a class, all public methods are traced automatically.
/// Use <see cref="NoTraceAttribute"/> to opt-out specific methods.
/// Method-level attributes override class-level settings.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Traced("MyApp.Orders")]  // Class-level: all public methods traced
/// public class OrderService
/// {
///     public async Task&lt;Order&gt; GetOrder([TracedTag] string id) { }  // Traced
///
///     [NoTrace]
///     public void HelperMethod() { }  // Not traced
///
///     [Traced(SpanName = "custom.operation")]  // Override
///     public void CustomOperation() { }
/// }
/// </code>
/// </para>
/// </remarks>
/// <param name="activitySourceName">
/// The name of the ActivitySource to use for creating spans.
/// This should match a registered ActivitySource in your application.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TracedAttribute(string activitySourceName) : Attribute
{
    /// <summary>
    /// Gets the name of the ActivitySource to use for creating spans.
    /// </summary>
    public string ActivitySourceName { get; } = Throw.IfNull(activitySourceName);

    /// <summary>
    /// Gets or sets the span name. If not specified, defaults to the method name.
    /// </summary>
    public string? SpanName { get; set; }

    /// <summary>
    /// Gets or sets the span kind.
    /// </summary>
    /// <value>Defaults to <see cref="ActivityKind.Internal"/>.</value>
    public ActivityKind Kind { get; set; } = ActivityKind.Internal;
}

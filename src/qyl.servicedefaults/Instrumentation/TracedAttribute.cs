namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a class or method for automatic OpenTelemetry tracing instrumentation.
/// </summary>
/// <remarks>
///     <para>
///         When applied to a class, all public methods are traced automatically.
///         Use <see cref="NoTraceAttribute"/> to opt-out specific methods.
///         Method-level attributes override class-level settings.
///     </para>
///     <para>
///         Example usage:
///         <code>
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
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TracedAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TracedAttribute" /> class.
    /// </summary>
    /// <param name="activitySourceName">
    ///     The name of the ActivitySource to use for creating spans.
    ///     This should match a registered ActivitySource in your application.
    /// </param>
    public TracedAttribute(string activitySourceName) =>
        ActivitySourceName = activitySourceName ?? throw new ArgumentNullException(nameof(activitySourceName));

    /// <summary>
    ///     Gets the name of the ActivitySource to use for creating spans.
    /// </summary>
    public string ActivitySourceName { get; }

    /// <summary>
    ///     Gets or sets the span name. If not specified, defaults to the method name.
    /// </summary>
    public string? SpanName { get; set; }

    /// <summary>
    ///     Gets or sets the span kind. Defaults to <see cref="System.Diagnostics.ActivityKind.Internal" />.
    /// </summary>
    public System.Diagnostics.ActivityKind Kind { get; set; } = System.Diagnostics.ActivityKind.Internal;
}

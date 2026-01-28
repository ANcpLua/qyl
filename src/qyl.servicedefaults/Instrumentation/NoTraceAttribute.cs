namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
/// Excludes a method from automatic tracing when the containing class has <see cref="TracedAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute to opt-out specific methods from class-level tracing.
/// This is useful for helper methods, internal operations, or methods that
/// should not create spans.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Traced("MyApp.Orders")]
/// public class OrderService
/// {
///     public async Task&lt;Order&gt; GetOrder(string id) { }  // Traced
///
///     [NoTrace]
///     public void HelperMethod() { }  // Not traced
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NoTraceAttribute : Attribute;

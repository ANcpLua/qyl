namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
/// Marks a parameter to be recorded as a metric tag.
/// </summary>
/// <remarks>
/// <para>
/// Used with <see cref="CounterAttribute"/> or <see cref="HistogramAttribute"/>
/// to automatically add tags to metric recordings.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Counter("orders.created")]
/// public static partial void RecordOrderCreated(
///     [Tag("order.status")] string status,
///     [Tag("order.region")] string region);
/// </code>
/// </para>
/// </remarks>
/// <param name="name">The tag name (e.g., "order.status").</param>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TagAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name { get; } = Throw.IfNull(name);
}

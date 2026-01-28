namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a parameter to be recorded as a metric tag.
/// </summary>
/// <remarks>
///     <para>
///         Used with <see cref="CounterAttribute"/> or <see cref="HistogramAttribute"/>
///         to automatically add tags to metric recordings.
///     </para>
///     <para>
///         Example usage:
///         <code>
/// [Counter("orders.created")]
/// public static partial void RecordOrderCreated(
///     [Tag("order.status")] string status,
///     [Tag("order.region")] string region);
/// </code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TagAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TagAttribute" /> class.
    /// </summary>
    /// <param name="name">The tag name (e.g., "order.status").</param>
    public TagAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    ///     Gets the tag name.
    /// </summary>
    public string Name { get; }
}

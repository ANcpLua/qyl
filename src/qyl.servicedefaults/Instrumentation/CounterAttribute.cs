namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial method as a counter metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates a Counter instrument and implements the method
///         to record increments with tags from parameters marked with <see cref="TagAttribute" />.
///     </para>
///     <para>
///         Example:
///         <code>
/// [Counter("orders.created", Unit = "{order}", Description = "Orders created")]
/// public static partial void RecordOrderCreated([Tag("status")] string status);
/// </code>
///     </para>
/// </remarks>
/// <param name="name">The metric name (e.g., "orders.created").</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CounterAttribute(string name) : Attribute
{
    /// <summary>
    ///     Gets the metric name.
    /// </summary>
    public string Name { get; } = Guard.NotNull(name);

    /// <summary>
    ///     Gets or sets the unit of measurement.
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    ///     Gets or sets the metric description.
    /// </summary>
    public string? Description { get; set; }
}

namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial method as a counter metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates a Counter instrument and implements the method
///         to record increments with tags from parameters marked with [Tag].
///     </para>
///     <para>
///         Example usage:
///         <code>
/// [Counter("orders.created", Unit = "{order}", Description = "Orders created")]
/// public static partial void RecordOrderCreated([Tag("status")] string status);
/// </code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CounterAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CounterAttribute" /> class.
    /// </summary>
    /// <param name="name">The metric name (e.g., "orders.created").</param>
    public CounterAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    ///     Gets the metric name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets or sets the unit of measurement.
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    ///     Gets or sets the metric description.
    /// </summary>
    public string? Description { get; set; }
}

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     Marks a partial method as a counter metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates a Counter instrument and implements the method
///         to record increments or explicit deltas with tags from parameters marked with
///         <see cref="TagAttribute" />.
///     </para>
///     <para>
///         When the first non-tag parameter is numeric, its value is passed to
///         <c>Counter&lt;long&gt;.Add(...)</c>. If no non-tag parameter exists, the generator
///         emits <c>Add(1)</c> for simple occurrence counters.
///     </para>
///     <para>
///         Examples:
///         <code>
/// [Counter("orders.created", Unit = "{order}", Description = "Orders created")]
/// public static partial void RecordOrderCreated([Tag("status")] string status);
/// 
/// [Counter("orders.retried", Unit = "{retry}", Description = "Retries processed")]
/// public static partial void RecordRetries(long value, [Tag("status")] string status);
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

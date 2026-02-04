namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial method as a histogram metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates a Histogram instrument and implements the method
///         to record values with tags from parameters marked with <see cref="TagAttribute" />.
///     </para>
///     <para>
///         The first parameter (not marked with <see cref="TagAttribute" />) is the value to record.
///     </para>
///     <para>
///         Example:
///         <code>
/// [Histogram("order.processing.duration", Unit = "ms")]
/// public static partial void RecordProcessingDuration(
///     double duration,
///     [Tag("order.type")] string orderType);
/// </code>
///     </para>
/// </remarks>
/// <param name="name">The metric name (e.g., "order.processing.duration").</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute(string name) : Attribute
{
    /// <summary>
    ///     Gets the metric name.
    /// </summary>
    public string Name { get; } = Throw.IfNull(name);

    /// <summary>
    ///     Gets or sets the unit of measurement.
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    ///     Gets or sets the metric description.
    /// </summary>
    public string? Description { get; set; }
}

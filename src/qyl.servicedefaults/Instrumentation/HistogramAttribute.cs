namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial method as a histogram metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates a Histogram instrument and implements the method
///         to record values with tags from parameters marked with [Tag].
///     </para>
///     <para>
///         The first parameter (not marked with [Tag]) is the value to record.
///     </para>
///     <para>
///         Example usage:
///         <code>
/// [Histogram("order.processing.duration", Unit = "ms")]
/// public static partial void RecordProcessingDuration(
///     double duration,
///     [Tag("order.type")] string orderType);
/// </code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HistogramAttribute" /> class.
    /// </summary>
    /// <param name="name">The metric name (e.g., "order.processing.duration").</param>
    public HistogramAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

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

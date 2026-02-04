namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial method as a gauge metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates an ObservableGauge instrument with a stored value.
///         Calling the method updates the stored value, which the gauge reports on collection.
///     </para>
///     <para>
///         The first parameter (not marked with <see cref="TagAttribute" />) is the value to store.
///         Remaining parameters become metric tags.
///     </para>
///     <para>
///         Example:
///         <code>
/// [Gauge("system.memory.usage", Unit = "By", Description = "Current memory usage")]
/// public static partial void RecordMemoryUsage(long bytes, [Tag("process")] string process);
/// </code>
///     </para>
/// </remarks>
/// <param name="name">The metric name (e.g., "system.memory.usage").</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GaugeAttribute(string name) : Attribute
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

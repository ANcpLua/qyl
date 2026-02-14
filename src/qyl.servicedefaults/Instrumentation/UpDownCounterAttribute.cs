namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial method as an up-down counter metric recorder.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates an UpDownCounter instrument and implements the method
///         to record increments/decrements with tags from parameters marked with <see cref="TagAttribute" />.
///     </para>
///     <para>
///         The first parameter (not marked with <see cref="TagAttribute" />) is the delta value (long).
///         Positive values increment, negative values decrement.
///     </para>
///     <para>
///         Example:
///         <code>
/// [UpDownCounter("resources.active", Unit = "{resource}", Description = "Currently active resources")]
/// public static partial void UpdateActiveResources(long delta, [Tag("name")] string name);
/// </code>
///     </para>
/// </remarks>
/// <param name="name">The metric name (e.g., "resources.active").</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UpDownCounterAttribute(string name) : Attribute
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

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a parameter to be recorded as a metric tag.
/// </summary>
/// <remarks>
///     <para>
///         Used with <see cref="CounterAttribute" />, <see cref="HistogramAttribute" />,
///         <see cref="GaugeAttribute" />, or <see cref="UpDownCounterAttribute" /> to
///         automatically add dimensions to metric recordings.
///     </para>
///     <para>
///         Example:
///         <code>
/// [Counter("orders.created")]
/// public static partial void RecordOrderCreated(
///     [Tag, OTel("order.status")] string status,
///     [Tag("order.region")] string region);
/// </code>
///     </para>
/// </remarks>
/// <param name="name">
///     Optional tag name. When omitted, the generator falls back to <see cref="OTelAttribute" />
///     or the parameter name.
/// </param>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TagAttribute(string? name = null) : Attribute
{
    /// <summary>
    ///     Gets the tag name if one was specified explicitly.
    /// </summary>
    public string? Name { get; } = name;
}

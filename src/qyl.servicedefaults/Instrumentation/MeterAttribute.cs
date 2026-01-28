using Microsoft.Shared.Diagnostics;

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
/// Marks a partial class for automatic meter instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// The source generator creates a static Meter instance and instrument fields
/// for methods marked with <see cref="CounterAttribute"/> or <see cref="HistogramAttribute"/>.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Meter("MyApp")]
/// public static partial class AppMetrics
/// {
///     [Counter("orders.created")]
///     public static partial void RecordOrderCreated([Tag("status")] string status);
/// }
/// </code>
/// </para>
/// </remarks>
/// <param name="name">The meter name (e.g., "MyApp").</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MeterAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the meter name.
    /// </summary>
    public string Name { get; } = Throw.IfNull(name);

    /// <summary>
    /// Gets or sets the meter version.
    /// </summary>
    public string? Version { get; set; }
}

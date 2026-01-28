namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a partial class for automatic meter instrumentation.
/// </summary>
/// <remarks>
///     <para>
///         The source generator creates a static Meter instance and instrument fields
///         for methods marked with [Counter] or [Histogram].
///     </para>
///     <para>
///         Example usage:
///         <code>
/// [Meter("MyApp")]
/// public static partial class AppMetrics
/// {
///     [Counter("orders.created")]
///     public static partial void RecordOrderCreated([Tag("status")] string status);
/// }
/// </code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MeterAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MeterAttribute" /> class.
    /// </summary>
    /// <param name="name">The meter name (e.g., "MyApp").</param>
    public MeterAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    ///     Gets the meter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets or sets the meter version.
    /// </summary>
    public string? Version { get; set; }
}

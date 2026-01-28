namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a parameter or property to be automatically set as a span tag.
/// </summary>
/// <remarks>
///     <para>
///         Used in conjunction with <see cref="TracedAttribute" /> to automatically
///         add parameter values as tags on the created span.
///     </para>
///     <para>
///         Example usage:
///         <code>
/// [Traced("MyApp.Orders")]
/// public async Task&lt;Order&gt; ProcessOrder(
///     [TracedTag("order.id")] string orderId,
///     [TracedTag("order.amount")] decimal amount)
/// {
///     // orderId and amount are automatically added as span tags
/// }
/// </code>
///     </para>
/// </remarks>
/// <summary>
///     Marks a parameter or property to be automatically set as a span tag.
/// </summary>
/// <remarks>
///     <para>
///         Used in conjunction with <see cref="TracedAttribute" /> to automatically
///         add parameter values as tags on the created span.
///     </para>
///     <para>
///         Example usage:
///         <code>
/// [Traced("MyApp.Orders")]
/// public async Task&lt;Order&gt; ProcessOrder(
///     [TracedTag] string orderId,           // Tag name = "orderId" (from parameter)
///     [TracedTag("order.amount")] decimal amount)  // Tag name = "order.amount" (explicit)
/// {
///     // orderId and amount are automatically added as span tags
/// }
/// </code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class TracedTagAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TracedTagAttribute" /> class.
    ///     The tag name will be derived from the parameter name.
    /// </summary>
    public TracedTagAttribute() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TracedTagAttribute" /> class.
    /// </summary>
    /// <param name="name">The tag name to use in the span (e.g., "order.id").</param>
    public TracedTagAttribute(string name) =>
        Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    ///     Gets the tag name to use in the span.
    ///     If null, the parameter name is used.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether this tag should be skipped if the value is null.
    /// </summary>
    /// <value>Defaults to <c>true</c>.</value>
    public bool SkipIfNull { get; set; } = true;
}

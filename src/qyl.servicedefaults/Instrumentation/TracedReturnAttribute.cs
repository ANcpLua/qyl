namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks the return value of a <see cref="TracedAttribute" />-decorated method to be
///     automatically recorded as a span attribute.
/// </summary>
/// <remarks>
///     <para>
///         Applied on the return value (via <c>[return: TracedReturn(...)]</c>), this attribute
///         instructs the source generator to capture the method's result and add it to the active
///         span as a tag.
///     </para>
///     <para>
///         By default the value is recorded via <c>ToString()</c>. Use the <see cref="Property" />
///         option to capture a specific member instead.
///     </para>
///     <para>
///         Example — capture result directly:
///         <code>
/// [Traced("MyApp.Catalog")]
/// [return: TracedReturn("product.name")]
/// public Product GetProduct(string id) { ... }
///         </code>
///     </para>
///     <para>
///         Example — capture specific member via dotted path:
///         <code>
/// [Traced("MyApp.Catalog")]
/// [return: TracedReturn("product.id", Property = "Id")]
/// public Product GetProduct(string id) { ... }
///
/// // Nested path:
/// [return: TracedReturn("usage.input_tokens", Property = "Usage.InputTokens")]
/// public async Task&lt;ChatResponse&gt; ChatAsync(string prompt) { ... }
///         </code>
///     </para>
/// </remarks>
/// <param name="tagName">The span attribute key (e.g. <c>"product.id"</c>).</param>
[AttributeUsage(AttributeTargets.ReturnValue)]
public sealed class TracedReturnAttribute(string tagName) : Attribute
{
    /// <summary>
    ///     Gets the span attribute key.
    /// </summary>
    public string TagName { get; } = Guard.NotNull(tagName);

    /// <summary>
    ///     Gets or sets an optional dotted member-access path on the return value.
    ///     When <see langword="null" />, <c>ToString()</c> is called on the result.
    /// </summary>
    /// <example><c>Property = "Usage.InputTokens"</c></example>
    public string? Property { get; set; }
}

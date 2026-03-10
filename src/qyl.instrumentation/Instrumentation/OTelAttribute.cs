namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     Supplies the canonical OpenTelemetry semantic-convention key for generated telemetry.
/// </summary>
/// <remarks>
///     <para>
///         This attribute does not emit telemetry on its own. It acts as a naming override for
///         generator-driven capture attributes such as <see cref="TracedTagAttribute" /> and
///         <see cref="TagAttribute" />.
///     </para>
///     <para>
///         Example:
///         <code>
/// [Traced("qyl.chat")]
/// public Task CompleteAsync(
///     [TracedTag, OTel(GenAiRequestAttributes.Model)] string model);
/// </code>
///     </para>
/// </remarks>
/// <param name="name">The OpenTelemetry semantic convention attribute name (e.g., "gen_ai.request.model").</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OTelAttribute(string name) : Attribute
{
    /// <summary>
    ///     Gets the OpenTelemetry semantic convention attribute name.
    /// </summary>
    public string Name { get; } = Guard.NotNull(name);

    /// <summary>
    ///     Gets or sets a value indicating whether this attribute should be skipped if the value is null.
    /// </summary>
    /// <value>Defaults to <see langword="true" />.</value>
    public bool SkipIfNull { get; set; } = true;
}

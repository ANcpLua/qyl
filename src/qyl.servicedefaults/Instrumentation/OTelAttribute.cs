namespace Qyl.ServiceDefaults.AspNetCore.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a property or parameter with an OpenTelemetry semantic convention attribute name.
/// </summary>
/// <remarks>
///     <para>
///         Used by source generators to automatically emit Activity.SetTag() calls.
///     </para>
///     <para>
///         Example usage:
///         <code>
/// public record ChatRequest(
///     [OTel(GenAiRequestAttributes.Model)] string Model,
///     [OTel(GenAiRequestAttributes.MaxTokens)] int? MaxTokens);
/// </code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OTelAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OTelAttribute" /> class.
    /// </summary>
    /// <param name="name">The OpenTelemetry semantic convention attribute name (e.g., "gen_ai.request.model").</param>
    public OTelAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    ///     Gets the OpenTelemetry semantic convention attribute name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether this attribute should be skipped if the value is null.
    /// </summary>
    /// <value>Defaults to <c>true</c>.</value>
    public bool SkipIfNull { get; set; } = true;
}

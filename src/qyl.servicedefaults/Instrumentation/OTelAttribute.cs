using Microsoft.Shared.Diagnostics;

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
/// Marks a property or parameter with an OpenTelemetry semantic convention attribute name.
/// </summary>
/// <remarks>
/// <para>
/// Used by source generators to automatically emit <c>Activity.SetTag()</c> calls.
/// </para>
/// <para>
/// Example:
/// <code>
/// public record ChatRequest(
///     [OTel(GenAiRequestAttributes.Model)] string Model,
///     [OTel(GenAiRequestAttributes.MaxTokens)] int? MaxTokens);
/// </code>
/// </para>
/// </remarks>
/// <param name="name">The OpenTelemetry semantic convention attribute name (e.g., "gen_ai.request.model").</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OTelAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the OpenTelemetry semantic convention attribute name.
    /// </summary>
    public string Name { get; } = Throw.IfNull(name);

    /// <summary>
    /// Gets or sets a value indicating whether this attribute should be skipped if the value is null.
    /// </summary>
    /// <value>Defaults to <see langword="true"/>.</value>
    public bool SkipIfNull { get; set; } = true;
}

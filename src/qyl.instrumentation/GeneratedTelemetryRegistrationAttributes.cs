namespace Qyl.Instrumentation;

/// <summary>
///     Marks an assembly as producing generated ActivitySource names that qyl should auto-register.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedActivitySourceAttribute(string name) : Attribute
{
    /// <summary>
    ///     Gets the generated ActivitySource name.
    /// </summary>
    public string Name { get; } = Guard.NotNull(name);
}

/// <summary>
///     Marks an assembly as producing generated Meter names that qyl should auto-register.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedMeterAttribute(string name) : Attribute
{
    /// <summary>
    ///     Gets the generated Meter name.
    /// </summary>
    public string Name { get; } = Guard.NotNull(name);
}

/// <summary>
///     Marks an assembly as declaring a compile-time capability that qyl registers
///     as an OTel Resource attribute for fleet-wide topology discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedCapabilityAttribute(string kind, string value) : Attribute
{
    /// <summary>
    ///     The capability kind (e.g. "agent", "genai.provider", "genai.model", "genai.operation").
    /// </summary>
    public string Kind { get; } = Guard.NotNull(kind);

    /// <summary>
    ///     The capability value (e.g. agent name, provider ID, model name).
    /// </summary>
    public string Value { get; } = Guard.NotNull(value);
}

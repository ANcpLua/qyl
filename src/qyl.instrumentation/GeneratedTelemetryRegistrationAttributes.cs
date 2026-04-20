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


namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class OTelAttribute(string name) : Attribute
{
    public string Name { get; } = Guard.NotNull(name);

    public bool SkipIfNull { get; set; } = true;
}

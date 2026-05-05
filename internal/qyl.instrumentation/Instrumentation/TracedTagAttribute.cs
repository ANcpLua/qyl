namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class TracedTagAttribute : Attribute
{
    public TracedTagAttribute()
    {
    }

    public TracedTagAttribute(string name) => Name = Guard.NotNull(name);

    public string? Name { get; }

    public bool SkipIfNull { get; set; } = true;

    public bool SkipIfDefault { get; set; }
}

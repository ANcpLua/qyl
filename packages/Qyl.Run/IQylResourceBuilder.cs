
using System.Collections.ObjectModel;

namespace Qyl.Run;

public interface IQylResourceBuilder
{
    QylAppBuilder App { get; }
    QylResource Resource { get; }

    IQylResourceBuilder Update(Func<QylResource, QylResource> mutate);
}

internal sealed class QylResourceBuilder(
    QylAppBuilder app,
    QylResource resource,
    Action<QylResource, QylResource> replace) : IQylResourceBuilder
{
    public QylAppBuilder App { get; } = app;

    public QylResource Resource { get; private set; } = resource;

    public IQylResourceBuilder Update(Func<QylResource, QylResource> mutate)
    {
        var updated = mutate(Resource);
        replace(Resource, updated);
        Resource = updated;
        return this;
    }
}

public static class QylResourceBuilderExtensions
{
    public static IQylResourceBuilder WaitFor(this IQylResourceBuilder builder, params IQylResourceBuilder[] others)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(others);
        if (others.Length == 0) return builder;

        var merged = new List<string>(builder.Resource.WaitForNames);
        foreach (var other in others)
        {
            if (!merged.Contains(other.Resource.Name, StringComparer.Ordinal))
            {
                merged.Add(other.Resource.Name);
            }
        }

        return builder.Update(r => r with { WaitForNames = new ReadOnlyCollection<string>(merged) });
    }

    public static IQylResourceBuilder WithCollector(this IQylResourceBuilder builder, IQylResourceBuilder collector)
    {
        return builder.WaitFor(collector);
    }
}

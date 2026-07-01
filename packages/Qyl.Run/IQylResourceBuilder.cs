
using System.Collections.ObjectModel;
using ANcpLua.Roslyn.Utilities;

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
    extension(IQylResourceBuilder builder)
    {
        public IQylResourceBuilder WaitFor(params IQylResourceBuilder[] others)
        {
            Guard.NotNull(builder);
            Guard.NotNull(others);
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

        // Inject the referenced resources' resolved endpoints into this resource's environment once they are
        // ready (env-based service discovery). Referencing implies waiting — the endpoint must exist first.
        public IQylResourceBuilder WithReference(params IQylResourceBuilder[] others)
        {
            Guard.NotNull(builder);
            Guard.NotNull(others);
            if (others.Length == 0) return builder;

            var merged = new List<string>(builder.Resource.References);
            foreach (var other in others)
            {
                if (!merged.Contains(other.Resource.Name, StringComparer.Ordinal))
                {
                    merged.Add(other.Resource.Name);
                }
            }

            return builder
                .WaitFor(others)
                .Update(r => r with { References = new ReadOnlyCollection<string>(merged) });
        }

        public IQylResourceBuilder WithCollector(IQylResourceBuilder collector) => builder.WithReference(collector);
    }
}

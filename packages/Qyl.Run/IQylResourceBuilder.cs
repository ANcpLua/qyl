// Copyright (c) 2025-2026 ancplua

using System.Collections.ObjectModel;

namespace Qyl.Run;

/// <summary>
/// Thin handle on a resource that lets callers chain <c>.WithCollector(...)</c>, <c>.WaitFor(...)</c>
/// etc. back into the owning <see cref="QylAppBuilder"/>. Records are immutable, so mutations go
/// through <see cref="Update"/> which atomically swaps the entry in the builder's resource list.
/// </summary>
public interface IQylResourceBuilder
{
    QylAppBuilder App { get; }
    QylResource Resource { get; }

    /// <summary>Atomically replace the underlying resource with a mutated copy.</summary>
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

/// <summary>Fluent extensions — wrap the <see cref="IQylResourceBuilder.Update"/> hook.</summary>
public static class QylResourceBuilderExtensions
{
    /// <summary>
    /// Declare a start-order dependency. The orchestrator won't spawn <paramref name="builder"/>
    /// until every <paramref name="others"/> has reached <see cref="ResourceLifecycle.Ready"/>.
    /// </summary>
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

    /// <summary>Dashboard sugar — aliases <see cref="WaitFor"/> to communicate intent.</summary>
    public static IQylResourceBuilder WithCollector(this IQylResourceBuilder builder, IQylResourceBuilder collector)
        => builder.WaitFor(collector);
}

// Copyright (c) 2025-2026 ancplua

using Aspire.Hosting.ApplicationModel;

namespace Qyl.Fleet.Hosting;

/// <summary>
/// Tracks one collector backend registered under a <see cref="QylDashboardResource"/>.
/// Emitted by <c>WithCollector&lt;TSource&gt;</c>.
/// </summary>
public sealed class QylCollectorAnnotation : IResourceAnnotation
{
    /// <param name="collector">The collector resource (must expose an <c>http</c> endpoint).</param>
    /// <param name="idPrefix">
    /// Prefix prepended to entity IDs from this backend (<c>{prefix}/{id}</c>) to keep the
    /// aggregated listing unique. Defaults to the collector resource name.
    /// </param>
    /// <param name="info">Declarative metadata; the dashboard uses this to build the fleet listing without probing the backend.</param>
    public QylCollectorAnnotation(IResource collector, string? idPrefix, QylCollectorInfo info)
    {
        ArgumentNullException.ThrowIfNull(collector);
        ArgumentNullException.ThrowIfNull(info);

        Collector = collector;
        IdPrefix = idPrefix;
        Info = info;
    }

    public IResource Collector { get; }

    public string? IdPrefix { get; }

    public QylCollectorInfo Info { get; }
}

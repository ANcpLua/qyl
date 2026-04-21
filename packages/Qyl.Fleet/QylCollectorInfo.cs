// Copyright (c) 2025-2026 ancplua

namespace Qyl.Fleet.Hosting;

/// <summary>
/// Declarative metadata describing one <c>qyl.collector</c> backend in the dashboard's fleet.
/// Declared at the AppHost layer so the aggregator doesn't have to probe each backend to build
/// the fleet listing.
/// </summary>
/// <param name="Id">The stable identifier. Defaults to the resource name when left blank.</param>
/// <param name="Description">Human-readable description surfaced in the fleet picker.</param>
public record QylCollectorInfo(string Id, string? Description = null)
{
    /// <summary>Display name for the collector. Defaults to <see cref="Id"/>.</summary>
    public string Name { get; init; } = Id;

    /// <summary>Deployment environment tag (e.g. <c>dev</c>, <c>staging</c>, <c>prod</c>).</summary>
    public string Environment { get; init; } = "dev";

    /// <summary>Region tag used for routing-by-geo hints in the dashboard.</summary>
    public string? Region { get; init; }
}

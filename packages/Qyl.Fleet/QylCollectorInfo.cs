// Copyright (c) 2025-2026 ancplua

namespace Qyl.Fleet.Hosting;

/// <summary>
/// Metadata for one <c>qyl.collector</c> backend in the dashboard's fleet. Returned on the
/// <c>/api/v1/fleet</c> endpoint so the dashboard can build a collector picker without
/// probing each backend.
/// </summary>
/// <param name="Id">The stable identifier used as the URL prefix (<c>/api/v1/{id}/...</c>).</param>
/// <param name="Endpoint">The collector's base URL. Reads + writes are forwarded to it.</param>
/// <param name="Description">Human-readable description surfaced in the fleet picker.</param>
public sealed record QylCollectorInfo(string Id, Uri Endpoint, string? Description = null)
{
    /// <summary>Display name for the collector. Defaults to <see cref="Id"/>.</summary>
    public string Name { get; init; } = Id;

    /// <summary>Deployment environment tag (e.g. <c>dev</c>, <c>staging</c>, <c>prod</c>).</summary>
    public string Environment { get; init; } = "dev";

    /// <summary>Region tag used for routing-by-geo hints in the dashboard.</summary>
    public string? Region { get; init; }
}

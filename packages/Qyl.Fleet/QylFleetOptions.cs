// Copyright (c) 2025-2026 ancplua

namespace Qyl.Fleet.Hosting;

/// <summary>Options for <c>AddQylFleet()</c>. Populated via the fluent builder in the same call.</summary>
public sealed class QylFleetOptions
{
    /// <summary>Port the aggregator listens on. <c>0</c> (default) picks a free port.</summary>
    public int Port { get; set; }

    /// <summary>Bind host. Defaults to loopback to match the DevUI sibling pattern.</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>Registered collector backends, keyed by <see cref="QylCollectorInfo.Id"/>.</summary>
    public IList<QylCollectorInfo> Collectors { get; } = [];
}

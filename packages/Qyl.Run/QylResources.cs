// Copyright (c) 2025-2026 ancplua

using System.Collections.ObjectModel;

namespace Qyl.Run;

/// <summary>
/// One service instance inside a qyl.run app. The orchestrator spawns it, polls its health
/// endpoint, and surfaces its state in the Spectre live table. Immutable; the resource is
/// constructed at builder time and the mutable runtime state lives on
/// <see cref="QylResourceState"/>.
/// </summary>
public sealed record QylResource
{
    /// <summary>Stable identifier — unique within the app; shown as the row label in the CLI.</summary>
    public required string Name { get; init; }

    /// <summary>Kind tag (one of <see cref="QylConstants.ResourceKinds"/>).</summary>
    public required string Kind { get; init; }

    /// <summary>Environment tag (one of <see cref="QylConstants.Environments"/>).</summary>
    public required string Environment { get; init; }

    /// <summary>Pinned port (when set) or <see cref="QylConstants.Ports.DynamicAllocation"/> for port-0.</summary>
    public required int Port { get; init; }

    /// <summary>Launch recipe the orchestrator hands to <c>Process.Start</c>.</summary>
    public required QylLaunchSpec Launch { get; init; }

    /// <summary>Other resources that must reach <see cref="ResourceLifecycle.Ready"/> first.</summary>
    public ReadOnlyCollection<string> WaitForNames { get; init; } = ReadOnlyCollection<string>.Empty;

    /// <summary>Optional human-readable description, surfaced on <c>/api/v1/fleet</c>.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Everything the orchestrator needs to spawn one subprocess. Env vars are merged on top of
/// the parent process's environment; <see cref="QylConstants.Env.AspNetCoreUrls"/> is injected
/// automatically by the orchestrator after port allocation.
/// </summary>
public sealed record QylLaunchSpec
{
    public required string Executable { get; init; }
    public ReadOnlyCollection<string> Args { get; init; } = ReadOnlyCollection<string>.Empty;
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
    public string? WorkingDirectory { get; init; }
    public string HealthPath { get; init; } = QylConstants.Routes.Health;
}

/// <summary>Lifecycle states surfaced in the live table.</summary>
public enum ResourceLifecycle
{
    Pending,
    Starting,
    Ready,
    Stopping,
    Stopped,
    Failed,
}

/// <summary>
/// Mutable runtime snapshot emitted by the orchestrator on every state change. The orchestrator
/// stamps <see cref="Timestamp"/> from an injected <see cref="TimeProvider"/>; never mutate
/// after construction.
/// </summary>
public sealed record QylResourceState
{
    public required string Name { get; init; }
    public required ResourceLifecycle Lifecycle { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public int? AllocatedPort { get; init; }
    public Uri? Endpoint { get; init; }
    public string? LastError { get; init; }
}

// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9056740+00:00
//     Models for Qyl.Domains.Ops.Deployment
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Ops.Deployment;

/// <summary>Complete deployment record</summary>
public sealed record DeploymentEntity
{
    /// <summary>Deployment ID</summary>
    [JsonPropertyName("deployment.id")]
    public required string DeploymentId { get; init; }

    /// <summary>Service name</summary>
    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("service.version")]
    public required global::Qyl.Common.SemVer ServiceVersion { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("environment")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentEnvironment Environment { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }

    /// <summary>Strategy</summary>
    [JsonPropertyName("strategy")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentStrategy Strategy { get; init; }

    /// <summary>Start time</summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>End time</summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>Duration in seconds</summary>
    [JsonPropertyName("duration_s")]
    public global::Qyl.Common.DurationS? DurationS { get; init; }

    /// <summary>Deployed by (user/system)</summary>
    [JsonPropertyName("deployed_by")]
    public string? DeployedBy { get; init; }

    /// <summary>Git commit SHA</summary>
    [JsonPropertyName("git_commit")]
    public string? GitCommit { get; init; }

    /// <summary>Git branch</summary>
    [JsonPropertyName("git_branch")]
    public string? GitBranch { get; init; }

    /// <summary>Previous version</summary>
    [JsonPropertyName("previous_version")]
    public global::Qyl.Common.SemVer? PreviousVersion { get; init; }

    /// <summary>Rollback target (if rolled back)</summary>
    [JsonPropertyName("rollback_target")]
    public string? RollbackTarget { get; init; }

    /// <summary>Replica count</summary>
    [JsonPropertyName("replica_count")]
    public int? ReplicaCount { get; init; }

    /// <summary>Healthy replica count</summary>
    [JsonPropertyName("healthy_replicas")]
    public int? HealthyReplicas { get; init; }

    /// <summary>Error message (if failed)</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

}

/// <summary>Deployment lifecycle event</summary>
public sealed record DeploymentEvent
{
    /// <summary>Event name</summary>
    [JsonPropertyName("event.name")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentEventName EventName { get; init; }

    /// <summary>Deployment ID</summary>
    [JsonPropertyName("deployment.id")]
    public required string DeploymentId { get; init; }

    /// <summary>Service name</summary>
    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    /// <summary>Environment</summary>
    [JsonPropertyName("deployment.environment.name")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentEnvironment DeploymentEnvironmentName { get; init; }

    /// <summary>Status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.Ops.Deployment.DeploymentStatus Status { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}


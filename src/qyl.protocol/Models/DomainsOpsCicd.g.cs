// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9056520+00:00
//     Models for Qyl.Domains.Ops.Cicd
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Ops.Cicd;

/// <summary>Pipeline run event</summary>
public sealed record PipelineRunEvent
{
    /// <summary>Event name</summary>
    [JsonPropertyName("event.name")]
    public required global::Qyl.Domains.Ops.Cicd.CicdEventName EventName { get; init; }

    /// <summary>Pipeline name</summary>
    [JsonPropertyName("cicd.pipeline.name")]
    public required string CicdPipelineName { get; init; }

    /// <summary>Pipeline run ID</summary>
    [JsonPropertyName("cicd.pipeline.run.id")]
    public required string CicdPipelineRunId { get; init; }

    /// <summary>Pipeline status</summary>
    [JsonPropertyName("status")]
    public required global::Qyl.Domains.Ops.Cicd.CicdPipelineStatus Status { get; init; }

    /// <summary>CI/CD system</summary>
    [JsonPropertyName("system")]
    public required global::Qyl.Domains.Ops.Cicd.CicdSystem System { get; init; }

    /// <summary>Trigger type</summary>
    [JsonPropertyName("trigger_type")]
    public global::Qyl.Domains.Ops.Cicd.CicdTriggerType? TriggerType { get; init; }

    /// <summary>Git branch</summary>
    [JsonPropertyName("vcs.repository.ref.name")]
    public string? VcsRepositoryRefName { get; init; }

    /// <summary>Git commit SHA</summary>
    [JsonPropertyName("vcs.repository.ref.revision")]
    public string? VcsRepositoryRefRevision { get; init; }

    /// <summary>Duration in seconds</summary>
    [JsonPropertyName("duration_s")]
    public global::Qyl.Common.DurationS? DurationS { get; init; }

    /// <summary>Event timestamp</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

}


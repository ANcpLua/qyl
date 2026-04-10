// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-04-09T04:24:58.6008800+00:00
//     Models for Qyl.Domains.Identity
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.Identity;

/// <summary>Service dependency map</summary>
public sealed record ServiceDependency
{
    /// <summary>Source service</summary>
    [JsonPropertyName("source_service")]
    public required string SourceService { get; init; }

    /// <summary>Target service</summary>
    [JsonPropertyName("target_service")]
    public required string TargetService { get; init; }

    /// <summary>Request count</summary>
    [JsonPropertyName("request_count")]
    public required long RequestCount { get; init; }

    /// <summary>Error rate</summary>
    [JsonPropertyName("error_rate")]
    public required double ErrorRate { get; init; }

    /// <summary>Average latency in milliseconds</summary>
    [JsonPropertyName("avg_latency_ms")]
    public required double AvgLatencyMs { get; init; }

}


// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9053280+00:00
//     Models for Qyl.Common
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Common;

/// <summary>Key-value attribute pair following OTel conventions</summary>
public sealed record Attribute
{
    /// <summary>Attribute key (dot-separated namespace)</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>Attribute value</summary>
    [JsonPropertyName("value")]
    public required global::System.Text.Json.Nodes.JsonNode Value { get; init; }

}

/// <summary>Instrumentation scope identifying the library/component emitting telemetry</summary>
public sealed record InstrumentationScope
{
    /// <summary>Name of the instrumentation scope (library name)</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Version of the instrumentation scope</summary>
    [JsonPropertyName("version")]
    public global::Qyl.Common.SemVer? Version { get; init; }

    /// <summary>Additional attributes for the scope</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public global::Qyl.Common.Count? DroppedAttributesCount { get; init; }

}


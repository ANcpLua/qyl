// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    schema/generated/openapi.yaml
//     Generated: 2026-01-13T06:07:10.6797040+00:00
//     Models for Qyl.Api
// =============================================================================
// To modify: update TypeSpec in schema/ then run: nuke Generate
// =============================================================================

#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Api;

/// <summary>Error response</summary>
public sealed record ApiError
{
    /// <summary>Error code</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; }

    /// <summary>Error message</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; }

    /// <summary>Additional details</summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }

}


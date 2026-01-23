// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9053480+00:00
//     Models for Qyl.Common.Errors
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Common.Errors;

/// <summary>Conflict - resource state conflict (409)</summary>
public sealed record ConflictError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The conflicting resource ID</summary>
    [JsonPropertyName("conflicting_resource")]
    public string? ConflictingResource { get; init; }

}

/// <summary>Forbidden - insufficient permissions (403)</summary>
public sealed record ForbiddenError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Required permission that is missing</summary>
    [JsonPropertyName("required_permission")]
    public string? RequiredPermission { get; init; }

}

/// <summary>Internal server error (500)</summary>
public sealed record InternalServerError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Error code for support reference</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

}

/// <summary>Resource not found (404)</summary>
public sealed record NotFoundError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The type of resource that was not found</summary>
    [JsonPropertyName("resource_type")]
    public string? ResourceType { get; init; }

    /// <summary>The identifier that was not found</summary>
    [JsonPropertyName("resource_id")]
    public string? ResourceId { get; init; }

}

/// <summary>RFC 7807 Problem Details for HTTP APIs</summary>
public sealed record ProblemDetails
{
    /// <summary>A URI reference identifying the problem type</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>A short, human-readable summary of the problem type</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>The HTTP status code (informational only, actual code set by subtype)</summary>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary>A human-readable explanation specific to this occurrence</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    /// <summary>A URI reference identifying the specific occurrence</summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>Timestamp of the error</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

}

/// <summary>Rate limited - too many requests (429)</summary>
public sealed record RateLimitError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

}

/// <summary>Service unavailable (503)</summary>
public sealed record ServiceUnavailableError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Reason for unavailability</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

}

/// <summary>Unauthorized - authentication required (401)</summary>
public sealed record UnauthorizedError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

}

/// <summary>Bad request - validation failed (400)</summary>
public sealed record ValidationError
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>List of validation errors</summary>
    [JsonPropertyName("errors")]
    public required IReadOnlyList<global::Qyl.Common.Errors.ValidationErrorDetail> Errors { get; init; }

}

/// <summary>Individual validation error detail</summary>
public sealed record ValidationErrorDetail
{
    /// <summary>The field/property that failed validation</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>The error message</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>The validation rule that failed</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>The rejected value (if safe to include)</summary>
    [JsonPropertyName("rejected_value")]
    public string? RejectedValue { get; init; }

}


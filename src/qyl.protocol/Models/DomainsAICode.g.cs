// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9053900+00:00
//     Models for Qyl.Domains.AI.Code
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.Domains.AI.Code;

/// <summary>Precise source code location for debugging and tracing</summary>
public sealed record CodeLocation
{
    /// <summary>Source file path</summary>
    [JsonPropertyName("filepath")]
    public required string Filepath { get; init; }

    /// <summary>Line number (1-indexed)</summary>
    [JsonPropertyName("line_number")]
    public required int LineNumber { get; init; }

    /// <summary>Column number (1-indexed)</summary>
    [JsonPropertyName("column_number")]
    public int? ColumnNumber { get; init; }

    /// <summary>Function/method name</summary>
    [JsonPropertyName("function_name")]
    public string? FunctionName { get; init; }

    /// <summary>Class/type name</summary>
    [JsonPropertyName("class_name")]
    public string? ClassName { get; init; }

    /// <summary>Namespace/module</summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

}

/// <summary>Single frame in a call stack</summary>
public sealed record StackFrame
{
    /// <summary>Frame index (0 = top of stack)</summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>Source location</summary>
    [JsonPropertyName("location")]
    public required global::Qyl.Domains.AI.Code.CodeLocation Location { get; init; }

    /// <summary>Whether this is user code (not library/framework)</summary>
    [JsonPropertyName("is_user_code")]
    public bool? IsUserCode { get; init; }

    /// <summary>Assembly/module name</summary>
    [JsonPropertyName("module_name")]
    public string? ModuleName { get; init; }

    /// <summary>Assembly/module version</summary>
    [JsonPropertyName("module_version")]
    public global::Qyl.Common.SemVer? ModuleVersion { get; init; }

    /// <summary>Native/managed indicator</summary>
    [JsonPropertyName("is_native")]
    public bool? IsNative { get; init; }

}

/// <summary>Full stack trace</summary>
public sealed record StackTrace
{
    /// <summary>Stack frames (top to bottom)</summary>
    [JsonPropertyName("frames")]
    public required IReadOnlyList<global::Qyl.Domains.AI.Code.StackFrame> Frames { get; init; }

    /// <summary>Whether the stack was truncated</summary>
    [JsonPropertyName("truncated")]
    public bool? Truncated { get; init; }

    /// <summary>Total frame count before truncation</summary>
    [JsonPropertyName("total_frames")]
    public int? TotalFrames { get; init; }

}


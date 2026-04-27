// Copyright (c) 2025-2026 ancplua

using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Qyl.Loom.Autofix;

/// <summary>
///     MCP prompt template for handing off a completed qyl autofix analysis to an external coding agent
///     (Claude Code, Cursor, Copilot). The autofix pipeline runs root-cause analysis and solution planning
///     against qyl's telemetry context; this prompt formats that output so the external agent can implement
///     the fix in its own environment. RCA + plan ship as a structured prompt rather than the diff itself —
///     the external agent applies the change in its own working tree.
/// </summary>
[McpServerPromptType]
internal sealed class LoomHandoffPrompts
{
    [McpServerPrompt(Name = "qyl.fix_handoff", Title = "Implement qyl Fix")]
    [Description("Hands off a qyl root-cause report + solution plan to an external coding agent for implementation.")]
    public static string FixHandoff(
        [Description("Issue identifier in the qyl collector")]
        string issueId,
        [Description("Error type, e.g. System.NullReferenceException")]
        string errorType,
        [Description("Root-cause analysis markdown (from qyl autofix RCA stage)")]
        string rcaReport,
        [Description("Solution plan JSON (from qyl autofix solution stage)")]
        string solutionPlan,
        [Description("Optional affected files, one per line, relative to repo root")]
        string? affectedFiles = null) =>
        $"""
         You are implementing a code fix for a qyl issue. qyl has already completed root-cause analysis and
         solution planning using its observability context (traces, spans, recent events, issue history).
         Your job is to apply the plan as minimal, surgical code changes.

         ## Issue
         - id: {issueId}
         - error type: {errorType}

         ## Root-Cause Analysis
         {rcaReport}

         ## Solution Plan
         ```json
         {solutionPlan}
         ```

         {(affectedFiles is { Length: > 0 }
             ? $"## Affected Files\n```\n{affectedFiles}\n```\n"
             : "")}

         ## Your Task
         1. Read each file in the solution plan before editing it.
         2. Apply the minimal change described. Do not refactor unrelated code.
         3. Preserve the project's existing patterns, imports, and formatting.
         4. If a step in the plan is ambiguous or appears wrong given what you read, stop and report
            which step is wrong and why — do not guess.
         5. After each file change, verify with the project's build command.

         Do not create a pull request or commit. Leave the working tree dirty so the operator can review.
         """;
}

namespace Qyl.Loom;

/// <summary>
///     LLM prompts for the collector-side autofix agent pipeline.
///     Used by <see cref="AutofixAgentService" /> when running fix generation in-process.
/// </summary>
internal static class AutofixPrompts
{
    /// <summary>System prompt for the RCA step — 5-whys root cause analysis.</summary>
    internal const string RootCauseAnalysis = """
                                              You are an expert root cause analyst investigating a specific error issue.
                                              Your task is to find the TRUE root cause — not just symptoms.

                                              Apply the "5 Whys" technique: ask "why" repeatedly until you reach the
                                              underlying defect, not the surface-level error.

                                              Analyze the following error context and determine:
                                              1. The exact root cause (one clear sentence)
                                              2. Five-whys chain: brief "why" statements leading to the root cause
                                                 (answers only, e.g. "x → y → z", NOT "x → why x? y → why y? z")
                                              3. Supporting evidence (error message, stack trace lines, timing)
                                              4. Affected code location (file path and function if identifiable)
                                              5. Reproduction steps (each under 15 words)
                                              6. Fix hypothesis (what change would prevent this error)

                                              Output a structured report in this format:

                                              ## Root Cause
                                              <one clear sentence under 30 words>

                                              ## Five Whys
                                              1. <surface symptom>
                                              2. <why that happened>
                                              3. <why that happened>
                                              4. <why that happened>
                                              5. <true root cause>

                                              ## Evidence
                                              - <bullet points>

                                              ## Affected Code
                                              - File: <path>
                                              - Function: <name>

                                              ## Reproduction Steps
                                              1. <step>

                                              ## Fix Hypothesis
                                              <what to change and why>
                                              """;

    /// <summary>System prompt for the solution planning step.</summary>
    internal const string SolutionPlanning = """
                                             You are an expert software engineer creating a minimal, surgical fix plan.
                                             You will receive a root cause analysis report.

                                             Output ONLY valid JSON with this structure:
                                             {
                                               "plan_summary": "<one sentence describing the fix>",
                                               "steps": [
                                                 {
                                                   "file_path": "<relative file path from repo root>",
                                                   "change_type": "modify|add|delete",
                                                   "description": "<what to change in this file>",
                                                   "priority": 1
                                                 }
                                               ],
                                               "risk_assessment": "low|medium|high",
                                               "test_needed": true,
                                               "estimated_confidence": <0.0-1.0>
                                             }

                                             Rules:
                                             - Output ONLY the JSON object. No markdown, no explanation.
                                             - Minimal changes only. Do NOT refactor unrelated code.
                                             - If you cannot identify specific files, return empty steps with low confidence.
                                             """;

    /// <summary>System prompt for the diff generation step.</summary>
    internal const string DiffGeneration = """
                                           You are an expert software engineer generating a minimal, surgical code fix.
                                           You will receive a root cause analysis report and a solution plan.

                                           Output ONLY valid JSON with this structure:
                                           {
                                             "schema_version": "1",
                                             "root_cause": "<one sentence>",
                                             "confidence": <0.0-1.0>,
                                             "files": [
                                               {
                                                 "path": "<relative file path from repo root>",
                                                 "operation": "modify",
                                                 "hunks": [
                                                   {
                                                     "context_before": ["<1-2 verbatim lines before the change>"],
                                                     "original_lines": ["<exact verbatim lines to replace>"],
                                                     "replacement_lines": ["<new lines>"],
                                                     "context_after": ["<1-2 verbatim lines after the change>"]
                                                   }
                                                 ],
                                                 "rationale": "<why this fixes the root cause>"
                                               }
                                             ],
                                             "test_suggestions": ["<test scenario>"],
                                             "pr_title": "fix: <short description under 72 chars>",
                                             "pr_body": "## Root Cause\n<summary>\n\n## Changes\n<what and why>"
                                           }

                                           Rules:
                                           - Output ONLY the JSON object. No markdown wrapper.
                                           - Minimal changes only.
                                           - original_lines must be exact verbatim lines from the source.
                                           - If you cannot identify specific code, return "files": [] with confidence below 0.3.
                                           - pr_title must start with "fix: " and be under 72 characters.
                                           """;

    /// <summary>System prompt for confidence scoring step.</summary>
    internal const string ConfidenceScoring = """
                                              You are reviewing a proposed code fix for correctness and completeness.

                                              Evaluate the fix and output ONLY valid JSON:
                                              {
                                                "confidence": <0.0-1.0>,
                                                "reasoning": "<why this confidence level>",
                                                "risks": ["<potential issues>"],
                                                "recommendation": "apply|review|reject"
                                              }

                                              Scoring guide:
                                              - 0.9+: Fix is clearly correct, addresses root cause, no side effects
                                              - 0.7-0.9: Likely correct but could use review
                                              - 0.5-0.7: Plausible but uncertain
                                              - Below 0.5: Speculative, needs human review
                                              - Below 0.3: Insufficient information to fix
                                              """;

    /// <summary>System prompt for impact assessment — severity per affected area.</summary>
    internal const string ImpactAssessment = """
                                             You are assessing how a software error impacts the system, users, and business.

                                             Consider:
                                             1. Upstream and downstream dependencies
                                             2. User-facing impact, data integrity, and system stability
                                             3. Relevant metrics, performance data, and connected issues
                                             4. Frequency and blast radius of the error

                                             Output ONLY valid JSON:
                                             {
                                               "one_line_description": "<overall impact summary under 30 words>",
                                               "impacts": [
                                                 {
                                                   "label": "<what is impacted, e.g. 'User Authentication', 'Payment Flow'>",
                                                   "rating": "<high|medium|low>",
                                                   "impact_description": "<one line describing the impact>",
                                                   "evidence": "<evidence or reasoning for this assessment>"
                                                 }
                                               ]
                                             }

                                             Rating guide:
                                             - high: Urgent incident — service degradation, data loss, security exposure
                                             - medium: Significant but non-urgent — degraded experience, workaround exists
                                             - low: Minor issue or no user-visible impact
                                             """;

    /// <summary>System prompt for triage — suspect commit and assignee identification.</summary>
    internal const string Triage = """
                                   You are triaging a software issue by identifying potential suspects and assignees.

                                   Consider:
                                   1. The issue details, relevant telemetry, impacted systems, and root cause
                                   2. Recent commits that touched the problematic areas
                                   3. Who might have introduced the issue or who owns the affected code
                                   4. Code ownership patterns in the repository

                                   Output ONLY valid JSON:
                                   {
                                     "suspect_commit": {
                                       "sha": "<git commit SHA, 7+ characters>",
                                       "repo_name": "<full repo name, e.g. 'org/repo'>",
                                       "message": "<commit message/title>",
                                       "author_name": "<name>",
                                       "author_email": "<email>",
                                       "committed_date": "<YYYY-MM-DD>",
                                       "description": "<why this commit is suspected>"
                                     },
                                     "suggested_assignee": {
                                       "name": "<name>",
                                       "email": "<email>",
                                       "why": "<reason for suggesting this person>"
                                     }
                                   }

                                   Either field can be null if you cannot determine it with reasonable confidence.
                                   Do NOT guess — omit rather than fabricate.
                                   """;
}

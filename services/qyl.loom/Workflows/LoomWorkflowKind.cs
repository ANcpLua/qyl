// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Workflows;

/// <summary>
///     The four Loom workflow shapes <see cref="LoomWorkflowRouter" /> dispatches between.
///     <see cref="Clarify" /> is returned when the user request is ambiguous — the router
///     surfaces a focused question instead of silently guessing across workflows.
/// </summary>
public enum LoomWorkflowKind
{
    /// <summary>Request did not match any known Loom workflow signal.</summary>
    None = 0,

    /// <summary>Investigate and fix a production issue surfaced by qyl telemetry.</summary>
    FixProductionIssue = 1,

    /// <summary>Process and resolve review-bot PR comments (<c>sentry[bot]</c>, <c>seer-by-sentry[bot]</c>, qyl review bot).</summary>
    ReviewBotPrComments = 2,

    /// <summary>Install and configure the Sentry .NET SDK features (error, tracing, profiling, logging, metrics, crons) in the user's app.</summary>
    SetupDotnetSdk = 3,

    /// <summary>Configure AI agent monitoring for <c>gen_ai.*</c> traffic (LLM calls, agents, tools).</summary>
    SetupAiMonitoring = 4,

    /// <summary>
    ///     Run the headless five-stage autofix pipeline on a qyl issue. Emits a structured
    ///     artifact (fixability score, context, hypothesis, diff, confidence audit). Distinct
    ///     from <see cref="FixProductionIssue" />: that is a human-driven investigation, this
    ///     is a background pipeline that produces a diff + confidence report.
    /// </summary>
    Autofix = 5,

    /// <summary>Ambiguous request — router is asking the caller to pick.</summary>
    Clarify = 99,
}

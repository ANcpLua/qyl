namespace qyl.mcp.Skills;

/// <summary>
///     Categories for grouping MCP tools into logical skill sets.
///     Users enable skills via the QYL_SKILLS environment variable.
/// </summary>
public enum QylSkillKind
{
    /// <summary>Telemetry inspection: spans, traces, logs, errors, sessions, services, GenAI</summary>
    Inspect,

    /// <summary>System health: health checks, storage stats, system context</summary>
    Health,

    /// <summary>Chat analytics: conversations, coverage gaps, satisfaction, user journeys</summary>
    Analytics,

    /// <summary>AI-powered agents: investigate, use_qyl, RCA, summaries</summary>
    Agent,

    /// <summary>Build telemetry: build failures and analysis</summary>
    Build,

    /// <summary>Anomaly detection: z-score analysis, baselines, period comparison</summary>
    Anomaly,

    /// <summary>Loom AI debugging: triage, autofix, code review, agent handoff</summary>
    Loom,

    /// <summary>Interactive ext-apps: trace viewer, error explorer, query studio</summary>
    Apps,

    /// <summary>IDE debugging: breakpoints, stepping, evaluation, variables (proxied from Rider)</summary>
    Debug
}

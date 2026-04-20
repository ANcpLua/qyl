namespace qyl.mcp.Capabilities.Definitions;

[QylCapabilityDefinition("server_introspection",
    Title = "Server Introspection",
    Summary =
        "Discover qyl.mcp capabilities, enabled skill families, and the smallest useful tool chain before starting an investigation.",
    SkillLabel = "core",
    Tags = ["mcp", "planning", "discovery", "server"],
    UseCases =
    [
        "Find the right investigation entrypoint before using a high-cost meta-agent.",
        "Check which observability domains are currently enabled by QYL_SKILLS."
    ],
    PrimaryIdentifiers = ["capability_id", "skill", "tag"],
    ScopingHints = ["Capability discovery is transport-local and does not require collector scoping."],
    EvidenceHints = ["Use the guide to choose the smallest sufficient tool chain before escalating to qyl.use_qyl."],
    RelatedCapabilities = ["trace_investigation", "agentic_investigation"])]
internal sealed class ServerIntrospectionCapability;

[QylCapabilityDefinition("trace_investigation", QylSkillKind.Inspect,
    Title = "Trace Investigation",
    Summary =
        "Search traces, inspect span trees, correlate latency and failure boundaries, and escalate to RCA only after evidence is captured.",
    Tags = ["traces", "spans", "latency", "otel"],
    UseCases =
    [
        "Investigate a slow or failing request path.",
        "Walk from trace-level symptoms down to span-level bottlenecks."
    ],
    PrimaryIdentifiers = ["trace_id", "span_id", "service_name", "duration_ms", "status"],
    ScopingHints =
    [
        "Apply QYL_SERVICE before wide trace searches when the issue is isolated to one service.",
        "Carry trace_id into logs and session tools whenever correlation is available."
    ],
    EvidenceHints =
    [
        "Compare root-span duration with child-span duration before blaming the root service.",
        "Treat high latency and error status as separate signals that may diverge."
    ],
    RelatedCapabilities = ["log_investigation", "error_investigation", "agentic_investigation"])]
internal sealed class TraceInvestigationCapability;

[QylCapabilityDefinition("error_investigation", QylSkillKind.Inspect,
    Title = "Error Investigation",
    Summary =
        "Move from issue-level symptoms into event details, breadcrumbs, attachments, and timelines before mutating triage state.",
    Tags = ["errors", "issues", "events", "breadcrumbs"],
    UseCases =
    [
        "Investigate a recurring application or agent error.",
        "Distinguish one-off failures from clustered issue families."
    ],
    PrimaryIdentifiers = ["issue_id", "event_id", "fingerprint", "service_name"],
    ScopingHints =
    [
        "Use service scoping first if the issue belongs to a known service boundary.",
        "Use issue identifiers rather than free-text error messages once the issue is known."
    ],
    EvidenceHints =
    [
        "Breadcrumbs explain the sequence leading to failure, not just the failure point.",
        "Tag distributions are useful for blast radius estimation across environments, releases, or tenants."
    ],
    RelatedCapabilities = ["trace_investigation", "loom_triage_and_fix"])]
internal sealed class ErrorInvestigationCapability;

[QylCapabilityDefinition("log_investigation", QylSkillKind.Inspect,
    Title = "Log Investigation",
    Summary =
        "Search logs directly or pivot from a trace to its structured logs to confirm request-level evidence and runtime context.",
    Tags = ["logs", "structured", "trace-correlation"],
    UseCases =
    [
        "Confirm runtime context around a trace, issue, or session.",
        "Search for matching log lines across severity and time windows."
    ],
    PrimaryIdentifiers = ["log_id", "trace_id", "span_id", "session_id", "severity"],
    ScopingHints =
    [
        "Trace-correlated log tools are preferable once a trace_id is known.",
        "Use QYL_SESSION to narrow logs when replay/session context is already established."
    ],
    EvidenceHints =
    [
        "Structured logs often reveal request parameters and dependency outcomes missing from span attributes.",
        "Severity alone is insufficient; correlate on trace_id or session_id when possible."
    ],
    RelatedCapabilities = ["trace_investigation", "session_investigation"])]
internal sealed class LogInvestigationCapability;

[QylCapabilityDefinition("session_investigation", QylSkillKind.Inspect,
    Title = "Session Investigation",
    Summary =
        "Investigate end-user or agent sessions, replay transcripts, and correlated traces to reconstruct multi-step behavior.",
    Tags = ["sessions", "replay", "transcript", "journey"],
    UseCases =
    [
        "Explain what happened in a specific agent or user session.",
        "Correlate replay evidence with traces and error surfaces."
    ],
    PrimaryIdentifiers = ["session_id", "trace_id", "service_name", "status"],
    ScopingHints =
    [
        "QYL_SESSION should be set whenever repeated drill-down calls stay within a single session.",
        "Session investigations often narrow log search more effectively than broad time ranges."
    ],
    EvidenceHints =
    [
        "Transcripts explain user-visible flow; traces explain system-visible flow.",
        "Use both before concluding whether the failure is UX, orchestration, or backend."
    ],
    RelatedCapabilities = ["trace_investigation", "log_investigation", "analytics"])]
internal sealed class SessionInvestigationCapability;

[QylCapabilityDefinition("service_discovery", QylSkillKind.Inspect,
    Title = "Service and Release Discovery",
    Summary =
        "List projects and services, inspect service topology, and tie health regressions back to release boundaries.",
    Tags = ["services", "topology", "release", "projects"],
    UseCases =
    [
        "Understand the service landscape before starting deep investigation.",
        "Check whether a release boundary lines up with a new failure pattern."
    ],
    PrimaryIdentifiers = ["project_slug", "service_name", "release"],
    ScopingHints =
    [
        "Use project_slug to bound multi-tenant or multi-project environments early.",
        "Service scoping becomes more valuable after topology inspection identifies the blast radius."
    ],
    EvidenceHints =
    [
        "A service map explains dependency direction, not necessarily root cause ownership.",
        "Release health is strongest when paired with traces or issue spikes around the same window."
    ],
    RelatedCapabilities = ["trace_investigation", "anomaly_detection"])]
internal sealed class ServiceDiscoveryCapability;

[QylCapabilityDefinition("metrics_analysis", QylSkillKind.Inspect,
    Title = "Metrics Analysis",
    Summary =
        "Enumerate metrics, query timeseries, and baseline operational behavior before escalating to anomaly or regression workflows.",
    Tags = ["metrics", "timeseries", "baseline"],
    UseCases =
    [
        "Inspect an operational metric directly.",
        "Build context before anomaly detection or RCA."
    ],
    PrimaryIdentifiers = ["metric_name", "time_range", "service_name"],
    ScopingHints =
    [
        "Use service scoping when the metric is emitted by one service family.",
        "Keep time windows tight before escalating to expensive downstream workflows."
    ],
    EvidenceHints =
    [
        "Look for slope changes and sustained level shifts, not just single spikes.",
        "A metric query gives shape; anomaly tools explain deviation strength."
    ],
    RelatedCapabilities = ["anomaly_detection", "trace_investigation"])]
internal sealed class MetricsAnalysisCapability;

[QylCapabilityDefinition("genai_observability", QylSkillKind.Inspect,
    Title = "GenAI Observability",
    Summary =
        "Track provider, model, token, cost, and latency behavior for agent and LLM workloads, then pivot into traces or sessions when needed.",
    Tags = ["genai", "tokens", "cost", "latency", "models"],
    UseCases =
    [
        "Compare model or provider behavior over time.",
        "Investigate token, latency, or cost spikes in agent workloads."
    ],
    PrimaryIdentifiers = ["provider", "model_name", "run_id", "service_name", "time_range"],
    ScopingHints =
    [
        "Service scoping is often the fastest way to isolate one agent pipeline.",
        "Model and provider filters are more useful than raw trace pivots during early triage."
    ],
    EvidenceHints =
    [
        "Token spikes and latency spikes are related but not equivalent.",
        "Provider health issues often appear as cross-model latency changes before hard failures."
    ],
    RelatedCapabilities = ["agentic_investigation", "trace_investigation"])]
internal sealed class GenAiObservabilityCapability;

[QylCapabilityDefinition("health_and_storage", QylSkillKind.Health,
    Title = "Health and Storage",
    Summary =
        "Check overall collector/system health and storage posture before concluding an investigation is product-specific.",
    Tags = ["health", "storage", "system"],
    UseCases =
    [
        "Rule out platform-level issues early.",
        "Inspect resource pressure before investigating application-specific symptoms."
    ],
    PrimaryIdentifiers = ["service_name", "storage_backend", "time_range"],
    ScopingHints =
    [
        "Health tools are usually most useful before service-specific scoping is applied.",
        "Use system context as the control-plane baseline for later evidence."
    ],
    EvidenceHints =
    [
        "If health is degraded globally, downstream trace or error symptoms may be secondary effects.",
        "Storage pressure can explain missing or partial observability data."
    ],
    RelatedCapabilities = ["trace_investigation", "genai_observability"])]
internal sealed class HealthAndStorageCapability;

[QylCapabilityDefinition("analytics", QylSkillKind.Analytics,
    Title = "Conversation and User Analytics",
    Summary =
        "Inspect conversations, coverage gaps, source analytics, satisfaction, and user journeys to evaluate product behavior over time.",
    Tags = ["analytics", "conversations", "coverage", "journeys"],
    UseCases =
    [
        "Understand what users or agents are asking for most often.",
        "Find coverage gaps and degraded experience trends."
    ],
    PrimaryIdentifiers = ["conversation_id", "user_id", "source", "time_range"],
    ScopingHints =
    [
        "Analytics work is usually time-window driven rather than service-scoped.",
        "Pivot to user_id or source once you have a pattern worth explaining."
    ],
    EvidenceHints =
    [
        "Top questions explain demand; satisfaction explains outcome quality.",
        "Coverage gaps should be tied back to traces, errors, or sessions before proposing fixes."
    ],
    RelatedCapabilities = ["session_investigation", "agentic_investigation"])]
internal sealed class AnalyticsCapability;

[QylCapabilityDefinition("agentic_investigation", QylSkillKind.Agent,
    Title = "Agentic Investigation",
    Summary =
        "Use qyl's embedded investigation and summarization tools for multi-step reasoning after narrower evidence-oriented tools have established context.",
    Tags = ["agent", "rca", "summaries", "reasoning"],
    UseCases =
    [
        "Ask a complex multi-domain question after collecting concrete evidence.",
        "Summarize or explain causal structure across traces, errors, sessions, and analytics."
    ],
    PrimaryIdentifiers = ["question", "trace_id", "issue_id", "session_id"],
    ScopingHints =
    [
        "Set QYL_SERVICE or QYL_SESSION before invoking broad agentic tools whenever possible.",
        "Pass concrete IDs into the question or context so the agent does not waste tool budget rediscovering them."
    ],
    EvidenceHints =
    [
        "Agentic tools are strongest when grounded in previously collected telemetry facts.",
        "Use summaries as compression layers, not as the first source of truth."
    ],
    RelatedCapabilities = ["trace_investigation", "error_investigation", "loom_triage_and_fix"])]
internal sealed class AgenticInvestigationCapability;

[QylCapabilityDefinition("project_and_access_management", QylSkillKind.Build,
    Title = "Project and Access Management",
    Summary =
        "Manage projects, retention, API keys, teams, and DSNs from the MCP server when operational workflows need bounded write actions.",
    Tags = ["projects", "retention", "access", "dsn", "teams"],
    UseCases =
    [
        "Create or update qyl project configuration.",
        "Manage access material and routing information required by instrumentation."
    ],
    PrimaryIdentifiers = ["project_slug", "team_id", "dsn", "api_key"],
    ScopingHints =
    [
        "Management tools usually operate on explicit identifiers rather than environment scope injection.",
        "Establish identity first when access or role uncertainty is part of the workflow."
    ],
    EvidenceHints =
    [
        "These are state-changing tools; use discovery tools first to confirm intent.",
        "Keep configuration changes narrow and explicit to avoid accidental blast radius."
    ],
    RelatedCapabilities = ["service_discovery", "health_and_storage"])]
internal sealed class ProjectAndAccessManagementCapability;

[QylCapabilityDefinition("anomaly_detection", QylSkillKind.Anomaly,
    Title = "Anomaly Detection",
    Summary =
        "Detect unusual behavior, compare periods, and establish metric baselines before escalating to regression or RCA workflows.",
    Tags = ["anomaly", "baseline", "period-comparison", "metrics"],
    UseCases =
    [
        "Determine whether current behavior is outside the normal operating envelope.",
        "Compare one release or time window against another."
    ],
    PrimaryIdentifiers = ["metric_name", "service_name", "baseline_window", "comparison_window"],
    ScopingHints =
    [
        "Metric-level anomaly work is usually best scoped by service before comparing windows.",
        "Keep comparison windows aligned to release or deployment boundaries whenever possible."
    ],
    EvidenceHints =
    [
        "An anomaly score is not a root cause; it is evidence of deviation.",
        "Always pivot back to traces, errors, or release boundaries after finding deviation."
    ],
    RelatedCapabilities = ["metrics_analysis", "loom_triage_and_fix", "trace_investigation"])]
internal sealed class AnomalyDetectionCapability;

[QylCapabilityDefinition("loom_triage_and_fix", QylSkillKind.Loom,
    Title = "Loom Triage and Fix Pipeline",
    Summary =
        "Drive qyl's AI-assisted triage, autofix, regression, code review, and handoff workflows once issue evidence is stable enough for action.",
    Tags = ["loom", "triage", "fix", "regression", "github"],
    UseCases =
    [
        "Move from investigation into bounded remediation workflows.",
        "Inspect or control the state of a fix pipeline tied to an issue or regression."
    ],
    PrimaryIdentifiers = ["issue_id", "run_id", "triage_id", "pull_request", "regression_event"],
    ScopingHints =
    [
        "Stay issue-centric once a Loom workflow begins; avoid dropping back to broad searches.",
        "Use regression tools to establish change boundaries before generating fixes."
    ],
    EvidenceHints =
    [
        "Pipeline state is execution truth, not necessarily business truth.",
        "Fix approval should follow concrete trace, error, and regression evidence."
    ],
    RelatedCapabilities = ["error_investigation", "agentic_investigation", "anomaly_detection"])]
internal sealed class LoomTriageAndFixCapability;

[QylCapabilityDefinition("mcp_apps", QylSkillKind.Apps,
    Title = "Interactive MCP Apps",
    Summary =
        "Open qyl's interactive app surfaces for traces, errors, and query workflows when a visual or operator-facing experience is better than raw tool chaining.",
    Tags = ["apps", "ui", "trace-viewer", "query-studio"],
    UseCases =
    [
        "Launch a visual investigation aid from an MCP host.",
        "Inspect query schema or execute targeted queries through a UI-oriented workflow."
    ],
    PrimaryIdentifiers = ["trace_id", "issue_id", "query"],
    ScopingHints =
    [
        "App tools are best when a host supports MCP app resources cleanly.",
        "Use low-level tools first if you only need one fact, not an interactive surface."
    ],
    EvidenceHints =
    [
        "Apps are presentation surfaces over the same telemetry contracts; they do not replace evidence capture.",
        "Prefer UI flows when human operator inspection is part of the workflow."
    ],
    RelatedCapabilities = ["trace_investigation", "error_investigation", "service_discovery"])]
internal sealed class McpAppsCapability;

[QylCapabilityDefinition("debugger_control", QylSkillKind.Debug,
    Title = "Debugger Control",
    Summary =
        "Proxy into the Rider debugger MCP surface to inspect program state, set breakpoints, and walk execution when telemetry evidence points to live-code debugging.",
    Tags = ["debugger", "rider", "breakpoints", "frames"],
    UseCases =
    [
        "Move from telemetry evidence into a live debugger session.",
        "Inspect runtime values after traces or errors isolate a suspected code path."
    ],
    PrimaryIdentifiers = ["session_id", "frame_index", "file_path", "line_number", "breakpoint_id"],
    ScopingHints =
    [
        "Debugger workflows are live-state operations and do not use collector service/session injection.",
        "Keep debugger sessions tied to one hypothesis at a time."
    ],
    EvidenceHints =
    [
        "Use debugger control only after telemetry has already isolated a likely code path.",
        "Debugger state explains current execution, not historical production evidence."
    ],
    RelatedCapabilities = ["trace_investigation", "error_investigation"])]
internal sealed class DebuggerControlCapability;

[QylCapabilityDefinition("lsp_code_intelligence", QylSkillKind.Debug,
    Title = "LSP Code Intelligence",
    Summary =
        "Deterministic, semantic code navigation via Language Server Protocol — goto definition, find references, symbol lookup, diagnostics, and scoped rename. Replaces grep-based code reasoning for agent workflows.",
    Tags = ["lsp", "code", "navigation", "rename", "symbols"],
    UseCases =
    [
        "Follow a symbol to its declaration without guessing file layout.",
        "Find every reference to a symbol across a workspace before mutating behavior.",
        "Perform a safe, compiler-verified rename with a prepare-rename validity gate."
    ],
    PrimaryIdentifiers = ["file_path", "line", "column", "query", "new_name"],
    ScopingHints =
    [
        "LSP tools always require absolute paths — reject relative input fast.",
        "First call per workspace can take 10-30s (csharp-ls index build); subsequent calls complete in seconds."
    ],
    EvidenceHints =
    [
        "Prefer lsp_goto_definition and lsp_find_references over grep when a symbol is known.",
        "Always call lsp_prepare_rename before lsp_rename — a null prepareRename means the position is not renameable."
    ],
    RelatedCapabilities = ["debugger_control", "trace_investigation"])]
internal sealed class LspCodeIntelligenceCapability;

# qyl MCP — Tool Annotations Audit

> Source-of-truth for `[McpServerTool]` safety classifications across the
> qyl MCP server. Generated from `services/qyl.mcp/Tools/**/*.cs` on
> 2026-05-26 against 124 tool methods.
>
> Deliverable for qyl-PRD Stage E3.a (Phase 4 — Anthropic Connector Directory
> submission).

## Summary

| Bucket | Count | Notes |
|---|---|---|
| **ReadOnly** | 93 | Pure query/inspection. Surfaces as a non-warning tool call in Claude. |
| **Destructive** | 31 | Mutates collector state, kicks off pipelines, or commits decisions. Surfaces with an explicit confirmation prompt in Claude. |
| **Total** | 124 | Every method carries `Title` + exactly one of `ReadOnly=true` / `Destructive=true`. |

PRD rule applied for borderline cases: **default to `Destructive = true`**
(asymmetric cost — mis-labeling a destructive tool as ReadOnly is far worse
than the reverse, since users would skip the confirmation prompt).

## Table

| Tool name | Title | Class | Idempotent | OpenWorld | Source | Rationale |
|---|---|---|---|---|---|---|
| `analyze_session` | Analyze Session | **ReadOnly** | . | Y | `Analysis/AnalyzeSessionTool.cs` | LLM-driven session analysis; reads spans + logs from DuckDB, returns a synthesis. No writes. |
| `analyze_trace` | Analyze Trace | **ReadOnly** | . | Y | `Analysis/AnalyzeTraceTool.cs` | LLM-driven trace analysis; reads span tree, returns a synthesis. No writes. |
| `annotate_session` | Annotate Session | **Destructive** | Y | . | `Sessions/AnnotateSessionTool.cs` | Writes annotations to the session record. |
| `annotate_trace` | Annotate Trace | **Destructive** | Y | . | `Triage/AnnotateTraceTool.cs` | Writes annotations metadata to the trace row. |
| `configure_retention` | Configure Retention | **Destructive** | Y | . | `Management/ConfigureRetentionTool.cs` | Mutates qyl-collector state, triggers a pipeline, or commits a decision (PRD safer-default for borderline cases). |
| `create_api_key` | Create API Key | **Destructive** | . | . | `Management/CreateApiKeyTool.cs` | Provisions a new API key for the authenticated user/tenant. |
| `create_dsn` | Create DSN | **Destructive** | . | . | `Management/CreateDsnTool.cs` | Provisions a new DSN credential bound to a project. |
| `create_project` | Create Project | **Destructive** | . | . | `Management/CreateProjectTool.cs` | Inserts a new project under the current tenant. |
| `create_team` | Create Team | **Destructive** | . | . | `Management/CreateTeamTool.cs` | Inserts a new team into the tenant directory. |
| `get_attachments` | Get Event Attachments | **ReadOnly** | Y | . | `Errors/GetAttachmentsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_breadcrumbs` | Get Event Breadcrumbs | **ReadOnly** | Y | . | `Errors/GetBreadcrumbsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_log_details` | Get Log Details | **ReadOnly** | Y | . | `Logs/GetLogDetailsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_profile` | Get Span Profile | **ReadOnly** | Y | . | `Traces/GetProfileTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_release_health` | Get Release Health | **ReadOnly** | Y | . | `Discovery/GetReleaseHealthTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_service_map` | Get Service Map | **ReadOnly** | Y | Y | `Discovery/GetServiceMapTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_session` | Get Session | **ReadOnly** | . | . | `Sessions/GetSessionTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_span` | Get Span | **ReadOnly** | Y | . | `Traces/GetSpanTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_tag_distribution` | Get Tag Distribution | **ReadOnly** | Y | . | `Errors/GetTagDistributionTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `get_trace_details` | Get Trace Details | **ReadOnly** | Y | . | `Traces/GetTraceDetailsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `link_errors` | Link Error Issues | **Destructive** | Y | . | `Triage/LinkErrorsTool.cs` | Inserts edges in error_links table. |
| `list_dsns` | List DSNs | **ReadOnly** | Y | . | `Management/ListDsnsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `list_metrics` | List Metrics | **ReadOnly** | . | Y | `Metrics/ListMetricsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `list_projects` | List Projects | **ReadOnly** | Y | Y | `Discovery/ListProjectsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `list_services` | List Services | **ReadOnly** | Y | Y | `Discovery/ListServicesTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `list_teams` | List Teams | **ReadOnly** | Y | Y | `Management/ListTeamsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `lsp_diagnostics` | Diagnostics | **ReadOnly** | Y | . | `Lsp/LspTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `lsp_find_references` | Find References | **ReadOnly** | Y | . | `Lsp/LspTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `lsp_goto_definition` | Go to Definition | **ReadOnly** | Y | . | `Lsp/LspTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `lsp_prepare_rename` | Prepare Rename | **ReadOnly** | Y | . | `Lsp/LspTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `lsp_rename` | Rename Symbol | **Destructive** | . | . | `Lsp/LspTools.cs` | Mutates qyl-collector state, triggers a pipeline, or commits a decision (PRD safer-default for borderline cases). |
| `lsp_symbols` | Symbols | **ReadOnly** | Y | . | `Lsp/LspTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `mark_trace_reviewed` | Mark Trace Reviewed | **Destructive** | Y | . | `Triage/MarkTraceReviewedTool.cs` | Sets reviewed=true on the trace row. |
| `merge_errors` | Merge Error Issues | **Destructive** | . | . | `Triage/MergeErrorsTool.cs` | Mutates qyl-collector state, triggers a pipeline, or commits a decision (PRD safer-default for borderline cases). |
| `query_metrics` | Query Metrics | **ReadOnly** | . | Y | `Metrics/QueryMetricsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.analyze_session_errors` | Analyze Session Errors | **ReadOnly** | Y | Y | `ReplayTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.approve_fix_run` | Approve Fix Run | **Destructive** | Y | . | `AutofixMcpTools.cs` | Promotes a fix run to merged state in the Loom autofix pipeline. |
| `qyl.assisted_query` | Assisted Query | **ReadOnly** | Y | Y | `AssistedQueryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.check_regressions` | Check Regressions for Service | **ReadOnly** | Y | . | `RegressionTools.cs` | Compares current vs historical baselines; pure read, may cache results internally but no user-visible state changes. |
| `qyl.compare_periods` | Compare Periods | **ReadOnly** | Y | Y | `AnomalyTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.debug.evaluate` | Evaluate Expression | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.get_source` | Get Source Context | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.get_stack_trace` | Get Stack Trace | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.get_variables` | Get Variables | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.list_available_tools` | List Debugger Tools | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.list_breakpoints` | List Breakpoints | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.list_run_configs` | List Run Configurations | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.list_sessions` | List Debug Sessions | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.list_threads` | List Threads | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.pause` | Pause Execution | **Destructive** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.remove_breakpoint` | Remove Breakpoint | **Destructive** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.resume` | Resume Execution | **Destructive** | . | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.run_to_line` | Run to Line | **Destructive** | . | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.select_frame` | Select Stack Frame | **Destructive** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.session_status` | Get Debug Session Status | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.set_breakpoint` | Set Breakpoint | **Destructive** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.set_variable` | Set Variable Value | **Destructive** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.start_session` | Start Debug Session | **Destructive** | . | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.status` | Debug Connection Status | **ReadOnly** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.step_into` | Step Into | **Destructive** | . | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.step_out` | Step Out | **Destructive** | . | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.step_over` | Step Over | **Destructive** | . | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.debug.stop_session` | Stop Debug Session | **Destructive** | Y | . | `Debug/DebugTools.cs` | Debugger operation that mutates the active debug session in the connected JetBrains IDE. |
| `qyl.detect_anomalies` | Detect Anomalies | **ReadOnly** | Y | Y | `AnomalyTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.export_for_agent` | Export Issue for Coding Agent | **ReadOnly** | Y | . | `ExportForAgentTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.find_similar_errors` | Find Similar Errors | **ReadOnly** | Y | Y | `ErrorTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.generate_fix` | Generate Fix | **Destructive** | . | Y | `FixTools.cs` | Creates a new fix-run row in mcp_fix_runs; long-running, side-effectful. |
| `qyl.generate_test_from_error` | Generate Test from Error | **ReadOnly** | Y | Y | `TestGenerationTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_agent_run` | Get Agent Run | **ReadOnly** | Y | Y | `TelemetryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_conversation` | Get Conversation | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_coverage_gaps` | Get Coverage Gaps | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_error_issue` | Get Error Issue | **ReadOnly** | Y | Y | `ErrorTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_error_timeline` | Get Error Timeline | **ReadOnly** | Y | Y | `ErrorTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_fix_run` | Get Fix Run Details | **ReadOnly** | Y | . | `AutofixMcpTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_fix_run_steps` | Get Fix Run Pipeline Steps | **ReadOnly** | Y | . | `AutofixMcpTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_genai_stats` | Get GenAI Stats | **ReadOnly** | Y | Y | `GenAiTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_issue_regressions` | Get Issue Regression History | **ReadOnly** | Y | . | `RegressionTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_latency_stats` | Get Latency Stats | **ReadOnly** | Y | Y | `TelemetryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_metric_baseline` | Get Metric Baseline | **ReadOnly** | Y | Y | `AnomalyTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_satisfaction` | Get Satisfaction | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_session_transcript` | Get Session Transcript | **ReadOnly** | Y | Y | `ReplayTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_source_analytics` | Get Source Analytics | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_storage_stats` | Get Storage Stats | **ReadOnly** | Y | Y | `StorageHealthTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_system_context` | Get System Context | **ReadOnly** | Y | Y | `StorageHealthTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_token_timeseries` | Get Token Timeseries | **ReadOnly** | Y | Y | `GenAiTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_token_usage` | Get Token Usage | **ReadOnly** | Y | Y | `TelemetryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_top_questions` | Get Top Questions | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_trace` | Get Trace | **ReadOnly** | Y | Y | `ReplayTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_triage` | Get Triage Result | **ReadOnly** | Y | . | `TriageTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.get_user_journey` | Get User Journey | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.health_check` | Health Check | **ReadOnly** | Y | Y | `StorageHealthTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_conversations` | List Conversations | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_error_issues` | List Error Issues | **ReadOnly** | Y | Y | `ErrorTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_errors` | List Errors | **ReadOnly** | Y | Y | `TelemetryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_fix_runs` | List Fix Runs | **ReadOnly** | Y | . | `AutofixMcpTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_genai_spans` | List GenAI Spans | **ReadOnly** | Y | Y | `GenAiTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_models` | List Models | **ReadOnly** | Y | Y | `GenAiTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_regressions` | List Regression Events | **ReadOnly** | Y | . | `RegressionTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_services` | List Services | **ReadOnly** | Y | Y | `ServiceTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_sessions` | List Sessions | **ReadOnly** | Y | Y | `ReplayTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_structured_logs` | List Structured Logs | **ReadOnly** | Y | Y | `StructuredLogTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_trace_logs` | List Trace Logs | **ReadOnly** | Y | Y | `StructuredLogTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_triage` | List Triage Results | **ReadOnly** | Y | . | `TriageTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.list_users` | List Users | **ReadOnly** | Y | Y | `AnalyticsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.reject_fix_run` | Reject Fix Run | **Destructive** | Y | . | `AutofixMcpTools.cs` | Closes a fix run as rejected in the Loom autofix pipeline. |
| `qyl.root_cause_analysis` | Root Cause Analysis | **ReadOnly** | . | Y | `RcaTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.search_agent_runs` | Search Agent Runs | **ReadOnly** | Y | Y | `TelemetryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.search_logs` | Search Logs | **ReadOnly** | Y | Y | `StructuredLogTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.search_spans` | Search Spans | **ReadOnly** | Y | Y | `SpanQueryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.summarize_error` | Summarize Error | **ReadOnly** | . | Y | `SummaryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.summarize_session` | Summarize Session | **ReadOnly** | . | Y | `SummaryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.summarize_trace` | Summarize Trace | **ReadOnly** | . | Y | `SummaryTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.tracker_stats_for_site` | Tracker Stats For Site | **ReadOnly** | Y | Y | `TrackerStatsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.tracker_stats_top` | Top Trackers | **ReadOnly** | Y | Y | `TrackerStatsTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `qyl.trigger_triage` | Trigger Triage | **Destructive** | . | . | `TriageTools.cs` | Kicks off the triage pipeline for an issue — mutates triage state machine. |
| `qyl.use_qyl` | Use qyl | **ReadOnly** | . | Y | `UseQylTools.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `search_logs` | Search Logs | **ReadOnly** | Y | Y | `Logs/SearchLogsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `search_sessions` | Search Sessions | **ReadOnly** | . | Y | `Sessions/SearchSessionsTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `search_traces` | Search Traces | **ReadOnly** | Y | Y | `Traces/SearchTracesTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |
| `set_error_priority` | Set Error Priority | **Destructive** | Y | . | `Triage/SetErrorPriorityTool.cs` | Writes priority field on the error row. |
| `snooze_error` | Snooze Error Issue | **Destructive** | Y | . | `Triage/SnoozeErrorTool.cs` | Writes snooze_until field on the error row. |
| `suggest_fix` | Suggest Fix | **ReadOnly** | . | Y | `Analysis/SuggestFixTool.cs` | LLM-suggested patch; pure inference, no Loom commit or autofix-run mutation. |
| `update_project` | Update Project | **Destructive** | Y | . | `Management/UpdateProjectTool.cs` | Mutates qyl-collector state, triggers a pipeline, or commits a decision (PRD safer-default for borderline cases). |
| `update_session_status` | Update Session Status | **Destructive** | Y | . | `Sessions/UpdateSessionStatusTool.cs` | Mutates qyl-collector state, triggers a pipeline, or commits a decision (PRD safer-default for borderline cases). |
| `whoami` | Who Am I | **ReadOnly** | Y | . | `Auth/WhoamiTool.cs` | Query / inspection only — no persistent state mutation at the qyl-collector layer. |


## Methodology

1. Parsed every `[McpServerTool(...)]` attribute across
   `services/qyl.mcp/Tools/**/*.cs` via a regex over the attribute argument
   list.
2. Verified each method declares `Title = "..."` and exactly one of
   `ReadOnly = true` / `Destructive = true`. Methods that previously declared
   neither were classified by the rule above (verb semantics) and committed
   in the same change as this audit.
3. Stored the rationale column inline so the audit doc remains the
   single source-of-truth: changes to safety classification MUST update both
   the attribute and this row.

## Re-running the audit

```bash
# From repo root — fails if any [McpServerTool] is missing Title or has both
# / neither of ReadOnly|Destructive.
python3 -c '
import re, glob, pathlib, sys
mcp_re = re.compile(r"\[McpServerTool\s*\(([^\]]+)\)\s*\]", re.DOTALL)
bad = 0
for p in glob.glob("services/qyl.mcp/Tools/**/*.cs", recursive=True):
    for m in mcp_re.finditer(pathlib.Path(p).read_text()):
        flat = " ".join(m.group(1).split())
        title = bool(re.search(r"Title\s*=\s*\"", flat))
        ro = bool(re.search(r"ReadOnly\s*=\s*true", flat))
        dx = bool(re.search(r"Destructive\s*=\s*true", flat))
        if not title or (ro == dx):
            print(p)
            bad += 1
sys.exit(1 if bad else 0)
'
```


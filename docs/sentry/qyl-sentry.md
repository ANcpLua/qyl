# qyl ↔ Sentry Coverage

Sentry registry: `sentry.md`
qyl constraints: net10.0 only, no Azure, no Windows, Linux server-side

File paths verified against disk as of 2026-02-22.

Status key:
- **DONE** — fully implemented, file paths verified
- **PARTIAL** — core present, specific gap stated
- **NOT PRESENT** — not implemented
- **EXCLUDED** — intentionally out of scope (outside qyl's design boundary)
- **EXCEEDS** — qyl has this + more than Sentry's equivalent

---

## Summary Table

| SENTRY-ID | Feature | qyl Status | Notes |
|-----------|---------|------------|-------|
| SENTRY-001 | Issues grouping and triage | DONE | — |
| SENTRY-002 | Issue status state machine | DONE | 7 states including `regressed` |
| SENTRY-003 | Issue alerts | DONE | — |
| SENTRY-004 | Metric alerts | DONE | — |
| SENTRY-005 | Cron check-in lifecycle | NOT PRESENT | No cron monitor subsystem |
| SENTRY-006 | Uptime check criteria | NOT PRESENT | No uptime monitoring subsystem |
| SENTRY-007 | Uptime issue creation | NOT PRESENT | No uptime monitoring subsystem |
| SENTRY-008 | Release health (sessions/crash-free) | PARTIAL | Release markers + version tagging exist; crash-free rate computation not implemented |
| SENTRY-009 | Ownership rules | DONE | — |
| SENTRY-010 | Suspect commits | NOT PRESENT | Release markers track commit SHA but no stack-trace → commit author resolution |
| SENTRY-011 | Trace Explorer | DONE | — |
| SENTRY-012 | Save queries as alert/dashboard widgets | DONE | — |
| SENTRY-013 | Structured logs | DONE | — |
| SENTRY-014 | Metrics (counter/gauge/distribution) | DONE | Standard OTel, no proprietary API |
| SENTRY-015 | Session Replay | NOT PRESENT | Out of scope |
| SENTRY-016 | Dashboards | DONE | — |
| SENTRY-017 | Seer AI debugging / Autofix | EXCEEDS | AutofixOrchestrator + Workflow system; agent-native approach |
| SENTRY-018 | Sentry MCP server | EXCEEDS | qyl.mcp has 15+ tools; no OAuth required (local-first) |
| SENTRY-019 | AI Agent Monitoring | EXCEEDS | Full agent_runs + tool_calls tables; cost/token analytics |
| SENTRY-020 | SentrySdk.CaptureException + global hooks | DONE | ExceptionCapture.cs covers all capture paths |
| SENTRY-021 | SentryOptions (SDK config model) | PARTIAL | OTel resource attributes + QylServiceDefaultsOptions cover env/service config; no error filter/sample callbacks |
| SENTRY-022 | SentryHttpMessageHandler / HTTP errors | DONE | OTel auto-instrumentation covers HTTP; ErrorExtractor handles GraphQL |
| SENTRY-023 | BeforeSend hook | NOT PRESENT | No BeforeSend/event filter callback in qyl SDK |
| SENTRY-024 | BeforeBreadcrumb hook | NOT PRESENT | No per-breadcrumb or per-event filter callback |
| SENTRY-025 | OpenTelemetry bridge | EXCLUDED | qyl IS OTel-native; no bridge layer needed |

**Result: 12 DONE, 3 EXCEEDS, 3 PARTIAL, 6 NOT PRESENT, 1 EXCLUDED**

---

## Detail: SENTRY-001 — Issues grouping and triage

**Status: DONE**

| Sentry Feature | qyl Equivalent | File |
|---|---|---|
| Issues with grouping | `IssueService` — creates/upserts by fingerprint, occurrence count, first/last seen | `src/qyl.collector/Errors/IssueService.cs` |
| Error extraction from spans | `ErrorExtractor.Extract()` — reads `exception.*`, `gen_ai.error.type` from span attributes | `src/qyl.collector/Errors/ErrorExtractor.cs` |
| Fingerprint computation | SHA-256 of `{exceptionType}\n{normalizedMessage}\n{normalizedStack}` → 16-char hex | `src/qyl.collector/Errors/ErrorFingerprinter.cs` |
| Error categorization | 13 categories (8 GenAI + 5 general) | `src/qyl.collector/Errors/ErrorCategorizer.cs` |
| REST API | `IssueEndpoints` | `src/qyl.collector/Errors/IssueEndpoints.cs` |

**Notes:** GenAI-aware grouping (rate_limit by provider, content_filter by model) exceeds Sentry.

---

## Detail: SENTRY-002 — Issue status state machine

**Status: DONE**

| Sentry State | qyl Equivalent | File |
|---|---|---|
| new/ongoing/escalating | `unresolved` (initial), `acknowledged`, `investigating` | `src/qyl.collector/Errors/IssueService.cs:26` |
| archived | `ignored` | `src/qyl.collector/Errors/IssueService.cs:26` |
| resolved | `resolved` | `src/qyl.collector/Errors/IssueService.cs:26` |
| regressed | `regressed` | `src/qyl.collector/Errors/IssueService.cs:26` |
| Transition enforcement | `AllowedTransitions` frozen dictionary | `src/qyl.collector/Errors/IssueService.cs:29` |
| Transition API | `TransitionStatusAsync()` — validates legality, sets `resolved_at` on resolve | `src/qyl.collector/Errors/IssueService.cs:225` |
| Regression detection | `ErrorRegressionDetector.CheckForRegressionsAsync()` | `src/qyl.collector/Errors/ErrorRegressionDetector.cs` |

---

## Detail: SENTRY-003 — Issue alerts

**Status: DONE**

| Sentry Feature | qyl Equivalent | File |
|---|---|---|
| Alert rules (trigger/filter/action) | YAML-configured alert rules: query, condition, interval, cooldown | `src/qyl.collector/Alerting/AlertModels.cs` |
| Alert evaluation | `AlertEvaluator` — runs rules on schedule against DuckDB | `src/qyl.collector/Alerting/AlertEvaluator.cs` |
| Notification channels | Type + URL per channel | `src/qyl.collector/Alerting/AlertModels.cs` |
| Alert state tracking | `AlertRuleState` — firing status, last eval, active alert ID | `src/qyl.collector/Alerting/AlertModels.cs` |
| Deduplication | `AlertDeduplicator` | `src/qyl.collector/Alerting/AlertDeduplicator.cs` |
| REST CRUD | `AlertRuleEndpoints`, `AlertService` | `src/qyl.collector/Alerting/AlertRuleEndpoints.cs`, `src/qyl.collector/Alerting/AlertService.cs` |
| Storage | `alert_rules` + `alert_firings` tables | `src/qyl.collector/Storage/Migrations/V2026021622__create_alert_rules.sql`, `V2026021623__create_alert_firings.sql` |

---

## Detail: SENTRY-004 — Metric alerts

**Status: DONE**

Same `AlertEvaluator` infrastructure handles metric-based alert rules. GenAI alert templates add alerts
specific to token usage and operation latency thresholds.

| qyl Addition | File |
|---|---|
| Escalation chains (Notify → Page → Incident) | `src/qyl.collector/Alerting/AlertEscalationService.cs` |
| Priority tiers (Critical: 5m, High: 15m, Medium: 30m, Low: 1h) | `src/qyl.collector/Alerting/AlertEscalationService.cs` |
| GenAI alert rule templates | `src/qyl.collector/Alerting/GenAiAlertRules.cs` |

---

## Detail: SENTRY-005 — Cron check-in lifecycle

**Status: NOT PRESENT**

Sentry Crons (`SentrySdk.CaptureCheckIn`) have no equivalent in qyl. No cron monitor tables, no
watchdog service for missed/timed-out check-ins.

---

## Detail: SENTRY-006 — Uptime check criteria

**Status: NOT PRESENT**

No uptime monitoring subsystem. `src/qyl.collector/Health/` covers internal health checks for the
qyl collector itself (DuckDB health, readiness), not for external endpoint probing.

---

## Detail: SENTRY-007 — Uptime issue creation

**Status: NOT PRESENT**

Depends on SENTRY-006 (uptime probing). Not implemented.

---

## Detail: SENTRY-008 — Release health

**Status: PARTIAL**

| Sentry Feature | qyl Status | File |
|---|---|---|
| Release version tagging | DONE — `release_version` on error issue events | `src/qyl.collector/Errors/IssueService.cs` |
| Deploy markers with commit SHA | DONE — `error_release_markers` table | `src/qyl.collector/Storage/Migrations/V2026021621__create_error_release_markers.sql` |
| Regression on new deploy | DONE — `ErrorRegressionDetector` | `src/qyl.collector/Errors/ErrorRegressionDetector.cs` |
| Crash-free session rate | NOT PRESENT — no session-level crash rate computation |
| Crash-free user rate | NOT PRESENT |
| Errors by release breakdown | NOT PRESENT as dedicated API |

**Gap:** Session tracking exists (`session_entities`) but crash-free rate computation against sessions
is not implemented.

---

## Detail: SENTRY-009 — Ownership rules

**Status: DONE**

| Sentry Feature | qyl Equivalent | File |
|---|---|---|
| Owner assignment | `IssueService.AssignOwnerAsync()` | `src/qyl.collector/Errors/IssueService.cs:278` |
| Ownership service | `ErrorOwnershipService.AssignOwnerAsync()` — service/team/individual | `src/qyl.collector/Errors/ErrorOwnershipService.cs` |
| Priority setting | `IssueService.SetPriorityAsync()` | `src/qyl.collector/Errors/IssueService.cs:302` |
| Ownership storage | `error_ownership` table | `src/qyl.collector/Storage/Migrations/V2026021620__create_error_ownership.sql` |

---

## Detail: SENTRY-010 — Suspect commits

**Status: NOT PRESENT**

`error_release_markers` stores `commit_sha` and `commit_message` on deploys, but there is no
stack-trace-to-commit-author resolution (no SCM integration, no blame query).

---

## Detail: SENTRY-011 — Trace Explorer

**Status: DONE**

| Sentry Feature | qyl Equivalent | File |
|---|---|---|
| Span/trace storage | `spans` table — full hierarchy with GenAI columns | `src/qyl.collector/Storage/DuckDbSchema.g.cs` |
| Trace query API | `/api/v1/traces`, span endpoints | `src/qyl.collector/SpanEndpoints.cs` |
| Real-time streaming | SSE — `/api/v1/live` | `src/qyl.collector/Realtime/` |
| Browser SDK | W3C traceparent propagation on same-origin fetch | `src/qyl.browser/src/context.ts` |

---

## Detail: SENTRY-012 — Save queries as alert/dashboard widgets

**Status: DONE**

Alert rules are stored in `alert_rules` table and can reference any DuckDB query. Dashboard widgets
read from the same span/log/metric data. REST API for rule CRUD: `src/qyl.collector/Alerting/AlertRuleEndpoints.cs`.

---

## Detail: SENTRY-013 — Structured logs

**Status: DONE**

| Sentry Feature | qyl Equivalent | File |
|---|---|---|
| Log ingestion | OTLP log records via gRPC/HTTP | `src/qyl.collector/Ingestion/` |
| Source enrichment | `LogSourceEnricher.Enrich()` — `code.*` attrs first, stacktrace fallback | `src/qyl.collector/Ingestion/LogSourceEnricher.cs` |
| Log storage | `logs` table with `source_file`, `source_line`, `source_column`, `source_method` | `src/qyl.collector/Storage/DuckDbSchema.g.cs` |
| MCP tools | `StructuredLogTools` for AI-assisted log queries | `src/qyl.mcp/Tools/StructuredLogTools.cs` |

---

## Detail: SENTRY-014 — Metrics

**Status: DONE**

Sentry's custom metrics API (`SentrySdk.Experimental.Metrics`) is replaced by standard OTel metrics.
No proprietary API needed.

| qyl Implementation | File |
|---|---|
| OTLP metrics ingestion | `src/qyl.collector/Ingestion/` |
| GenAI metrics (token usage, duration histograms) | `src/qyl.servicedefaults/Instrumentation/GenAi/GenAiInstrumentation.cs` |
| Metrics REST API | `/api/v1/metrics`, `/api/v1/metrics/query`, `/api/v1/metrics/{metricName}` — `src/qyl.collector/Program.cs` |

---

## Detail: SENTRY-015 — Session Replay

**Status: NOT PRESENT**

Sentry Session Replay records DOM mutations for playback. `qyl.browser` SDK captures Web Vitals
and interactions but does not record DOM snapshots. Out of scope.

---

## Detail: SENTRY-016 — Dashboards

**Status: DONE**

| qyl Implementation | File |
|---|---|
| Dashboard data API | `src/qyl.collector/Dashboard/` |
| Dashboards storage | `src/qyl.collector/Dashboards/` |
| React frontend | `src/qyl.dashboard/src/` |
| MCP analytics tools | `src/qyl.mcp/Tools/AnalyticsTools.cs` |

---

## Detail: SENTRY-017 — Seer AI debugging / Autofix

**Status: EXCEEDS**

Sentry Seer is a SaaS AI agent. qyl has a self-hosted, workflow-integrated autofix system.

| qyl Implementation | File |
|---|---|
| Fix run orchestration | `AutofixOrchestrator` — fix runs linked to issues, assembles context, tracks lifecycle | `src/qyl.collector/Autofix/AutofixOrchestrator.cs` |
| Policy gate | `FixPolicy` enum: `AutoApply`, `RequireReview`, `DryRun` | `src/qyl.collector/Autofix/PolicyGate.cs` |
| Workflow integration | Fix runs create `autofix` workflow executions | `src/qyl.collector/Autofix/AutofixOrchestrator.cs` |
| Fix storage | `fix_runs`, `fix_artifacts`, `fix_policy_gates` tables | `src/qyl.collector/Storage/Migrations/V2026021624__create_fix_runs.sql` |
| MCP issue tools | `IssueTools` — AI agents can query and triage issues | `src/qyl.mcp/Tools/IssueTools.cs` |

---

## Detail: SENTRY-018 — Sentry MCP server

**Status: EXCEEDS**

Sentry MCP requires OAuth and connects to Sentry's SaaS. qyl.mcp is local-first, stdio-based, with
no auth overhead.

| qyl MCP Tools | File |
|---|---|
| Telemetry tools | `src/qyl.mcp/Tools/TelemetryTools.cs` |
| Structured log tools | `src/qyl.mcp/Tools/StructuredLogTools.cs` |
| GenAI tools | `src/qyl.mcp/Tools/GenAiTools.cs` |
| Agent tools | `src/qyl.mcp/Tools/AgentTools.cs` |
| Issue tools | `src/qyl.mcp/Tools/IssueTools.cs` |
| Build tools | `src/qyl.mcp/Tools/BuildTools.cs` |
| Replay tools | `src/qyl.mcp/Tools/ReplayTools.cs` |
| Search tools | `src/qyl.mcp/Tools/SearchTools.cs` |
| Workflow tools | `src/qyl.mcp/Tools/WorkflowTools.cs` |

---

## Detail: SENTRY-019 — AI Agent Monitoring

**Status: EXCEEDS**

Sentry's `Sentry.Extensions.AI` (experimental) wraps `IChatClient`. qyl adds agent run tracking,
tool call sequencing, sub-agent spawn correlation, cost and token breakdown.

| qyl Implementation | File |
|---|---|
| IChatClient wrapping | `WithQylTelemetry()` — `OpenTelemetryChatClient`, double-wrap prevention | `src/qyl.servicedefaults/Instrumentation/GenAi/GenAiInstrumentation.cs` |
| agent_runs table | `run_id`, `trace_id`, `agent_name`, `model`, `provider`, token counts, `total_cost` | `src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs` |
| tool_calls table | `call_id`, `run_id`, `tool_name`, `sequence_number`, arguments/result JSON | `src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs` |
| Agent dashboard | `/agents` page — waterfall traces, token/cost analytics | `src/qyl.dashboard/src/pages/AgentRunsPage.tsx` |

---

## Detail: SENTRY-020 — SentrySdk.CaptureException + global hooks

**Status: DONE**

| Sentry SDK API | qyl Equivalent | File |
|---|---|---|
| `SentrySdk.CaptureException()` | `ExceptionCaptureMiddleware` — catches exceptions, records span events with `exception.*` attributes | `src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs` |
| `AppDomain.UnhandledException` | `GlobalExceptionHooks.Register()` — unhandled + unobserved task hooks | `src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs` |
| Auto-registration | `ExceptionHookRegistrar` IHostedService | `src/qyl.servicedefaults/ErrorCapture/ExceptionCapture.cs` |
| Error extraction | `ErrorExtractor.Extract()` — reads `exception.*`, `gen_ai.error.type` from span attributes | `src/qyl.collector/Errors/ErrorExtractor.cs` |

---

## Detail: SENTRY-021 — SentryOptions (SDK configuration model)

**Status: PARTIAL**

| Sentry Option | qyl Equivalent | File |
|---|---|---|
| `SentryOptions.Dsn` / `Environment` / `Release` | OTel resource attributes (service name, version, env) configured via `AddServiceDefaults()` | `src/qyl.servicedefaults/QylServiceDefaultsOptions.cs` |
| `SentryOptions.OpenTelemetry` | `QylServiceDefaultsOptions.OpenTelemetry` — delegates to configure tracing/metrics/logging providers | `src/qyl.servicedefaults/QylServiceDefaultsOptions.cs` |
| `SentryOptions.BeforeSend` | NOT PRESENT — no error event filter callback |
| `SentryOptions.SampleRate` | NOT PRESENT — no error sampling rate option |
| `SentryOptions.IgnoreExceptions` | NOT PRESENT — no exception type ignore list |

**Gap:** Filter/sample/ignore callbacks (`BeforeSend`, `SampleRate`, `IgnoreExceptions`) are not implemented. Core OTel resource configuration is present.

---

## Detail: SENTRY-022 — SentryHttpMessageHandler / HTTP errors

**Status: DONE**

| Sentry SDK API | qyl Equivalent | File |
|---|---|---|
| `SentryHttpMessageHandler` (4xx/5xx capture) | OTel HTTP spans capture status codes automatically via `AddServiceDefaults()` | `src/qyl.servicedefaults/` |
| `SentryGraphQLHttpMessageHandler` | `ErrorExtractor.Extract()` reads span attributes including graphql error fields if set by SDK | `src/qyl.collector/Errors/ErrorExtractor.cs` |

---

## Detail: SENTRY-023 — BeforeSend hook

**Status: NOT PRESENT**

No `BeforeSend` or event filter callback exists in qyl. Error events are recorded from all
exceptions captured by `ExceptionCaptureMiddleware` with no pre-recording filter hook.

---

## Detail: SENTRY-024 — BeforeBreadcrumb hook

**Status: NOT PRESENT**

No per-breadcrumb or per-event filter callback equivalent to `SentryOptions.BeforeBreadcrumb`
exists in qyl. Breadcrumb storage via `error_breadcrumbs` table is implemented but without
a SDK-side filter hook.

---

## Detail: SENTRY-025 — OpenTelemetry bridge

**Status: EXCLUDED**

Sentry requires an `OpenTelemetrySdkOptions` bridge to receive OTel data. qyl is OTel-native —
OTLP is the first-class wire protocol. No bridge layer is needed or applicable.

---

## qyl-Only Features (no Sentry equivalent)

| Feature | File |
|---|---|
| GenAI-aware error grouping (by provider/model) | `src/qyl.collector/Errors/ErrorFingerprinter.cs` |
| Build failure capture (MSBuild binlog) | `src/qyl.collector/BuildFailures/`, `src/qyl.collector/Storage/DuckDbSchema.g.cs` |
| 4-classification PII taxonomy (Pii/Secret/PromptContent/InternalId) | `src/qyl.collector/Telemetry/QylDataClassification.cs` |
| OTLP PII scrubber at ingestion boundary | `src/qyl.collector/Ingestion/` |
| Workflow engine (WorkflowRun, nodes, checkpoints, shared state) | `src/qyl.collector/Workflow/` |
| Compile-time [Traced]/[GenAi]/[Db] interceptors (zero runtime reflection) | `src/qyl.servicedefaults.generator/` |

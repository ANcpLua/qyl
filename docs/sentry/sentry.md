# Sentry Feature Registry

Pinned commit: `d660071dadc5807885c4154eb87a50efa0e721df`
Date: 2026-02-22
Source: https://github.com/getsentry/sentry-docs/commit/d660071dadc5807885c4154eb87a50efa0e721df

Replaces: `sentry-catalog-pinned.md`

IDs SENTRY-001..019 carry forward from the previous catalog (stable).
IDs SENTRY-020..025 cover .NET SDK surface that maps directly to qyl coverage areas.

---

## Feature Catalog (CSV)

```csv
"id","feature_name","category","subcategory","sdk_api","docs_url","abstraction_tags"
"SENTRY-001","Issues grouping and triage surfaces","issues","detection","","https://docs.sentry.io/product/issues/","DetectionLoop;Triage"
"SENTRY-002","Issue status state machine (new, ongoing, escalating, regressed, archived, resolved)","issues","lifecycle","","https://docs.sentry.io/product/issues/states-triage/","DetectionLoop;ReleaseSafetyLoop"
"SENTRY-003","Issue alerts (trigger/filter/action, event-time evaluation)","alerts","issue-alerts","","https://docs.sentry.io/product/alerts/alert-types/","AlertLoop"
"SENTRY-004","Metric alerts (thresholds, warning/critical/resolved transitions)","alerts","metric-alerts","","https://docs.sentry.io/product/alerts/alert-types/","AlertLoop;ReleaseSafetyLoop"
"SENTRY-005","Cron check-in lifecycle (missed/failed/successful)","crons","job-monitoring","SentrySdk.CaptureCheckIn","https://docs.sentry.io/product/crons/job-monitoring/","CronReliabilityLoop"
"SENTRY-006","Uptime check criteria (2xx, redirects, timeout, DNS)","uptime","monitoring","","https://docs.sentry.io/product/uptime-monitoring/","UptimeGuardLoop"
"SENTRY-007","Uptime issue creation after consecutive failures","uptime","issue-creation","","https://docs.sentry.io/product/uptime-monitoring/","UptimeGuardLoop;AlertLoop"
"SENTRY-008","Release health (sessions/users/crash-free/adoption)","releases","health","","https://docs.sentry.io/product/releases/health/","ReleaseSafetyLoop"
"SENTRY-009","Ownership rules (path/module/url/tag auto-assignment)","issues","ownership","","https://docs.sentry.io/product/issues/ownership-rules/","OwnershipRemediationLoop"
"SENTRY-010","Suspect commits (stack trace -> commit author/PR)","issues","ownership","","https://docs.sentry.io/product/issues/suspect-commits/","OwnershipRemediationLoop"
"SENTRY-011","Trace Explorer (spans, traces, interactive investigation)","tracing","explorer","","https://docs.sentry.io/product/explore/trace-explorer/","DetectionLoop;PerformanceLoop"
"SENTRY-012","Save Trace Explorer queries as alerts/dashboard widgets","tracing","explorer","","https://docs.sentry.io/product/explore/trace-explorer/","AlertLoop;DashboardLoop"
"SENTRY-013","Structured logs with trace-connected debugging","logs","structured-logs","","https://docs.sentry.io/product/explore/logs/","DetectionLoop;RootCauseLoop"
"SENTRY-014","Trace-connected metrics (counter/gauge/distribution)","metrics","custom-metrics","SentrySdk.Experimental.Metrics","https://docs.sentry.io/product/explore/metrics/","DetectionLoop;RootCauseLoop"
"SENTRY-015","Session Replay (web/mobile) with access controls","replay","session-replay","","https://docs.sentry.io/product/explore/session-replay/","DetectionLoop;UXLoop"
"SENTRY-016","Dashboards with global filters and cross-widget zooming","dashboards","visualization","","https://docs.sentry.io/product/dashboards/","DashboardLoop"
"SENTRY-017","Seer AI debugging agent (RCA, PR creation, coding-agent delegation)","ai","autofix","","https://docs.sentry.io/product/ai-in-sentry/seer/","AgenticDebugLoop"
"SENTRY-018","Sentry MCP server for LLM clients (OAuth, remote tools)","ai","mcp","","https://docs.sentry.io/product/sentry-mcp/","AgenticDebugLoop;AutomationLoop"
"SENTRY-019","AI Agent Monitoring (agent runs, tool calls, model interactions)","ai","agent-monitoring","Sentry.Extensions.AI","https://docs.sentry.io/product/insights/ai/agents/","AgenticDebugLoop"
"SENTRY-020","Exception capture (SentrySdk.CaptureException, global hooks)","sdk","error-capture","SentrySdk.CaptureException;SentrySdk.CaptureEvent;IHub","https://docs.sentry.io/platforms/dotnet/","DetectionLoop"
"SENTRY-021","SDK configuration model (SentryOptions, DSN, environment, release)","sdk","configuration","SentryOptions;SentryOptions.Dsn;SentryOptions.Environment","https://docs.sentry.io/platforms/dotnet/configuration/options/","DetectionLoop"
"SENTRY-022","HTTP client error capture (SentryHttpMessageHandler, CaptureFailedRequests)","sdk","http","SentryHttpMessageHandler;SentryOptions.CaptureFailedRequests","https://docs.sentry.io/platforms/dotnet/configuration/http-client-errors/","DetectionLoop"
"SENTRY-023","BeforeSend event hook (filter/enrich/drop error events)","sdk","hooks","SentryOptions.BeforeSend;SentryOptions.BeforeSendTransaction","https://docs.sentry.io/platforms/dotnet/configuration/filtering/","DetectionLoop"
"SENTRY-024","BeforeBreadcrumb hook (filter/modify breadcrumbs before attach)","sdk","hooks","SentryOptions.BeforeBreadcrumb","https://docs.sentry.io/platforms/dotnet/enriching-events/breadcrumbs/","DetectionLoop"
"SENTRY-025","OpenTelemetry bridge (OpenTelemetrySdkOptions, Sentry as OTel exporter)","sdk","otel","SentryOptions.UseOpenTelemetry","https://docs.sentry.io/platforms/dotnet/tracing/instrumentation/opentelemetry/","DetectionLoop;PerformanceLoop"
```

---

## Abstraction Registry (CSV)

```csv
"id","abstraction_name","intent","member_ids"
"ABS-LOOP-001","DetectionLoop","Ingest and prioritize anomalies across issues/traces/logs/metrics","SENTRY-001;SENTRY-002;SENTRY-011;SENTRY-013;SENTRY-014;SENTRY-020"
"ABS-LOOP-002","AlertLoop","Evaluate conditions and dispatch notifications/actions","SENTRY-003;SENTRY-004;SENTRY-007;SENTRY-012"
"ABS-LOOP-003","CronReliabilityLoop","Track recurring job executions via check-ins and monitor states","SENTRY-005"
"ABS-LOOP-004","UptimeGuardLoop","Detect endpoint unavailability and create outage signal","SENTRY-006;SENTRY-007"
"ABS-LOOP-005","ReleaseSafetyLoop","Watch release adoption/crash-free regressions and surface risk","SENTRY-002;SENTRY-004;SENTRY-008"
"ABS-LOOP-006","OwnershipRemediationLoop","Route incidents to responsible owners/commit authors quickly","SENTRY-009;SENTRY-010"
"ABS-LOOP-007","AgenticDebugLoop","Use AI agents + MCP + telemetry context for automated debugging","SENTRY-017;SENTRY-018;SENTRY-019"
"ABS-LOOP-008","SdkSurfaceLoop","Core .NET SDK APIs for capture, config, hooks, and OTel bridge","SENTRY-020;SENTRY-021;SENTRY-022;SENTRY-023;SENTRY-024;SENTRY-025"
```

# Telemetry Data Model

> Owner: collector
> SSOT: YES (DuckDB schema, promoted columns, table definitions)
> Depends on: none
> Used by: `collector.md`, `issue-fingerprinting.md`, `telemetry-intelligence.md`, `loom.md`, `mcp.md`, `cost.md`

Canonical schema for all telemetry stored in DuckDB. Source of truth for agents, MCP tools, and dashboard queries.

Code context attributes (`code_filepath`, `code_function`, `code_lineno`, `code_namespace`): see `instrumentation.md` section 5.1 (SSOT).

---

## 1. Spans

Primary telemetry signal. Every OTel span becomes a row.

### 1.1 Core Columns

| Column | Type | Description |
|--------|------|-------------|
| `span_id` | VARCHAR(16) PK | Unique span identifier |
| `trace_id` | VARCHAR(32) | Trace this span belongs to |
| `parent_span_id` | VARCHAR(16) | Parent span (NULL = root) |
| `session_id` | VARCHAR(128) | User/agent session |
| `name` | VARCHAR | Span name (operation) |
| `kind` | DOUBLE | SpanKind (0=Internal, 1=Server, 2=Client, 3=Producer, 4=Consumer) |
| `start_time_unix_nano` | BIGINT | Start timestamp (nanoseconds since epoch) |
| `end_time_unix_nano` | BIGINT | End timestamp (nanoseconds since epoch) |
| `duration_ns` | UBIGINT | Duration in nanoseconds |
| `status_code` | DOUBLE | 0=Unset, 1=Ok, 2=Error |
| `status_message` | VARCHAR | Status description |
| `service_name` | VARCHAR | `service.name` resource attribute |
| `schema_url` | VARCHAR(256) | OTel schema URL |
| `created_at` | TIMESTAMP | Ingestion timestamp |

### 1.2 GenAI Promoted Columns

Inline columns for fast GenAI queries. No JSON parsing needed.

| Column | Type | Semconv Attribute |
|--------|------|-------------------|
| `gen_ai_provider_name` | VARCHAR | `gen_ai.system` / `gen_ai.provider.name` |
| `gen_ai_request_model` | VARCHAR | `gen_ai.request.model` |
| `gen_ai_response_model` | VARCHAR | `gen_ai.response.model` |
| `gen_ai_input_tokens` | BIGINT | `gen_ai.usage.input_tokens` |
| `gen_ai_output_tokens` | BIGINT | `gen_ai.usage.output_tokens` |
| `gen_ai_temperature` | DOUBLE | `gen_ai.request.temperature` |
| `gen_ai_stop_reason` | VARCHAR | `gen_ai.response.finish_reasons` |
| `gen_ai_tool_name` | VARCHAR | `gen_ai.tool.name` |
| `gen_ai_tool_call_id` | VARCHAR | `gen_ai.tool.call.id` |
| `gen_ai_cost_usd` | DOUBLE | Computed: tokens x pricing table |

### 1.3 Overflow Columns

| Column | Type | Description |
|--------|------|-------------|
| `attributes_json` | VARCHAR | All non-promoted span attributes as JSON |
| `resource_json` | VARCHAR | All resource attributes as JSON |
| `baggage_json` | VARCHAR | W3C baggage as JSON |

### 1.4 Indexes

```sql
idx_spans_trace_id           ON spans(trace_id)
idx_spans_session_id         ON spans(session_id)
idx_spans_start_time         ON spans(start_time_unix_nano)
idx_spans_service_name       ON spans(service_name)
idx_spans_gen_ai_provider    ON spans(gen_ai_provider_name)
idx_spans_gen_ai_model       ON spans(gen_ai_request_model)
```

---

## 2. Promoted Columns

Generated from OTel Semantic Conventions 1.40.0 by `eng/semconv/generate-semconv.ts`.

File: `src/qyl.collector/Storage/promoted-columns.g.sql`

ALL semconv attributes get their own DuckDB column. Non-promoted attributes land in `attributes_json`. This is a columnar storage strategy — DuckDB is efficient with sparse wide tables.

### 2.1 Agent-Critical Promoted Columns

These are the attributes an AI agent needs for debugging, RCA, and autofix.

**Service context:**

| Column | Semconv | Purpose |
|--------|---------|---------|
| `service_name` | `service.name` | Which service produced this span |
| `service_version` | `service.version` | Which version is deployed |
| `service_instance_id` | `service.instance.id` | Which instance (for per-replica issues) |
| `deployment_id` | `deployment.id` | Which deployment triggered this |
| `deployment_environment` | `deployment.environment` | prod / staging / dev |

**Code context:**

| Column | Semconv | Purpose |
|--------|---------|---------|
| `code_filepath` | `code.filepath` | Source file path |
| `code_function` | `code.function` | Function/method name |
| `code_namespace` | `code.namespace` | Class/module namespace |
| `code_lineno` | `code.lineno` | Line number |
| `code_stacktrace` | `code.stacktrace` | Full stacktrace |

**Error context:**

| Column | Semconv | Purpose |
|--------|---------|---------|
| `exception_type` | `exception.type` | Exception class name |
| `exception_message` | `exception.message` | Error message |
| `exception_stacktrace` | `exception.stacktrace` | Exception stacktrace |
| `error_type` | `error.type` | Error classification |

**Trace context:**

| Column | Semconv | Purpose |
|--------|---------|---------|
| `span_id` | (core) | Span identifier |
| `trace_id` | (core) | Trace identifier |
| `parent_span_id` | (core) | Parent span |

### 2.2 Deployment and Correlation Attributes

Required/optional matrix for agent-critical context. "Who emits" clarifies which component is responsible for setting the attribute.

| Attribute | Column | Required | Who emits | Notes |
|-----------|--------|----------|-----------|-------|
| `service.name` | `service_name` | **Required** | SDK (OTel resource) | Set via `AddQyl()` or `OTEL_SERVICE_NAME` |
| `service.version` | `service_version` | **Required** | SDK (assembly metadata) | Auto-detected from `AssemblyInformationalVersion` |
| `service.instance.id` | `service_instance_id` | Optional | SDK (OTel resource) | Auto-generated if not set |
| `deployment.id` | `deployment_id` | **Recommended** | App (resource attribute) | Links spans to `deployments` table |
| `deployment.environment` | `deployment_environment` | **Recommended** | App (resource attribute or `OTEL_RESOURCE_ATTRIBUTES`) | prod / staging / dev |
| `git.commit` | — | **Recommended** | SDK (assembly metadata) | Stored in `resource_json`, also in `deployments.git_commit` |
| `build.id` | — | Optional | CI pipeline | Stored in `resource_json` |
| `code.filepath` | `code_filepath` | **Required** | SDK generators | Emitted by `[Traced]` at compile time. `[GenAi]`/`[Db]` pending. |
| `code.function` | `code_function` | **Required** | SDK generators | Emitted by `[Traced]` at compile time. |
| `code.lineno` | `code_lineno` | **Required** | SDK generators | Emitted by `[Traced]` at compile time. |
| `code.namespace` | `code_namespace` | **Required** | SDK generators | Emitted by `[Traced]` at compile time. |
| `exception.type` | `exception_type` | Required (on error) | OTel SDK (automatic) | Set by `ActivityExceptionTelemetry.Record()` |
| `exception.stacktrace` | `exception_stacktrace` | Required (on error) | OTel SDK (automatic) | Full `exception.ToString()` |

### 2.3 Query Guarantees

| Entity | Query guarantee | Index |
|--------|----------------|-------|
| Span by ID | O(1) lookup | PK `span_id` |
| Spans by trace | All spans in a trace, ordered | `idx_spans_trace_id` |
| Spans by time | Range scan, newest first | `idx_spans_start_time` |
| Spans by service | All spans for a service | `idx_spans_service_name` |
| GenAI spans | Filter by provider/model | `idx_spans_gen_ai_provider`, `idx_spans_gen_ai_model` |
| Issues by fingerprint | Upsert dedup | `idx_error_issues_fingerprint` |
| Issues by status | Filter active issues | `idx_error_issues_status` |
| Deployments by service | Correlate spans to deploys | Scan `deployments.service_name` |

---

## 2b. Logs

| Column | Type | Description |
|--------|------|-------------|
| `log_id` | VARCHAR | Log record identifier |
| `trace_id` | VARCHAR | Correlated trace |
| `span_id` | VARCHAR | Correlated span |
| `session_id` | VARCHAR | Session |
| `time_unix_nano` | UBIGINT NOT NULL | Log timestamp (nanoseconds) |
| `observed_time_unix_nano` | UBIGINT | When the log was observed |
| `severity_number` | TINYINT NOT NULL | OTel severity (1-24) |
| `severity_text` | VARCHAR | Severity string (TRACE, DEBUG, INFO, WARN, ERROR, FATAL) |
| `body` | VARCHAR | Log message body |
| `service_name` | VARCHAR | Emitting service |
| `source_file` | VARCHAR | Source file path |
| `source_line` | INTEGER | Source line number |
| `source_method` | VARCHAR | Source method name |
| `attributes_json` | VARCHAR | Log attributes as JSON |
| `resource_json` | VARCHAR | Resource attributes as JSON |

---

## 3. Error Issues

Aggregated error tracking. Issues group multiple error occurrences by fingerprint.

### 3.1 error_issues

| Column | Type | Description |
|--------|------|-------------|
| `id` | VARCHAR PK | Issue identifier |
| `project_id` | VARCHAR NOT NULL | Owning project |
| `fingerprint` | VARCHAR NOT NULL | Computed by `ErrorFingerprinter` (see `issue-fingerprinting.md`) |
| `title` | VARCHAR NOT NULL | Human-readable issue title |
| `culprit` | VARCHAR | Code location that caused the error |
| `error_type` | VARCHAR NOT NULL | Exception type |
| `category` | VARCHAR NOT NULL | Error category (e.g. `rate_limit`, `content_filter`, `token_limit`) |
| `level` | VARCHAR NOT NULL | Severity level |
| `platform` | VARCHAR | Runtime platform |
| `first_seen_at` | TIMESTAMP | First occurrence |
| `last_seen_at` | TIMESTAMP | Most recent occurrence |
| `occurrence_count` | BIGINT | Total occurrences |
| `affected_users_count` | INTEGER | Unique affected users |
| `status` | VARCHAR NOT NULL | Lifecycle status (see 3.3) |
| `substatus` | VARCHAR | Sub-status detail |
| `priority` | VARCHAR NOT NULL | Issue priority |
| `assigned_to` | VARCHAR | Owner |
| `resolved_at` | TIMESTAMP | Resolution time |
| `resolved_by` | VARCHAR | Who resolved it |
| `regression_count` | INTEGER | Times this issue regressed |
| `last_release` | VARCHAR | Last release where this occurred |
| `tags_json` | JSON | Custom tags |
| `metadata_json` | JSON | Additional metadata |

### 3.2 error_issue_events

Links individual error occurrences to issues.

| Column | Type | Description |
|--------|------|-------------|
| `id` | VARCHAR PK | Event identifier |
| `issue_id` | VARCHAR NOT NULL | Parent issue |
| `trace_id` | VARCHAR | Linked trace |
| `span_id` | VARCHAR | Linked span |
| `message` | VARCHAR | Error message |
| `stack_trace` | VARCHAR | Stack trace |
| `stack_frames_json` | JSON | Parsed stack frames |
| `environment` | VARCHAR | Deployment environment |
| `release_version` | VARCHAR | Service version |
| `user_id` | VARCHAR | Affected user |
| `timestamp` | TIMESTAMP | Event time |

### 3.3 Issue Lifecycle

```text
unresolved → acknowledged → investigating → in_progress → resolved
                                                              ↓
ignored ← unresolved                                     regressed
```

Valid transitions enforced by `IssueService.TransitionStatusAsync()`.

---

## 4. Deployments

| Column | Type | Description |
|--------|------|-------------|
| `deployment_id` | VARCHAR PK | Deployment identifier |
| `service_name` | VARCHAR NOT NULL | Deployed service |
| `service_version` | VARCHAR NOT NULL | Version deployed |
| `environment` | VARCHAR NOT NULL | Target environment |
| `status` | VARCHAR NOT NULL | Deployment status |
| `strategy` | VARCHAR NOT NULL | Deployment strategy |
| `start_time` | TIMESTAMP | Deployment start |
| `end_time` | TIMESTAMP | Deployment end |
| `duration_s` | DOUBLE | Duration in seconds |
| `deployed_by` | VARCHAR | Deployer identity |
| `git_commit` | VARCHAR | Git commit SHA |
| `git_branch` | VARCHAR | Git branch |
| `previous_version` | VARCHAR | Version being replaced |
| `rollback_target` | VARCHAR | Rollback version |

---

## 5. Sessions

| Column | Type | Description |
|--------|------|-------------|
| `session_id` | VARCHAR(128) PK | Session identifier |
| `user_id` | VARCHAR | User identity |
| `start_time` | TIMESTAMP | Session start |
| `end_time` | TIMESTAMP | Session end |
| `duration_ms` | DOUBLE | Duration |
| `trace_count` | INTEGER | Traces in session |
| `span_count` | INTEGER | Spans in session |
| `error_count` | INTEGER | Errors in session |
| `services` | VARCHAR | Services accessed |
| `state` | VARCHAR | Session state |
| `genai_usage` | VARCHAR | GenAI usage summary |

---

## 6. Other Tables

| Table | Purpose | Owner |
|-------|---------|-------|
| `errors` | Legacy error aggregation | Collector |
| `fix_runs` | Autofix execution records | Loom (writes via collector) |
| `alert_rules` | Alert configuration | Collector |
| `alert_firings` | Alert history | Collector |
| `projects` | Project registry | Collector |
| `project_environments` | Environment config | Collector |
| `workflow_runs` | Workflow execution records | Collector (storage only) |
| `workflow_nodes` | Workflow node state | Collector (storage only) |
| `workflow_events` | Workflow event log | Collector (storage only) |

---

## 7. Query Patterns for Agents

### 7.1 Find errors for a service

```sql
SELECT trace_id, span_id, exception_type, exception_message, exception_stacktrace,
       code_filepath, code_function, code_lineno
FROM spans
WHERE service_name = 'checkout-api'
  AND status_code = 2
  AND start_time_unix_nano > ?
ORDER BY start_time_unix_nano DESC
LIMIT 50
```

### 7.2 Trace an error to its deployment

```sql
SELECT s.trace_id, s.exception_type, s.service_name,
       d.service_version, d.git_commit, d.deployed_by
FROM spans s
LEFT JOIN deployments d ON s.service_name = d.service_name
  AND d.start_time <= to_timestamp(s.start_time_unix_nano / 1000000000)
  AND (d.end_time IS NULL OR d.end_time >= to_timestamp(s.start_time_unix_nano / 1000000000))
WHERE s.trace_id = ?
```

### 7.3 GenAI cost by model

```sql
SELECT gen_ai_request_model,
       COUNT(*) AS call_count,
       SUM(gen_ai_input_tokens) AS total_input,
       SUM(gen_ai_output_tokens) AS total_output,
       SUM(gen_ai_cost_usd) AS total_cost
FROM spans
WHERE gen_ai_request_model IS NOT NULL
  AND start_time_unix_nano > ?
GROUP BY gen_ai_request_model
ORDER BY total_cost DESC
```

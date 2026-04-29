# qyl Attributes

Auto-generated from `eng/semconv/model/qyl/` via Weaver. Do not edit manually.

## `qyl.api_key`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.api_key.id` | `string` | development | First 8 characters of the API key hash (sha256[:8]). Never log the full key. Used for audit trails only. |

## `qyl.auth`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.auth.instance_id` | `string` | development | Stable instance identifier for log correlation (typically hostname) |
| `qyl.auth.keycloak_claims` | `string` | development | Serialized Keycloak JWT claims attached to a request span. High-cardinality — only emit when ENABLE_SENSITIVE_DATA is set. |

## `qyl.capability`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.capability.id` | `string` | development | Unique capability identifier declared via [QylCapability] at compile time. Examples: 'qyl.triage.score', 'qyl.fix.plan', 'qyl.regression.analyze' |
| `qyl.capability.kind` | `enum` (2 values) | development | Capability kind: Starting or FollowUp |

## `qyl.check_in`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.check_in.duration_ms` | `int` | development | Observed runtime of the monitored job in milliseconds |
| `qyl.check_in.monitor_slug` | `string` | development | Slug of the cron/monitor that produced the check-in |
| `qyl.check_in.schedule_cron` | `string` | development | Crontab expression describing the monitor schedule, when applicable |
| `qyl.check_in.schedule_interval_minutes` | `int` | development | Interval schedule in minutes, when the monitor uses a fixed-interval schedule instead of a crontab expression. |
| `qyl.check_in.status` | `enum` (5 values) | development | Current status reported by the check-in |

## `qyl.duckdb`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.duckdb.dropped_jobs_total` | `int` | development | Cumulative count of background write jobs dropped (back-pressure) |
| `qyl.duckdb.dropped_spans_total` | `int` | development | Cumulative count of span records dropped before persistence |

## `qyl.feedback`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.feedback.contact_email` | `string` | development | Optional contact email supplied with the feedback. High-cardinality PII — only emit when ENABLE_SENSITIVE_DATA is set. |
| `qyl.feedback.event_id` | `string` | development | Associated error/trace event identifier |
| `qyl.feedback.id` | `string` | development | Feedback record identifier |
| `qyl.feedback.source` | `enum` (4 values) | development | Channel the feedback was submitted through |

## `qyl.fix_run`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.fix_run.id` | `string` | development | Fix run identifier |
| `qyl.fix_run.status` | `enum` (5 values) | development | Current status of the fix run |
| `qyl.fix_run.trigger` | `enum` (2 values) | development | What initiated this fix run |

## `qyl.genai`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.genai.cache_hit` | `boolean` | development | Whether the response was served from prompt cache |
| `qyl.genai.cost_input_usd` | `double` | development | Computed USD cost for input/prompt tokens when per-token pricing is known. Companion of qyl.genai.cost_usd; omit when pricing is unavailable. |
| `qyl.genai.cost_output_usd` | `double` | development | Computed USD cost for output/completion tokens when per-token pricing is known. Companion of qyl.genai.cost_usd; omit when pricing is unavailable. |
| `qyl.genai.cost_status` | `enum` (3 values) | development | Outcome of cost computation by QylGenAiCostProcessor |
| `qyl.genai.cost_usd` | `double` | development | Computed USD cost for the call when per-token pricing is known. Omit when pricing is unavailable; do not emit a zero placeholder. |
| `qyl.genai.input_tokens` | `int` | development | Consumed input tokens for the call |
| `qyl.genai.model` | `string` | development | Model identifier for the generative AI call |
| `qyl.genai.output_tokens` | `int` | development | Generated output tokens for the call |
| `qyl.genai.provider` | `string` | development | Upstream provider that served the completion |
| `qyl.genai.workflow_id` | `string` | development | Qyl workflow id that produced the call. Links generative AI spans to their enclosing autofix/exploration run. |

## `qyl.issue`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.issue.id` | `string` | development | Error issue identifier |
| `qyl.issue.severity` | `enum` (4 values) | development | Issue severity level |
| `qyl.issue.status` | `enum` (3 values) | development | Current resolution status |

## `qyl.project`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.project.id` | `string` | development | Project identifier (UUID or slug) |
| `qyl.project.name` | `string` | development | Human-readable project name |

## `qyl.release`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.release.channel` | `enum` (4 values) | development | Release track the deployment belongs to |
| `qyl.release.commit_sha` | `string` | development | Full 40-character git SHA for the release commit |
| `qyl.release.environment` | `string` | development | Deployment environment name |
| `qyl.release.version` | `string` | development | Semantic or calendar version identifier for the release |

## `qyl.run`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.run.id` | `string` | development | Agent run identifier |
| `qyl.run.kind` | `enum` (3 values) | development | Kind of agent run |
| `qyl.run.status` | `enum` (4 values) | development | Final or current status of the run |

## `qyl.storage`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.storage.size` | `int` | development | Current DuckDB database file size in bytes |

## `qyl.team`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.team.id` | `string` | development | Team identifier |
| `qyl.team.name` | `string` | development | Human-readable team name |

## `qyl.triage`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.triage.category` | `string` | development | Classified error category from triage analysis |
| `qyl.triage.id` | `string` | development | Triage record identifier |
| `qyl.triage.score` | `double` | development | Triage priority score (0.0 = lowest, 1.0 = highest) |

# qyl Attributes

Auto-generated from `eng/semconv/model/qyl/` via Weaver. Do not edit manually.

## `qyl.api_key`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.api_key.id` | `string` | development | First 8 characters of the API key hash (sha256[:8]). Never log the full key. Used for audit trails only.
 |

## `qyl.capability`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.capability.id` | `string` | development | Unique capability identifier declared via [QylCapability] at compile time. Examples: 'qyl.triage.score', 'qyl.fix.plan', 'qyl.regression.analyze'
 |
| `qyl.capability.kind` | `{"members": [{"brief": "Entry-point capability in a workflow", "id": "starting", "value": "Starting"}, {"brief": "Capability that follows another in a workflow", "id": "follow_up", "value": "FollowUp"}]}` | development | Capability kind: Starting or FollowUp |

## `qyl.duckdb`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.duckdb.dropped_jobs_total` | `int` | development | Cumulative count of background write jobs dropped (back-pressure) |
| `qyl.duckdb.dropped_spans_total` | `int` | development | Cumulative count of span records dropped before persistence |

## `qyl.fix_run`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.fix_run.id` | `string` | development | Fix run identifier |
| `qyl.fix_run.status` | `{"members": [{"brief": "Fix run is queued", "id": "pending", "value": "pending"}, {"brief": "Fix run is in progress", "id": "running", "value": "running"}, {"brief": "Fix applied and verified", "id": "succeeded", "value": "succeeded"}, {"brief": "Fix attempt failed", "id": "failed", "value": "failed"}, {"brief": "Fix was rejected by reviewer", "id": "rejected", "value": "rejected"}]}` | development | Current status of the fix run |
| `qyl.fix_run.trigger` | `{"members": [{"brief": "Manually triggered by a user", "id": "manual", "value": "manual"}, {"brief": "Triggered automatically by a diagnostic rule", "id": "automatic", "value": "automatic"}]}` | development | What initiated this fix run |

## `qyl.instance_id`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.instance_id` | `string` | development | Stable instance identifier for log correlation (typically hostname) |

## `qyl.issue`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.issue.id` | `string` | development | Error issue identifier |
| `qyl.issue.severity` | `{"members": [{"brief": "Low severity — cosmetic or rare impact", "id": "low", "value": "low"}, {"brief": "Medium severity — notable but not blocking", "id": "medium", "value": "medium"}, {"brief": "High severity — significant user impact", "id": "high", "value": "high"}, {"brief": "Critical severity — service-breaking", "id": "critical", "value": "critical"}]}` | development | Issue severity level |
| `qyl.issue.status` | `{"members": [{"brief": "Issue is active and unresolved", "id": "open", "value": "open"}, {"brief": "Issue has been fixed", "id": "resolved", "value": "resolved"}, {"brief": "Issue has been acknowledged and dismissed", "id": "ignored", "value": "ignored"}]}` | development | Current resolution status |

## `qyl.keycloak`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.keycloak.claims` | `string` | development | Serialized Keycloak JWT claims attached to a request span. High-cardinality — only emit when ENABLE_SENSITIVE_DATA is set.
 |

## `qyl.project`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.project.id` | `string` | development | Project identifier (UUID or slug) |
| `qyl.project.name` | `string` | development | Human-readable project name |

## `qyl.run`

| Attribute | Type | Stability | Description |
|-----------|------|-----------|-------------|
| `qyl.run.id` | `string` | development | Agent run identifier |
| `qyl.run.kind` | `{"members": [{"brief": "Automated fix run triggered by a diagnostic", "id": "autofix", "value": "autofix"}, {"brief": "Code review run", "id": "review", "value": "review"}, {"brief": "Issue triage run", "id": "triage", "value": "triage"}]}` | development | Kind of agent run |
| `qyl.run.status` | `{"members": [{"brief": "Run is queued", "id": "pending", "value": "pending"}, {"brief": "Run is executing", "id": "running", "value": "running"}, {"brief": "Run completed successfully", "id": "succeeded", "value": "succeeded"}, {"brief": "Run completed with failure", "id": "failed", "value": "failed"}]}` | development | Final or current status of the run |

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

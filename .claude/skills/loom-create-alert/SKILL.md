---
name: loom-create-alert
description: Create and manage qyl alert rules via the collector's Alerts API. Use when asked to create a qyl alert, set up an error-rate / threshold / regression rule, acknowledge a firing, or list existing rules and firings. Routes to the correct phase (gather config, look up IDs, build condition/threshold JSON, create, verify, manage).
license: Apache-2.0
category: feature-setup
parent: loom-feature-setup
---

> [Top-level Loom Router](../SKILL.md) > [Feature Setup](../loom-feature-setup/SKILL.md) > Create Alert

# Create qyl Alert

Create alert rules via the qyl collector's Alerts API.

## Current Surface — Read This First

The qyl collector exposes an Alerts API defined in `core/specs/api/routes.tsp` under `interface AlertsApi` and emitted at `services/qyl.collector/Generated/generated/controllers/AlertsApiController.cs`. **Today that controller exposes only GET endpoints:**

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/v1/alerts/rules` | List alert rules (filter by `projectId`, `enabled`) |
| `GET` | `/api/v1/alerts/rules/{ruleId}` | Get single rule by id |
| `GET` | `/api/v1/alerts/firings` | List firings (filter by `ruleId`, `status`, time window) |
| `GET` | `/api/v1/alerts/fixes` | List fix runs (filter by `issueId`, `status`) |
| `GET` | `/api/v1/alerts/fixes/{fixId}` | Get single fix run |

**There is no `POST`, `PUT`, `PATCH`, or `DELETE` endpoint for rule creation, update, or deletion in the current contract.** Rule creation, update, and manual acknowledgment/resolution of firings are **not yet available over HTTP**.

In addition, `IAlertsApi` (the interface the controller depends on) has no implementation bound in the collector's DI container as of this writing — any call against the five GET routes will fail at request time until the implementation lands.

What this means for the skill:

- **You can teach the user the data model** — rule types, severity, condition/threshold JSON shapes — so they are ready the moment the write endpoints ship.
- **You can document the read endpoints** so the user can discover what rules / firings exist once the backend binds `IAlertsApi`.
- **You cannot create an alert over HTTP today.** The only production alert path today is the in-process heuristic detector at `services/qyl.collector/Insights/AlertsMaterializer.cs`, which scans the `spans` table every materialization pass and emits error-spike, cost-drift, and slow-operation lines. That code path is not user-configurable.
- **If the user needs a rule now**, the only honest option is a direct DuckDB insert into the `alert_rules` table (schema in `services/qyl.collector/Storage/DuckDbSchema.g.cs`, DDL snippet in *Phase 3* below). Flag this clearly as an escape hatch that bypasses the API contract.

## Invoke This Skill When

- User asks to "create a qyl alert" or "set up a qyl alert rule".
- User wants to configure an error-rate, threshold, burn-rate, regression, new-issue, or anomaly rule on qyl telemetry.
- User wants to list existing qyl alert rules or firings.
- User wants to acknowledge or resolve a qyl alert firing (noting the write path is not yet exposed).

## Prerequisites

- `curl` available in shell.
- `QYL_COLLECTOR_URL` pointing at the collector (default `http://localhost:5100`).
- Auth token accepted by `services/qyl.collector/Auth/TokenAuth.cs`. The collector accepts **three** equivalent forms:
    - `Authorization: Bearer <token>` header,
    - `x-mcp-api-key: <token>` header (Aspire MCP convention),
    - `?t=<token>` query parameter (sets the auth cookie and strips the param).
- For direct-DuckDB escape hatch (*Phase 3*): filesystem access to the collector's DuckDB file.

## Phase 1: Gather Configuration

Ask the user for any missing details:

| Detail | Required | Example |
|--------|----------|---------|
| Collector base URL | Yes | `http://localhost:5100` or value of `$QYL_COLLECTOR_URL` |
| Auth token | Yes | Value for `Authorization: Bearer` or `x-mcp-api-key` |
| Project id | Yes | `proj_01HABC...` (the `project_id` the rule scopes to) |
| Rule name | Yes | `"payments API error-rate > 5%"` |
| Rule type | Yes | `threshold`, `error_rate`, `new_issue`, `regression`, `burn_rate`, `anomaly`, `custom` |
| Severity | Yes | `critical`, `warning`, `info` |
| Condition JSON | Yes | Shape depends on `rule_type` — see Phase 3 |
| Threshold JSON | Sometimes | Required for `threshold`, `error_rate`, `burn_rate`; optional otherwise |
| Target type | Yes | `service`, `span_name`, `issue`, `project` — free-form string, matched by the future evaluator |
| Target filter JSON | Optional | JSON describing which rows the rule evaluates over |
| Cooldown (seconds) | Yes | Minimum gap between firings; `300` is a reasonable default |
| Notification channels JSON | Optional | JSON array describing where firings go (email, webhook, Slack, etc.) |
| Enabled | Yes | `true` to arm immediately, `false` to stage |

## Phase 2: Look Up IDs (Read-Only Paths That Work Today)

Use the existing GET endpoints to discover projects and existing rules before composing a new one.

```bash
BASE="${QYL_COLLECTOR_URL:-http://localhost:5100}"
AUTH="Authorization: Bearer ${QYL_API_KEY}"

# List existing rules for a project (verify no duplicate by name / project_id)
curl -s "$BASE/api/v1/alerts/rules?projectId=<projectId>&limit=100" -H "$AUTH"

# Inspect one rule to copy its condition_json shape
curl -s "$BASE/api/v1/alerts/rules/<ruleId>" -H "$AUTH"

# List recent firings to calibrate thresholds against real traffic
curl -s "$BASE/api/v1/alerts/firings?status=firing&limit=50" -H "$AUTH"
```

Reminder: until `IAlertsApi` is bound in DI, these calls will surface a 500-class error. When that happens, fall back to querying DuckDB directly (see *Troubleshooting*).

## Phase 3: Build the Rule

### Rule type → condition / threshold shape

| `rule_type` | `condition_json` shape (illustrative — validated by the future evaluator) | `threshold_json` |
|-------------|------------------------------------------------------------------------|------------------|
| `threshold` | `{"metric":"spans.duration_ns","agg":"p95","window":"5m","group_by":["service_name"]}` | `{"op":">=","value":2000000000}` (nanoseconds) |
| `error_rate` | `{"window":"5m","group_by":["service_name"]}` | `{"op":">=","value":0.05}` (5%) |
| `new_issue` | `{"project_id":"<id>","min_events":1}` | `null` |
| `regression` | `{"issue_id_filter":null}` | `null` |
| `burn_rate` | `{"slo_id":"<slo>","lookback":"1h"}` | `{"op":">=","value":14.4}` |
| `anomaly` | `{"metric":"spans.rate","window":"10m","method":"stddev"}` | `{"op":">=","value":3}` (z-score) |
| `custom` | `{"sql":"SELECT COUNT(*) FROM spans WHERE ..."}` | `{"op":">=","value":100}` |

The qyl schema stores both as JSON strings (`condition_json`, `threshold_json`) — the concrete evaluator lives in `services/qyl.collector/Insights/` and materializers; today only the heuristics in `AlertsMaterializer.cs` are wired. Treat the shapes above as **forward-compatible suggestions**, not a validated contract.

### Severity

| Value | Intent |
|-------|--------|
| `critical` | Paging-worthy; wake someone up |
| `warning` | Investigate soon; does not page |
| `info` | Log-level; dashboards only |

### Full payload structure (matches `AlertRuleEntity`)

```json
{
  "id": "rule_01HABC...",
  "project_id": "proj_01H...",
  "name": "payments-api error rate > 5%",
  "description": "5-minute rolling error rate on /checkout spans",
  "rule_type": "error_rate",
  "condition_json": "{\"window\":\"5m\",\"group_by\":[\"service_name\"]}",
  "threshold_json": "{\"op\":\">=\",\"value\":0.05}",
  "target_type": "service",
  "target_filter_json": "{\"service_name\":\"payments-api\"}",
  "severity": "critical",
  "cooldown_seconds": 300,
  "notification_channels_json": "[{\"kind\":\"webhook\",\"url\":\"https://example/hook\"}]",
  "enabled": true,
  "trigger_count": 0,
  "created_at": "2026-04-23T00:00:00Z",
  "updated_at": "2026-04-23T00:00:00Z"
}
```

Field names follow the `AlertRuleEntity` JSON encoding (`project_id`, `rule_type`, `condition_json`, `threshold_json`, `target_type`, `target_filter_json`, `cooldown_seconds`, `notification_channels_json`, `last_triggered_at`, `trigger_count`, `created_at`, `updated_at`). They are `snake_case` on the wire, not `camelCase`.

## Phase 4: Create the Rule

### Primary path — HTTP POST (NOT YET AVAILABLE)

**There is no `POST /api/v1/alerts/rules` endpoint in the current contract.** If the user expects to `curl -X POST` a payload, tell them plainly:

> qyl's Alerts API is GET-only today. Rule creation over HTTP is on the roadmap but not yet exposed. Until the write endpoints land, use the DuckDB direct-insert escape hatch below, or extend `AlertsMaterializer.cs` with the heuristic you need.

### Escape hatch — direct DuckDB insert

The collector's DuckDB file backs the `alert_rules` table. Inserting a row there registers the rule for future evaluators. This bypasses any API-level validation — treat it as a dev-box / bootstrap path, not a production workflow.

```bash
# Compose a rule row matching the DuckDbSchema.AlertRulesDdl shape
duckdb "${QYL_DUCKDB_PATH}" <<SQL
INSERT INTO alert_rules (
    id, project_id, name, description, rule_type,
    condition_json, threshold_json, target_type, target_filter_json,
    severity, cooldown_seconds, notification_channels_json,
    enabled, trigger_count, created_at, updated_at
) VALUES (
    'rule_01H...',
    'proj_01H...',
    'payments-api error rate > 5%',
    '5-minute rolling error rate on /checkout spans',
    'error_rate',
    '{"window":"5m","group_by":["service_name"]}',
    '{"op":">=","value":0.05}',
    'service',
    '{"service_name":"payments-api"}',
    'critical',
    300,
    '[{"kind":"webhook","url":"https://example/hook"}]',
    true,
    0,
    now(),
    now()
);
SQL
```

The collector's read path enforces read/write separation (`services/qyl.collector/Storage/DuckDbStore.cs`) — if you do this from inside the collector process, route the insert through `DuckDbStore.ExecuteWriteAsync`, never through a read lease. From outside the process, `duckdb` CLI is fine as long as the collector is not holding an exclusive write lock.

### Preferred long-term path — extend `AlertsMaterializer`

Until write endpoints exist, the most honest way to ship a new alert is to add a detector to `services/qyl.collector/Insights/AlertsMaterializer.cs`. That file already materializes error-spike, cost-drift, and slow-operation heuristics; a new alert is a new SQL block plus a `hasAlerts = true` line. This is code, not configuration — reserve it for alerts that every deployment needs.

## Phase 5: Verify

```bash
# Confirm the rule landed (GET path works once IAlertsApi is bound)
curl -s "$BASE/api/v1/alerts/rules/<ruleId>" -H "$AUTH"

# Or query DuckDB directly
duckdb "${QYL_DUCKDB_PATH}" -c "SELECT id, name, rule_type, severity, enabled FROM alert_rules WHERE id = 'rule_01H...';"

# Watch for firings once the evaluator runs
curl -s "$BASE/api/v1/alerts/firings?ruleId=<ruleId>&limit=20" -H "$AUTH"
```

There is no dashboard UI link to hand the user today. If the qyl dashboard (`services/qyl.dashboard/`) gains an alerts view, surface it here; until then, `firings` is the ground truth.

## Managing Alerts

| Task | How |
|------|-----|
| List rules | `GET /api/v1/alerts/rules?projectId=...&enabled=...&limit=...&cursor=...` |
| Get one rule | `GET /api/v1/alerts/rules/{ruleId}` |
| List firings | `GET /api/v1/alerts/firings?ruleId=...&status=...&startTime=...&endTime=...` |
| Get fix runs | `GET /api/v1/alerts/fixes?issueId=...&status=...` |
| Update a rule | Not available via HTTP — update the DuckDB row (`UPDATE alert_rules SET ... WHERE id = ...`) through `DuckDbStore.ExecuteWriteAsync` |
| Delete a rule | Not available via HTTP — `DELETE FROM alert_rules WHERE id = ...` through `DuckDbStore.ExecuteWriteAsync` |
| Acknowledge a firing | Not available via HTTP — set `acknowledged_at`, `acknowledged_by`, and `status = 'acknowledged'` on the `alert_firings` row |
| Resolve a firing | Not available via HTTP — set `resolved_at` and `status = 'resolved'` on the `alert_firings` row |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `401 Unauthorized` | Token missing or invalid. Confirm `Authorization: Bearer`, `x-mcp-api-key`, or `?t=` matches a value `TokenAuth.ValidateToken` accepts. |
| `404 Not Found` on a route that should exist | Check the collector is the current commit — the Alerts routes were added with the typed API. If the binary predates the contract, the controller is absent. |
| `500` / unbound service on any alerts call | `IAlertsApi` has no DI implementation yet. Fall back to DuckDB direct queries until the implementation lands. |
| Rule inserted but never fires | There is no evaluator loop wired to arbitrary `alert_rules` rows today — only `AlertsMaterializer` heuristics run. The rule row will sit idle until the evaluator ships. |
| `POST /api/v1/alerts/rules` returns `405` | Expected. The route is not defined. See Phase 4 — use the DuckDB escape hatch. |
| User asks for Slack / PagerDuty / email channel wiring | Persist the channel descriptor in `notification_channels_json` as JSON, but the delivery side does not exist yet; no notifications will be sent. State this before the user invests in channel-config copy. |

## Honesty Checklist

Before handing this skill's output to the user, confirm every statement below is true for the current commit:

- [ ] You told the user the API is GET-only today.
- [ ] You told the user `IAlertsApi` has no implementation bound yet (if that is still the case when they run it).
- [ ] If the user wanted a POST path, you redirected to the DuckDB escape hatch or `AlertsMaterializer` extension — not a fabricated endpoint.
- [ ] You did not invent URLs, headers, or payload fields that do not exist in `AlertRuleEntity` or `core/specs/api/routes.tsp`.

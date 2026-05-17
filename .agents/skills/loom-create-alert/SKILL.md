---
name: loom-create-alert
description: Create and manage qyl alert rules via the collector's Alerts API. Use when asked to create a qyl alert, set up an error-rate / threshold / regression rule, acknowledge a firing, or list existing rules and firings. Routes to the correct phase (gather config, look up IDs, build condition/threshold JSON, create, verify, manage).
license: Apache-2.0
category: feature-setup
parent: loom-feature-setup
---

> [Top-level Loom Router](../SKILL.md) > [Feature Setup](../loom-feature-setup/SKILL.md) > Create Alert

# Create qyl Alert

Create and manage alert rules via the qyl collector's Alerts API.

## Surface

The qyl collector exposes the Alerts API defined in `core/specs/api/routes.tsp` under `interface AlertsApi`. Minimal-API endpoints are wired in `services/qyl.collector/Alerts/AlertsEndpoints.cs`; persistence lives in `services/qyl.collector/Storage/DuckDbStore.Alerts.cs` (DuckDB `alert_rules` + `alert_firings` tables).

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/v1/alerts/rules` | Create a new alert rule (body = `AlertRuleEntity`) |
| `GET` | `/api/v1/alerts/rules` | List alert rules (filter by `projectId`, `enabled`) |
| `GET` | `/api/v1/alerts/rules/{ruleId}` | Get single rule by id |
| `PUT` | `/api/v1/alerts/rules/{ruleId}` | Update an existing rule (body = `AlertRuleEntity`) |
| `DELETE` | `/api/v1/alerts/rules/{ruleId}` | Delete an alert rule |
| `GET` | `/api/v1/alerts/firings` | List firings (filter by `ruleId`, `status`, time window) |
| `POST` | `/api/v1/alerts/firings/{firingId}/acknowledge` | Acknowledge a firing (body = `{ acknowledgedBy }`) |
| `POST` | `/api/v1/alerts/firings/{firingId}/resolve` | Mark a firing resolved |
| `GET` | `/api/v1/alerts/fixes` | List fix runs (filter by `issueId`, `status`) |
| `GET` | `/api/v1/alerts/fixes/{fixId}` | Get single fix run |

Base URL: `${QYL_COLLECTOR_URL}` (defaults to `http://localhost:5100` for local collector). Auth: send the same `Authorization: Bearer <token>` or `x-mcp-api-key: <key>` the collector accepts via `TokenAuth.cs`, or pass `?t=<token>` on the query string.

## Invoke this skill when
- The user asks to "create a qyl alert", "add an alert rule", "set up a threshold / error-rate / regression alert".
- The user wants to acknowledge or resolve an alert firing.
- The user asks to enumerate existing rules or firings.
- `loom-feature-setup` routed to `Create Alert`.

## Phase 1 — Gather configuration

Ask the user for the fields the POST body requires:

| Field | Required | Example |
|-------|----------|---------|
| `id` | No (server assigns GUID when blank) | `rule_prod_checkout_error_rate` |
| `projectId` | Yes | `checkout-api` |
| `name` | Yes | `"Checkout error rate > 5%"` |
| `description` | No | `"Fires when >5% of /api/v1/checkout requests error over 5m."` |
| `ruleType` | Yes | `threshold`, `errorRate`, `newIssue`, `regression`, `burnRate`, `anomaly`, `custom` (see `AlertRuleType` in `core/specs/models/alerting.tsp`) |
| `conditionJson` | Yes | JSON string encoding the rule-type-specific condition |
| `thresholdJson` | No | JSON string for rule-specific threshold (e.g. `{"value":0.05,"interval":"5m"}`) |
| `targetType` | Yes | `service`, `issue`, `transaction`, … (free-form; matches qyl's target-resolver) |
| `targetFilterJson` | No | JSON filter expression (e.g. `{"service_name":"checkout-api"}`) |
| `severity` | Yes | `critical`, `warning`, `info` (see `AlertSeverity`) |
| `cooldownSeconds` | No (default 300) | `600` |
| `notificationChannelsJson` | No | JSON array of channel ids/slack webhooks |
| `enabled` | Yes | `true` |

`createdAt` / `updatedAt` / `triggerCount` / `lastTriggeredAt` are server-managed — send zero / nulls, the collector overrides.

## Phase 2 — Build the request

```bash
curl -s -w "\n%{http_code}" -X POST \
  "${QYL_COLLECTOR_URL}/api/v1/alerts/rules" \
  -H "Authorization: Bearer ${QYL_API_KEY}" \
  -H "Content-Type: application/json" \
  -d @rule.json
```

Where `rule.json` mirrors the `AlertRuleEntity` JSON shape. Example payload for an error-rate rule:

```json
{
  "id": "",
  "project_id": "checkout-api",
  "name": "Checkout error rate > 5%",
  "description": "Fires when error rate on /api/v1/checkout exceeds 5% over 5m.",
  "rule_type": "error_rate",
  "condition_json": "{\"endpoint\":\"/api/v1/checkout\",\"window\":\"5m\"}",
  "threshold_json": "{\"value\":0.05}",
  "target_type": "service",
  "target_filter_json": "{\"service_name\":\"checkout-api\"}",
  "severity": "warning",
  "cooldown_seconds": 600,
  "notification_channels_json": null,
  "enabled": true,
  "trigger_count": 0,
  "created_at": "1970-01-01T00:00:00Z",
  "updated_at": "1970-01-01T00:00:00Z"
}
```

Expected response: HTTP `201 Created` with the persisted `AlertRuleEntity` in the body (server fills `id`, `created_at`, `updated_at`).

## Phase 3 — Verify

```bash
curl -s "${QYL_COLLECTOR_URL}/api/v1/alerts/rules?projectId=${PROJECT}" \
  -H "Authorization: Bearer ${QYL_API_KEY}" | jq '.items[] | {id, name, enabled, severity}'
```

Confirm the created rule appears in the list. Snake-case field names on the wire — `condition_json`, `rule_type`, `project_id` — per the `@encodedName("application/json", "...")` directives on `AlertRuleEntity`.

## Phase 4 — Manage

### Update a rule
```bash
curl -s -X PUT "${QYL_COLLECTOR_URL}/api/v1/alerts/rules/${RULE_ID}" \
  -H "Authorization: Bearer ${QYL_API_KEY}" \
  -H "Content-Type: application/json" \
  -d @updated-rule.json
```
Full-body PUT — send the complete `AlertRuleEntity`, not a partial patch.

### Disable without deleting
PUT the rule with `"enabled": false`.

### Delete a rule
```bash
curl -s -X DELETE "${QYL_COLLECTOR_URL}/api/v1/alerts/rules/${RULE_ID}" \
  -H "Authorization: Bearer ${QYL_API_KEY}"
```
Expected: HTTP `204 No Content` on success, `404` when the rule is not found.

### Acknowledge a firing
```bash
curl -s -X POST "${QYL_COLLECTOR_URL}/api/v1/alerts/firings/${FIRING_ID}/acknowledge" \
  -H "Authorization: Bearer ${QYL_API_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"acknowledged_by": "alice@example.com"}'
```
Sets `status=acknowledged`, records `acknowledged_at` (server timestamp) and `acknowledged_by`.

### Resolve a firing
```bash
curl -s -X POST "${QYL_COLLECTOR_URL}/api/v1/alerts/firings/${FIRING_ID}/resolve" \
  -H "Authorization: Bearer ${QYL_API_KEY}"
```
Sets `status=resolved`, records `resolved_at`.

## Condition / threshold JSON by rule type

The `condition_json` and `threshold_json` fields are free-form JSON strings. The collector's alert-materialiser interprets them based on `rule_type` — shapes are stable by convention, not by schema:

| `rule_type` | Typical `condition_json` | Typical `threshold_json` |
|-------------|--------------------------|--------------------------|
| `threshold` | `{"metric":"http.server.duration","aggregation":"p95","window":"5m"}` | `{"value":2000,"unit":"ms"}` |
| `error_rate` | `{"endpoint":"/api/v1/checkout","window":"5m"}` | `{"value":0.05}` |
| `new_issue` | `{"severity_min":"warning"}` | `null` (unused) |
| `regression` | `{"baseline_window":"1h","comparison_window":"5m"}` | `{"degradation":0.20}` |
| `burn_rate` | `{"slo_id":"checkout-availability","window":"1h"}` | `{"multiplier":2.0}` |
| `anomaly` | `{"metric":"request_rate","sensitivity":0.95}` | `null` (sensitivity in condition) |
| `custom` | `{"sql":"SELECT COUNT(*) ..."}` | `{"value":100}` |

For the authoritative shape at any point in time, read `services/qyl.collector/Alerts/` rule-type evaluators — they are the ground truth.

## Hard rules

- **Snake-case on the wire.** JSON field names follow `AlertRuleEntity` `@encodedName("application/json", "...")` directives — `project_id`, `rule_type`, `condition_json`, `target_filter_json`. The C# record has them in PascalCase; the transport is snake_case.
- **Enum values are lower-case strings.** `severity: "warning"`, `rule_type: "error_rate"` — not the C# PascalCase casing.
- **`cooldown_seconds` defaults to 300** when zero or unset. Set explicitly to override.
- **`trigger_count` / `last_triggered_at` / `created_at` / `updated_at` are server-managed.** Send sentinel values; the collector overrides on INSERT / UPDATE.
- **`PUT` is full-body.** There is no JSON-patch support; send the complete `AlertRuleEntity`.
- **Firing ack needs an `acknowledged_by`.** The endpoint 400s on empty or missing `acknowledgedBy`.

## Troubleshooting

| Issue | Likely cause | Fix |
|-------|--------------|-----|
| `400 Bad Request` on POST | One of `projectId`, `name`, `conditionJson`, `targetType` is empty | Check the request body — all four are required. |
| `404 Not Found` on PUT/DELETE | `ruleId` does not exist | List rules first; confirm the id. |
| Rule saved but never fires | `enabled: false` or the alert-materialiser has no evaluator for the `ruleType` | Verify `enabled: true`; check `services/qyl.collector/Alerts/AlertsMaterializer.cs` (or equivalent) for supported types. |
| `condition_json` validation error | Shape does not match what the evaluator expects for the `rule_type` | See "Condition / threshold JSON by rule type" table; read the evaluator source. |
| Acknowledge returns 404 | `firingId` typo or the firing was already resolved | `GET /api/v1/alerts/firings` to confirm the id and current status. |
| 401 Unauthorized | Missing or invalid auth header | Send `Authorization: Bearer <token>` or `x-mcp-api-key: <key>`; see `services/qyl.collector/Auth/TokenAuth.cs`. |
| Rule appears in list but not in DuckDB | Write contention on `alert_rules` | Extremely unlikely — `ExecuteWriteAsync` serialises writes. Check collector logs. |

## Related files

- Contract: `core/specs/api/routes.tsp` (AlertsApi interface)
- Domain models: `core/specs/models/alerting.tsp` (AlertRuleEntity, AlertFiringEntity, AlertRuleType, AlertSeverity, AlertFiringStatus)
- Endpoints: `services/qyl.collector/Alerts/AlertsEndpoints.cs`
- Persistence: `services/qyl.collector/Storage/DuckDbStore.Alerts.cs`
- Migration: `services/qyl.collector/Storage/Migrations/V2026021622__create_alert_rules.sql`, `V2026021623__create_alert_firings.sql`
- Generated interface: `services/qyl.collector/Generated/generated/operations/IAlertsApi.cs` (auto-regenerated by `nuke Generate`; not directly implemented — the minimal-API endpoints are the runtime surface)

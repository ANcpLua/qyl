# API Contract Specification

> Owner: collector (defines), mcp (consumes)
> SSOT: YES (response envelope, error model, pagination, timestamps, entity IDs)
> Depends on: none
> Used by: `collector.md`, `mcp.md`, `cost.md`, `dashboard.md`

Single contract for all REST and MCP responses. Define once, reference everywhere.

---

## 1. Response Envelope

Every API response uses this envelope:

```json
{
  "data": {},
  "meta": {
    "cursor": "eyJ0IjoxNzEwNjMy...",
    "has_more": true
  },
  "error": null
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `data` | object or array | Yes | Response payload |
| `meta` | object | Yes for lists | Pagination metadata |
| `meta.cursor` | string or null | Yes for lists | Opaque cursor for next page |
| `meta.has_more` | boolean | Yes for lists | Whether more results exist |
| `error` | object or null | Yes | Error details (null on success) |

Single-entity responses omit `meta`. List responses always include `meta`.

---

## 2. Error Model

```json
{
  "data": null,
  "error": {
    "code": "not_found",
    "message": "Trace abc123 does not exist",
    "details": {}
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `code` | string | Yes | Machine-readable error code |
| `message` | string | Yes | Human-readable description |
| `details` | object | No | Additional context (validation errors, etc.) |

---

## 3. Status Codes

| Code | Error code | When |
|------|-----------|------|
| 200 | — | Success (including empty results) |
| 400 | `invalid_request` | Schema validation failed, malformed input |
| 404 | `not_found` | Entity does not exist |
| 409 | `conflict` | State transition conflict (e.g., invalid issue status change) |
| 429 | `rate_limited` | Too many requests |
| 500 | `internal` | Unexpected server error |
| 503 | `loom_unavailable` | MCP analysis tool cannot reach Loom |

Empty results are **200 with empty `data`**, not 404. "No traces matched your filter" is a successful query with zero results.

---

## 4. Pagination

### 4.1 Contract

- All list endpoints use **cursor-based** pagination
- Default page size: **50**
- Max page size: **200**
- No offset-based pagination
- No `COUNT(*)` for totals — show "load more" instead of page numbers

### 4.2 Cursor Format

Opaque base64-encoded string. Consumers must not parse or construct cursors. The cursor encodes the ordering column value of the last returned item.

### 4.3 Request

```
GET /api/v1/traces?cursor={cursor}&limit=50
```

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `cursor` | string | null | Cursor from previous response |
| `limit` | integer | 50 | Page size (max 200) |

### 4.4 Ordering

Cursor pagination requires a stable sort order. Default: `start_time_unix_nano DESC` for telemetry, `created_at DESC` for entities.

---

## 5. Timestamps

- **Format:** ISO 8601 UTC always (`2026-03-17T14:30:00.000Z`)
- **Precision:** Milliseconds in API responses
- **Storage:** Nanoseconds internally (DuckDB `BIGINT` or `TIMESTAMP`)
- **No local time.** No timezone offsets. UTC only.
- **No relative time** in responses ("5 minutes ago"). Always absolute.

---

## 6. Entity IDs

| Entity | Format | Example |
|--------|--------|---------|
| `trace_id` | 32-char hex (16 bytes) | `4bf92f3577b34da6a3ce929d0e0e4736` |
| `span_id` | 16-char hex (8 bytes) | `00f067aa0ba902b7` |
| `session_id` | String (up to 128 chars) | `user-session-abc123` |
| `issue_id` | String | `issue_7f3a2b` |
| `deployment_id` | String | `deploy_2026-03-17_v1.2.3` |
| `log_id` | String | `log_9x8y7z` |
| `project_id` | String | `my-project` |

IDs must be consistent across all tools and endpoints. An `issue_id` returned by one endpoint must be usable as input to any other endpoint that accepts `issue_id`.

---

## 7. MCP Tool Contract

MCP tools follow this envelope with additional structure:

```json
{
  "facts": {},
  "analysis": {},
  "actions": [],
  "pagination": {
    "cursor": "...",
    "hasMore": true
  },
  "evidence": {
    "sources": ["span_id:00f067aa0ba902b7", "trace_id:4bf92f..."],
    "timeRange": { "from": "2026-03-17T14:00:00Z", "to": "2026-03-17T15:00:00Z" }
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `facts` | Yes | Raw telemetry data, never AI-generated |
| `analysis` | Only if Loom involved | AI-generated analysis, structurally separated from facts |
| `actions` | No | Proposed follow-up actions |
| `pagination` | For lists | Cursor-based pagination |
| `evidence` | For analysis | Entity IDs and time range backing the analysis |

---

## 8. Definition of Done

- [ ] All collector REST endpoints use the response envelope (section 1)
- [ ] All error responses use the error model (section 2)
- [ ] All list endpoints implement cursor-based pagination (section 4)
- [ ] All timestamps in ISO 8601 UTC with millisecond precision
- [ ] Entity IDs consistent across all endpoints and MCP tools
- [ ] MCP tools separate facts from analysis in responses
- [ ] No endpoint returns unbounded results
- [ ] Empty results return 200, not 404

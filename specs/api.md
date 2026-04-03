# API Contract

> Owner: collector
> SSOT: YES for cross-cutting HTTP invariants; NO for per-feature route inventory
> Depends on: nothing
> Used by: `collector.md`, `cost.md`, `dashboard.md`, `issue-fingerprinting.md`, `src/qyl.loom/specs/loom.md`, `mcp.md`

This spec owns the rules that must stay consistent across the REST surface:

- error semantics
- collection/pagination shape
- timestamp and ID conventions
- auth entry points
- serialization invariants

This spec does **not** own the route table for every feature. Route inventory belongs in the
feature specs and should be verified from runtime endpoint metadata, not hand-maintained here.

---

## 1. Current Mechanical Truth

### 1.1 Path families

- Most REST endpoints live under `/api/v1/*`.
- OTLP ingestion remains on `/v1/traces`, `/v1/logs`, and `/v1/metrics`.
- Health endpoints (`/health`, `/alive`, `/ready`, `/health/ui`) sit outside `/api/v1`.
- Static assets and dashboard routes are not part of this contract.

### 1.2 Response shape

There is no universal outer envelope today.

- Singular reads usually return the resource payload directly.
- Collection endpoints return one of several top-level collection shapes:
  - `{ "items": [...], "total": N }`
  - `{ "<featurePlural>": [...], "total": N }`
  - `{ "<featurePlural>": [...], "total": N, "hasMore": bool }`
- SSE endpoints stream typed events and are documented by their owning feature specs.

That drift is real. This spec should describe it honestly and then narrow it, not hide it.

### 1.3 Error shape

Two error families are in use today:

1. `application/problem+json` for validation-style failures.
2. A legacy JSON object shape equivalent to:

```json
{ "error": "Short description", "message": "Optional detail" }
```

This means callers cannot assume a single non-2xx payload format yet.

### 1.4 Pagination

Two pagination styles exist today:

- offset/limit or page/pageSize for most REST collections
- cursor-based pagination for some log/live surfaces

Defaults and caps are feature-owned. This spec should not duplicate every endpoint's numbers.

### 1.5 Timestamps

- Public API timestamps are UTC ISO 8601 strings.
- Internal storage may use Unix nanoseconds, ticks, or other internal forms, but those are not
  part of the wire contract.
- Duration query parameters such as `30s`, `1m`, `5m`, and `1h` are accepted on relevant
  time-windowed endpoints.

### 1.6 IDs

- `traceId` is a 32-character lowercase hex W3C Trace Context value.
- `spanId` is a 16-character lowercase hex W3C Trace Context value.
- All other IDs are opaque strings unless a feature spec says otherwise.

Do not force ULID, GUID, or numeric semantics into clients unless the wire contract actually
requires them.

### 1.7 Authentication

The API currently accepts:

- `Authorization: Bearer <token>`
- query-param bootstrap via `?t=<token>` for browser flows
- cookie continuation for symmetric token flows
- `x-mcp-api-key` where MCP-facing endpoints allow it
- Keycloak JWT bearer auth when configured

Exact auth exclusions are owned by `collector.md`, because they are route-level behavior.

### 1.8 Serialization

- JSON is camelCase.
- Nulls are normally omitted.
- Source-generated `System.Text.Json` contexts are the standard.
- Runtime reflection-based serialization is not part of the intended platform contract.

---

## 2. Target Contract

The clean target is smaller and stricter than the current surface.

### 2.1 Ownership

- `specs/api.md` owns only cross-cutting invariants.
- Feature specs own route inventory, query parameters, resource payload fields, and SSE event
  types.
- Build verification should derive route inventory from runtime endpoint metadata instead of
  relying on a hand-maintained table here.

### 2.2 Errors

Target rule:

- all non-2xx REST errors return RFC 7807 `ProblemDetails`

Migration note:

- legacy `{ error, message }` payloads may exist during cleanup, but they are drift, not the end
  state

### 2.3 Collections

Target rule:

```json
{
  "items": [],
  "total": 0,
  "hasMore": false,
  "nextCursor": null
}
```

Only include `hasMore` or `nextCursor` when the endpoint semantics support them. Do not invent
feature-specific collection root names when `items` is sufficient.

### 2.4 Singular resources

Target rule:

- singular reads and mutations return the resource payload directly unless the endpoint is
  explicitly asynchronous or streaming

No generic `{ data: ... }` wrapper should be introduced.

### 2.5 Time and IDs

Target rule:

- timestamps stay UTC ISO 8601 on the wire
- `traceId` and `spanId` keep W3C hex semantics
- every other identifier stays opaque unless explicitly standardized by the owning feature spec

---

## 3. What This Spec Must Not Do

Do not use this document to:

- duplicate every route from `collector.md`, `cost.md`, `mcp.md`, or other feature specs
- restate feature-specific query semantics
- encode storage-layer implementation details as wire guarantees
- declare cleanup complete when the runtime still serves mixed error or pagination shapes

If a route table is needed, generate it from runtime routing or keep it in the owning feature
spec. Do not grow a second manual index here.

---

## 4. Migration Sequence

1. Make route ownership explicit.
   - Remove per-feature route inventory from this spec.
   - Keep route inventories in the owning feature specs only.

2. Standardize errors.
   - Replace legacy `{ error, message }` responses with `ProblemDetails`.
   - Delete legacy error DTOs once all callers compile and tests pass.

3. Standardize collections.
   - Converge collection responses on `items` / `total` with optional `hasMore` /
     `nextCursor`.
   - Delete feature-specific plural collection roots when they are only naming noise.

4. Add runtime verification.
   - Derive a route inventory from ASP.NET endpoint metadata in build or test verification.
   - Fail when the generated inventory and the owning feature specs drift.

5. Lock the invariants.
   - Keep serializer settings, auth entry points, timestamp rules, and ID conventions covered by
     tests.

---

## 5. Validation

Minimum validation for this spec to be trustworthy:

- endpoint inventory verification sourced from runtime routing, not a hand-written table
- tests proving validation and error middleware return `ProblemDetails`
- tests proving collection endpoints use the canonical collection shape after migration
- tests proving timestamps remain UTC ISO 8601 on the wire
- tests proving `traceId` and `spanId` keep W3C format expectations
- tests covering auth entry points and documented exclusions

---

## 6. Definition Of Done

This spec is mechanically true when all of these are true:

- `specs/api.md` contains only cross-cutting invariants
- feature specs own route inventory
- runtime verification can enumerate endpoints and catch drift
- the legacy `{ error, message }` payload family is gone
- collection responses converge on the canonical `items` shape

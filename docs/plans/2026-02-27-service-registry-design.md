# Telemetry-Derived Service Registry

**Date:** 2026-02-27
**Status:** Approved
**Approach:** A — Ingest-Enriched Registry (cheap upsert at ingest + background aggregation)

## Problem

qyl has no service registry. The `/api/v1/services` endpoint is a stub returning empty data. `resource_json` is always null for spans (resource attributes discarded at ingest). The dashboard cannot auto-detect which services are sending telemetry or render service-specific panels.

## Architecture

Two-tier design following the enterprise telemetry-derived pattern (Pattern C from Grafana/New Relic/Last9):

- **Tier 1: `service_instances`** — physical table, one row per running instance, upserted at ingest time
- **Tier 2: `services`** — virtual DuckDB view over `service_instances`, aggregates across instances

### DuckDB Schema

```sql
CREATE TABLE IF NOT EXISTS service_instances (
    -- Identity (composite PK follows OTel identity)
    service_namespace        VARCHAR NOT NULL DEFAULT '',
    service_name             VARCHAR NOT NULL,
    service_instance_id      VARCHAR NOT NULL,
    service_type             VARCHAR NOT NULL DEFAULT 'traditional',

    -- Resource attributes (OTel 1.40.0)
    service_version          VARCHAR,
    deployment_environment   VARCHAR,
    os_type                  VARCHAR,
    host_arch                VARCHAR,

    -- AI-specific (null for traditional)
    agent_name               VARCHAR,
    provider_name            VARCHAR,
    default_model            VARCHAR,

    -- Lifecycle
    first_seen               TIMESTAMP_NS NOT NULL,
    last_seen                TIMESTAMP_NS NOT NULL,
    last_error_at            TIMESTAMP_NS,
    status                   VARCHAR NOT NULL DEFAULT 'active',

    -- Aggregates (populated by background worker)
    total_spans              BIGINT NOT NULL DEFAULT 0,
    total_logs               BIGINT NOT NULL DEFAULT 0,
    total_errors             BIGINT NOT NULL DEFAULT 0,
    total_input_tokens       BIGINT DEFAULT 0,
    total_output_tokens      BIGINT DEFAULT 0,
    total_cost_usd           DOUBLE DEFAULT 0,
    total_duration_ns        BIGINT DEFAULT 0,

    -- Catch-all for unpromoted resource attributes
    metadata                 JSON,

    PRIMARY KEY (service_namespace, service_name, service_type, service_instance_id)
);
```

```sql
CREATE OR REPLACE VIEW services AS
SELECT
    service_namespace,
    service_name,
    service_type,
    arg_max(service_version, last_seen)                                AS latest_version,
    arg_max(provider_name, last_seen) FILTER (WHERE provider_name IS NOT NULL) AS provider_name,
    arg_max(default_model, last_seen) FILTER (WHERE default_model IS NOT NULL) AS default_model,
    MIN(first_seen)                                                    AS first_seen,
    MAX(last_seen)                                                     AS last_seen,
    MAX(last_error_at)                                                 AS last_error_at,
    COUNT(*)                                                           AS total_instances,
    COUNT(*) FILTER (WHERE status = 'active')                          AS active_instances,
    array_agg(DISTINCT deployment_environment) FILTER (WHERE deployment_environment IS NOT NULL) AS environments,
    array_agg(DISTINCT service_version) FILTER (WHERE service_version IS NOT NULL)              AS versions_seen,
    SUM(total_spans)          AS total_spans,
    SUM(total_logs)           AS total_logs,
    SUM(total_errors)         AS total_errors,
    SUM(total_input_tokens)   AS total_input_tokens,
    SUM(total_output_tokens)  AS total_output_tokens,
    SUM(total_cost_usd)       AS total_cost_usd,
    SUM(total_duration_ns)    AS total_duration_ns,
    SUM(total_duration_ns) / NULLIF(SUM(total_spans), 0)                                 AS avg_duration_ns,
    SUM(total_errors)::DOUBLE / NULLIF(SUM(total_spans) + SUM(total_logs), 0)            AS error_rate
FROM service_instances
GROUP BY service_namespace, service_name, service_type;
```

### Service Type Classification

Applied at ingest time using attributes, not string matching:

| Priority | Condition | Type |
|----------|-----------|------|
| 1 | `meter.name == "com.anthropic.claude_code"` or `event_name` starts with `claude_code.` | `ai_agent` |
| 2 | Any span attribute starts with `gen_ai.agent.` | `ai_agent` |
| 3 | Any span attribute starts with `mcp.` | `mcp_server` |
| 4 | `gen_ai.provider.name` present (resource or span) | `llm_provider` |
| 5 | Default | `traditional` |

Valid values: `traditional`, `ai_agent`, `llm_provider`, `mcp_server`

### Ingest Pipeline

Cheap idempotent upsert per unique service per batch:

```sql
INSERT INTO service_instances (
    service_namespace, service_name, service_instance_id, service_type,
    service_version, deployment_environment, os_type, host_arch,
    agent_name, provider_name, default_model,
    first_seen, last_seen, status, metadata
) VALUES (...)
ON CONFLICT (service_namespace, service_name, service_type, service_instance_id)
DO UPDATE SET
    service_version        = COALESCE(EXCLUDED.service_version, service_instances.service_version),
    deployment_environment = COALESCE(EXCLUDED.deployment_environment, service_instances.deployment_environment),
    os_type                = COALESCE(EXCLUDED.os_type, service_instances.os_type),
    host_arch              = COALESCE(EXCLUDED.host_arch, service_instances.host_arch),
    agent_name             = COALESCE(EXCLUDED.agent_name, service_instances.agent_name),
    provider_name          = COALESCE(EXCLUDED.provider_name, service_instances.provider_name),
    default_model          = COALESCE(EXCLUDED.default_model, service_instances.default_model),
    last_seen              = GREATEST(EXCLUDED.last_seen, service_instances.last_seen),
    status                 = 'active',
    metadata               = COALESCE(EXCLUDED.metadata, service_instances.metadata);
```

Also fix: populate `resource_json` for spans (currently always null in OtlpConverter).

### Background Worker

`ServiceMaterializerService` (IHostedService, 5-minute interval):

1. Recompute per-instance aggregates from spans/logs tables
2. Update status: `active` (seen within 5 min), `inactive` (stale), `degraded` (error_rate > 10%)
3. Backfill any services found in spans/logs but missing from `service_instances`

### REST Endpoints

Replace existing stubs:

| Endpoint | Returns |
|----------|---------|
| `GET /api/v1/services` | Query `services` view — list with aggregates, filterable by type/status |
| `GET /api/v1/services/{serviceName}` | Single service detail with instance list |

### MCP Tool

`qyl.list_services` — detected services with type, status, instance count, aggregates formatted as markdown table.

## Files to Create/Modify

| File | Action |
|------|--------|
| `Storage/Migrations/V{next}__add_service_registry.sql` | CREATE — service_instances table + services view |
| `Storage/DuckDbStore.Services.cs` | CREATE — upsert + query methods (partial class) |
| `Services/ServiceClassifier.cs` | CREATE — attribute-based type classification |
| `Services/ServiceMaterializerService.cs` | CREATE — background aggregate worker |
| `Services/ServiceEndpoints.cs` | CREATE — REST endpoints + DTOs + JSON context |
| `Ingestion/OtlpConverter.cs` | MODIFY — extract resource attributes, populate resource_json, call upsert |
| `Program.cs` (collector) | MODIFY — register services, replace stub endpoints |
| `Tools/ServiceTools.cs` (mcp) | CREATE — list_services MCP tool |
| `Program.cs` (mcp) | MODIFY — register ServiceTools |

## Verification

1. `dotnet build` — zero errors, zero warnings
2. `dotnet test` — all tests pass
3. Start collector, send OTLP span with `service.name = "test-app"` → `GET /api/v1/services` returns it
4. Send span with `gen_ai.agent.name` attribute → service classified as `ai_agent`
5. Wait 5 min → aggregates populated by background worker

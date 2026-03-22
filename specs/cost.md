# Cost Engine Specification

> Owner: collector
> SSOT: YES (cost computation, pricing schema, budget alerts)
> Depends on: `telemetry-data-model.md` (GenAI promoted columns)
> Used by: `dashboard.md`, `mcp.md`, `00-architecture.md`

Server-side cost computation from telemetry qyl already stores. No proxy, no middleware, no SDK changes.

---

## 1. Data Source

GenAI spans contain the inputs. No additional collection required.

| Span attribute | Promoted column | Purpose |
|---------------|----------------|---------|
| `gen_ai.usage.input_tokens` | `gen_ai_input_tokens` | Input token count |
| `gen_ai.usage.output_tokens` | `gen_ai_output_tokens` | Output token count |
| `gen_ai.request.model` | `gen_ai_request_model` | Model identifier |
| `gen_ai.system` / `gen_ai.provider.name` | `gen_ai_provider_name` | Provider identifier |

---

## 2. Cost Formula (normative)

```text
cost =
    input_tokens  * (input_cost  / 1_000_000)
  + output_tokens * (output_cost / 1_000_000)
  + reasoning_tokens * (reasoning_cost / 1_000_000)   -- if applicable
  + cached_input_tokens * (cache_read_cost / 1_000_000) -- if applicable
```

All pricing is per 1M tokens. Division happens at computation time, not storage time.

The computed cost is written to the `gen_ai_cost_usd` promoted column at ingestion time.

---

## 3. Pricing Schema

### 3.1 Base Pricing

```sql
CREATE TABLE model_pricing (
    provider       VARCHAR NOT NULL,
    model          VARCHAR NOT NULL,
    input_cost     DECIMAL NOT NULL,  -- per 1M input tokens
    output_cost    DECIMAL NOT NULL,  -- per 1M output tokens
    reasoning_cost DECIMAL,           -- per 1M reasoning tokens (NULL if N/A)
    cache_read_cost  DECIMAL,         -- per 1M cached input tokens (NULL if N/A)
    cache_write_cost DECIMAL,         -- per 1M cache write tokens (NULL if N/A)
    valid_from     TIMESTAMP NOT NULL,
    valid_to       TIMESTAMP,         -- NULL = current pricing
    PRIMARY KEY (provider, model, valid_from)
);
```

### 3.2 Tiered Pricing

```sql
CREATE TABLE model_pricing_tiers (
    provider         VARCHAR NOT NULL,
    model            VARCHAR NOT NULL,
    tier_name        VARCHAR NOT NULL,  -- 'standard', 'batch', 'volume_1m+'
    input_cost       DECIMAL NOT NULL,
    output_cost      DECIMAL NOT NULL,
    reasoning_cost   DECIMAL,
    min_tokens       BIGINT,            -- threshold to activate (NULL = default)
    valid_from       TIMESTAMP NOT NULL,
    PRIMARY KEY (provider, model, tier_name, valid_from)
);
```

---

## 4. Seed Data

qyl ships `data/model-pricing.json` with current pricing for the top 30 models across OpenAI, Anthropic, Google, Meta, and Mistral.

On first boot, if `model_pricing` is empty, seed data auto-loads.

---

## 5. Time Aggregation

### 5.1 Buckets

| Bucket | Use |
|--------|-----|
| Minute | Real-time cost monitoring |
| Hour | Dashboard cost charts |
| Day | Daily spend reports, budget alerts |

### 5.2 Pre-aggregated View

```sql
CREATE OR REPLACE VIEW cost_by_model_hourly AS
SELECT
    date_trunc('hour', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket,
    service_name AS service,
    gen_ai_request_model AS model,
    gen_ai_provider_name AS provider,
    COUNT(*) AS call_count,
    SUM(gen_ai_input_tokens) AS total_input_tokens,
    SUM(gen_ai_output_tokens) AS total_output_tokens,
    SUM(gen_ai_cost_usd) AS total_cost
FROM spans
WHERE gen_ai_request_model IS NOT NULL
GROUP BY ALL;
```

### 5.3 Rounding

Costs are stored as `DOUBLE` with full precision. Display rounding happens at the API layer, not storage.

### 5.4 Late-arriving Data

Spans arriving after their bucket window closed are included in the next aggregation refresh. The pre-aggregated view is eventually consistent — reads from the base `spans` table are always accurate.

---

## 6. Consistency Guarantees

| Query type | Consistency | Source |
|-----------|-------------|--------|
| Single span cost | Strong | `gen_ai_cost_usd` computed at ingestion |
| Aggregated cost (view) | Eventual | Pre-aggregated view refreshed on ingest batch or every 60s |
| Budget threshold check | Eventual | Based on aggregated view |

### 6.1 Recomputation

If pricing is updated retroactively (`valid_from` in the past), a recomputation job must update `gen_ai_cost_usd` on affected spans. This is an explicit admin action, not automatic.

---

## 7. API Endpoints

See `api.md` for response envelope and pagination contract.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/genai/spans?include=cost` | GET | Cost per individual GenAI call |
| `/api/v1/cost/by-session` | GET | Cost aggregated by session |
| `/api/v1/cost/by-service` | GET | Cost aggregated by service |
| `/api/v1/cost/by-model` | GET | Cost aggregated by model |
| `/api/v1/cost/timeseries` | GET | Cost over time (hour/day buckets) |
| `/api/v1/cost/budget` | GET | Budget status and remaining spend |
| `/api/v1/cost/sync-pricing` | POST | Pull latest pricing from community registry |
| `/api/v1/cost/pricing/{provider}/{model}` | PUT | Manual pricing override |

---

## 8. Budget Alerts

Configurable spend threshold per service or model. When exceeded:

1. Alert record written to `alert_firings` table
2. SSE event emitted on the realtime stream
3. Optional webhook POST to configured URL

---

## 9. Definition of Done

- [x] Cost formula implemented in `ModelPricingService.EnrichBatchWithCost()` (computed at ingestion)
- [x] Pricing tables created via DuckDB migration (V20260322)
- [x] Seed data loads on first boot (30 models, FrozenDictionary cache)
- [x] Pre-aggregated cost view refreshes correctly (`cost_by_model_hourly`)
- [x] All 7 API endpoints return paginated results
- [ ] Budget alerts fire when threshold exceeded (endpoint exists, alert_firings not wired — not implemented, tracked as future work)
- [x] Pricing override works without restarting the server (PUT + cache refresh)
- [ ] Recomputation job updates historical costs when pricing changes (not implemented, tracked as future work)

# pricing

LiteLLM-shape model pricing snapshot consumed by `Qyl.Instrumentation.Instrumentation.GenAi.QylPricingTable`.

- `models.json` — current snapshot. Per-token rates. Hand-seeded; refreshed by `nuke UpdatePricing`.
- `provenance.jsonl` — append-only log of `{source_url, sha256, fetched_at_utc}` per refresh, so a price diff is reviewable.

The processor reads `qyl.genai.cost_usd` into spans before export. Server-side `ModelPricingService` (DuckDB `model_pricing` table) handles overrides and dynamic pricing; the static table here is the SDK-side fast path.

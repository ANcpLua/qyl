---
paths:
  - "src/**/*.cs"
  - "core/specs/otel/**/*.tsp"
---

# OpenTelemetry Semantic Conventions

## Version

OTel Semantic Conventions 1.39.0

## GenAI Attributes

When instrumenting GenAI operations, use these attributes:

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.system` | string | Provider (e.g., "openai", "anthropic") |
| `gen_ai.request.model` | string | Model requested |
| `gen_ai.response.model` | string | Model used (may differ) |
| `gen_ai.usage.input_tokens` | int | Prompt tokens |
| `gen_ai.usage.output_tokens` | int | Completion tokens |
| `gen_ai.request.temperature` | double | Temperature parameter |
| `gen_ai.response.finish_reasons` | string[] | Stop reasons |
| `gen_ai.tool.name` | string | Tool/function name |
| `gen_ai.tool.call_id` | string | Tool invocation ID |

## DuckDB Promotion

These attributes are promoted to DuckDB columns for fast queries:
- `gen_ai_provider_name`
- `gen_ai_request_model`
- `gen_ai_response_model`
- `gen_ai_input_tokens`
- `gen_ai_output_tokens`
- `gen_ai_temperature`
- `gen_ai_stop_reason`
- `gen_ai_tool_name`
- `gen_ai_tool_call_id`
- `gen_ai_cost_usd` (custom)

All other attributes go into `attributes_json` blob.

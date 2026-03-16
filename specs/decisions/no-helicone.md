# Decision: No Helicone Sidecar

## Status

Accepted.

## Context

Should qyl use Helicone OSS as a sidecar for LLM telemetry capture?

## Decision

No.

## Rationale

Helicone was acquired by Mintlify on March 3, 2026. The hosted service is in maintenance mode. Depending on an acquired project's community fork for a critical-path component is a risk qyl doesn't need.

Helicone exports OpenLLMetry attributes (`llm.usage.prompt_tokens`, `llm.model_name`) — not OTel GenAI semantic conventions 1.40 (`gen_ai.usage.input_tokens`, `gen_ai.request.model`). qyl uses standard semconv. Building adapter layers for legacy schemas is maintenance qyl won't carry.

A sidecar also breaks qyl's single Docker image deployment model.

## Competitive Context

Helicone captures by interception (proxy). qyl captures by instrumentation (OTel). Proxy vs protocol — fundamentally different philosophies. qyl's position: MIT-licensed, self-hosted, no vendor lock-in, no critical-path dependency on a third-party proxy that just got acquired.

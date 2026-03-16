# Decision: No Proxy Gateway

## Status

Accepted.

## Context

Should qyl include an LLM proxy gateway that intercepts API calls to capture telemetry?

## Decision

No. qyl captures telemetry via OTel instrumentation (side channel), not interception (critical path).

## Rationale

A proxy sits in the critical path of every LLM call. If it goes down, all LLM calls fail. qyl is an observability platform — if qyl goes down, apps keep working; they just lose telemetry. These are fundamentally different reliability contracts.

Mixing proxy responsibilities into a process that also runs DuckDB, a dashboard, and an MCP server creates resource contention and operational risk.

Maintaining proxy compatibility with 100+ LLM provider APIs (OpenAI, Anthropic, Google, Cohere, Mistral, and counting) is a full team's ongoing work. Every provider API change becomes a qyl release.

## Alternative

GenAI controls implemented as in-process `DelegatingChatClient` middleware. See `specs/08-genai-controls.md`.

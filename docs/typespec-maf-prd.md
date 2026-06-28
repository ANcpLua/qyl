# TypeSpec for Microsoft Agent Framework (`typespec-maf`) — moved

This PRD was **extracted into its own repository at PR-0 (2026-06-28)**. The live,
authoritative copy and all execution now live there:

> **https://github.com/ANcpLua/typespec-agent-framework** →
> [`docs/PRD.md`](https://github.com/ANcpLua/typespec-agent-framework/blob/main/docs/PRD.md)

This stub remains so existing links into qyl don't 404.

## What stays in qyl

The flagship depends on these qyl assets — they stay here, not in the new repo:

- **`qyl.conformance`** — the runtime verifier (declared-vs-observed diff engine,
  `conformant` gate). The flagship's PR-7 wires `qyl verify` against it.
- **`Qyl.Api.Contracts`** — the `TelemetryControlGraph` / `ConformanceReport` types
  (published to nuget.org via `qyl-api-schema`) that the loop's reports round-trip through.

## Status

Campaign live: building `typespec-agent-framework` through the PRD's PR-0→~PR-8
pipeline. PR-0 (scaffold) merged 2026-06-28.

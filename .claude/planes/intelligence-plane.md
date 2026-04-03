# Intelligence plane

Mission:
- convert telemetry into structured diagnostic facts and evidence

Owns:
- issue fingerprinting
- regression detection
- deployment correlation
- anomaly and cost heuristics
- evidence assembly for investigations and autofix
- deterministic scoring and ranking

Depends on:
- data plane
- contracts

Must not depend on:
- AG-UI
- workflow hosting concerns
- prose-first agent behavior for core classification logic

Current qyl areas:
- `src/qyl.collector/Intelligence`
- `core/specs/intelligence`
- `src/qyl.contracts/Intelligence`
- `specs/issue-fingerprinting.md`
- `specs/telemetry-intelligence.md`

Success condition:
- agents receive structured evidence packs instead of reconstructing truth from raw spans and logs.

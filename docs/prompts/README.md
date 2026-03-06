# Prompt Playbook

Reusable prompt chains for qyl development tasks.
Copy-paste each prompt into any AI CLI (Claude Code, Codex, etc.) and execute.

**Pattern:** Prompt Chaining — self-contained, ordered prompts that build on each other's output.

## Index

| # | Prompt | Type | File |
|---|--------|------|------|
| 1 | Worktree Bootstrap | Infrastructure-as-Prompt (one-shot) | [01-worktree-bootstrap.md](01-worktree-bootstrap.md) |
| 2.1 | Meter & Instrument Types | Prompt Chain (1/4) | [02-1-metrics-instrument-types.md](02-1-metrics-instrument-types.md) |
| 2.2 | Diagnostics Tooling | Prompt Chain (2/4) | [02-2-metrics-diagnostics-tooling.md](02-2-metrics-diagnostics-tooling.md) |
| 2.3 | Metrics Source Generators | Prompt Chain (3/4) | [02-3-metrics-source-generators.md](02-3-metrics-source-generators.md) |
| 2.4 | MeterListener & Subscription | Prompt Chain (4/4) | [02-4-metrics-meterlistener.md](02-4-metrics-meterlistener.md) |

## Chains

### Metrics API Extraction (Chain 2)

**Source:** `docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`
**Target:** `docs/roadmap/loom-design.md` section 15.11
**Run in order:** 2.1 → 2.2 → 2.3 → 2.4

## Usage Notes

- **Paths are relative** — prompts use `docs/...` so they work from any repo root.
- **Each prompt is self-contained** — safe to run in a fresh session.
- **Chain order matters** for chain 2: Part 4 references Parts 1–3 for the correlation matrix.
- **Bootstrap (1) is idempotent** — run it anytime to repair or update worktrees.

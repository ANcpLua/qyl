# qyl.mcp merge — execution prompt

> **EXECUTED 2026-07-11 — do not run again.** The merge landed as
> `qyl-workspace/qyl.mcp` (github.com/ANcpLua/qyl.mcp); the old repos are
> archived with pointer READMEs. The merge record with evidence lives in the
> workspace router `qyl-workspace/CLAUDE.md` ("The qyl.mcp merge — DONE").
> Kept for provenance only.
>
> Companion to [MCP-STRATEGY.md](./MCP-STRATEGY.md) (the why / the parity
> targets). This file was the hand-off prompt for the session that performed
> the mcp-run + qyl-apps-server merge.

## === QYL.MCP MERGE PROMPT (execute) ===

You are the sole dev merging two TypeScript repos into one product: **qyl.mcp**.
Work on `main`, local-first, no PR ceremony. Evidence rule: report only what
you can point to real command output for — words are claims, tool output is
proof.

Inputs (read before any edit):

- `qyl-workspace/mcp-run/` — host half: Qyl.Run-shaped orchestrator (runner
  API `:18888`, proxy `/runner/mcp/<name>`, App Bridge dashboard,
  `telemetry.ts` OTLP self-monitoring). Its `ARCHITECTURE.md` is binding.
- `qyl-workspace/qyl-apps-server/` — visual half: MCP Apps trace/log explorer
  (7 tools, demo fallback, collector wire shapes). Its `INTERFACE.md` is
  binding.
- `qyl/docs/design/qyl-host/MCP-STRATEGY.md` — the why and the parity targets,
  grounded against `~/RiderProjects/qyl-references/sentry-mcp`.
- Predecessor: `services/qyl.mcp` was deleted in qyl commit `43d032f9`
  (recover ideas via git history there, not by resurrecting code).

Goal: ONE repo `qyl-workspace/qyl.mcp` (new git repo; preserve both histories —
subtree merge or `--allow-unrelated-histories`), ONE MCP surface, still two
runtime concerns: the runner hosts the telemetry tools natively in-process
instead of spawning a sibling checkout.

Definition of done — each item needs run evidence, not intention:

1. One `tools/list`: curated top-level set + a search/execute catalog for the
   rest (Sentry tool-slot economy — `tools/surfaces.ts` in the sentry-mcp
   clone). Enforce the budget in code, not convention.
2. The MCP Apps UI (waterfall + dashboard) renders through the merged server;
   `hasAppUi` true; the sandbox CSP path stays intact.
3. Live mode against a running collector on `:5100` AND demo fallback both
   proven — start once with and once without the collector; show both outputs.
4. `telemetry.ts` self-monitoring still emits a span for every tool and
   passthrough call.
5. Collector wire shapes unchanged (snake_case bodies, dotted OTel keys,
   camelCase query params) — the demo dataset stays drop-in for live mode.
6. The error rule holds everywhere: tool failures return `isError:true` text,
   never throw (both halves already comply — do not regress it).
7. Build green from a clean clone: `npm ci && npm run build`; the runner
   boots, the resource lifecycle reaches `Ready`, SIGTERM stops clean.
8. Old repos closed out: README pointer to `qyl.mcp`, archived on GitHub;
   workspace router `CLAUDE.md` updated (the two entries collapse into one,
   the merge-plan section becomes a merge record).

Constraints:

- Keep the Qyl.Run 1:1 shape (constants / resources / app-builder mapping) —
  the later C# port onto `Qyl.Host` must stay mechanical. This merge is
  TS-only; do NOT port to C# here.
- Never bundle static semconv copies; first-party Weaver output only.
- No new runtime dependencies beyond what the two halves already use.
- Skill→capability authorization and the eval harness are the NEXT step, not
  this one — leave clean seams for them, do not build them.

Sequencing inside is yours. Stop only on a blocker your tools can't resolve.
When done, append the merge record with evidence to the workspace router
`CLAUDE.md`.

## === END PROMPT ===

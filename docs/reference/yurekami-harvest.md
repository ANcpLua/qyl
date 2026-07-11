# yurekami harvest — pattern routing table

> Source of truth for what qyl mines from the local reference clones under
> `~/yurekami` (11 projects, ~143k LOC). Full per-project dossiers + master index
> are vendored at [`./yurekami-extraction/`](./yurekami-extraction/) (start at
> [`INDEX.md`](./yurekami-extraction/INDEX.md)). The source itself is archived
> **source-only** in the private repo **`ANcpLua/yurekami-refs`** (upstream git
> history + `node_modules` + build artifacts + >1 MB data stripped; ~12 MB). The
> local clones under `~/yurekami/` were deleted after the harvest — to consult a
> donor's code, `gh repo clone ANcpLua/yurekami-refs` (the repo is private;
> SSH-URL clones need a key configured — `gh`/HTTPS auth is the reliable path).
> Verified 2026-07-08: a fresh clone restores HEAD `14e393e`, all 1078 files,
> `git fsck` clean, no LFS pointers.
>
> **Nothing here is a straight file-merge.** The donors are Python / Lean / TS /
> Markdown; qyl is .NET + TS. Every row is a *pattern transpile* into qyl's own
> idiom, pulled **one at a time behind the existing verify gates**
> (`BuildVerify`, `VerifyInstrumentationTelemetryIsBoundedAndRedacted`,
> `VerifyInstrumentationHasNoStorageTenantKnowledge`,
> `VerifyCollectorHasNoRuntimeRoslynUtilityReference`).

## Legend
- **Status:** `TODO` · `IN-PROGRESS` · `REF-ONLY` (study only, not vendored).
  There is deliberately no `LANDED` state: **a reference is for what's missing** —
  when a row ships (or turns out to already exist in qyl), delete the row; git
  history keeps the disposition evidence.

## Routing table (ranked by value × low integration risk)

| # | Pattern | Donor (path) | qyl receiving target | Status |
|---|---|---|---|---|
| 5 | **Statistically-honest automation gate** — Wilson lower-bound + z-test, shrink-to-neutral; gate on severity AND significance | aer-las `competence/score.ts`; agent-lightning `deployer/regression.ts`, `fitness.ts` | — | **REF-ONLY** — no .NET consumer. Every ratio in qyl is either display-only (`DuckDbStore.Sessions.cs:138` BounceRate, `:179` ErrorRate → dashboard) or a config-fixed head-sampling decision (`QylAotSampler.cs:39` — every root sampled; `QylTraceSampling.cs` was deleted in 3725a4c3); neither is a measured success/failure proportion a confidence bound would protect. Only auto-accept logic is GitHub-Actions YAML gating on boolean check status. `grep wilson\|z-test\|confidence\|regression\|canary` → 0 hits. |
| 8 | **Dependency-free diff / provenance** — LCS unified-diff + applyPatch; interval-coalescing line ranges + MurmurHash3 | agent-lightning LCS diff; agent-trace `src/trace-*.ts` | telemetry provenance dimension (AI-vs-human line attribution) — new capability, evaluate | REF-ONLY |
| 10 | **Verbalized Sampling** — default→forbid→T-scored alternatives→pick lowest-T meeting quality | anti-sameness-plugin skills | eval rubric / any generation surface | REF-ONLY |
| 11 | **Rubric-as-weighted-predicate autograder** — sum `(predicate, points, label)`, grep concepts not strings | claude-code-challenges; aegis `@grader`/`GraderRegistry` | deterministic eval/CI gate over agent output | REF-ONLY |
| 12 | **CSP-safe single-file HTML report** — zero-JS pure-CSS collapsibles | aegis report; claude-code-challenges arcade UI | dashboard/artifact CSP constraints reference | REF-ONLY |
| 14 | **Swappable-stage ML pipeline + sklearn-free model export** (source-inlining freeze) | autometrics | only if qyl ships exportable eval metrics | REF-ONLY |
| 15 | **Invariant-as-struct-field** correctness (refinement fields, total-accessor trick) | batteries union-find `WellFormed`, `PrefixTable.valid` | design idea for type-state-heavy C# | REF-ONLY |
| 2 | **Fail-closed PreToolUse hook glue** — JSON-in/out HookOutput, tsx→JS→fail-open fallback, 5s timeout | aer-las `runtime/gateway.ts` + `runtime-gate.sh`; aegis `hooks/claude_code.py` | only if qyl grows an agent-gating / telemetry-on-tool-use surface | REF-ONLY |
| 13 | **Workspace-per-target autonomous-loop** — per-run state folder as sole SoT, dual prose+TSV log, filesystem inbox | auto-reverse-engineer | long-running agent-task structure reference | REF-ONLY |

## Harvest outcome (2026-07-08 sweep · table pruned 2026-07-11)
All 15 rows were scoped against the live qyl code on 2026-07-08: **1 LANDED,
14 REF-ONLY.** On 2026-07-11 the table was pruned to open rows only — *a
reference is for what's missing*: the shipped **#7** (GenAI cache/reasoning
token cost) and the five patterns qyl already embodies (**#1** never-crash
instrumentation, **#3** bounded interval aggregation, **#4** redaction-by-
omission — a regex redactor would *introduce* the ReDoS surface qyl avoids,
**#6** memoized pricing lookup, **#9** removed-symbol governance =
`removedCollectorTokens`) were removed. Full per-row dispositions + evidence:
this file at `8b08f757` and earlier.

Net new liftable code from ~143k LOC of references = the cost wiring. That's expected:
the donors are Python/Lean/TS in different domains; qyl's overlap with them is
architectural, and qyl had already converged on the same patterns independently.

## Follow-up (deferred, low priority)
- The landed GenAI cost wiring (former row #7) was **verified live** ($0, synthetic OTLP span → DuckDB read: 1M tokens/class on
  `claude-sonnet-4-6` → `gen_ai_cost_usd = 22.05`, the unique correct-wiring value). Not yet
  a CI regression test — the repo has **no .NET test project**, so wiring one up for a single
  assertion isn't worth it pre-beta. Revisit when there's a second reason to add the test project.

## Rule
Pull **one row at a time**, transpile into qyl idiom, run the relevant verify gate,
commit — then **delete the row** (implemented patterns don't keep reference rows).
Donor source now lives in `ANcpLua/yurekami-refs` (private) — clone it when a
`REF-ONLY` row is revisited.

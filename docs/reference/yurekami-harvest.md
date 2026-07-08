# yurekami harvest — pattern routing table

> Source of truth for what qyl mines from the local reference clones under
> `~/yurekami` (11 projects, ~143k LOC). Full per-project dossiers + master index
> are vendored at [`./yurekami-extraction/`](./yurekami-extraction/) (start at
> [`INDEX.md`](./yurekami-extraction/INDEX.md)); the live clones remain at
> `~/yurekami/` as ground truth — **do not delete a clone until its pattern is
> fully transpiled and its `Status` below is `LANDED`**.
>
> **Nothing here is a straight file-merge.** The donors are Python / Lean / TS /
> Markdown; qyl is .NET + TS. Every row is a *pattern transpile* into qyl's own
> idiom, pulled **one at a time behind the existing verify gates**
> (`BuildVerify`, `VerifyInstrumentationTelemetryIsBoundedAndRedacted`,
> `VerifyInstrumentationHasNoStorageTenantKnowledge`,
> `VerifyCollectorHasNoRuntimeRoslynUtilityReference`).

## Legend
- **Status:** `TODO` · `IN-PROGRESS` · `LANDED` (transpiled + build green) · `REF-ONLY` (study only, not vendored) · `PARTIAL` (already partly present)

## Routing table (ranked by value × low integration risk)

| # | Pattern | Donor (path) | qyl receiving target | Status |
|---|---|---|---|---|
| 1 | **Never-crash SDK instrumentation** — lazy init, no-op-on-failure hooks, size-capped sanitization, debounced batch buffer w/ requeue | agent-lightning-optimizer `sdk-instrumentation/src/{instrument,buffer,hooks}.ts` | `internal/qyl.instrumentation/Instrumentation/QylServiceDefaultsExtensions.cs`, `ActivitySources.cs`, `ErrorCapture/ExceptionCapture.cs` | TODO |
| 3 | **Bounded / no-retention streaming aggregation** (Welford mean/variance, interval accounting) | batteries `Batteries/Data/RunningStats.lean`; aegis interval accounting | `internal/qyl.instrumentation/Instrumentation/Inventory/QylAgentInventory.cs` | PARTIAL (interval-accounting already landed 2026-07-07; Welford is the drop-in for any variance metric) |
| 4 | **ReDoS-guarded secret/PII redactor** — immutable deep traversal + nested-quantifier rejection + PII/destructive corpuses | aer-las `src/core/trace/redactor.ts`; aegis `_PII_PATTERNS`/`_DESTRUCTIVE_PATTERNS` | new `internal/qyl.instrumentation/Instrumentation/Redaction/` (must satisfy `...IsBoundedAndRedacted` gate) | TODO |
| 7 | **GenAI token/cost accounting** — per-model pricing table w/ cache-read discounting | aegis `CostEntry.compute_cost` | `services/qyl.collector/Cost/ModelPricingService.cs` + `Storage/DuckDbReaderExtensions.cs` | **GAP CONFIRMED** — `ModelPricingRow.CacheReadCost` column exists (`DuckDbReaderExtensions.cs:105`) but `ComputeCost` (`ModelPricingService.cs:39-40`) never applies it, AND no span-level cache-read-input-token field exists. Wiring needs: (a) add `GenAiCacheReadInputTokens` to `SpanStorageRow` + OTLP ingest/projection, (b) subtract cache-read tokens from billed input + price them at `CacheReadCost`. Scoped storage change — sequence as its own commit. |
| 5 | **Statistically-honest automation gate** — Wilson lower-bound + z-test, shrink-to-neutral; gate on severity AND significance | aer-las `competence/score.ts`; agent-lightning `deployer/regression.ts`, `fitness.ts` | qyl auto-review / canary math (see SSOT auto-merge first-green caution); candidate `eng/` helper | TODO |
| 6 | **Content-addressed cache for expensive/LLM callables** — hash canonicalized params, exclusion set, don't cache transient errors | autometrics `Metric._make_cache_key`; aer-las TTL read-through | collector GenAI token/cost lookup hot path | TODO |
| 8 | **Dependency-free diff / provenance** — LCS unified-diff + applyPatch; interval-coalescing line ranges + MurmurHash3 | agent-lightning LCS diff; agent-trace `src/trace-*.ts` | telemetry provenance dimension (AI-vs-human line attribution) — new capability, evaluate | REF-ONLY |
| 9 | **Disciplined monkey-patch / removed-symbol governance** — collision detection + audit log of replaced symbols | ArcticInference `arctic_inference/patching.py` | conceptual mirror of `eng/build/BuildVerify.cs` `removedCollectorTokens` guard | REF-ONLY |
| 10 | **Verbalized Sampling** — default→forbid→T-scored alternatives→pick lowest-T meeting quality | anti-sameness-plugin skills | eval rubric / any generation surface | REF-ONLY |
| 11 | **Rubric-as-weighted-predicate autograder** — sum `(predicate, points, label)`, grep concepts not strings | claude-code-challenges; aegis `@grader`/`GraderRegistry` | deterministic eval/CI gate over agent output | REF-ONLY |
| 12 | **CSP-safe single-file HTML report** — zero-JS pure-CSS collapsibles | aegis report; claude-code-challenges arcade UI | dashboard/artifact CSP constraints reference | REF-ONLY |
| 14 | **Swappable-stage ML pipeline + sklearn-free model export** (source-inlining freeze) | autometrics | only if qyl ships exportable eval metrics | REF-ONLY |
| 15 | **Invariant-as-struct-field** correctness (refinement fields, total-accessor trick) | batteries union-find `WellFormed`, `PrefixTable.valid` | design idea for type-state-heavy C# | REF-ONLY |
| 2 | **Fail-closed PreToolUse hook glue** — JSON-in/out HookOutput, tsx→JS→fail-open fallback, 5s timeout | aer-las `runtime/gateway.ts` + `runtime-gate.sh`; aegis `hooks/claude_code.py` | only if qyl grows an agent-gating / telemetry-on-tool-use surface | REF-ONLY |
| 13 | **Workspace-per-target autonomous-loop** — per-run state folder as sole SoT, dual prose+TSV log, filesystem inbox | auto-reverse-engineer | long-running agent-task structure reference | REF-ONLY |

## First harvest (lowest risk, highest fit)
Order to transpile: **#7** (pricing cross-check — file already exists) → **#3** (finish Welford) → **#1** (never-crash) → **#4** (redactor, behind the bounded/redacted gate). Everything else is `REF-ONLY` study until a concrete qyl surface needs it.

## Rule
Pull **one row at a time**, transpile into qyl idiom, run the relevant verify gate, commit. Only after a row is `LANDED` is its donor clone eligible for deletion. Until then the clone stays.

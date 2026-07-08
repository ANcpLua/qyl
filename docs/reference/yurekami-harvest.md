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
| 1 | **Never-crash SDK instrumentation** — lazy init, no-op-on-failure hooks, size-capped sanitization, debounced batch buffer w/ requeue | agent-lightning-optimizer `sdk-instrumentation/src/{instrument,buffer,hooks}.ts` | `internal/qyl.instrumentation/Instrumentation/QylServiceDefaultsExtensions.cs`, `ErrorCapture/ExceptionCapture.cs` | **REF-ONLY** — already present: `ExceptionCaptureMiddleware` catches+re-throws, `GlobalExceptionHooks.Register` is `Interlocked`-idempotent + deferred to a hosted service, `UseQyl` guards double-registration & skips OpenApi on non-web hosts. Only a 3-line inner-try/catch hardening of the two global handlers is even optional. |
| 3 | **Bounded / no-retention streaming aggregation** (Welford mean/variance, interval accounting) | batteries `Batteries/Data/RunningStats.lean`; aegis interval accounting | `internal/qyl.instrumentation/Instrumentation/Inventory/QylAgentInventory.cs` | **REF-ONLY** — interval-accounting already landed (2026-07-07). Welford needs a numeric measurement stream; `QylAgentInventory` only counts occurrences (no latency/value sample, no consumer) → adding variance would be speculative producer+consumer. |
| 4 | **ReDoS-guarded secret/PII redactor** — immutable deep traversal + nested-quantifier rejection + PII/destructive corpuses | aer-las `src/core/trace/redactor.ts`; aegis `_PII_PATTERNS`/`_DESTRUCTIVE_PATTERNS` | — | **REF-ONLY** — redaction already exists and is deliberately regex-free: collector `QylDataClassification.AddQylRedactors` (Erasing/Null redactors) + instrumentation redaction-by-omission enforced by `VerifyInstrumentationTelemetryIsBoundedAndRedacted`. A regex redactor would *introduce* the ReDoS surface qyl avoids. |
| 7 | **GenAI token/cost accounting** — per-model pricing table w/ cache-read discounting | aegis `CostEntry.compute_cost` | `services/qyl.collector/Cost/ModelPricingService.cs` + `Storage/*` + `eng/config/collector-semantic-policy.json` | **LANDED** — wired cache-read, cache-creation (write) AND reasoning tokens end-to-end: policy `projectionConstants`/`spanHotAttributeKeys` → regenerated catalog consts → `StorageAttributeProjection` → `SpanStorageRow` (+upsert) → `IngestionStorageMapper` → `ComputeCost` (additive, per-class rate w/ fallback to input/output). Lit up all three previously-dead `ModelPricingRow` cost columns. Collector build 0W/0E; `VerifyCollectorSemanticAttributeCatalog` green. |
| 5 | **Statistically-honest automation gate** — Wilson lower-bound + z-test, shrink-to-neutral; gate on severity AND significance | aer-las `competence/score.ts`; agent-lightning `deployer/regression.ts`, `fitness.ts` | — | **REF-ONLY** — no .NET consumer. Every ratio in qyl is either display-only (`DuckDbStore.Sessions.cs:138` BounceRate, `:179` ErrorRate → dashboard) or a config-fixed head-sampling probability (`QylAotSampler.cs:61`, `QylTraceSampling.cs:19`); neither is a measured success/failure proportion a confidence bound would protect. Only auto-accept logic is GitHub-Actions YAML gating on boolean check status. `grep wilson\|z-test\|confidence\|regression\|canary` → 0 hits. |
| 6 | **Content-addressed cache for expensive/LLM callables** — hash canonicalized params, exclusion set, don't cache transient errors | autometrics `Metric._make_cache_key`; aer-las TTL read-through | — | **REF-ONLY** — no consumer. The one repeated pure lookup (provider::model pricing) is already `FrozenDictionary`-memoized (`ModelPricingService.cs:5`). Per-span paths (`PersistedAttributePolicy.Serialize`) have span-unique inputs → ~0% hit rate; DuckDb dashboard queries are over mutable fresh telemetry → caching would serve stale data. Precondition (expensive *pure* fn, *repeated identical* inputs) is unmet. |
| 8 | **Dependency-free diff / provenance** — LCS unified-diff + applyPatch; interval-coalescing line ranges + MurmurHash3 | agent-lightning LCS diff; agent-trace `src/trace-*.ts` | telemetry provenance dimension (AI-vs-human line attribution) — new capability, evaluate | REF-ONLY |
| 9 | **Disciplined monkey-patch / removed-symbol governance** — collision detection + audit log of replaced symbols | ArcticInference `arctic_inference/patching.py` | conceptual mirror of `eng/build/BuildVerify.cs` `removedCollectorTokens` guard | REF-ONLY |
| 10 | **Verbalized Sampling** — default→forbid→T-scored alternatives→pick lowest-T meeting quality | anti-sameness-plugin skills | eval rubric / any generation surface | REF-ONLY |
| 11 | **Rubric-as-weighted-predicate autograder** — sum `(predicate, points, label)`, grep concepts not strings | claude-code-challenges; aegis `@grader`/`GraderRegistry` | deterministic eval/CI gate over agent output | REF-ONLY |
| 12 | **CSP-safe single-file HTML report** — zero-JS pure-CSS collapsibles | aegis report; claude-code-challenges arcade UI | dashboard/artifact CSP constraints reference | REF-ONLY |
| 14 | **Swappable-stage ML pipeline + sklearn-free model export** (source-inlining freeze) | autometrics | only if qyl ships exportable eval metrics | REF-ONLY |
| 15 | **Invariant-as-struct-field** correctness (refinement fields, total-accessor trick) | batteries union-find `WellFormed`, `PrefixTable.valid` | design idea for type-state-heavy C# | REF-ONLY |
| 2 | **Fail-closed PreToolUse hook glue** — JSON-in/out HookOutput, tsx→JS→fail-open fallback, 5s timeout | aer-las `runtime/gateway.ts` + `runtime-gate.sh`; aegis `hooks/claude_code.py` | only if qyl grows an agent-gating / telemetry-on-tool-use surface | REF-ONLY |
| 13 | **Workspace-per-target autonomous-loop** — per-run state folder as sole SoT, dual prose+TSV log, filesystem inbox | auto-reverse-engineer | long-running agent-task structure reference | REF-ONLY |

## Harvest outcome (2026-07-08 — sweep complete)
All 15 rows scoped against the live qyl code. **1 LANDED, 14 REF-ONLY.** The single
real, consumer-backed gap was **#7 (GenAI cache/reasoning token cost)** — transpiled
and shipped. Candidates **#1, #3, #4, #5, #6** were each investigated and found
already-handled or without a consumer (see per-row evidence) — deliberately **not**
written, because speculative code violates qyl's "no unused surface" discipline. The
finding *is* the value: qyl already embodies the never-crash, bounded-aggregation,
redaction, and memoization patterns these donors demonstrate.

Net new liftable code from ~143k LOC of references = the cost wiring. That's expected:
the donors are Python/Lean/TS in different domains; qyl's overlap with them is
architectural, and qyl had already converged on the same patterns independently.

## Rule
Pull **one row at a time**, transpile into qyl idiom, run the relevant verify gate, commit. Only after a row is `LANDED` is its donor clone eligible for deletion. Until then the clone stays.

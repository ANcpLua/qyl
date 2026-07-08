# Extraction Sweep — Master Index

Master index for the code-extraction sweep of 11 projects under `/Users/ancplua/yurekami`.
Per-project dossiers live alongside this file at `./<project>.md`.

## 1. Overview

| Project | Language | ~LOC | One-liner | Dossier |
|---|---|---:|---|---|
| aegis | Python ≥3.12 (Pydantic v2, Click, Rich, OTel) | 6,092 | Sandboxed Claude Code agent simulation + runtime tool-firewall & earned-autonomy control plane. | [./aegis.md](./aegis.md) |
| aer-las | TypeScript (ESM, Node ≥20; SQLite, zod) | 5,303 | CLI + Claude Code plugin: simulate agents, Wilson-score competence, fail-closed PreToolUse gate. | [./aer-las.md](./aer-las.md) |
| agent-lightning-optimizer | TypeScript (Express + Next.js 14/React 19; Postgres) | 20,107 | Real-time evolutionary prompt-optimization for Claude Agent SDK via GA + human preference. | [./agent-lightning-optimizer.md](./agent-lightning-optimizer.md) |
| agent-trace | TypeScript (CommonJS, zero deps) | 985 | Reference impl of Agent Trace v0.1.0: git-diff line-range AI-vs-human code attribution. | [./agent-trace.md](./agent-trace.md) |
| anti-sameness-plugin | Markdown + JSON (Claude Code plugin) | 1,403 | Verbalized-Sampling plugin to escape LLM mode collapse across writing/design/security. | [./anti-sameness-plugin.md](./anti-sameness-plugin.md) |
| ArcticInference | Python 3.10+ on vLLM 0.11.0 + C++/CUDA | 13,888 | Snowflake vLLM plugin: sequence-parallelism, spec decoding, SwiftKV via monkey-patching. | [./ArcticInference.md](./ArcticInference.md) |
| auto-reverse-engineer | Markdown (prompt framework) | 176 | Three-file prompt framework turning any coding agent into an autonomous RE loop. | [./auto-reverse-engineer.md](./auto-reverse-engineer.md) |
| autometrics | Python ≥3.9 (dspy, scikit-learn, litellm, Deno) | 55,823 | Induces one interpretable eval metric from <100 labeled examples (ICLR 2026, Stanford). | [./autometrics.md](./autometrics.md) |
| batteries | Lean 4 v4.30.0 (Lake) | 33,040 | Community extended stdlib for Lean 4: verified data structures, tactics, linters, tree-shaking. | [./batteries.md](./batteries.md) |
| claude-code-challenges | Markdown + Python + TS + vanilla JS | 6,211 | LeetGPU-style gamified, auto-graded Claude Code CLI challenge set + arcade UI. | [./claude-code-challenges.md](./claude-code-challenges.md) |
| claude-plugins | JSON manifest + Markdown | 97 | Claude Code plugin-marketplace manifest advertising the anti-sameness plugin. | [./claude-plugins.md](./claude-plugins.md) |

Total: ~143,125 LOC across 11 projects.

## 2. Projects by Theme

### Agent observability, tracing & provenance
- **agent-trace** — line-level AI-vs-human attribution keyed to git revisions; the Agent Trace v0.1.0 schema.
- **aegis** — AgentTrace→Turn→ToolCall OTel-flavored schema + thread-safe streaming JSON logger; policy/firewall.
- **aer-las** — tool-call traces, ReDoS-guarded secret redactor, SHA-256 audit rows.
- **agent-lightning-optimizer** — never-crash SDK instrumentation + trajectory collector.

### Agent safety / earned-autonomy / runtime policy gating
- **aegis** — priority-ordered YAML tool-firewall, sliding-window rate limiter, autonomy tiers.
- **aer-las** — fail-closed PreToolUse hook, Wilson-score competence gating, first-match policy engine.
- **agent-lightning-optimizer** — statistical guardrails (z-test) gating destructive automation + auto-rollback.

### LLM evaluation / metrics / grading
- **autometrics** — auto-induced composite eval metric; sandboxed code self-healing; content-addressed LLM cache.
- **aer-las** / **agent-lightning-optimizer** — Wilson scores, Cohen's kappa, competence maps.
- **claude-code-challenges** — rubric-as-weighted-predicate autograder.
- **aegis** — plugin grader registry; destructive-command & PII rule corpuses.

### Prompt engineering / optimization frameworks
- **agent-lightning-optimizer** — structured prompt-genome genetic algorithm + 11 mutation meta-prompts.
- **anti-sameness-plugin** — Verbalized Sampling anti-mode-collapse primitive.
- **auto-reverse-engineer** — two-phase bootstrap→headless-loop autonomous agent prompt framework.

### Inference optimization / systems
- **ArcticInference** — vLLM monkey-patch framework, sequence/shift parallelism, suffix-tree spec decoding, CUDA op builder.

### Formal methods / Lean
- **batteries** — verified data structures, invariant-as-struct-field, Welford stats, artifact-based tree-shaking, linter framework.

### Claude Code plugins / tooling / marketplace
- **anti-sameness-plugin** — clean plugin scaffold (commands/skills/hooks).
- **claude-plugins** — marketplace manifest federation template.
- **claude-code-challenges** — self-contained challenge-directory convention + zero-dep arcade UI kit.

## 3. Cross-project Extractable Value (ranked)

Ranked by reusability and relevance to qyl's observability / agent-instrumentation work.

1. **Never-crash SDK instrumentation template** — *agent-lightning-optimizer* (`sdk-instrumentation/`). Lazy init, no-op-hook fallback on failure, size-capped payload sanitization, debounced size/time batch buffer with requeue-on-failure. **qyl:** the canonical "add one line, never break the host" shape for `qyl.instrumentation` — directly mirrors qyl's bounded-telemetry discipline.

2. **Fail-closed PreToolUse hook pattern** — *aer-las* (`runtime/gateway.ts` + `hooks.json` + `runtime-gate.sh`) and *aegis* (`hooks/claude_code.py`). Complete JSON-in/JSON-out HookOutput with tsx→JS→fail-open fallback, 5s timeout, `CLAUDE_PLUGIN_ROOT` resolution; aegis translates decisions into the exit-0/exit-2 `{"decision":"block"}` protocol. **qyl:** ready-made Claude Code hook glue if qyl grows an agent-gating or telemetry-on-tool-use surface.

3. **Bounded / no-sample-retention streaming aggregation** — *batteries* (Welford `RunningStats`) and *aegis* interval accounting. O(1)-memory streaming mean/variance. **qyl:** directly mirrors the recently-landed `QylAgentInventory` interval-accounting rewrite (SSOT 2026-07-07); Welford is the drop-in for any variance metric without retaining samples.

4. **ReDoS-guarded secret/PII redactor** — *aer-las* (`trace/redactor.ts`), with rule corpuses from *aegis* (`_PII_PATTERNS`, `_DESTRUCTIVE_PATTERNS`). Immutable deep traversal + nested-quantifier rejection heuristic for user-supplied regexes. **qyl:** reusable scrubber for any telemetry/trace pipeline that accepts user patterns — pairs with qyl's privacy/redaction guardrails.

5. **Statistically-honest automation gate (Wilson / z-test)** — *aer-las* (`competence/score.ts`) and *agent-lightning-optimizer* (`deployer/regression.ts`, `fitness.ts`). Wilson lower-bound + reliability-shrinkage toward neutral; gate destructive actions on BOTH severity AND significance. **qyl:** the "don't auto-act on noise" canary and earned-autonomy/reliability-signal math (also aligns with qyl auto-merge/first-green caution).

6. **Content-addressed cache for expensive/LLM callables** — *autometrics* (`Metric._make_cache_key`). MD5 over canonicalized params with explicit exclusion set; transient exceptions re-raised without caching. Complemented by *aer-las* TTL read-through cache and *batteries* once-per-process lazy cache. **qyl:** hot-path caching for GenAI token/cost lookups and repeated evaluations.

7. **GenAI token/cost accounting shape** — *aegis* (`CostEntry.compute_cost`). Per-model Claude token pricing table with cache-read discounting. **qyl:** directly reusable for GenAI token/cost accounting surfaced by the qyl collector/skill (refresh rates/model-ids from the Claude API reference).

8. **Dependency-free diff / attribution engines** — *agent-lightning-optimizer* (LCS unified-diff + applyPatch) and *agent-trace* (unified-diff parser + interval-coalescing ranges + MurmurHash3). **qyl:** line-level AI-vs-human provenance keyed to git revisions is a natural telemetry provenance dimension; MurmurHash3 is a fast non-crypto fingerprint.

9. **Disciplined monkey-patch / removed-symbol governance** — *ArcticInference* (`patching.py` ArcticPatch: collision detection + audit log of replaced symbols). **qyl:** conceptual runtime cousin of qyl's `removedCollectorTokens` build guard; the lazy-after-fork entry-point plugin pattern is a real gotcha worth cataloguing.

10. **Verbalized Sampling prompt primitive** — *anti-sameness-plugin* / *claude-plugins*. Identify default → forbid → generate T-scored alternatives → pick lowest-T that meets quality. **qyl:** liftable into any generation/agent surface to avoid generic output; also usable as eval rubrics.

11. **Rubric-as-weighted-predicate autograder** — *claude-code-challenges* + *aegis* `@grader`/`GraderRegistry`. Sum `(predicate, points, label)` tuples; grep for concepts not exact strings. **qyl:** deterministic eval/CI gate over open-ended agent output.

12. **Zero-JS / zero-dep self-contained artifact templates** — *aegis* (CSP-safe single-file HTML report, pure-CSS collapsibles) and *claude-code-challenges* (Web Audio SFX, localStorage, CRT CSS). **qyl:** CSP-safe single-file report shape aligns with dashboard/artifact constraints.

13. **Workspace-per-target autonomous-loop framework** — *auto-reverse-engineer*. Generic prompt repo + per-run state folder as sole source of truth; dual prose+TSV attempt log; filesystem inbox as async human-in-the-loop. Reusable structure for long-running agent tasks.

14. **Composable swappable-stage ML pipeline + sklearn-free model export** — *autometrics*. Each stage an ABC constructor-arg; freeze trained model into a standalone dependency-light module via source-inlining. Blueprint for lightweight exportable eval components.

15. **Invariant-as-struct-field correctness pattern** — *batteries*. Refinement fields (union-find `WellFormed`, `PrefixTable.valid`) for correct-by-construction data structures; total-accessor trick removes bounds-checking obligations. Portable design idea for type-state-heavy C#.

## 4. What To Do Next

- **First harvest for qyl:** items #1 (never-crash instrumentation), #3 (Welford/interval aggregation — already partially adopted), #4 (redactor), #5 (Wilson/z-test gates), and #7 (token/cost accounting). These map cleanly onto qyl's existing instrumentation, privacy, bounded-telemetry, and GenAI-cost surfaces and carry the lowest integration risk.
- **Reference-only / study:** ArcticInference patch governance (#9) as a conceptual mirror of qyl's removed-symbol guards; autometrics pipeline/export (#14) if qyl ever ships exportable eval metrics.
- **Sequencing:** treat this INDEX as the routing table — pull one pattern at a time behind qyl's existing verify gates (`BuildVerify`, instrumentation-bounded/redaction gates) rather than bulk-importing any project. The two pure-config repos (claude-plugins, and the manifest layer of anti-sameness-plugin) are templates, not code to vendor.

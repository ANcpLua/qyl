# AER LAS — Agent Episode Recorder + Look-Ahead Simulation

**One-line:** A TypeScript CLI + Claude Code plugin that simulates Claude Code agents against scenario packs, measures per-task-class competence with Wilson scores, and then gates risky tool calls at runtime via a `PreToolUse` hook policy engine with an audit trail.

**Stack / language:** TypeScript (ESM, Node ≥ 20), `commander` CLI, `better-sqlite3` (WAL), `zod` schema validation, `yaml`, `cross-spawn`, `chalk` + `cli-table3` output, `glob`. Built with `tsup`, tested with `vitest`. Distributed both as an `aer` npm bin and as a Claude Code plugin (`plugin/.claude-plugin/plugin.json`).

Approx total tracked lines (ts/sh/yaml/json/md): **~9,372**; TypeScript source alone: **~5,303 LOC**.

---

## 1. What it is / why

Two coupled subsystems around one shared data store (SQLite):

- **AER (simulation):** Run Claude Code (`claude -p ... --output-format json`) N times against versioned YAML **scenarios**, each in an isolated git-clone **workspace**, capture the tool-call **trace**, run **graders** (tests/lint/forbidden-cmd/diff-size/file-pattern), and roll episode outcomes into a **competence map** keyed by `(scenarioClass, toolCategory)`.
- **LAS (runtime safety):** A `PreToolUse` hook reads a tool attempt from stdin, evaluates it against YAML **policies** (hard-blocks + competence-gated conditional-allow/escalate), consults the cached competence map, returns an `allow`/`deny`/`ask` permission decision, and writes an **audit** row. Fails **closed** on error or missing policy.

The thesis (see `specs/prd.md`): earned autonomy — a tool category is only allowed unattended once simulation proves a competence threshold for that scenario class.

## 2. Architecture

```
Scenario YAML ─► loader/pack ─► Orchestrator (semaphore concurrency)
                                    │  per work item:
                                    ▼
             Workspace (git clone --local / cp) ─► Runner (spawn `claude`)
                                    │                     │
                                    ▼                     ▼
                            Trace capture ◄──── stdout JSON / stream-json parse
                                    │ redact secrets
                                    ▼
                            Graders (parallel) ─► computeOutcome (critical-gate)
                                    │
                                    ▼
                      Trace Store (SQLite) ─► Competence map (Wilson upsert)
                                    │
   ┌────────────────────────────────┘
   ▼
Runtime Gateway (PreToolUse hook, stdin→stdout)
   loadPolicies ─► evaluatePolicy(rule match → hard-block/conditional/escalate)
              ▲ CompetenceCache (TTL 60s) ─► buildCompetenceMap
              └─ logAuditEntry (sha256 context hash) ─► audit_log
```

Data flows one direction into the DB from simulation, and the runtime side reads the competence map back out. Single SQLite file `.aer/data/aer.db`, 6 tables (episodes, trace_entries, grade_results, competence_scores, audit_log, policy_versions) + `_migrations`.

## 3. Module-by-module map

### Core types & config
- `src/core/types.ts` — All domain types: Scenario/Episode/TraceEntry/Grader/CompetenceScore/PolicyRule/AuditEntry/HookInput/HookOutput, plus `AUTONOMY_LEVELS` (0 read-only → 3 autonomous). The single contract surface; everything else keys off it.
- `src/core/config.ts` — Zod-validated `AerConfig` loader; `DEFAULT_CONFIG` + deep-merge from `.aer/config.yaml`.

### Persistence
- `src/db/schema.ts` — Array-of-SQL migrations + indices; `_migrations` tracking table.
- `src/db/connection.ts` — Singleton `getDb()`, auto-mkdir, WAL pragma, idempotent migration runner in a transaction.

### Scenario subsystem
- `src/core/scenario/schema.ts` — Zod schemas for Scenario / ScenarioPack / GraderConfig (7 scenario classes, 6 grader types).
- `src/core/scenario/loader.ts` — Load/validate one scenario or a whole dir; aggregates all validation errors before throwing.
- `src/core/scenario/pack.ts` — `loadPack` (`{name}.pack.yaml` file OR directory of scenarios) + `resolveScenarios` (ID→file resolution with ID-mismatch guard).

### Episode subsystem
- `src/core/episode/workspace.ts` — Isolated workspace: git repos → `git clone --local --no-checkout` then `checkout -- <ref>` (flag-injection safe); non-git → recursive `cp`. Cleanup retries once (Windows lock).
- `src/core/episode/runner.ts` — Spawns `claude -p <prompt> --output-format json [--allowedTools ...] [--append-system-prompt ...]` with SIGTERM→SIGKILL timeout; parses JSON output into a trace; redacts; classifies status completed/failed/timeout.
- `src/core/episode/orchestrator.ts` — Promise-based **semaphore** for concurrency; per-item create→run→grade→cleanup; `computeOutcome` = success iff zero critical grader failures and ≥1 grader ran.

### Grading subsystem
- `src/core/grading/runner.ts` — Grader factory registry; builds graders from config, runs all in parallel, converts throws/unknown-types into failed results.
- `src/core/grading/test-pass.ts` / `lint-pass.ts` — Run a shell command (default `npm test` / `npm run lint`), pass on exit 0, keep last 500 chars as evidence.
- `src/core/grading/forbidden-cmd.ts` — Regex-scan every `Bash` tool-use in the trace against forbidden patterns (critical severity).
- `src/core/grading/diff-size.ts` — Parse `git diff --stat` insertions+deletions vs `maxLines`.
- `src/core/grading/file-pattern.ts` — glob required/forbidden file patterns in the workspace.
- `src/core/grading/types.ts` — `GraderFactory` / registry types.

### Competence subsystem
- `src/core/competence/score.ts` — **Wilson lower-bound** score, `getConfidenceLevel` (insufficient/low/medium/high by episode-count tiers of minEpisodes), `computeCompetenceScore`.
- `src/core/competence/map.ts` — `buildCompetenceMap` (read), `updateCompetenceFromEpisodes` (group by class×toolCategory, upsert with additive counts), `getCompetenceForTool` (most-conservative min), tool→category mapping.
- `src/core/competence/staleness.ts` — days-since-last-run staleness boolean + fresh/stale/expired label.

### Trace subsystem
- `src/core/trace/capture.ts` — Parse `claude --output-format stream-json` NDJSON lines into TraceEntries; sequential tool_use↔tool_result pairing.
- `src/core/trace/redactor.ts` — Built-in secret regexes (AWS keys, GH tokens, bearer, PEM keys, emails) + user patterns with a **ReDoS guard** (`isSafeRegex`); deep-redacts objects/arrays immutably.
- `src/core/trace/store.ts` — Atomic `saveEpisode` (episode+trace+grades in one transaction), plus typed row→domain reconstruction queries.

### Policy / runtime subsystem
- `src/core/policy/schema.ts` — Zod schemas for PolicyRule/PolicySet.
- `src/core/policy/defaults.ts` — 7 hardcoded `DEFAULT_HARD_BLOCKS` (rm -rf, force-push, reset --hard, chmod 777, curl|sh, dd of=/dev/, credential exfil).
- `src/core/policy/evaluator.ts` — **The core:** first-match-wins rule engine; hard-block→deny, conditional-allow/escalate check competence score+episode thresholds → allow or ask.
- `src/core/policy/loader.ts` — Load/validate policy YAML files from a dir.
- `src/core/runtime/gateway.ts` — Hook entry: read stdin JSON, validate cwd (path-traversal guard), load+merge policies, build competence map (cached), `handleHookInput`, audit-log, emit `HookOutput`. Fails **closed** (deny) on error or no policy.
- `src/core/runtime/cache.ts` — `CompetenceCache` with TTL refresh (default 60s) to avoid per-call DB reads on the hot hook path.
- `src/core/runtime/audit.ts` — `logAuditEntry`, `getAuditLog` (filterable), `computeContextHash` (SHA-256 of session|tool|input).

### CLI
- `src/cli/index.ts` — `commander` wiring: `aer init | sim run|report|trace | policy check | competence show | runtime status`.
- `src/cli/commands/init.ts` — Scaffolds `.aer/{config.yaml,policies,scenarios,data}` from `templates/` with **inline fallbacks** baked into the source (works when template dir absent).
- `src/cli/commands/sim-run.ts` — Loads pack, checks `claude` on PATH, runs orchestrator with progress, saves episodes, updates competence, prints summary table.
- `src/cli/commands/sim-report.ts` — Aggregates episodes by class → pass-rate + top-failure table (or `--json`).
- `src/cli/commands/sim-trace.ts` — Detailed episode trace by ID (exact or prefix, with ambiguity detection).
- `src/cli/commands/competence-show.ts` — Color-coded competence table (score %, confidence, staleness status).
- `src/cli/commands/policy-check.ts` / `runtime-status.ts` — Validate policy files / health-check config+policies+DB.

### Plugin & hooks
- `hooks/pre-tool-use.ts` — 2-line entry that calls `runGateway()`.
- `plugin/hooks/hooks.json` — Registers `PreToolUse` (runtime-gate.sh, 5s), async `PostToolUse` (trace-capture.sh), `SessionStart` (session-init.sh) for `Bash|Edit|Write|Task|WebFetch|WebSearch`.
- `plugin/scripts/*.sh` — Bash wrappers: gate via `tsx` then compiled JS then fail-open; async trace append to `trace.jsonl`; session context injection.

## 4. Notable code

### (a) Wilson score — the competence primitive
`src/core/competence/score.ts:24`
```ts
export function wilsonScore(successes: number, total: number, confidence = 0.95): number {
  if (total === 0) return 0;
  const z = confidence === 0.99 ? 2.58 : 1.96;
  const p = successes / total, n = total, z2 = z * z;
  const denominator = 1 + z2 / n;
  const centerAdjustment = p + z2 / (2 * n);
  const margin = z * Math.sqrt((p * (1 - p)) / n + z2 / (4 * n * n));
  return Math.max(0, Math.min(1, (centerAdjustment - margin) / denominator));
}
```
Uses the **lower bound** of the Wilson confidence interval instead of naive success/total, so few observations yield a conservative score (10/10 ≠ 100% confidence). This is what makes "earned autonomy" statistically honest.

### (b) Policy evaluator — first-match rule engine
`src/core/policy/evaluator.ts:1225`
```ts
export function evaluatePolicy(policy, toolAttempt, competenceMap): PolicyDecision {
  const toolCategory = getToolCategory(toolAttempt.toolName);
  for (const rule of policy.rules) {
    if (!ruleMatches(rule, toolAttempt)) continue;
    switch (rule.type) {
      case 'hard-block': return { action: 'deny', ruleId: rule.id, reason: rule.reason };
      case 'conditional-allow': {
        const minScore = rule.conditions?.minCompetenceScore ?? 0;
        const score = findCompetenceScore(competenceMap, rule.match.scenarioClass, toolCategory);
        if (score !== undefined && score >= minScore && totalEpisodes >= minEpisodes)
          return { action: 'allow', ruleId: rule.id, competenceScore: score, ... };
        return { action: 'ask', reason: `Competence threshold not met ...`, ... };
      }
      case 'escalate': { /* like conditional but defaults min 0.8 / 10 episodes */ }
    }
  }
  return { action: policy.defaultAction, reason: 'No matching rule found' };
}
```
Clean, testable pure function. The gate couples policy authoring to measured competence — the novel bit vs. static allowlists.

### (c) Fail-closed gateway with path-traversal guard
`src/core/runtime/gateway.ts:382` (+ `validateCwd` :246)
```ts
export async function runGateway(): Promise<void> {
  try {
    const input: HookInput = JSON.parse(await readStdin());
    const cwd = validateCwd(input.cwd);           // resolve+normalize, reject '..'
    const policy = loadPolicies(cwd);
    if (!policy) { /* emit deny: "No policy configured (fail-closed)" */ return; }
    const cache = new CompetenceCache(join(cwd,'.aer','data'), competenceConfig);
    const output = handleHookInput(input, policy, cache.getMap());
    logAuditEntry(db, { ...computeContextHash(input) ... });   // best-effort
    console.log(JSON.stringify(output));
  } catch (error) {
    // emit deny: `Gateway error (fail-closed): ...`
  }
}
```
Safety-critical default: any parse/IO error or missing policy → **deny**. Audit failures are swallowed so they never block the decision.

### (d) Secret redactor with ReDoS guard
`src/core/trace/redactor.ts:635`
```ts
function isSafeRegex(pattern: string): boolean {
  if (/(\+|\*|\{)\)?(\+|\*|\{)/.test(pattern)) return false;  // nested quantifiers
  if (pattern.length > 500) return false;
  return true;
}
```
Built-in patterns catch AWS/GitHub/bearer/PEM/email; user-supplied patterns are screened for catastrophic-backtracking shapes before compilation. Deep, immutable redaction over trace objects.

### (e) Promise semaphore for episode concurrency
`src/core/episode/orchestrator.ts:26`
```ts
function createSemaphore(max: number): Semaphore {
  let current = 0; const queue: Array<() => void> = [];
  return {
    async acquire() { if (current < max) { current++; return; }
      return new Promise(resolve => queue.push(resolve)); },
    release() { current--; const next = queue.shift(); if (next) { current++; next(); } },
  };
}
```
Dependency-free bounded concurrency; all work items launched at once, throttled by the semaphore.

### (f) Flag-injection-safe git workspace
`src/core/episode/workspace.ts` — `git clone --local --no-checkout` then `git checkout -- <ref>` (the `--` prevents a malicious ref from being parsed as flags). Non-git dirs fall back to recursive copy. Cleanup retries once after 1s to survive Windows file locks.

## 5. Extractable value

1. **Wilson-lower-bound competence scoring** (`competence/score.ts`) — drop-in for any "should we trust this automation yet?" gate; pairs a statistically-sound score with a confidence tier keyed to sample size. Directly reusable for qyl-style reliability/earned-autonomy signals.
2. **Fail-closed stdin/stdout PreToolUse hook pattern** (`runtime/gateway.ts` + `plugin/hooks/hooks.json` + `runtime-gate.sh`) — a complete, copyable template for Claude Code tool-gating hooks: JSON-in/JSON-out `HookOutput` shape, tsx→compiled-JS→fail-open shell fallback, 5s timeout, `${CLAUDE_PLUGIN_ROOT}` resolution.
3. **Pure first-match policy engine** (`policy/evaluator.ts`) — hard-block + threshold-conditional rule evaluation as a side-effect-free function; trivially unit-testable, portable to any allow/deny/ask decision surface.
4. **ReDoS-guarded secret redactor** (`trace/redactor.ts`) — reusable secret/PII scrubber with a nested-quantifier rejection heuristic and immutable deep-object traversal; useful anywhere telemetry/traces may contain user regexes or secrets.
5. **SQLite migration + singleton-connection pattern** (`db/`) — array-of-SQL migrations with a `_migrations` tracking table, WAL, transactional apply; minimal, no ORM.
6. **TTL competence cache** (`runtime/cache.ts`) — generic time-boxed read-through cache for hot-path DB reads.
7. **Dependency-free promise semaphore** (`orchestrator.ts`) — bounded-concurrency task runner in ~15 lines.
8. **Inline-fallback scaffolder** (`cli/commands/init.ts`) — templates copied from disk when present, else identical inline strings compiled into the binary, so `init` works from a global install with no template assets.
9. **Claude subprocess harness** (`episode/runner.ts`) — spawn `claude -p --output-format json`, timeout with SIGTERM→SIGKILL escalation, parse both `json` and `stream-json` outputs into a normalized trace. A ready pattern for programmatic Claude Code invocation + eval.

## 6. Build / run

```bash
npm install
npm run dev        # tsx src/cli/index.ts  (run from source)
npm run build      # tsup → dist/cli/index.js (+ hooks/pre-tool-use.js), esm, node20
npm run typecheck  # tsc --noEmit
npm test           # vitest run

# Usage
aer init                                   # scaffold .aer/
aer sim run --pack <name> --episodes 20    # needs `claude` on PATH
aer sim report [--json]
aer sim trace <episode-id>
aer competence show [--class <c>] [--json]
aer policy check
aer runtime status
```
`better-sqlite3` is a native dep kept `external` in tsup. As a Claude Code plugin, `plugin/.claude-plugin/plugin.json` + `plugin/hooks/hooks.json` register the gateway/trace/session hooks.

## 7. Notes / observations

- **Status:** README says "Foundation complete… implementation in progress," but the codebase is substantially complete end-to-end (sim → competence → runtime gate all wired). Version `0.1.0`.
- **Behavioral quirk worth flagging:** `gateway.ts loadPolicies()` hardcodes the merged `defaultAction: 'allow'` and `failBehavior: 'open'` when combining policy files, **overriding** each YAML's own `defaultAction`/`failBehavior` (the template ships `defaultAction: ask`). So an un-matched tool call is allowed by the live gateway even though the authored policy said "ask" — a real merge-semantics gap.
- Tool→category mapping (`Bash→shell`, `Edit/Write→edit`, etc.) is duplicated in `competence/map.ts` and `policy/evaluator.ts` — a candidate for consolidation.
- No test files are present in the tree despite the vitest setup.

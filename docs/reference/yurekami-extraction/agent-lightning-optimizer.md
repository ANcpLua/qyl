# Agent Lightning Prompt Optimizer

**One-line summary:** A real-time evolutionary prompt-optimization platform for Claude Agent SDK agents — it collects agent execution trajectories, gathers comparison-based human feedback (DPO-style), runs a genetic algorithm over prompt "genomes," applies Claude-powered prompt mutations, and ships winning prompts through an approval + auto-rollback deployment pipeline.

**Stack / language:** TypeScript end-to-end. Six independent services/packages:
- **collector** — Express REST API (helmet, cors, zod, `pg`, uuid) for trajectory ingestion.
- **optimizer** — standalone Node genetic-algorithm daemon (no HTTP; setInterval evolution loop).
- **mutator** — Node service wrapping `@anthropic-ai/sdk` for LLM prompt mutations.
- **deployer** — Node service (raw `http` module + `node-cron`) for approval workflow, regression detection, rollback.
- **dashboard** — Next.js 14 (App Router) + React 19 + TanStack Query + Tailwind + shadcn/ui review/admin UI (~12k LOC, largest module).
- **sdk-instrumentation** — publishable npm helper (`@agent-lightning/instrumentation`) that hooks into the Claude Agent SDK.
- **db** — PostgreSQL 14+ schema (11 tables, triggers, plpgsql fitness function).

Approx **20,100 LOC** of TypeScript/TSX (+ ~1,060 lines SQL). Inspired by Microsoft Research's *Agent Lightning* RL framework, but optimizes **prompts** instead of model weights, using human preference instead of an RL reward signal.

---

## 1. Architecture overview

```
Claude Agent SDK apps
   │  (hooks: SessionStart / PreToolUse / PostToolUse / Stop)
   ▼
sdk-instrumentation  ──HTTP──▶  collector (REST)  ──▶  PostgreSQL
                                                        │
      ┌──────────────────┬──────────────────────────────┼─────────────────────┐
      ▼                  ▼                               ▼                     ▼
  dashboard          optimizer                        mutator              deployer
 (human review)   (genetic algo daemon)          (Claude mutations)   (approval + rollback)
```

Data + control flow:
1. **Collect.** Instrumented agents stream `TrajectoryStep`s to the collector, which persists them (`trajectories` table, `steps` JSONB).
2. **Compare.** Trajectory pairs with similar tasks but different `prompt_version_id`s are queued (`review_queue`) for humans.
3. **Review.** Reviewers pick A/B/tie + rate success & efficiency → `comparison_feedback`.
4. **Evolve.** The optimizer computes fitness from win-rate + success-rate + efficiency, does tournament selection with elitism, uniform crossover over prompt *components*, and mutation. New candidate `prompt_versions` are written back.
5. **Mutate.** The mutator applies one of 11 Claude-driven meta-prompt mutations to a version and validates the result.
6. **Deploy.** A candidate goes through a multi-approver vote, baseline metrics are captured, it deploys, and a scheduled/cron regression check compares post-deploy metrics to baseline — auto-rolling-back on critical, statistically-significant regressions.

The genetic "genome" is `PromptContent = { systemPrompt: string, toolDescriptions: Record<string,string>, subagentPrompts?: Record<string,string> }`. Crossover and mutation operate at the granularity of these components — this is the core domain insight.

---

## 2. File-by-file / module map

### sdk-instrumentation/ (`@agent-lightning/instrumentation`, ~805 LOC)
- `src/instrument.ts` — Public `instrument(config)` entry. Lazy-initializes a trajectory on first hook call; returns `{ hooks, flush, complete, getSessionId, getTrajectoryId }`. **Graceful-degrade design: never throws into the host agent** — on init failure it swaps in no-op hooks.
- `src/hooks.ts` — Builds the four SDK lifecycle hooks; each `PreToolUse`/`PostToolUse` creates a numbered `TrajectoryStep`, sanitizes+size-caps payloads (50 KB truncation), buffers them. `Stop` flushes and completes.
- `src/buffer.ts` — `StepBuffer`: size- and time-based batching with a debounced auto-flush timer and **requeue-on-failure** (`buffer.unshift(...steps)`).
- `src/client.ts` — `LightningClient`: thin `fetch` wrapper with Bearer auth, start/append/batch-append/complete/upload/ping endpoints.
- `src/types.ts` — `TrajectoryStep`, `Outcome`, `Trajectory`, `InstrumentationConfig`, `Hooks`.
- `examples/*.ts` — basic, manual-completion, multi-task, error-handling usage samples.

### collector/ (~770 LOC)
- `src/index.ts` — Express app; endpoints `/trajectories` (POST full / GET list), `/trajectories/start`, `/trajectories/stream`, `/trajectories/:id/complete`, `/trajectories/:id`, `/health`. Consistent `{success, data|error}` envelope; central Zod-error → 400 handling.
- `src/schemas.ts` — Zod schemas for every input/output (`TrajectoryInputSchema`, `TrajectoryFiltersSchema` with `z.coerce` query parsing + pagination caps, etc.). Single source of truth for wire types via `z.infer`.
- `src/services/trajectory.ts` — Service layer: create/start/appendStep/complete/get/list + a `formatTrajectoryResponse` mapper between DB rows and API shape.
- `src/middleware/auth.ts` — Bearer-token middleware + an `optionalAuthMiddleware`; warns (not fails) when `API_KEY` unset.
- `src/db.ts` — `pg` access layer.

### optimizer/ (genetic algorithm, ~2,360 LOC)
- `src/genetic/evolution.ts` — **`EvolutionEngine`**: the GA loop. Per generation: update fitness → detect plateau → adapt mutation rate → tournament+elite selection → crossover+mutation offspring → archive non-elites → emit `GenerationStats`. `runEvolutionLoop()` drives it on an interval across active branches.
- `src/genetic/selection.ts` — Tournament select, elitism, roulette, **stochastic universal sampling**, truncation, and a fitness+diversity `selectDiverse`.
- `src/genetic/crossover.ts` — Single-point, **uniform** (per-tool mix-and-match), section-based (markdown-header aware), paragraph-blend, and multi-parent crossover over `PromptContent`.
- `src/genetic/mutation.ts` — Mutation-type selection, `shouldMutate`, simple in-process fallback mutation, and the `MutationService` interface + `PlaceholderMutationService` (so the optimizer runs without the mutator online).
- `src/genetic/fitness.ts` — Weighted composite fitness (winRate 0.5 / successRate 0.3 / efficiency 0.2) with a **reliability penalty** that blends unreliable low-sample scores toward 0.5. Also relative-fitness normalization and ranking.
- `src/genetic/population.ts` — `Population`: loads/holds versions for a branch, creates candidates, archives/rejects/promotes, computes stats, getBestN.
- `src/index.ts` — Env-driven config, loads active branches, starts the loop, graceful SIGINT/SIGTERM shutdown.
- `src/types.ts` / `src/db.ts` — Zod-validated `EvolutionConfig`, domain types, DB access.

### mutator/ (Claude-powered mutation, ~1,650 LOC)
- `src/service.ts` — `MutationService.applyMutation`: fetch version → validate structure → pick model by complexity → apply with **exponential-backoff retry (rate-limit aware)** → validate output → persist new version + log the attempt (success/fail, duration). `generateVariants(n)` and `applyRandomMutation`.
- `src/prompts.ts` — 11 meta-prompt templates (one per mutation) with a `{original_prompt}` slot and a strict "return ONLY the modified prompt" instruction; `formatMutationPrompt()`.
- `src/mutations/index.ts` — `MUTATIONS` registry mapping each `MutationType` → `{name, description, weight, complexity, apply}`; weighted random selection and complexity→model routing.
- `src/mutations/{rephrase,examples,verbosity,structure,tone}.ts` — Individual mutation implementations calling `anthropic.messages.create` and extracting text blocks.
- `src/validation.ts` — Length bounds, empty/no-op detection, extreme-length-ratio warnings, keyword-retention heuristic, and a **Jaccard word-overlap semantic-similarity** guard (comment notes production should use embeddings).
- `src/types.ts` / `src/db.ts` — Config, types, persistence.

### deployer/ (approval + rollback, ~2,440 LOC)
- `src/index.ts` — Hand-rolled `http` server (regex route matching), wires all services, and **two node-cron jobs**: hourly approval-expiry sweep, and 15-min active-deployment regression monitor with auto-rollback.
- `src/approval.ts` — `ApprovalService`: request/approve/reject/status. Enforces permissions, one-vote-per-approver, expiry, and multi-approval thresholds; flips version status to `approved` when quorum reached.
- `src/deployment.ts` — `DeploymentService`: `deploy` (approval-gated, captures baseline, schedules regression eval, links previous deployment), `rollback`, `autoRollback` (picks a system admin), history/current queries.
- `src/regression.ts` — `RegressionDetector`: compares baseline vs post-deploy metrics, classifies severity (low→critical), generates human recommendations, decides auto-rollback (critical + statistically significant only), and schedules windowed evaluations via `setTimeout`.
- `src/metrics.ts` — `MetricsService`: baseline/post-deploy/current metric capture, `compareMetrics`, a **two-proportion z-test** for significance, Wald confidence intervals, trends, and trajectory-count-weighted aggregation.
- `src/notifications.ts` — Slack-webhook notification service.
- `src/types.ts` / `src/db.ts` — Deployment/approval/regression types + persistence (`initializeDeployerSchema`).

### dashboard/ (Next.js, ~12,080 LOC)
- `src/lib/metrics.ts` — All admin analytics SQL: system metrics, reviewer stats & streaks, fitness trends, **Cohen's kappa (pairwise + overall inter-rater reliability + confusion matrix)**, queue health + backlog alerting, per-agent summaries with up/down/stable trend.
- `src/lib/diff.ts` — Full **LCS-based line diff** engine: `computeLCS`, backtracking `generateDiffLines`, hunk grouping with context, `diffPromptContent` (system prompt + per-tool + per-subagent), unified-diff formatting, and `applyPatch`.
- `src/lib/prompts.ts`, `src/lib/db.ts`, `src/lib/utils.ts` — Prompt model helpers, `postgres`/`sql` client, cn/util.
- `src/hooks/useKeyboardNavigation.ts` — Vim-style (j/k), arrows, Enter/Space expand, Escape collapse-all, Home/End keyboard nav; ignores input/textarea focus.
- `src/hooks/{useReviewQueue,useAdminMetrics,useSyncScroll}.ts` — TanStack Query data hooks + synced side-by-side scrolling.
- `src/components/` — `ComparisonInterface`, `ReviewQueue`, `TrajectoryViewer`, `PromptDiffViewer`, `VersionTimeline`, `LineageGraph`, `BranchManager`, `PromptEditor`, `ApprovalQueue`, `DeploymentHistory`, `ReviewStats`, `KeyboardShortcutOverlay` + shadcn `ui/*`.
- `src/components/admin/` — `SystemMetrics`, `ReviewerActivity`, `InterRaterReliability`, `FitnessTrends`, `QueueHealth`, `AgentOverview`.
- `src/app/api/**/route.ts` — **33 App-Router API routes** covering prompts (versions/branches/diff/merge/lineage/deploy/approve), reviews (queue/stats/skip), and admin (metrics/reviewers/agents/fitness/reliability/queue-health/approvals/deployments/rollback/export).

### db/
- `schema.sql` — 11 tables, enums, indexes (incl. partial indexes), triggers (auto-update reviewer activity, auto-complete review queue on feedback), and plpgsql `calculate_fitness()` + `get_next_version()`.
- `migrations/001_initial_schema.sql`, `seed.sql`.

---

## 3. Notable code

### (a) Component-wise uniform crossover of prompt "genomes" — `optimizer/src/genetic/crossover.ts:43`
The domain-specific heart of the GA: instead of splicing raw text, it recombines *structured* prompt components — system prompt chosen wholesale, individual tool descriptions mixed key-by-key, with a fallback to guarantee non-empty tool sets.

```ts
export function uniformCrossover(parent1, parent2, parent1Id, parent2Id): CrossoverResult {
  const systemPrompt = Math.random() < 0.5 ? parent1.systemPrompt : parent2.systemPrompt;
  const toolDescriptions: Record<string, string> = {};
  const allToolKeys = new Set([...Object.keys(parent1.toolDescriptions),
                               ...Object.keys(parent2.toolDescriptions)]);
  for (const key of allToolKeys) {
    const inP1 = key in parent1.toolDescriptions, inP2 = key in parent2.toolDescriptions;
    if (inP1 && inP2) toolDescriptions[key] = Math.random() < 0.5 ? parent1.toolDescriptions[key]
                                                                  : parent2.toolDescriptions[key];
    else if (inP1) { if (Math.random() < 0.5) toolDescriptions[key] = parent1.toolDescriptions[key]; }
    else if (inP2) { if (Math.random() < 0.5) toolDescriptions[key] = parent2.toolDescriptions[key]; }
  }
  if (Object.keys(toolDescriptions).length === 0)   // never emit a genome with no tools
    Object.assign(toolDescriptions, Math.random() < 0.5 ? parent1.toolDescriptions : parent2.toolDescriptions);
  ...
}
```
`sectionCrossover` (same file, `:175`) goes further, parsing markdown `#`/`##` headers into a section map and recombining whole sections.

### (b) Reliability-penalized composite fitness — `optimizer/src/genetic/fitness.ts:82`
Fitness blends three weighted signals, then *pulls low-evidence scores toward neutral 0.5* so a version that "won" 2/2 lucky comparisons doesn't dominate selection.

```ts
let composite = weights.winRate*winRate + weights.successRate*successRate + weights.efficiency*avgEfficiency;
if (totalComparisons < MIN_COMPARISONS_FOR_RELIABILITY) {           // MIN = 5
  const reliabilityFactor = totalComparisons / MIN_COMPARISONS_FOR_RELIABILITY;
  composite = composite * reliabilityFactor + 0.5 * (1 - reliabilityFactor);  // shrink toward 0.5
}
```
Win-rate itself is confidence-weighted: a tie contributes `0.5 * comparison.confidence`.

### (c) Plateau detection → adaptive mutation rate — `optimizer/src/genetic/evolution.ts:196`
Escapes local optima by watching max-fitness variance across recent generations and doubling (up to 80%) the mutation rate when stuck.

```ts
async detectPlateau(branchId) {
  const recent = (await getGenerationHistory(branchId, this.config.plateauThreshold+1))
                   .slice(0, this.config.plateauThreshold);
  const maxF = recent.map(h => h.maxFitness);
  const mean = maxF.reduce((a,b)=>a+b,0)/maxF.length;
  const variance = maxF.reduce((s,f)=>s+(f-mean)**2,0)/maxF.length;
  const noImprovement = newestMax <= oldestMax;
  return variance < 0.001 && noImprovement;
}
// adaptMutationRate(): base = mutationRate*2, ×1.5 if variance<0.0001, capped at 0.8
```

### (d) LCS diff for prompt versions — `dashboard/src/lib/diff.ts:47`
A from-scratch dynamic-programming diff (no external diff dep) that produces git-style hunks with configurable context, used to render side-by-side prompt version comparisons and unified-diff export.

```ts
function computeLCS(a: string[], b: string[]): number[][] {
  const lcs = Array(a.length+1).fill(null).map(() => Array(b.length+1).fill(0));
  for (let i=1;i<=a.length;i++) for (let j=1;j<=b.length;j++)
    lcs[i][j] = a[i-1]===b[j-1] ? lcs[i-1][j-1]+1 : Math.max(lcs[i-1][j], lcs[i][j-1]);
  return lcs;
}
// generateDiffLines backtracks the table; groupIntoHunks(lines, context=3) builds @@ hunks
```

### (e) Cohen's kappa inter-rater reliability — `dashboard/src/lib/metrics.ts:253`
Real statistics on reviewer agreement, corrected for chance, over trajectory pairs reviewed by two people.

```ts
const pObserved = agreements / sharedReviews.length;
sharedReviews.forEach(r => { countsA[r.pref_a]++; countsB[r.pref_b]++; });
const pExpected = (countsA.A*countsB.A + countsA.B*countsB.B + countsA.tie*countsB.tie) / (n*n);
const kappa = (pObserved - pExpected) / (1 - pExpected);
```
`calculateOverallReliability()` builds the full pairwise-kappa matrix + a 3×3 A/B/tie confusion matrix.

### (f) Two-proportion z-test gating auto-rollback — `deployer/src/metrics.ts:94` + `regression.ts:246`
Regression severity is classified, but **auto-rollback only fires on `critical` severity AND statistical significance** — preventing noisy rollbacks on small samples.

```ts
const pooled = (p1*n1 + p2*n2)/(n1+n2);
const se = Math.sqrt(pooled*(1-pooled)*(1/n1 + 1/n2));
const zScore = Math.abs(p1-p2)/se;
return zScore > 1.96;      // 95% confidence; also requires n1,n2 >= 30
// shouldAutoRollback: detected && severity==='critical' && comparison.statisticallySignificant
```

### (g) Non-crashing, requeueing telemetry buffer — `sdk-instrumentation/src/buffer.ts:44`
Batches steps, auto-flushes on a debounced timer, and on network failure puts the batch back at the front of the queue — instrumentation must never lose data or break the agent.

```ts
async flush() {
  if (this.flushing || this.buffer.length === 0) return;
  this.flushing = true; this.clearTimer();
  const steps = [...this.buffer]; this.buffer = [];
  try { await this.onFlush(steps); }
  catch (e) { this.buffer.unshift(...steps); }   // requeue on failure
  finally { this.flushing = false; }
}
```

---

## 4. Extractable value

Concrete, reusable pieces worth lifting into other projects (notably qyl-style observability / agent-tooling):

1. **Never-crash SDK instrumentation pattern** (`sdk-instrumentation/`): lazy trajectory init, no-op hook fallback on init failure, size-capped payload sanitization (50 KB truncation with preview), and a debounced size/time batch buffer with requeue-on-failure. This is a clean, provider-agnostic template for any "add one line, capture telemetry, never break the host" library — directly relevant to qyl's instrumentation surface.

2. **Structured "prompt genome" GA** (`optimizer/src/genetic/`): the idea of treating `{systemPrompt, toolDescriptions, subagentPrompts}` as a genome and doing component-wise crossover/mutation rather than text-splicing. Reusable for any prompt/config-optimization loop. Selection.ts is a self-contained library of selection operators (tournament, elitism, roulette, SUS, truncation, diversity-aware).

3. **Reliability-shrunk fitness + plateau-adaptive mutation**: two small, high-value tricks — Bayesian-flavored shrink-to-neutral for low-sample scores, and variance-based plateau detection that raises mutation pressure. Applicable to any bandit/eval-ranking system with sparse feedback.

4. **Dependency-free LCS diff engine** (`dashboard/src/lib/diff.ts`): complete git-style hunk diff + unified-diff formatter + patch applier in ~400 lines, zero deps. Drop-in for any "compare two versions of text/config" UI.

5. **Statistical guardrails for automated actions** (`deployer/src/metrics.ts` + `regression.ts`): two-proportion z-test, Wald CIs, trajectory-weighted metric aggregation, and the policy of gating destructive automation (rollback) on *both* effect-size severity *and* significance. A reusable "don't auto-act on noise" pattern for any canary/regression monitor.

6. **Inter-rater reliability toolkit** (`dashboard/src/lib/metrics.ts`): Cohen's kappa, pairwise-kappa matrix, and confusion matrices computed straight from SQL — reusable wherever human labels/reviews need quality metrics.

7. **Human-in-the-loop review UX primitives**: Vim-style keyboard navigation hook, synced-scroll hook, side-by-side comparison + keyboard shortcut overlay — a compact pattern kit for building fast review/labeling dashboards.

8. **Multi-approver deployment + auto-rollback workflow** (`deployer/`): quorum voting, expiry, baseline capture, windowed post-deploy evaluation via cron, and auto-rollback selecting a system admin. A reusable approval-gate + safe-deploy skeleton.

9. **Zod-single-source wire contracts** (`collector/src/schemas.ts`): every request/response validated by Zod with types derived via `z.infer`, plus `z.coerce` query parsing and pagination caps — a tidy contract pattern.

10. **11 prompt-mutation meta-prompts** (`mutator/src/prompts.ts`): a ready-made library of "improve this prompt" instructions (clarity, examples±, verbosity±, edge-cases, restructure, tone±, constraints, simplify) with weighted selection and complexity→model routing.

---

## 5. Build / run instructions

Prereqs: Node 18+, PostgreSQL 14+, an Anthropic API key (for the mutator).

```bash
# 1. Database
createdb agent_lightning
psql -d agent_lightning -f db/migrations/001_initial_schema.sql
psql -d agent_lightning -f db/seed.sql          # optional

# 2. Each service: npm install, copy .env.example -> .env, set DATABASE_URL etc.
cd dashboard  && npm install && cp .env.example .env && npm run dev   # :3000
cd collector  && npm install && cp .env.example .env && npm run dev   # :3001 (needs API_KEY)
cd optimizer  && npm install && npm run dev                           # GA daemon (POPULATION_SIZE, MUTATION_RATE, ...)
cd mutator    && npm install && npm run dev                           # needs ANTHROPIC_API_KEY
cd deployer   && npm install && npm run dev                           # :3002 (SLACK_WEBHOOK_URL optional)
```

Instrument an agent:
```ts
import { query } from '@anthropic-ai/claude-agent-sdk';
import { instrument } from '@agent-lightning/instrumentation';
const lightning = instrument({ collectorUrl: 'http://localhost:4000',
                               apiKey: process.env.LIGHTNING_API_KEY, agentId: 'my-agent' });
for await (const m of query({ prompt: "...", options: { hooks: lightning.hooks } })) {}
```

Docker: each of `collector/`, `mutator/`, `deployer/` ships a `Dockerfile`.

**Config knobs:** optimizer env — `POPULATION_SIZE=20`, `ELITE_COUNT=2`, `TOURNAMENT_SIZE=3`, `CROSSOVER_RATE=0.7`, `MUTATION_RATE=0.3`, `PLATEAU_THRESHOLD=5`, `EVOLUTION_INTERVAL=60000`. Deployer env — `EVALUATION_WINDOW_MINUTES=30`, `MIN_SAMPLE_SIZE=50`, `SUCCESS_RATE_THRESHOLD=0.05`, `EFFICIENCY_THRESHOLD=0.10`, `BASELINE_WINDOW_MINUTES=60`.

**Note (port drift):** README/SDK examples reference the collector on `:4000`, while `collector/src/index.ts` defaults to `PORT=3001`; deployer defaults `:3002`, dashboard `:3000`. Set env explicitly to reconcile.

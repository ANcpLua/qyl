# auto-reverse-engineer

**One-line summary:** A prompt framework (three markdown files, no code) that turns any coding-agent harness (Codex, Claude, etc.) into an autonomous, structured, per-target reverse-engineering loop.

**Stack/language:** Pure Markdown prompt engineering — no source code, no build system. Designed to be driven by an external agent CLI (`codex exec --yolo`, `claude -p`) in a shell loop. ~176 lines total.

---

## Architecture overview

The framework is a two-phase, two-agent design that separates *setup* from *execution* and isolates each reverse-engineering target in its own workspace:

```
repo root (generic, reusable)
├── README.md        ← human-facing docs + run instructions
├── bootstrap.md     ← PHASE 1 prompt: scaffold a per-target workspace
└── program.md       ← PHASE 2 prompt: the runtime RE loop (copied into each workspace)

projects/<project-slug>/   (created by bootstrap, target-specific state)
├── program.md             (copy)
├── goal.md, context.md
├── progress.md
├── attempts.md, attempts.tsv
├── paths.md               (prioritized investigation paths + blockers + triggers)
├── inbox/README.md        (human drop-zone for requested resources)
├── knowledge-base/        (index/facts/hypotheses/disproved, by topic)
└── artifacts/ derived/ scripts/ logs/
```

**Flow:** Agent #1 runs `bootstrap.md` → interviews the human, scaffolds `projects/<slug>/`, copies `program.md` in. Agent #2 runs `program.md` from inside that folder → executes a continuous hypothesis→experiment→record loop, polling `inbox/` for human-supplied resources and never stopping until the goal is met or a hard block is hit. The framework repo stays generic; all mutable state lives in the project folder, which is the agent's declared "sole source of truth."

---

## File-by-file map

- **`README.md`** (70 lines) — Human documentation. Explains the two-file model, the bootstrap→run handoff, and gives concrete launch commands. Key detail: Codex needs an external `while true; do codex exec --yolo ...; sleep 1; done` shell loop because it doesn't self-loop, whereas Claude "usually does not need a shell loop." Includes a worked example (reverse-engineering a CGM app's APK + BLE glucose protocol) and the design principles the runtime agent must follow (structured KB, avoid repeating failed work, always work highest-priority ready path).

- **`bootstrap.md`** (36 lines) — Phase-1 initialization prompt. Instructs the agent to create a clean isolated workspace and *not* to do any RE yet. Specifies exact deliverables (files/dirs above), a 4-step workflow (Ask Required Info → Suggest Optional Resources → Initialize Workspace → Handoff), and per-file initialization rules. Notable guardrails: "Do not invent missing artifacts" and "Prefer `partially blocked` over `blocked` if any useful work can begin."

- **`program.md`** (70 lines) — Phase-2 runtime loop prompt. The core artifact. Defines a Startup ritual (read goal/context, review progress/attempts/paths, inventory artifacts), a 10-step experimentation Loop, hard Rules, structured output formats for `attempts.md`/`attempts.tsv`, knowledge-base organization, and a "NEVER STOP" continuous-operation contract with three explicit exit conditions.

---

## Notable content (verbatim excerpts)

### 1. The core experimentation loop — `program.md:19-30`
The heart of the framework: a self-ranking, blocker-aware, human-in-the-loop cycle.

```md
**The Loop:**
1. Quickly recheck blockers, then pick the highest-value ready path from `paths.md`.
2. State a clear hypothesis.
3. Run a time-boxed experiment (default 15-30 mins) using the simplest, cheapest method.
4. Save outputs to `derived/`, `scripts/`, or `logs/`.
5. Update `knowledge-base/` (categorize as facts, hypotheses, or disproved).
6. Record the outcome in `attempts.md` and `attempts.tsv`.
7. Update `progress.md` and re-rank paths in `paths.md`, abandoning superseded paths...
8. Read the `inbox/README.md` and see if the human has provided any new resources.
9. Queue human help in `paths.md` and `inbox/README.md` when it would unblock...
10. Repeat.
```
Why valuable: encodes cheapest-experiment-first, evidence-tiering, and dual human/agent async coordination via a filesystem inbox.

### 2. Structured attempt-logging schema — `program.md:42-56`
Machine- and human-readable dual logging: a rich markdown block plus a greppable TSV row.

```md
## ATTEMPT-0001
- timestamp: YYYY-MM-DDTHH:MM:SSZ
- status: keep | discard | blocked | needs-human
- hypothesis: [What you tried to prove]
- method: [How you tested it]
- evidence: [Bullet points of findings]
- outcome: [Result summary]
- artifacts: [Paths to relevant files]
- next action: [Logical next step]
```
TSV: `attempt_id\tstatus\tconfidence\tcategory\thypothesis\tsummary\tartifacts`
Why valuable: the parallel prose+TSV pattern gives both narrative context and queryable state.

### 3. Falsifiable knowledge-base contract — `program.md:60`
```md
Every finding must answer: Claim? Why believe it? Evidence? Confidence? Falsification criteria?
```
Why valuable: forces epistemic rigor — findings are tiered into `facts.md`/`hypotheses.md`/`disproved.md` by topic, preventing hallucinated certainty from compounding.

### 4. Continuous-operation contract — `program.md:62-70`
```md
**NEVER STOP** unless:
1. The goal in `goal.md` is achieved.
2. The human explicitly interrupts.
3. A documented hard block is reached AND configuration permits stopping.

A blocked path is not a blocked run: document it, queue human help, and switch paths.
```
Why valuable: distinguishes a blocked *path* from a blocked *run* — keeps the agent productive by pivoting layers (static → dynamic → network → crypto) instead of halting.

### 5. Bootstrap anti-hallucination rule — `bootstrap.md:23,36`
> "Do not invent missing artifacts." / "Prefer `partially blocked` over `blocked` if any useful work can begin."
Why valuable: two terse rules that prevent the two most common failure modes of autonomous agents — fabricating inputs and giving up prematurely.

---

## Extractable value

1. **Workspace-per-target isolation pattern** — generic prompt repo + mutable per-run state folder declared as "sole source of truth." Directly reusable for any long-running autonomous agent task (not just RE): research runs, migrations, audits. Mirrors qyl's SSOT/memory-stream discipline.

2. **Two-phase bootstrap→program split** — separating an interactive scaffolding prompt from a headless looping prompt is a clean, reusable agent-orchestration pattern. The bootstrap interview (target, goal, artifacts, safety boundaries, stop-behavior) is a good generic intake checklist.

3. **Dual prose+TSV attempt log** — the `attempts.md` + `attempts.tsv` pairing (narrative for humans, tab-separated for grep/scripts) is a lightweight, tool-free "database" pattern worth lifting into any agent that must avoid repeating failed work.

4. **Falsifiability-required KB schema** — the 5-question finding template (Claim/Why/Evidence/Confidence/Falsification) + facts/hypotheses/disproved tiering is a portable rubric for any evidence-gathering agent to resist over-confidence.

5. **Filesystem `inbox/` as async human-in-the-loop channel** — agent queues requests in `inbox/README.md` and polls each loop for dropped resources; keeps working other paths meanwhile. Simple, harness-agnostic coordination primitive.

6. **Path re-ranking with trigger conditions** — `paths.md` as a live priority queue with blockers and auto-unblock triggers; "always work the highest-priority ready path" + "abandon superseded paths." A reusable planning-state model.

7. **Harness-portability note** — README's observation that Codex needs an external shell `while` loop while Claude self-loops is a practical cross-harness deployment insight.

---

## Build / run instructions

No build — pure prompts. To run (from README):

```bash
# Phase 1: scaffold a target workspace (interactive)
codex "run bootstrap.md"          # or: claude -p "run bootstrap.md"

# Phase 2: run the RE loop from inside the created project folder
# Codex (needs external loop):
while true; do
    codex exec --yolo "run program.md, target projects/<project-slug>/goal.md" 2>&1 | tee -a agent.log
    sleep 1
done

# Claude (self-loops):
claude -p "run program.md"
```

**Note:** The framework itself performs no reverse engineering and ships no tooling — the target agent creates helper scripts under `scripts/` at runtime. Safety/legal boundaries and hard-block stop behavior are captured during bootstrap and enforced by `program.md`'s Rules ("no risky real-world actions without approval," "preserve original artifacts").

# PRD — TypeSpec for Microsoft Agent Framework (`typespec-maf`)

> Status: **approved scope, execution gated until the microsoft-ai-hackathon submission
> (2026-06-14) is complete.** This document is the complete maximum scope. An executing
> agent takes the lowest unchecked PR, keeps the PR exactly that small, and changes
> NOTHING outside the task's file list. Written 2026-06-12; owner: Alexander (PO) +
> agent fleet. The PO does not want to re-edit this document — if a task seems to
> require violating a law in §4, the task is wrong: stop and surface it.

---

## 1 · Vision (stable)

**TypeSpec becomes the single typed source of truth for every declarative surface of
Microsoft Agent Framework — agents, workflows, hosted manifests — and every emitted
agent carries its telemetry declaration from birth.** One `tsp compile` produces the
YAML that MAF executes, the Telemetry Control Graph entry that declares what the agent
emits, and the conformance plan a runtime verifier proves. Declared agents, declared
telemetry, verifier on top: this is the qyl mission (self-healing validation loop,
0% human-in-the-loop as the asymptote) executed on Microsoft's agent stack.

The strategic bet: Microsoft demonstrably loves this pattern — they built
`@microsoft/typespec-m365-copilot` for exactly this reason — but **MAF itself has no
TypeSpec story**, and almost nobody outside Microsoft ships production TypeSpec
emitters. We do: four of them, in production, with lint infrastructure and a
generation pipeline (Weaver→TypeSpec). That asymmetry is the moat.

## 2 · Evidence — the gap is real (verified 2026-06-12, local checkout)

Ground truth: `~/RiderProjects/agent-framework` @ `4285bf012` (2026-06-11). All claims
below were verified against source, not docs (fleet law: local checkout over Learn).

MAF has **three hand-authored YAML dialects, none covered by any TypeSpec emitter**:

| # | Dialect | Loader / interpreter | Verified at |
|---|---|---|---|
| D1 | Prompt-agent YAML | `ChatClientPromptAgentFactory.CreateFromYamlAsync(text)` | `dotnet/samples/02-agents/DeclarativeAgents/ChatClient/Program.cs` |
| D2 | Workflow YAML (`kind: Workflow`, triggers `OnConversationStart`, actions `SetVariable`/`Goto`/`LoopContinue`/…, PowerFx as raw strings `value: =3`, hand-managed string ids) | `Microsoft.Agents.AI.Workflows.Declarative` (`DeclarativeWorkflowBuilder`, `ObjectModel/`, `PowerFx/`, `Interpreter/`) + `.Foundry` + `.Mcp` variants | `dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/`, `dotnet/tests/...UnitTests/Workflows/*.yaml` |
| D3 | Foundry hosted manifests (`agent.yaml` + `agent.manifest.yaml`) | Foundry hosting | `dotnet/samples/04-hosting/FoundryHostedAgents/**` |

Failure classes of these dialects today (the disease TypeSpec cures): untyped strings,
hand-managed ids and cross-references, PowerFx with zero compile-time checking, no
schema gate before the runtime crash, no telemetry contract whatsoever.

Microsoft's own precedent that validates the play: **`@microsoft/typespec-m365-copilot`**
(TypeSpec DSL → declarative-agent manifest + plugin manifest + OpenAPI for M365
Copilot; Learn: `microsoft-365/copilot/extensibility/overview-typespec`,
`build-api-plugins-typespec`, `typespec-capabilities`, plus the OpenAPI quality rules
in `openapi-document-guidance`). They applied the pattern to M365 Copilot and stopped
before their own agent framework. We finish the sentence.

Our proven assets (all shipped, all green as of 2026-06-12):

- `@qyl/telemetry-control-graph` — TypeSpec value-decorator emitter, 8 artifacts,
  hard invariants in `$onValidate` (TCG-001..010) — `qyl-api-schema@8b017c6`.
- `@ancplua/typespec-otelconventions-lint` — `createRule`-based policy linter with
  `defaultOptions` + exit-event whole-program rules.
- `@ancplua/typespec-emit-csharp`, `@ancplua/typespec-emit-ts-types` — contract codegen
  (now including TypeSpec 1.13 `ModelProperty` member-access mapping).
- `@ancplua/typespec-otel-semconv` — the Weaver play: upstream model → generated
  TypeSpec library, version-pinned, regenerate-don't-chase.
- `qyl.conformance` — runtime verifier (declared vs observed diff engine, severity
  law, `conformant` gate) — `qyl@9084b786`, CI green, 7/7 evals.
- `Qyl.Api.Contracts 0.2.0` on nuget.org with `TelemetryControlGraph` /
  `ConformanceReport` types.

## 3 · Product pillars (maximum scope)

### P1 — `@qyl/typespec-maf`: the emitter library

TypeSpec DSL + emitters for all three MAF dialects.

- **Decorator surface (authoring):** `@agent(name, description)`, `@instructions`,
  `@model`, `@tool` (function-tool declaration with typed parameters → MAF tool
  binding names), `@workflow`, `@trigger(kind)`, typed action models (one TypeSpec
  model per ObjectModel action kind), `@hosted` (D3 manifest data).
- **References instead of strings:** workflow actions reference variables, agents and
  other actions as TypeSpec references; the emitter assigns/derives stable ids.
  Renaming a variable is a compile-time refactor, not a grep-and-pray.
- **PowerFx surface, staged:** v0 emits raw expression strings via a `powerfx<T>`
  scalar (typed *slot*, untyped body); v1 adds known-function helpers and reference
  validation (every `Local.X` / `Topic.X` mentioned in an expression must exist as a
  declared variable — checked in `$onValidate`); full PowerFx parsing is explicitly
  out of scope (Microsoft ships the parser; we validate references, not semantics).
- **Emitters:** `maf-agent-yaml` (D1), `maf-workflow-yaml` (D2), `maf-hosted` (D3).
  Options per emitter follow the `@qyl/telemetry-control-graph` conventions
  (`file-types`, `include-*`, JSONSchema-declared options, `NoTarget` diagnostics).
- **Round-trip gate (the eval law):** every emitted artifact must load through the
  REAL Microsoft loader in CI — D1 via `CreateFromYamlAsync`, D2 via
  `DeclarativeWorkflowBuilder`, D3 via schema validation — in a small .NET test
  harness referencing the pinned MAF packages. Emitting YAML nobody can load is the
  failure mode this project exists to kill; we do not get to have it ourselves.

### P2 — ObjectModel→TypeSpec generation (the Weaver play, applied to MAF)

The action/type library for D2 is **generated, not hand-written**. Source of truth is
the `ObjectModel/` namespace of the pinned `Microsoft.Agents.AI.Workflows.Declarative`
package (+ XML sidecars). A .NET reflection tool (`tools/maf-model-gen`) walks the
ObjectModel types per version pin and emits `generated/maf-actions.gen.tsp`
(`// <auto-generated/>`, DO-NOT-EDIT, regenerate via `build.sh`). Preview churn is
therefore a **pin bump + regenerate + diff review**, never a hand-port. This mirrors
`typespec-otel-semconv` exactly and is the reason the project survives MAF's preview
cadence. Hand-authored TypeSpec is allowed only for the stable authoring sugar
(decorators, scalars), never for action shapes.

### P3 — Control Graph integration: telemetry declared at birth

The qyl twist nobody else will have:

- A `@telemetry(...)` decorator (or convention) on `@agent`/`@workflow` declares the
  signals the generated agent emits — span names, metric instruments, attribute keys
  (semconv-typed via `@ancplua/typespec-otel-semconv` where applicable, `qyl.*`
  otherwise, policed by the existing lint rules).
- The emitter additionally emits a **TCG fragment** per agent — the same wire shape
  `@qyl/telemetry-control-graph` produces — so a deployment's control graph can be
  composed from `tsp compile` outputs instead of hand-maintained instances.
- Default stamping: every generated workflow gets qyl observability defaults
  (`service.name` = agent name, baseline span/log declarations for trigger and action
  execution) unless explicitly suppressed. Declared by default, opt-out, never opt-in.

### P4 — The conformance loop: qyl closes the circle

`qyl.conformance` already verifies declared vs observed. With P3, the loop closes over
MAF agents end-to-end: **TypeSpec declares agent + telemetry → MAF runs the agent →
collector observes → verifier diffs → `conformant` gates promotion.** Maximum scope
adds: (a) a `qyl verify` CLI entry (loads `conformance-plan.json` + snapshot adapter
from collector storage, writes `ConformanceReport` using `Qyl.Api.Contracts` types —
replacing the verifier's temporary DTOs, the swap point is marked in
`internal/qyl.conformance/qyl.conformance.csproj`); (b) report-driven remediation
*proposals* (drift finding → suggested TypeSpec diff), human-applied at first —
propose ≠ commit, the Kelsen law, until the loop has earned autonomy.

### P5 — Upstream & ecosystem strategy

- The library is **standalone-valuable regardless of upstream's response** — that is
  the hedge. But the goal is the gift: a proposal issue on `microsoft/agent-framework`
  ("TypeSpec for Agent Framework") with working samples once v0 round-trips, then a
  contribution path (their repo is MIT and accepts contributions).
- A **bridge sample** to `@microsoft/typespec-m365-copilot`: ONE TypeSpec source
  authoring both a MAF declarative workflow and an M365 Copilot declarative agent
  (Otto is the obvious demo subject post-hackathon). One model, two Microsoft
  runtimes — that is the conference-talk slide.
- Naming: working title `@qyl/typespec-maf`, repo `typespec-agent-framework`
  (extracted at PR-0; until then this PRD lives in qyl). Final npm scope decided at
  PR-0 (`@qyl/*` vs `@ancplua/*` — taste call, defaults to `@ancplua` for consistency
  with the existing emitter fleet unless the PO says otherwise at kickoff).

## 4 · Laws (non-negotiable invariants)

1. **TypeSpec is the source of truth.** Generated YAML/manifests are never hand-edited;
   wrong output ⇒ fix emitter or model, regenerate. (Fleet generated-files law.)
2. **Three-layer validation, exactly as established in qyl-api-schema:**
   compile errors (`$onValidate`) for broken shapes and impossible references;
   `createRule` linter rules with options for tunable policy; the runtime verifier for
   declared-vs-observed. A broken graph/workflow is never merely a lint warning.
3. **Round-trip or it doesn't ship:** an emitter PR is mergeable only with the
   Microsoft-loader round-trip test green against the pinned MAF version.
4. **Generated action shapes only (P2):** no hand-written TypeSpec for ObjectModel
   surfaces. Pin bump → regenerate → review diff.
5. **Version pins everywhere:** MAF packages, TypeSpec toolchain (≥1.13), semconv lib.
   No floating `latest`, no `-dev` ranges.
6. **Propose ≠ commit for remediation (P4):** the loop suggests; gates promote; nothing
   self-applies until the verifier history justifies it.
7. **No execution before 2026-06-15** (hackathon submission is 06-14; this project
   never competes with it for hours). Reversal condition: submission shipped early.

## 5 · Architecture

```text
                    ┌────────────────────────────────────────────────┐
                    │  typespec-agent-framework (repo, PR-0)         │
                    │                                                │
 ObjectModel ──────►│ tools/maf-model-gen ──► generated/*.gen.tsp    │
 (pinned MAF pkg)   │                                                │
                    │ lib/  @agent @workflow @trigger @tool          │
                    │       @telemetry  powerfx<T>  typed actions    │
                    │                                                │
                    │ emitters/ maf-agent-yaml   (D1)                │
                    │           maf-workflow-yaml(D2)                │
                    │           maf-hosted      (D3)                 │
                    │           tcg-fragment    (P3, TCG wire shape) │
                    │                                                │
                    │ tests/  vitest (emitter units)                 │
                    │         dotnet RoundTrip.Tests (real loaders)  │
                    └───────────┬───────────────────┬────────────────┘
                                │                   │
                 MAF runtime ◄──┘                   └──► qyl-api-schema TCG
            (agents/workflows run)                       (graph composition)
                                │                            │
                       collector observes             conformance-plan.json
                                └──────────► qyl.conformance ◄┘
                                             (verify → conformant gate
                                              → exporter config / promotion
                                              → remediation proposals)
```

Repo layout mirrors `qyl-api-schema` conventions 1:1 (package.json scripts
`build:emitters`/`compile`/`lint`, emitter package shape with `lib/main.tsp` +
`src/index.ts` + `dist/`, tsconfig copied, vitest in emitter packages, `[skip ci]`
discipline while Actions budget is exhausted — local validation is the gate, per the
2026-06-11 budget freeze; reversal: repos public or budget refilled).

## 6 · Pipeline — PRs to maximum scope

Execution contract: lowest unchecked box; one PR = one commit train; done means build
+ all listed gates green locally; tick the box in the same PR. Each PR lists its
done-when explicitly so no agent has to interpret.

### [ ] PR-0 — Repo extraction & scaffold
Extract this PRD into new repo `typespec-agent-framework` (this file becomes
`docs/PRD.md`; the qyl copy gets a pointer stub). Scaffold per qyl-api-schema
conventions; pin TypeSpec 1.13 line + MAF package versions in one `versions.props`-
style location; decide npm scope (default `@ancplua`).
**Done when:** `npm run build:emitters && npm run compile` green on an empty lib;
README states vision §1 in five sentences; repo private; PRD copy + stub committed.

### [ ] PR-1 — D1 vertical slice (agent YAML, end to end)
`@agent`/`@instructions`/`@model`/`@tool` decorators; `maf-agent-yaml` emitter; a
sample `.tsp` agent; **.NET round-trip test**: emitted YAML loads via
`ChatClientPromptAgentFactory.CreateFromYamlAsync` against pinned MAF.
**Done when:** round-trip green; emitted YAML byte-stable across two compiles
(determinism gate); lint + compile green.

### [ ] PR-2 — maf-model-gen (P2)
Reflection tool over pinned `Microsoft.Agents.AI.Workflows.Declarative` ObjectModel →
`generated/maf-actions.gen.tsp` (+ trigger kinds, variable scopes). Auto-generated
header, regenerate script, diff-review workflow documented.
**Done when:** generated lib compiles; spot-check eval pins ≥5 action shapes
(SetVariable, Goto, LoopContinue, conversation actions) against the package's own
test YAMLs.

### [ ] PR-3 — D2 vertical slice (workflow YAML)
`@workflow`/`@trigger` + generated typed actions; reference-based ids; `powerfx<T>`
v0; `maf-workflow-yaml` emitter; `$onValidate` hard invariants (unknown reference,
duplicate id, trigger-less workflow, action kind not in pinned model).
**Done when:** the package's own test workflows (`SetVariable.yaml`, `Goto.yaml`,
`LoopContinue.yaml`) are reproduced from TypeSpec sources byte-equivalently (modulo
key order — define canonical ordering in the emitter); round-trip via
`DeclarativeWorkflowBuilder` green; negative eval: `BadId.yaml`-class defect is a
compile error in TypeSpec where MAF only fails at load.

### [ ] PR-4 — PowerFx reference validation (P1 v1)
Expression reference extraction (`Local.*`, `Topic.*`, declared variables) +
`$onValidate` unknown-reference errors; linter rule (options: allowed scopes) for
style policy. No PowerFx semantic parsing (law §4 boundary).
**Done when:** eval matrix: valid refs pass, unknown ref = compile error, policy
violation = configurable warning.

### [ ] PR-5 — Telemetry declaration + TCG fragment (P3)
`@telemetry` surface; default stamping with opt-out; `tcg-fragment` emitter producing
the `@qyl/telemetry-control-graph` wire shape; semconv-typed keys via
`@ancplua/typespec-otel-semconv`; `otelconventions-lint` rules apply (attribute-key
format/roots) — reuse, do not re-implement.
**Done when:** a sample agent's fragment validates against the TCG JSON schema
(`control-graph.schema.json`) and composes into a qyl-api-schema graph instance
compile; lint rules fire on a planted bad key (negative eval).

### [ ] PR-6 — D3 hosted manifests
`@hosted` surface + `maf-hosted` emitter for `agent.yaml`/`agent.manifest.yaml`,
modeled from the FoundryHostedAgents samples; schema-validation round-trip (no live
Foundry dependency in CI).
**Done when:** sample manifests reproduced from TypeSpec; schema validation green.

### [ ] PR-7 — Conformance loop closure (P4)
`qyl verify` CLI in qyl repo: snapshot adapter (collector storage → ObservedSignal
JSONL), plan loader, report writer using `Qyl.Api.Contracts` ControlGraph types
(replace verifier DTOs at the marked swap point). End-to-end demo: TypeSpec agent →
emitted workflow + TCG fragment → run under collector → verify → `conformant`.
**Done when:** the e2e script runs no-tenant (fixture collector snapshot) and the
report round-trips through the published contract types; existing 7 verifier evals
still green plus ≥3 new e2e evals.

### [ ] PR-8 — Remediation proposals (P4 max)
Drift finding → proposed TypeSpec diff (e.g. observed-but-undeclared attribute →
proposed `@telemetry` addition), emitted as a reviewable patch file. Propose ≠ commit:
no auto-apply path exists in this PR by design.
**Done when:** golden evals map ≥4 finding kinds to deterministic proposal patches.

### [ ] PR-9 — Bridge sample + upstream proposal (P5)
One TypeSpec source → MAF workflow YAML + `@microsoft/typespec-m365-copilot` agent
manifest (Otto as subject). Proposal issue text for `microsoft/agent-framework`
drafted in `docs/upstream-proposal.md` (PO reviews before anything is posted —
outward-facing, explicitly NOT auto-published).
**Done when:** bridge sample compiles both targets from one source; proposal doc
ready for PO sign-off. Posting the issue is a PO action, never an agent action.

### [ ] PR-10 — Release hardening
README with architecture + the §2 gap table; samples gallery; npm publish of the lib
+ emitters (auth currently missing locally — `ENEEDAUTH`; resolve or defer with note);
NuGet trusted publishing for the .NET round-trip/test helpers if any ship; version
0.1.0 tags. Public flip decision is the PO's (fleet repo policy).
**Done when:** release flow per fleet law (bump → commit → tag → push → CI watch →
report), or each blocked step explicitly reported with its unblock condition.

## 7 · Risks & honest counters

| Risk | Reality check | Mitigation |
|---|---|---|
| MAF preview churn breaks emitters | Certain, it's preview | Law §4+§5: generated shapes + pins; churn = regenerate + diff |
| PowerFx is a real language; typing it is a tarpit | True | Hard boundary: reference validation only, never semantics (§3 P1) |
| Upstream ignores the proposal | Possible | Library is standalone-valuable (P1–P4 need no upstream blessing); proposal is upside, not dependency |
| Byte-equivalence in PR-3 too strict vs YAML serializer freedom | Likely friction | Canonical ordering defined in emitter; equivalence = semantic (parsed-tree) where byte fails, documented per case |
| Two-agent collision repeats | It cost us real files on 06-12 | One session owns this repo at a time; ownership handoff is explicit in chat — process note, not tooling |
| Actions budget still exhausted | True since 2026-06-11 | Local gates are the merge gates; `[skip ci]` discipline; reversal noted in §5 |
| Scope seduction before 06-14 | The PO is tired and excited | Law §7. This document exists so nothing needs deciding tomorrow. |

## 8 · Success metrics (max scope = all of these)

1. **Crash-class conversion:** ≥3 documented defect classes (bad id, unknown ref,
   undeclared action kind) that are runtime/load failures in raw MAF YAML and compile
   errors in typespec-maf — each pinned by a negative eval (PR-3/4).
2. **One-source fan-out:** one `.tsp` produces D1+D2(+D3) artifacts + TCG fragment +
   conformance plan in a single compile (PR-5/6).
3. **Loop closure:** e2e fixture run ends in a `ConformanceReport` with
   `conformant: true` produced from published contract types (PR-7).
4. **Bridge:** one source, two Microsoft runtimes (PR-9).
5. **Ecosystem signal:** upstream proposal ready (posted = PO's call); the emitter
   library is the reference implementation the proposal points at.

## 9 · Explicit non-goals

- Touching anything before the 2026-06-14 submission is shipped (Law §7).
- Replacing `@microsoft/typespec-m365-copilot` — we bridge to it, never compete.
- Forking or shimming MAF runtime behavior — emitters target the public loaders only.
- Full PowerFx parsing/type-checking.
- Auto-applied remediation (PR-8 stops at proposals; autonomy is earned later, in qyl,
  with verifier history as evidence).
- Hand-written TypeSpec mirrors of ObjectModel shapes (Law §4).

---

*Provenance: gap evidence verified against `~/RiderProjects/agent-framework` @
`4285bf012` on 2026-06-12; M365 emitter facts from Microsoft Learn
(`overview-typespec`, `build-api-plugins-typespec`, `typespec-capabilities`,
`openapi-document-guidance`, fetched 2026-06-12). Companion assets:
`qyl-api-schema@8b017c6`, `qyl@9084b786`, `Qyl.Api.Contracts 0.2.0` (nuget.org).*

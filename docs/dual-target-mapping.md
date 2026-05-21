# Dual-Target Mapping — `semconv-srcgen` Team

**Owner:** otel-shape-strategist (Phase 0, Task #1)
**Inputs:** [`testbed#1`](https://github.com/ANcpLua/semconv-testbed) shape contract; memories `dual-target-otel-shipping`, `contrib-review-bandwidth`, `pr-4362-status`; verified qyl `global.json` (`ANcpLua.NET.Sdk 3.4.33`); verified contrib `src/OpenTelemetry.SemanticConventions/*.csproj` (`Microsoft.NET.Sdk`, `netstandard2.0`).
**Status of dual-target positioning:** ratified. Every commit ships on **both** tracks. This document encodes the mapping; it does not re-open the decision.

---

## Premise (do not relitigate)

Every OTel-contrib-shaped commit produced by this team lands in **two repositories in parallel**:

1. **qyl-private** (`O-ANcppLua/qyl`, `~/RiderProjects/qyl/`) — fast track. Agents push here directly via worktrees.
2. **contrib-fork** (a personal fork of `open-telemetry/opentelemetry-dotnet-contrib`, branch `feat/semconv-srcgen-2856`, target issue [#2856](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/2856)) — slow track, gated by maintainer review (~6 month review-bandwidth window, `contrib-review-bandwidth` memory).

The qyl track is the source of truth for *emission logic*. The contrib track is a downstream view obtained by **cherry-pick + path/namespace map**, never by parallel hand-authoring. **Emit logic does not branch on target.** A generator hard-coding different emitted type names for contrib vs qyl would force per-target conditional code in the generator — that is rejected. The split is applied only at packaging/project metadata, embedded-resource naming, and the post-initialization marker-source namespace literal. Consumer generated output takes its namespace from the consumer's partial marker class.

qyl is the private-alpha proving ground, not the upstream API contract. It may
absorb churn, renames, broader experiments, and extra validation so the
OpenTelemetry contribution can stay idiomatic and reviewable. If qyl's current
shape conflicts with maintainer-preferred OpenTelemetry/.NET shape, qyl bends
toward upstream. Do not hide instrumentation registration glue, OTLP-shaped
output, `ILogger` generation, runtime delivery shims, or runtime-object
ownership inside the source-generator PR stack; those are separate proposals if
maintainers ask for them.

qyl also hosts private-only runtime instrumentation generators and DI glue under
`internal/qyl.instrumentation.generators/` and `internal/qyl.instrumentation/`.
Those are separate qyl-private projects and are not part of any upstream
OpenTelemetry proposal. The generator project has no static dependency on the
semantic-conventions source-generator pipeline; the runtime instrumentation
library consumes generated semantic-convention constants like an ordinary
qyl-private consumer.

---

## 1. Branch-mapping spec

### 1.1 qyl-side branch graph

```
origin/main (qyl)
   │
   ├── agent/otel-shape-strategist        (this branch — docs only)
   ├── agent/weaver-spec-auditor          (Phase 0 audit charter + CLI doc)
   ├── agent/generator-foundation-eng     (Phase 1: project skeleton + marker + base const gen)
   ├── agent/instrument-surface-eng       (Phase 2: PR-B metric names + PR-C events)
   ├── agent/activity-surface-eng         (Phase 2: PR-D meters + PR-E activity ext)
   └── agent/gen-test-harness-eng         (Phase 2–4: test harness + sample + AOT)
            │
            └── (when all green) → squash-merge into:
                       feat/semconv-srcgen   (qyl integration branch)
                                │
                                └── fast-forward into origin/main
                                    on completion of a delivery bundle (PR-A … PR-E)
```

**Rules:**

- Each engineer agent commits **only** to its named `agent/<name>` branch off `origin/main`.
- Agents do **not** push to remote. The strategist (this agent) coordinates integration; the orchestrator (opus-team-lead) handles cross-repo cherry-pick and any forced operations.
- Integration into qyl HEAD is via **squash-merge** of `agent/<name>` into `feat/semconv-srcgen`, then fast-forward of `feat/semconv-srcgen` into `main` once a delivery bundle is complete.
- A delivery bundle = one row of the cadence table (§1.3 below).

### 1.2 contrib-side branch graph

```
open-telemetry/opentelemetry-dotnet-contrib  main
   │
   └── (personal fork)                    feat/semconv-srcgen-2856
                                                │
                                                │  strategist cherry-picks
                                                │  completed qyl bundles
                                                │  with path/namespace map applied
                                                │  (see §2)
                                                ▼
                                          PR-A (skeleton) → opened first
                                          PR-B (metric names) → stacked on PR-A
                                          PR-C (event names+structs)
                                          PR-D (meters)
                                          PR-E (activity ext)
```

**Rules:**

- The contrib fork branch `feat/semconv-srcgen-2856` is the only branch on the fork side. PR-A … PR-E are **stacked PRs** on that branch (or sequential PRs if the maintainer prefers — confirmed per PR).
- Agents 3–6 **never touch contrib**. Only the strategist cherry-picks. Reason: the path/namespace map (§2) must be applied uniformly; if any agent commits to contrib directly the dual-target invariant breaks.
- Cherry-pick is `git cherry-pick -x` so the trailer records the qyl SHA of origin — this makes feedback assimilation (§3) traceable.

### 1.3 Cherry-pick cadence (PR-A → PR-E)

One cadence row per contrib PR. Each row names the qyl `agent/<name>` branch(es) whose squash-merged commits make up the bundle.

| Contrib PR | Scope (per testbed#1) | qyl source branches (squash-merged into `feat/semconv-srcgen`) | Strategist cherry-pick trigger |
|---|---|---|---|
| **PR-A** | Generator package skeleton, paired `[SemanticConventionAttributes("foo")]` / `[SemanticConventionIncubatingAttributes("foo")]` markers emitted by post-initialization source, full `resolved-registry.json` embedded resource, DTOs from the Weaver-derived registry projection, attribute-key constants, and enum-value constants. | `agent/generator-foundation-eng` + stability-intercept fixes + harness smoke | All green on qyl `feat/semconv-srcgen`; stable/incubating attribute snapshots prove the split. |
| **PR-B** | Metric-name constants and descriptor metadata, with paired stable/incubating marker projections. | `agent/instrument-surface-eng` (PR-B slice) | Squash-merge of PR-B slice on qyl + audit clearance from `agent/weaver-spec-auditor` resume. |
| **PR-C** | Event names and event payload structs, with paired stable/incubating marker projections. | `agent/instrument-surface-eng` (PR-C slice) | As above. |
| **PR-D** | Meter surface: typed factories over consumer-provided `System.Diagnostics.Metrics.Meter`, with paired stable/incubating marker projections and no generated global Meter ownership. | `agent/activity-surface-eng` (PR-D slice) | As above. |
| **PR-E** | Activity extension surface: typed `Activity.SetTag` helpers over consumer-provided `Activity`, with paired stable/incubating marker projections and no generated ActivitySource ownership. | `agent/activity-surface-eng` (PR-E slice) + `agent/gen-test-harness-eng` (final AOT smoke + all-surface matrix) | As above. |

**Cadence guard rails:**

- A row's cherry-pick **does not** start until the prior row is OPEN (does not need to be merged, just opened) on contrib. This preserves the stacked-PR review order.
- If contrib PR-A receives maintainer-requested changes mid-cadence, §3 governs assimilation. The strategist pauses outgoing cherry-picks (rows still in flight) only if the change invalidates a downstream row's assumptions; otherwise downstream rows proceed and back-port the change after.
- Stale-bot watch: any contrib PR sitting >10 days without substantive activity gets a **substantive** update (rebase onto contrib `main`, or a doc/test edit responding to a real finding). Empty pings buy nothing (`contrib-review-bandwidth`).

---

## 2. Namespace + path map

The map is the **only** thing that differs between targets. It is applied at project/package metadata, embedded-resource naming, and the post-initialization marker-source namespace literal. Generator output itself reads the consumer marker class's containing namespace; it does **not** hard-code either target name.

| Concern | qyl | contrib |
|---|---|---|
| Solution folder | `packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/` | `src/OpenTelemetry.SemanticConventions.SourceGeneration/` |
| Root namespace / post-init marker namespace | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration` | `OpenTelemetry.SemanticConventions.SourceGeneration` |
| Marker library csproj | none — marker attributes are emitted by `RegisterPostInitializationOutput` | none — same architecture after path map |
| Generator csproj | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator.csproj` | `OpenTelemetry.SemanticConventions.SourceGeneration.csproj` |
| MSBuild SDK | `ANcpLua.NET.Sdk` in qyl | `Microsoft.NET.Sdk` in contrib |
| Target framework (generator) | `netstandard2.0` (Roslyn analyzer requirement) | `netstandard2.0` (Roslyn analyzer requirement) |
| `MinVerTagPrefix` / version | qyl pre-1.0 (sub-1.0 sem-ver as per qyl repo convention) | `SemanticConventions.SourceGeneration-` (matches existing contrib MinVer convention) |
| Marker attribute fully-qualified name | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributesAttribute` | `OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributesAttribute` |
| Embedded resource path | `Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json` | `OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json` |
| Nuke build hooks (regen target) | Present (`Build.cs` `RegenSemConv` target — qyl convention) | Not present (contrib uses MSBuild + `dotnet test` directly) |

### 2.1 Invariant: emit logic does not branch on target

The substantive invariant is: **the only diff between two generator runs targeting two different namespaces must be the namespace declaration line.** Generator code never contains a conditional of the form `if (namespace == "Qyl.…") … else …` — the target namespace flows through emit code as a single string token, no branching.

**Mechanism (reconciled with shipping implementation, Phase 3 audit Finding 5):** the namespace token is read from the consumer's marker-class containing namespace at extraction time (`packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/Extractors/MarkerExtractor.cs:25-29`):

```csharp
var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
    ? string.Empty
    : typeSymbol.ContainingNamespace.ToDisplayString();
```

This is the consumer's chosen namespace (where they place their `[SemanticConventionAttributes("…")]`-decorated partial class), **not** the MSBuild `RootNamespace` property the earlier Phase 0 draft hypothesized. Both satisfy the substantive §2.1 invariant (single string token, no conditional emit logic). The `ContainingNamespace` approach is strictly better than the original design: one consumer can host multiple marker classes in distinct namespaces, which the `RootNamespace` model cannot express. **Phase 4 ratifies `ContainingNamespace` as canonical.**

Invariant test: `tests/qyl.opentelemetry.semconv.sourcegen.tests/NamespaceParameterizationTest.cs` runs the generator under two distinct consumer namespaces (`ConsumerA.Telemetry`, `ConsumerB.Different.Nested.Path`) and asserts `bodyA.Should().Be(bodyB)` after stripping the namespace declaration line — strict byte-equality of everything except the namespace token. Verified PASS by `weaver-spec-auditor` Phase 3 audit (`docs/weaver-audit-phase3-report.md` Evidence row "§2.1 emit-logic invariant").

If a maintainer-requested change on contrib appears to require branching emit logic, that is automatic P0 escalation to the orchestrator (§3 invariant **(d)**).

---

## 3. Maintainer-feedback assimilation rule

**Default:** maintainer-requested changes on the contrib PR apply to **qyl first**, via the responsible track-engineer agent (one of 3–6, whichever owns the qyl `agent/<name>` branch the contrib commit was cherry-picked from — the cherry-pick trailer makes this traceable). The strategist then re-cherry-picks the updated qyl commit to contrib (`git cherry-pick -x`, force-push the contrib branch with a clear changelog comment on the PR).

**Exception:** orchestrator (opus-team-lead) sign-off is required when the maintainer-requested change would break a **qyl-private invariant**. In that case the strategist opens a thread with the orchestrator before any code moves on either side. The four invariants:

- **(a) Qyl uses `ANcpLua.NET.Sdk 3.4.33` (not `Microsoft.NET.Sdk`).** Verified against `qyl/global.json` `msbuild-sdks` map. Any maintainer suggestion that the generator-side code must `<Project Sdk="Microsoft.NET.Sdk">` applies to contrib only; the qyl mirror keeps `ANcpLua.NET.Sdk`. (Mechanical: csproj diff sits in the path/namespace map, not in source.)
- **(b) Qyl publishes pre-1.0 versioning.** Contrib will eventually publish `OpenTelemetry.SemanticConventions.SourceGeneration` as a stable NuGet (tied to the existing `OpenTelemetry.SemanticConventions` 1.x track per `pr-4362-status` open decision). Qyl ships sub-1.0 indefinitely (private workspace; nothing depends on it externally). Maintainer asks for "bump to 1.0" → applies on contrib, not on qyl.
- **(c) Qyl includes Nuke build hooks (`Build.cs` `RegenSemConv` target) that contrib does not need.** Contrib uses MSBuild + `dotnet test` only. Maintainer asks to remove the Nuke hook → applies to the cherry-pick filter (the Nuke file is excluded by path from cherry-pick), not to qyl.
- **(d) Emit logic does not branch on target (§2.1).** If a maintainer-requested change requires per-target conditional emit, that is **not** a normal feedback item — it is an architectural escalation. Strategist must consult orchestrator before any code lands on either side.

**Cherry-pick traceability:** every cherry-pick uses `git cherry-pick -x` so the contrib commit body contains `(cherry picked from commit <qyl-sha>)`. When a maintainer requests a change on a contrib commit, the strategist resolves the source qyl SHA from that trailer, identifies the owning `agent/<name>` branch via `git branch --contains <qyl-sha>`, and routes the change request to that agent.

**Reverse direction (qyl → contrib drift):** if a qyl agent makes a follow-up commit on `agent/<name>` after the bundle was cherry-picked, the strategist must re-cherry-pick during the next cadence row, **not** force-push the contrib branch silently. Maintainer review state survives only if the contrib PR's commit graph is stable.

---

## 4. Version-pin alignment

Both targets pin the **same** `semconv_version` (currently **1.41.0** per [PR #4362](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/4362)) and the **same** Weaver commit. Drift between targets = **P0 escalation** that halts all tracks until re-synced.

### 4.1 Pinned versions

| Component | Pin | Pin location (both targets) |
|---|---|---|
| `open-telemetry/semantic-conventions` YAML model | `v1.41.0` @ commit `e018fe6f91862f5ed63c082f87697cddac596784` | generator's `generate.sh` (qyl: `src/SemConv.Gen/scripts/generate.sh`; contrib: `src/OpenTelemetry.SemanticConventions.SourceGeneration/scripts/generate.sh`) |
| `open-telemetry/weaver` | `v0.23.0` (Docker image `otel/weaver:v0.23.0`) | same `generate.sh` |
| `Microsoft.CodeAnalysis.CSharp` (Roslyn) | `${ROSLYN_PIN}` — see §4.2 | generator `.csproj` `<PackageReference>` |
| `resolved-registry-v2` JSON Schema commit | derived from Weaver pin | embedded in DTO type generation step (agent 2's CLI doc resolves the exact mechanism) |

### 4.2 Roslyn pin — divergence note

Testbed#1 references `Microsoft.CodeAnalysis.CSharp 4.14.0`; the orchestrator's verified-ground-truth table for this team says `5.0.0`. The actual resolved value is **ratified by agent 2 (`weaver-spec-auditor`) in Phase 0 Task #2** (CLI decision + audit charter seed) — agent 2 reads contrib's current analyzer csprojs at `main` HEAD and commits the resolved value in `docs/weaver-cli-decision.md`. This document carries the value as `${ROSLYN_PIN}` until then. The strategist (this agent) will land a follow-up commit replacing `${ROSLYN_PIN}` with the resolved literal once agent 2's doc lands; that follow-up commit is **not** a blocker for this commit.

Rationale for the placeholder pattern (rather than picking a value): a wrong literal here would propagate to PR-A's `.csproj` and become wrong-by-default for downstream rows. A placeholder fails loud (build failure when the generator csproj is bootstrapped) and forces alignment with agent 2's resolved truth. The same value lands on both targets in the same commit.

### 4.3 Verification mechanism (CI check)

A CI workflow on the qyl repo (path: `.github/workflows/verify-dual-target-pin-sync.yml`) runs on every push to `agent/*` and to `feat/semconv-srcgen` and `main`:

1. Resolve the qyl branch tip's `resolved-registry.json` SHA via `sha256sum` on the `EmbeddedResource` file path (qyl: `src/SemConv.Gen/Resources/resolved-registry.json`).
2. Fetch the contrib fork's `feat/semconv-srcgen-2856` branch tip's `resolved-registry.json` SHA from its corresponding path (`src/OpenTelemetry.SemanticConventions.SourceGeneration/Resources/resolved-registry.json`) via `gh api`.
3. Compare. If they differ, the job fails with a clear message naming both SHAs and the last cherry-pick trailer.
4. The job is **non-blocking** on `agent/*` branches (warning only; cherry-picks haven't happened yet) and **blocking** on `feat/semconv-srcgen` + `main` and on the contrib fork's `feat/semconv-srcgen-2856` branch (drift here means the cadence is broken).

The same job also verifies the Weaver pin: extract `weaver_version` from each side's `generate.sh` and assert equality.

The Roslyn pin is verified by a downstream restore check (`dotnet restore` in the qyl skeleton mirror-checks the resolved version) once §4.2 lands — until then, the CI step for Roslyn is a no-op placeholder.

---

## Sign-off

This document is the authoritative source for: branch naming on both sides, namespace + path map (single point of truth), maintainer-feedback routing rule, version-pin verification mechanism. Any subsequent agent who finds this doc disagreeing with reality must **escalate to the strategist** rather than diverging silently; mid-cadence drift is the failure mode this team is set up to prevent.

Cross-references:

- `agent/weaver-spec-auditor` produces `docs/weaver-cli-decision.md` and `docs/weaver-audit-charter.md` in parallel (Phase 0 Task #2). The Roslyn pin literal lives there once resolved; the audit charter binds compliance gates (`specification-principles.md`, `telemetry-stability.md`, `spec-compliance-matrix.md`, `glossary.md`) onto Tracks A–E emissions.
- `agent/generator-foundation-eng` (Task #3) consumes §2 path/namespace map verbatim when bootstrapping the qyl-side skeleton.
- `agent/instrument-surface-eng` (Task #4) and `agent/activity-surface-eng` (Task #5) bind to the cadence table in §1.3 for slice ownership.
- `agent/gen-test-harness-eng` (Task #6) implements the §2.1 invariant test (generator output diff under two consumer marker namespaces differs only in the namespace token).

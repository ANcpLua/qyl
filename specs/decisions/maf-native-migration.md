# v2 Amendment: MAF Native Migration

**Goal:** Make the migration mechanically true in code, tests, and docs. The old `qyl.agents` / `qyl.workflows` topology is gone. The remaining work is to make every surviving rule, README, and validation step describe the repo as it exists now.

## Target State

- `qyl.agents` and `qyl.workflows` stay deleted.
- The collector stays the data plane. It must not take a dependency on MAF runtime packages, AG-UI hosting, GitHub Copilot SDK, or provider SDKs.
- `Microsoft.Extensions.AI` is allowed in the collector only as an **optional abstraction boundary** (`IChatClient`, `IEmbeddingGenerator`). No collector feature may require a concrete LLM provider to boot or function.
- Loom owns agent construction, orchestration, and provider selection.
- Docs, comments, and architecture tests enforce the same boundary instead of contradicting each other.

## Current State Snapshot

Already true:

- `src/qyl.agents/` and `src/qyl.workflows/` are deleted.
- [qyl.slnx](/Users/ancplua/qyl/qyl.slnx) no longer references those projects.
- [src/qyl.loom/qyl.loom.csproj](/Users/ancplua/qyl/src/qyl.loom/qyl.loom.csproj) no longer references those projects.
- [tests/qyl.collector.tests/qyl.collector.tests.csproj](/Users/ancplua/qyl/tests/qyl.collector.tests/qyl.collector.tests.csproj) is already clean.
- [tests/qyl.collector.tests/ArchitectureTests.cs](/Users/ancplua/qyl/tests/qyl.collector.tests/ArchitectureTests.cs) already bans `Microsoft.Agents.AI*` and `GitHub.Copilot.SDK` from the collector assembly.
- [specs/00-architecture.md](/Users/ancplua/qyl/specs/00-architecture.md) already captures the important nuance: collector may reference `Microsoft.Extensions.AI` abstractions, but not provider SDKs or required LLM functionality.

Still false or drifting:

- [README.md](/Users/ancplua/qyl/README.md) still advertises `qyl.agents` and the old topology.
- [.claude/rules/collector.md](/Users/ancplua/qyl/.claude/rules/collector.md) says "zero LLM dependencies", which is too blunt now that the collector intentionally references `Microsoft.Extensions.AI`.
- [.claude/rules/loom.md](/Users/ancplua/qyl/.claude/rules/loom.md) is mostly correct but still anchors the boundary in terms of deleted projects rather than the current package and ownership split.
- This ADR still contains obsolete tasks, obsolete verification commands, and references to nonexistent files like `specs/04-agents.md` and `specs/09-workflows.md`.

## Impacted Files

### Deletions

No remaining deletions should be planned here. The project and directory deletions are already done. Any step that still says `rm -rf src/qyl.agents/ src/qyl.workflows/` is historical noise and should be removed from the plan.

### Spec / documentation cleanup

- [specs/decisions/maf-native-migration.md](/Users/ancplua/qyl/specs/decisions/maf-native-migration.md)
  Rewrite from checkbox history into a current implementation plan. Remove completed tasks, nonexistent file references, and invalid verification criteria.
- [README.md](/Users/ancplua/qyl/README.md)
  Remove the old component table entry for `qyl.agents`, redraw the architecture diagram, and update the project tree so the repo shape matches `src/`.
- [.claude/rules/collector.md](/Users/ancplua/qyl/.claude/rules/collector.md)
  Replace "zero LLM dependencies" with the real invariant: no MAF runtime, no AG-UI, no workflow engine, no provider SDKs, but optional `Microsoft.Extensions.AI` abstractions are allowed.
- [.claude/rules/loom.md](/Users/ancplua/qyl/.claude/rules/loom.md)
  Tighten the wording around Loom ownership: Loom owns agents/workflows/provider selection; collector may expose optional AI-shaped hooks but does not own orchestration.

### Code / test changes

- [tests/qyl.collector.tests/ArchitectureTests.cs](/Users/ancplua/qyl/tests/qyl.collector.tests/ArchitectureTests.cs)
  Keep the collector ban focused on concrete framework and provider dependencies. Do not ban `Microsoft.Extensions.AI`.
- [src/qyl.collector/qyl.collector.csproj](/Users/ancplua/qyl/src/qyl.collector/qyl.collector.csproj)
  No planned edit if the package surface stays as-is. Only touch it if validation finds a banned provider or agent package reintroduced.

### Files explicitly **not** in scope for follow-up edits unless validation fails

- [qyl.slnx](/Users/ancplua/qyl/qyl.slnx)
- [src/qyl.loom/qyl.loom.csproj](/Users/ancplua/qyl/src/qyl.loom/qyl.loom.csproj)
- [tests/qyl.collector.tests/qyl.collector.tests.csproj](/Users/ancplua/qyl/tests/qyl.collector.tests/qyl.collector.tests.csproj)
- [specs/00-architecture.md](/Users/ancplua/qyl/specs/00-architecture.md)

These are already aligned with the migration and should not be churned casually.

## Patch Sketch

### 1. Collector boundary wording vs optional `Microsoft.Extensions.AI`

Replace every variant of "collector has zero LLM dependencies" with this narrower rule:

> Collector may reference `Microsoft.Extensions.AI` abstractions as optional injection points. Collector may not reference MAF runtime packages, AG-UI hosting, workflow engines, GitHub Copilot SDK, or concrete LLM provider SDKs.

That wording is the only one consistent with the actual collector package surface in [src/qyl.collector/qyl.collector.csproj](/Users/ancplua/qyl/src/qyl.collector/qyl.collector.csproj) and the existing architectural note in [specs/00-architecture.md](/Users/ancplua/qyl/specs/00-architecture.md).

### 2. Obsolete verification steps and checklist drift

Delete these classes of stale steps from the ADR:

- Steps that re-delete already deleted directories or projects.
- Steps that remove already-removed project references.
- Steps that update files that do not exist in this repo (`specs/04-agents.md`, `specs/09-workflows.md`).
- Verification that expects zero `Microsoft.Extensions.AI` presence in the collector.
- Commit-by-task instructions. They are process noise, not architecture.

Replace them with one short remaining-work sequence and one validation block.

### 3. Stale README / docs / comments from removed topology

The remaining topology drift is concentrated in documentation, not code:

- [README.md](/Users/ancplua/qyl/README.md): stale package table, stale architecture diagram, stale project tree.
- [.claude/rules/collector.md](/Users/ancplua/qyl/.claude/rules/collector.md): stale absolute wording about "zero LLM dependencies".
- [.claude/rules/loom.md](/Users/ancplua/qyl/.claude/rules/loom.md): acceptable direction, but should describe the current ownership boundary without leaning on deleted-project history.
- [tests/qyl.collector.tests/ArchitectureTests.cs](/Users/ancplua/qyl/tests/qyl.collector.tests/ArchitectureTests.cs): XML summary should match the refined invariant, not the oversimplified slogan.

### 4. Architecture test alignment for banned dependencies

The collector architecture test should enforce the real ban list:

- Ban: `Microsoft.Agents.AI`
- Ban: `Microsoft.Agents.AI.Hosting`
- Ban: `Microsoft.Agents.AI.Hosting.AGUI`
- Ban: `GitHub.Copilot.SDK`
- Ban: concrete provider/client packages if introduced into collector, for example `Microsoft.Extensions.AI.OpenAI`, `OpenAI`, `Azure.AI.OpenAI`, `Anthropic`
- Allow: `Microsoft.Extensions.AI`

Patch direction:

- Keep the current NetArchTest assembly-level assertion.
- Rename or reword the test and XML summary so it says "no agent framework or provider SDKs", not "no LLM dependencies".
- Add a package-surface assertion if stricter enforcement is needed. NetArchTest only catches referenced assemblies, not an inert `PackageReference`.

## Migration Sequence

1. Freeze the boundary in docs first.
   Update this ADR, then fix [README.md](/Users/ancplua/qyl/README.md), then fix the collector and loom rule docs. The repo needs one story before tightening tests.

2. Tighten the collector invariant in tests.
   Update [tests/qyl.collector.tests/ArchitectureTests.cs](/Users/ancplua/qyl/tests/qyl.collector.tests/ArchitectureTests.cs) so the test bans framework/provider packages but explicitly permits `Microsoft.Extensions.AI`.

3. Run targeted drift checks.
   Search for `qyl.agents` and `qyl.workflows` across docs and comments. Search for "zero LLM dependencies" and replace it where it now means "no provider/framework deps".

4. Only then touch code or project files if validation finds a real violation.
   The codebase already appears structurally migrated. Do not invent cleanup work where the repo is already correct.

## Validation

- `rg -n "qyl\\.agents|qyl\\.workflows" README.md specs .claude src tests --glob '!**/bin/**' --glob '!**/obj/**' --glob '!src/qyl.mcp/Clear.cs'`
  Expect only historical mentions in this ADR and the architecture spec's deleted-project notes.
- `rg -n "zero LLM dependencies" README.md specs .claude src tests --glob '!**/bin/**' --glob '!**/obj/**'`
  Expect no collector rule or test summary to overstate the ban.
- `dotnet list src/qyl.collector/qyl.collector.csproj package --include-transitive`
  Confirm collector has `Microsoft.Extensions.AI` but no `Microsoft.Agents.AI*`, `GitHub.Copilot.SDK`, or provider SDKs.
- `nuke`
  Clean build.
- `nuke test`
  Architecture tests pass with the refined invariant.

## Major Risks

- **Overcorrecting the collector rule.** If docs or tests ban `Microsoft.Extensions.AI`, they will directly contradict the current collector design.
- **Undercorrecting the collector rule.** If the test only bans `Microsoft.Agents.AI*`, a provider package can slip into the collector without tripping architecture checks.
- **Doc-only cleanup without validation.** README drift is obvious, but the more dangerous failures are hidden in rule docs and test slogans that engineers will cargo-cult later.
- **Keeping historical checklist sludge.** An ADR that mixes completed work with pending work stops being trustworthy and invites bad follow-up edits.

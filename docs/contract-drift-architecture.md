# Contract Drift Architecture

> **Status:** 2026-04-21 — authoritative map of qyl's code-generation pipelines, their outputs, the overlaps between them, and the per-file authority decisions that unblock cleanup.

## Why this document exists

qyl has **six** code-generation pipelines that produce overlapping outputs into shared namespaces. When an agent is asked "fix the contract drift" the honest answer is "which of the six pipelines do you mean?" — and because no single document has named them, no agent has ever committed to a full cleanup. We end up with symptoms like:

- `Qyl.Models.SpanRecord`, `Qyl.Storage.SpanRecord`, and `Qyl.Contracts.Models.SpanRecord` — three versions of the same record with divergent enum namespaces (the Schema Drift PR #138 was the emergency fix; the underlying duplication still stands).
- `src/qyl.collector/Ingestion/OtlpAttributes.Utf8.g.cs` (**6,923 lines**) and `src/qyl.instrumentation/Instrumentation/SemanticConventions.Utf8.g.cs` (**7,555 lines**) — both auto-generated UTF-8 byte-literal tables for OTel attributes. **Neither has a caller in `src/`**. 14,478 lines of dead generated code nobody dares delete.
- `src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs` and `src/qyl.instrumentation.generators/Emitters/ToolManifestEmitter.cs` — two emitters with the same name, both emitting `QylToolManifest`-shaped output, in different Roslyn generator projects.

The document below is the authority-decision framework. **Every output file has exactly one owning pipeline. Every duplicate is named with a disposition (keep / delete / migrate).** Future PRs reference this file and delete orphans without guilt.

## The six pipelines

```
                                                  ┌─────────────────────────────────────┐
                                                  │     PIPELINE 1 — Contract           │
                                                  │     (NUKE target, NPM + C# script)  │
                                                  └─────────────────────────────────────┘
┌─────────────────┐                                                 │
│  core/specs/    │  ── npm run compile (TypeSpec CLI) ──▶  openapi.yaml
│  (TypeSpec)     │                                                 │
└─────────────────┘                                                 ├── SchemaGenerator.cs ─▶ qyl.contracts/Models/**/*.g.cs
        ▲                                                           │                       qyl.contracts/Enums/**/*.g.cs
        │ depends on semconv.g.tsp                                  │                       qyl.contracts/Primitives/Scalars.g.cs
        │                                                           │                       qyl.collector/Storage/DuckDbSchema.g.cs
        │                                                           └── openapi-typescript ─▶ qyl.dashboard/src/types/api.ts
        │
        │ (feedback loop — semconv emits into TypeSpec source!)
        │
┌─────────────────┐    ┌──────────────────────────────────────────────────┐
│ node_modules/   │    │          PIPELINE 2 — Semconv                    │
│ @opentelemetry/ │──▶ │          (NPM script, .d.ts regex parser)        │
│  semantic-convs │    └──────────────────────────────────────────────────┘
└─────────────────┘                                │
                                                   ├── core/specs/generated/semconv.g.tsp  ◀─ FEEDS Pipeline 1
                                                   ├── qyl.instrumentation/**/SemanticConventions.g.cs
                                                   ├── qyl.instrumentation/**/SemanticConventions.Utf8.g.cs
                                                   ├── qyl.dashboard/src/lib/semconv.ts
                                                   ├── qyl.collector/Storage/promoted-columns.g.sql
                                                   └── qyl.contracts/Attributes/{GenAi,Db,Mcp}Attributes.g.cs  (3 facades)

┌─────────────────┐    ┌──────────────────────────────────────────────────┐
│ qyl-extensions  │──▶ │       PIPELINE 2b — Domain Contracts             │
│ .json           │    │       (NUKE target, ContractGenerator.cs)        │
└─────────────────┘    └──────────────────────────────────────────────────┘
                                                   ├── qyl.instrumentation.generators/Generated/DomainContracts.g.cs
                                                   └── qyl.collector/Observe/Generated/DomainContracts.g.cs

┌─────────────────────────────────────────────────────────────────────────┐
│  PIPELINE 3 — ServiceDefaults SG (Roslyn IIncrementalGenerator)         │
│  src/qyl.instrumentation.generators/ServiceDefaultsSourceGenerator.cs   │
│  Reads:  [Meter] [Traced] [HostedService] [QylHealthCheck] [QylService] │
│          [MapEndpoints] [DbCallSite] at compile time                    │
│  Emits:  in-memory .g.cs (no committed files — consumers see via SG)    │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│  PIPELINE 4 — MCP Tool Manifest (Roslyn IIncrementalGenerator)          │
│  src/qyl.mcp.generators/ToolManifestGenerator.cs                        │
│  Reads:  [McpServerToolType] [QylSkill] [QylCapability]                 │
│          [QylCapabilityDefinition]                                      │
│  Emits:  QylToolManifest in-memory                                      │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│  PIPELINE 5 — Loom Workflow (Roslyn IIncrementalGenerator)              │
│  src/qyl.instrumentation.generators/Loom/LoomSourceGenerator.cs         │
│  Reads:  [LoomTool] [LoomContract] [LoomStep] [LoomWorkflow]            │
│          [RequiresCapability] [RequiresApproval] [LoomBudget]           │
│          [ToolSideEffect] [EmitsStructuredOutput]                       │
│  Emits:  LoomGeneratedRegistry in-memory                                │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│  PIPELINE 6 — DuckDB Insert (Roslyn IIncrementalGenerator)              │
│  src/qyl.collector.storage.generators/DuckDbInsertGenerator.cs          │
│  Reads:  [DuckDbTable] [DuckDbColumn] [DuckDbIgnore]                    │
│  Emits:  AddParameters / MapFromReader / ColumnList per table, in-mem   │
└─────────────────────────────────────────────────────────────────────────┘
```

**Surface-area summary:**

| # | Pipeline | Runtime | Source of truth | Output files | In-memory SG? |
|---|---|---|---|---|---|
| 1 | Contract | NUKE (C# + npm) | `core/specs/*.tsp` | **~35** committed `.g.cs` + `api.ts` | No |
| 2 | Semconv | npm script (TS) | `node_modules/@opentelemetry/semantic-conventions` v1.40.0 | **9** committed files across 4 projects | No |
| 2b | Domain Contracts | NUKE (C#) | `eng/semconv/qyl-extensions.json` | **2** committed `.g.cs` | No |
| 3 | ServiceDefaults SG | Roslyn | `[Meter]` / `[Traced]` / ... call sites | 0 committed (SG) | **Yes** |
| 4 | MCP Tools | Roslyn | `[QylSkill]` / `[QylCapability]` / ... | 0 committed (SG) | **Yes** |
| 5 | Loom Workflow | Roslyn | `[LoomTool]` / `[LoomStep]` / ... | 0 committed (SG) | **Yes** |
| 6 | DuckDB Insert | Roslyn | `[DuckDbTable]` / `[DuckDbColumn]` | 0 committed (SG) | **Yes** |

## Output inventory with authority

**Rule:** every file below has **exactly one** owning pipeline. If an agent finds a file whose generator is unclear, open a drift issue — do not regenerate.

### Pipeline 1 — Contract (TypeSpec → OpenAPI → C#/TS/DuckDB)

| Output | Owner | Notes |
|---|---|---|
| `core/openapi/openapi.yaml` | **P1** | Intermediate. Built by `npm run compile` in `core/specs/`. Committed so consumers can diff. |
| `src/qyl.contracts/Models/*.g.cs` (12 files) | **P1** | Namespace routed by `NamespaceRoutingTable.cs`. |
| `src/qyl.contracts/Enums/*.g.cs` (10 files) | **P1** | Same routing table. |
| `src/qyl.contracts/Primitives/Scalars.g.cs` | **P1** | TraceId / SpanId / SessionId etc. |
| `src/qyl.collector/Storage/DuckDbSchema.g.cs` | **P1** | DDL const table. `Version` is a SHA-256 of content. |
| `src/qyl.dashboard/src/types/api.ts` | **P1** | `openapi-typescript` consumes `openapi.yaml`. |

### Pipeline 2 — Semconv (upstream `.d.ts` → 5 shapes)

| Output | Owner | Notes |
|---|---|---|
| `core/specs/generated/semconv.g.tsp` | **P2** | **Feedback loop — Pipeline 1 imports this**. NUKE enforces `GenerateSemconv → TypeSpecCompile`. |
| `src/qyl.instrumentation/Instrumentation/SemanticConventions.g.cs` | **P2** | String-const groups. |
| `src/qyl.instrumentation/Instrumentation/SemanticConventions.Utf8.g.cs` | **P2** | UTF-8 `ReadOnlySpan<byte>` literals. |
| `src/qyl.dashboard/src/lib/semconv.ts` | **P2** | TS `as const` objects + enums. |
| `src/qyl.collector/Storage/promoted-columns.g.sql` | **P2** | DuckDB promoted-column list. |

### Pipeline 2b — Domain Contracts (`qyl-extensions.json` + semconv facades)

| Output | Owner | Notes |
|---|---|---|
| `src/qyl.contracts/Attributes/GenAiAttributes.g.cs` | **P2b (facades)** | Emitted by the semconv TS script reading `qyl-extensions.json → facades[]`. |
| `src/qyl.contracts/Attributes/DbAttributes.g.cs` | **P2b (facades)** | Same emitter, same JSON. |
| `src/qyl.contracts/Attributes/McpAttributes.g.cs` | **P2b (facades)** | Same. |
| `src/qyl.instrumentation.generators/Generated/DomainContracts.g.cs` | **P2b (domains)** | Emitted by `eng/build/ContractGenerator.cs` reading the same JSON. |
| `src/qyl.collector/Observe/Generated/DomainContracts.g.cs` | **P2b (domains)** | **Byte-identical copy of the above.** Intentional — two consumers, one content. |

### Pipelines 3 – 6 — in-memory Roslyn SGs

No committed output files. Output is seen only by the consumer project at compile time. Drift detection: `dotnet build` fails if call-site attributes diverge from the emitter contract.

## Cross-pipeline overlaps (the "5× generiert" root cause)

### 🔴 O-1. Dead UTF-8 semconv duplicate (14,478 lines)

| File | Size | Active generator? | Last regenerated | Callers in `src/` |
|---|---|---|---|---|
| `src/qyl.collector/Ingestion/OtlpAttributes.Utf8.g.cs` | 6,923 lines | **No** — header says "run npm run generate in SemconvGenerator" but the script's `CONFIG.outputs` doesn't include this path | 2026-03-10 (`f6b817a7`) | **0** |
| `src/qyl.instrumentation/Instrumentation/SemanticConventions.Utf8.g.cs` | 7,555 lines | **Yes** — Pipeline 2 `csharpUtf8` output | 2026-04-10 (`1f5171c2`) | **0** |

**Both unreferenced in `src/`.** The first is an orphan from a pre-collector split; the second is the current generator's output. Neither is consumed.

**Decision:** delete **both**. Re-introduce via Pipeline 2 when a consumer appears. If zero-alloc OTLP parsing needs UTF-8 literals later, emit them on-demand as part of that feature, not speculatively.

### 🔴 O-2. Dead non-UTF-8 semconv duplicate

| File | Size | Callers |
|---|---|---|
| `src/qyl.collector/Ingestion/OtlpAttributes.cs` (hand-written, contains `SchemaNormalizer`) | 277 lines | **0** |
| `src/qyl.instrumentation/Instrumentation/SemanticConventions.g.cs` (Pipeline 2 `csharp` output) | ~thousands | **0** |

**Decision:** delete `OtlpAttributes.cs` outright (hand-written, zero callers, triggers AL0133 warnings on deprecated semconv keys). Keep `SemanticConventions.g.cs` *if* a caller appears; otherwise drop it in the same cleanup.

### 🔴 O-3. Triple `SpanRecord` definition (partially addressed by PR #138)

| Symbol | Namespace | Owner | Status |
|---|---|---|---|
| `SpanRecord` (generated) | `Qyl.Models` | **P1** via TypeSpec → `Models.g.cs` | **Removed on 2026-04-20** (PR #138 fix). TypeSpec routing no longer emits into this namespace. |
| `SpanRecord` (generated) | `Qyl.Storage` | **P1** via TypeSpec → `Storage.g.cs` | **Current authoritative version** post-PR #138. |
| `SpanRecord` (hand-written) | `Qyl.Contracts.Models` | Manual — `src/qyl.contracts/Models/SpanRecord.cs` | **Still exists.** Uses `Qyl.Contracts.Enums` instead of `Qyl.OTel.Enums`. |

**Decision:** delete `src/qyl.contracts/Models/SpanRecord.cs` after verifying no caller depends on the `Qyl.Contracts.Enums` variant. If callers exist, port them to `Qyl.Storage.SpanRecord` (uses `Qyl.OTel.Enums.*`). One caller audit + one follow-up PR. **Do not** attempt this as a drive-by during unrelated work.

### 🟠 O-4. `ToolManifestEmitter` name collision across two generator projects

| File | Project | Purpose |
|---|---|---|
| `src/qyl.mcp.generators/Emitters/ToolManifestEmitter.cs` | `qyl.mcp.generators` | Emits `QylToolManifest` from `[McpServerToolType]` types |
| `src/qyl.instrumentation.generators/Emitters/ToolManifestEmitter.cs` | `qyl.instrumentation.generators` | Emits… also tool manifest shape |

The projects are separately referenced by consumers, so the two emitters don't compile-conflict — but the duplication means one class must be kept in sync by hand, and the `[QylSkill]`/`[QylCapability]` ↔ `[McpServerToolType]` attribute surfaces are **ambiguously split across two pipelines**.

**Decision:** consolidate. `qyl.mcp.generators/ToolManifestGenerator.cs` is newer and owns the full MCP tool manifest contract (per the `microsoft-agent-framework-qyl` skill). Delete `src/qyl.instrumentation.generators/Emitters/ToolManifestEmitter.cs` and any analyzer refs in `src/qyl.instrumentation.generators/CallSites/ToolManifestAnalyzer.cs` unless that analyzer does something the MCP pipeline doesn't. Verify against `qyl.mcp.tests`.

### 🟠 O-5. Dual `DomainContracts.g.cs` is intentional — flag as such

Two files with identical content (`ContractGenerator.cs:42-45` emits to both):

- `src/qyl.instrumentation.generators/Generated/DomainContracts.g.cs` (consumed at compile time by the instrumentation SG)
- `src/qyl.collector/Observe/Generated/DomainContracts.g.cs` (consumed at runtime by the collector)

This is **not drift** — two consumers need the same content in different project trees (one is a Roslyn generator project, one is a runtime project, and Roslyn generators can't share runtime assemblies). But it looks like drift to every auditor.

**Decision:** keep both. Add a comment to both `.g.cs` files pointing to `ContractGenerator.cs` and stating "sibling copy exists at X — edits to one require the other." Do not attempt to unify.

### 🟡 O-6. `semconv.g.tsp` feedback loop is fragile but correct

Pipeline 2 emits into Pipeline 1's source tree (`core/specs/generated/semconv.g.tsp`). The NUKE graph enforces ordering (`TypeSpecCompile.DependsOn(GenerateSemconv)`), but if a human runs `npm run compile` inside `core/specs/` without first running `npm run generate` in `eng/semconv/`, they get a stale TypeSpec compile that drops semconv keys.

**Decision:** leave the dependency graph alone (it works). But add a `core/specs/generated/README.md` with a single line: "never edit. Regenerated by `nuke GenerateSemconv`. Required before `TypeSpecCompile`." Stops the next agent from `git add`-ing a manual fix.

## Decision framework — when should I add a generator?

Before adding a seventh pipeline, answer:

1. **Does an existing pipeline already produce something of this shape?** If yes, extend that pipeline, don't add one. (Adding `OtlpAttributes.Utf8.g.cs` as a sibling to `SemanticConventions.Utf8.g.cs` is the anti-pattern.)
2. **Is the source of truth external or internal?** External = upstream files, git repos, NuGet packages → belongs in Pipeline 1 (TypeSpec) or Pipeline 2 (semconv) with explicit provenance in the output header. Internal = attributes on qyl code → belongs in a Roslyn SG (Pipelines 3–6 pattern).
3. **Does the output feed another pipeline?** If yes, document the feedback arrow in this doc before committing the first output. Silent feedback loops (`semconv.g.tsp` → TypeSpec) are landmines.
4. **Who is the consumer?** A generator with zero consumers in `src/` (see O-1 / O-2) is not allowed to land. If you can't name a caller, the generator doesn't exist yet.

## The semconv → Weaver migration (proposed Pipeline 2 rewrite)

**Scope:** replace the `.d.ts` regex parser in `eng/semconv/generate-semconv.ts` with an `otel/weaver` pipeline driven by the upstream semconv YAML registry. **Does not touch Pipelines 1, 3, 4, 5, 6.** The output inventory above stays byte-identical except for added `[Experimental]` / `[Obsolete]` attributes on stability-gated members.

**Why consider it:**
- Current `.d.ts` regex breaks whenever the upstream JS SIG reformats exports.
- Weaver is the official OTel code-gen tool; it reads the canonical YAML registry.
- Public-ship-ability — qyl's semconv output could become `Qyl.SemanticConventions` NuGet, filling the gap where `OpenTelemetry.SemanticConventions` ships only `1.0.0-rc9.9`.

**Why NOT YET:**
- Seven design blockers from a prior analysis (2026-04-19) must be resolved first. Paste-ready checklist for the execution session:

```
[ ] 1. --registry twice doesn't work. Replace fetch script with registry_manifest.yaml
       listing upstream as a dependency registry. Weaver composes upstream + qyl-extensions
       automatically. Saves fetch-upstream.sh entirely.

[ ] 2. netstandard2.0 + [Experimental] + u8 literals are incompatible. Decide:
       drop ns2.0 → target net10.0 + net8.0 only (recommended — qyl is a new public
       package, ns2.0 is historic contrib baggage).

[ ] 3. Stability propagation. Emit [Experimental] per-const, not per-class — class
       remains neutral. Same for [Obsolete] with replacement_attribute where present.

[ ] 4. Verify strategy. Two phases:
       (a) strip stability attributes from new output → diff vs current commit must be empty
       (b) full diff with stability → only [Experimental]/[Obsolete] lines may appear
       git diff alone is insufficient — write a verify script.

[ ] 5. qyl-extensions.json → YAML migration. Semconv registry composition doesn't let
       Registry B add enum members to Registry A's attribute. Choose per extension:
       (a) shadow-attr (new qyl-named attribute with qyl-specific enum values), or
       (b) post-processing pass that merges Weaver output + qyl-extensions before write.
       Decide upfront — Phase 2 refactor costs triple.

[ ] 6. "No PRs upstream" strategic cost. Document in DESIGN.md what would make qyl
       migrate to OpenTelemetry.SemanticConventions if it ever ships stable. Keeps
       future-you from blindly reinventing.

[ ] 7. Session-budget reality. ~8-17h end-to-end including Jinja debug loops.
       Do not start after 18:00. Do not bundle with unrelated refactors.
```

**Sequencing:** blocker checklist is its own PR (`DESIGN.md` in `eng/semconv/`). Execution is a separate PR after checklist is ticked.

## What this doc does NOT do

- Does not rewrite any generator. Delivers decisions only.
- Does not declare Pipeline 2 dead. Weaver migration is a proposal, not a commitment.
- Does not touch Pipelines 3–6 (Roslyn SGs). They're in scope for a separate audit once the committed-output pipelines (1 / 2 / 2b) are clean.
- Does not merge `Qyl.Contracts.Models.SpanRecord` (O-3). That's a follow-up caller-audit PR.

## Follow-up PRs (ranked by safety-to-execute)

1. **Delete O-1 dead UTF-8 files** — zero callers, no consumer risk. ~14,478 LOC removed. [1h including CI]
2. **Delete O-2 dead non-UTF-8 + hand-written semconv shim** — zero callers, resolves AL0133 warnings by deletion. [1h]
3. **Add the `core/specs/generated/README.md` warning note** — O-6 mitigation. [15min]
4. **Consolidate `ToolManifestEmitter`** (O-4) — requires proving the instrumentation-side emitter is unused after MCP-side handles everything. Test suite guards. [2-3h]
5. **Audit + delete `Qyl.Contracts.Models.SpanRecord`** (O-3) — caller survey first, rewrite callers to `Qyl.Storage.SpanRecord`. [2-4h]
6. **Semconv Weaver migration** — only after the 7 blockers above are DESIGN.md'd. [8-17h]

Items 1–3 can land in the next dev session with no coordination overhead. Items 4–6 each want their own PR with a named reviewer.

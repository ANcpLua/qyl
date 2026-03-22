# Contracts Specification

> Owner: contracts
> SSOT: YES for shared serialized contracts; NO for feature-local runtime helpers
> Depends on: `core/specs/**`, `eng/build/**`, `eng/semconv/**`
> Used by: `collector.md`, `mcp.md`, `loom.md`, `instrumentation.md`, `telemetry-intelligence.md`

Shared contracts must be mechanically derived, not hand-maintained. Today the spec overclaims: `src/qyl.contracts/` still mixes generated models with hand-written DTOs, feature seed data, stale names, and helper code. This document defines the cleanup plan that makes the contracts layer true in code instead of true on paper.

---

## 1. Target Ownership

### 1.1 Canonical Ownership Chain

```text
core/specs/**/*.tsp
  -> tsp compile
  -> core/openapi/openapi.yaml
  -> eng/build/SchemaGenerator.cs
  -> src/qyl.contracts/**/*.g.cs
  -> src/qyl.collector/Storage/DuckDbSchema.g.cs
  -> src/qyl.dashboard/src/types/api.ts
```

- `core/specs/**` is the only hand-edited source of shared serialized shape.
- `core/openapi/openapi.yaml` is a committed, generated intermediate for review and downstream codegen. Never edit it manually.
- `src/qyl.contracts/**/*.g.cs` is generated output only.
- `src/qyl.contracts/**/*.cs` is allowed only for primitives/helpers that cannot be expressed in TypeSpec and are used by more than one project.
- `eng/semconv/generate-semconv.ts` owns semconv-derived outputs:
  - `core/specs/generated/semconv.g.tsp`
  - `src/qyl.contracts/Attributes/GenAiAttributes.g.cs`
  - `src/qyl.contracts/Attributes/DbAttributes.g.cs`

### 1.2 What Belongs In `qyl.contracts`

Keep in `qyl.contracts` only if all are true:

- Cross-project consumer exists.
- Value is serialized over HTTP/SSE/MCP or persisted as a shared record shape.
- Shape is generated from TypeSpec or semconv metadata.

Do not keep in `qyl.contracts`:

- Collector-only ingestion/storage helpers.
- Feature implementation logic, registries, parsers, or normalization helpers.
- Dead product-era namespaces such as `Copilot` when the feature is now Loom/agent-runs.

Feature-owned contracts can still live in the `qyl.contracts` project, but under feature namespaces and generated from feature TypeSpec definitions. Shared does not mean global dumping ground.

---

## 2. Current Drift Map

### 2.1 Delete and Replace With Generated Contracts

- `src/qyl.contracts/Models/SpanRecord.cs`
  - Delete.
  - Replace usages with the generated `SpanRecord` from `core/specs/otel/storage.tsp` via `src/qyl.contracts/Models/Models.g.cs`.
  - Current state is duplicate intent with a different namespace and different JSON naming.

- `src/qyl.contracts/Enums/OTelEnums.cs`
  - Delete.
  - Replace usages with generated enums from `core/specs/otel/enums.tsp` via `src/qyl.contracts/Enums/OTelEnumsEnums.g.cs`.
  - Current state duplicates `SpanKind` and `SpanStatusCode`.

- `src/qyl.contracts/Models/PagedResult.cs`
  - Delete.
  - Replace usages with generated pagination shapes from `core/specs/common/pagination.tsp`.
  - If the collector is intentionally returning `items/total_count/cursor`, then the TypeSpec pagination model is wrong and must be fixed there first. The hand-written wrapper is not acceptable drift.

- `src/qyl.contracts/Intelligence/DiagnosticPattern.cs`
- `src/qyl.contracts/Intelligence/CausalRule.cs`
- `src/qyl.contracts/Intelligence/InvestigationStrategy.cs`
- `src/qyl.contracts/Intelligence/InvestigationStep.cs`
- `src/qyl.contracts/Intelligence/PatternCategory.cs`
- `src/qyl.contracts/Intelligence/Signal.cs`
- `src/qyl.contracts/Intelligence/SignalOperator.cs`
  - Delete.
  - Replace with generated outputs from `core/specs/intelligence/*.tsp`.
  - The existing manual types prove the TypeSpec is not in the active generation path.

### 2.2 Move or Re-Specify As Feature-Owned Contracts

- `src/qyl.contracts/Copilot/CopilotTypes.cs`
- `src/qyl.contracts/Copilot/AgentAuditTypes.cs`
  - Do not keep as hand-written `Copilot` contracts.
  - Split by real ownership:
    - Loom/agent-run SSE payloads and audit records become generated feature contracts.
    - Any type without a current runtime consumer is deleted.
  - Namespace should be renamed away from `Qyl.Contracts.Copilot`; the product is not Copilot anymore.

- `src/qyl.contracts/Loom/CodingAgentProvider.cs`
  - `CodingAgentProvider` may stay in `qyl.contracts`, but only as generated feature contract.
  - `CodingAgentRunRecord` and `LoomSettingsRecord` should be generated from TypeSpec if they are persisted/shared records.
  - `CodingAgentProviderNames` is logic, not contract. Move it out to Loom or collector feature code.

### 2.3 Keep Hand-Written

- `src/qyl.contracts/Primitives/TimeConversions.cs`
  - Keep hand-written.
  - This is shared helper logic, not schema.
  - If a future `qyl.common` library exists, move it there; do not try to fake it into TypeSpec.

---

## 3. Required Source Changes

### 3.1 TypeSpec Inputs

Impacted source files:

- `core/specs/main.tsp`
- `core/specs/common/pagination.tsp`
- `core/specs/api/routes.tsp`
- `core/specs/api/streaming.tsp`
- `core/specs/models/agent/agent-run.tsp`
- `core/specs/models/agent/tool-call.tsp`
- `core/specs/models/agent/workflow-checkpoint.tsp`
- `core/specs/models/agent/workflow-execution.tsp`
- `core/specs/intelligence/main.tsp`
- `core/specs/intelligence/*.tsp`

Required direction:

- Pull every shared HTTP/SSE/persisted contract into the active TypeSpec graph.
- Make `core/specs/main.tsp` the only entry point for contracts that feed `openapi.yaml`.
- If intelligence seed data must remain generated but is not part of OpenAPI, add an explicit generator target for it under `eng/build`; do not leave parallel manual C# types in `src/qyl.contracts`.

### 3.2 Build and Generator Layer

Impacted build/codegen files:

- `eng/build/BuildPipeline.cs`
- `eng/build/BuildVerify.cs`
- `eng/build/BuildApiDiff.cs`
- `eng/build/SchemaGenerator.cs`
- `eng/build/NamespaceRoutingTable.cs`

Required direction:

- Treat `core/openapi/openapi.yaml` as the sole input to `SchemaGenerator`.
- Add namespace routing for feature-owned generated contracts instead of dropping them into ad hoc manual folders.
- Add a contracts verification step that fails when new hand-written `.cs` files appear under `src/qyl.contracts/` outside an allowlist.

### 3.3 Contract Project Surface

Impacted contract files:

- `src/qyl.contracts/Copilot/*.cs`
- `src/qyl.contracts/Loom/*.cs`
- `src/qyl.contracts/Intelligence/*.cs`
- `src/qyl.contracts/Models/PagedResult.cs`
- `src/qyl.contracts/Models/SpanRecord.cs`
- `src/qyl.contracts/Enums/OTelEnums.cs`
- `src/qyl.contracts/Primitives/TimeConversions.cs`

Required direction:

- Generated files end in `.g.cs`.
- Manual files in `src/qyl.contracts` must be reduced to a tiny allowlist, ideally only `Primitives/TimeConversions.cs`.

---

## 4. Migration Sequence

1. Fix the source graph.
   - Import missing shared contract definitions into `core/specs/main.tsp` or add a clearly separate generated-contract target for non-OpenAPI feature data.
   - Resolve pagination shape mismatch at the TypeSpec level before touching consumers.

2. Generate replacements before deleting consumers.
   - Produce generated replacements for Loom/agent-run, intelligence, and pagination contracts.
   - Add namespace routing so generated files land in stable feature namespaces.

3. Flip downstream consumers.
   - Collector: `src/qyl.collector/QylSerializerContext.cs`, `src/qyl.collector/Autofix/**`, `src/qyl.collector/Storage/DuckDbStore*.cs`, `src/qyl.collector/Intelligence/**`.
   - Loom: `src/qyl.loom/LoomEndpoints.cs`, `src/qyl.loom/LoomExplorerService.cs`, `src/qyl.loom/CodingAgent/**`, `src/qyl.loom/LoomSettingsEndpoints.cs`.
   - MCP: `src/qyl.mcp/Formatting/ResponseFormatter.cs`, `src/qyl.mcp/Tools/**` that deserialize shared payloads.

4. Delete drift.
   - Remove the hand-written files listed in section 2.
   - Remove dead `Copilot` naming once consumers compile against the generated feature namespace.

5. Lock the door.
   - Add verification that `src/qyl.contracts` cannot accrete new hand-written DTOs.
   - Add a diff gate for generated contracts the same way `openapi.yaml` already has one.

---

## 5. Validation And Tests

Build/runtime validation to add or tighten:

- `nuke TypeSpecCompile`
  - Verifies `core/openapi/openapi.yaml` regenerates cleanly from `core/specs/**`.

- `nuke Generate`
  - Regenerates `src/qyl.contracts/**/*.g.cs`, DuckDB schema, and dashboard API types.

- `nuke Verify`
  - Must fail on generated drift, not just compile drift.

- New verification: contracts manual-file allowlist
  - Fail if `src/qyl.contracts/**/*.cs` contains non-generated files outside approved helpers.

- New verification: duplicate contract detector
  - Fail if a generated type and a hand-written type represent the same schema concept under different namespaces or names.

- Existing architecture test to keep:
  - `tests/qyl.collector.tests/ArchitectureTests.cs`
  - Extend it with a contracts-surface assertion if needed; current dependency rule is not enough to stop drift.

- Consumer compile checks:
  - Collector, Loom, MCP serializer contexts must compile against only generated shared record types.

---

## 6. Major Risks

- Pagination mismatch is likely behavioral, not cosmetic. Deleting `PagedResult<T>` without fixing the actual API envelope will break MCP deserialization.
- `Copilot` contracts are stale naming wrapped around live Loom behavior; namespace cleanup will touch serializer contexts, SSE endpoints, and client code together.
- Intelligence contracts currently live outside the active `main.tsp` pipeline. If that stays split, drift will reappear. One owner or one explicit second generator; nothing in between.
- `CodingAgentProviderNames` is embedded parsing logic inside the contracts assembly. Leaving logic next to DTOs guarantees future drift.
- Downstream blast radius is moderate: collector, Loom, MCP, dashboard generated types, and DuckDB schema generation all sit on this edge. Sequence matters; source graph first, consumer rewrites second, deletions last.

---

## 7. Definition Of Done

- [ ] Every shared serialized contract in `src/qyl.contracts` is generated from TypeSpec or semconv metadata.
- [ ] `core/specs/**` is the only hand-edited source for shared contract shape.
- [ ] `core/openapi/openapi.yaml` is generated-only and matches HEAD after regeneration.
- [ ] Hand-written files under `src/qyl.contracts/` are reduced to an explicit helper allowlist.
- [ ] No stale `Copilot` namespace remains for Loom/agent-run contracts.
- [ ] Collector, Loom, and MCP compile against generated shared contracts only.
- [ ] Build verification fails on new hand-written contract drift.

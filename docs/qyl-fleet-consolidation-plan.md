# qyl fleet consolidation plan

Read-only audit, 2026-06-13, 5 parallel agents, every redundancy claim verified by opening
the actual files in each repo. **Nothing was modified or pushed.** HEADs at audit time:
`qyl@77381b9`, `Qyl.Opentelemetry.SemanticConventions@91b902d`,
`qyl-dotnet-autoinstrumentation@f898768`, `qyl-api-schema@02a2ab4` (advanced past my audit `8b017c6`).

The fleet is actually **5 repos** (the 4 named + the semconv-TypeSpec repo
`/Users/ancplua/RiderProjects/typespec-otel-semconv`). NOTE: that 5th repo is **local only — NOT
on public npm** (`npm view @ancplua/typespec-otel-semconv` = 404). qyl-api-schema's `package.json`
references it as `"@ancplua/typespec-otel-semconv": "1.41.0-2"`, but that resolves privately/locally,
not from a published package. Treat it as a local checkout in any cross-repo step.

> **AUTHORITY NOTE (read first).** For the conformance contract / DUP-1, the authoritative,
> owner-clarified spec is **`qyl-api-schema/HANDOFF.md` @ `02a2ab4`** (the binding 0.4.0
> "Auflage 1–3"). This fleet plan **defers to it** on DUP-1 and corrects my earlier overreach
> below. What this plan adds *beyond* HANDOFF.md — which only looked at the conformance contract —
> is the cross-repo **DUP-2 (Weaver pin)** and the **hygiene/defect** findings HANDOFF did not cover.

---

## North star (the done-state — gigantic but checkable)

**Every telemetry-contract concept in the qyl fleet is declared exactly once and consumed
everywhere else — zero duplicated contract truth across repos.**

This is one *deliberate* architecture (confirmed by `qyl/docs/typespec-maf-prd.md`), not an
accident. It currently has **two real duplications** and **two things that only look
duplicated**. The work is to collapse the two real ones; the two false alarms must be left
alone.

---

## The 3 sources of truth (+ 1 consumer)

| # | Source of truth | Owns | Canonical because |
|---|---|---|---|
| 1 | **qyl-api-schema** (TypeSpec) | The API contract **and** the Telemetry Control Graph: conformance-plan envelope, `DeclaredSignal`, `AttributeRequirement`, `ConformanceReport`/`ConformanceFinding`. Emitter `@qyl/telemetry-control-graph` (built, green, 8 fresh artifacts). | The other repos' own code comments name it as canonical; it emits the **superset** shape. |
| 2 | **Qyl.Opentelemetry.SemanticConventions** | The OTel semantic-convention **vocabulary** (attribute keys as C# constants + Roslyn generator + 47 analyzers). Pinned OTel v1.41.0 / Weaver v0.23.0 / commit `e018fe6f`. | It is the published package every consumer references. |
| 3 | **qyl-dotnet-autoinstrumentation** | The .NET AOT instrumentation lane **and its own ownership/coverage ledger** `docs/contracts/qyl-aot-ownership.yaml` (lane / evidence / payload-access / 60-item upstream mapping). | The ownership ledger is a genuinely separate coverage layer — no analogue elsewhere. |

**Consumer (not a source of truth):** **qyl** (umbrella) — collector + dashboard + the runtime
conformance verifier. It *consumes* all three. (The 5th repo `typespec-otel-semconv` is the
TypeSpec projection of source #2's vocabulary — see DUP-2.)

---

## DUP-1 — Conformance plan + signal/conformance model declared in 3 places (HIGH)

The same wire contract (`services[] → expected_signals[] → {required,recommended,opt_in}_attributes`,
envelope `schema_version` + `graph_schema_version`) is produced/modeled three times:

- **qyl-api-schema** `emitters/telemetry-control-graph/src/index.ts:568` (`buildConformancePlan`) → `generated/control-graph/conformance-plan.json`. **SUPERSET** — also emits `exporter_ids` + `export_edges`. → **CANONICAL.**
- **qyl-dotnet-autoinstrumentation** `tools/generate-contract-artifacts.py` (`render_conformance_plan`, ~line 1355) → `docs/qyl-aot-autoinstrumentation.conformance-plan.json`. **SUBSET** — hand-aligned (it even carries `graph_schema_version:"1"`) but **zero wiring** to api-schema.
- **qyl** `internal/qyl.conformance/ConformancePlan.cs` — hand-written C# DTO. The `.csproj` literally says: *"DTOs mirror the @qyl/telemetry-control-graph wire contract … replaced by `Qyl.Api.Contracts` types once the ControlGraph models ship."*

**Fix — AUTHORITATIVE, per `qyl-api-schema/HANDOFF.md` 0.4.0 Auflage 1 (this supersedes my
earlier "validate against api-schema's schema" wording, which was wrong):**

The two plans are **NOT merged** — disjoint subjects (product services vs 13 AOT demo services).
The shared thing is the **wire format**, which `qyl/internal/qyl.conformance` already pins:
`{schema_version, graph_schema_version, services:[{service_name, profile_id, expected_signals:[{kind, name, required_attributes, recommended_attributes, opt_in_attributes}]}]}`.

- **qyl-dotnet-autoinstrumentation:** the Python plan is the "forbidden parallel world" only in
  that its shape is hand-defined — fix = **generate** it from the YAML SSOT
  (`docs/otel-dotnet-auto-60-contract-items.yaml`; today the inputs are
  `docs/contracts/otel-dotnet-auto-60.upstream.yaml` + `qyl-aot-ownership.yaml`, so confirm the
  SSOT filename in cowork) into that exact wire format. The **hand-built JSON schema STAYS**
  (stricter `additionalProperties:false`, drift caught by a wire-roundtrip test). Cross-repo
  model-derivation from the TypeSpec is the **later** step, explicitly NOT 0.4.0.
- **qyl:** a C#-internal third model is **forbidden** — `ConformancePlan.cs` is a thin pinned wire
  mirror, not a re-declaration; `ConformanceVerifier.Verify` stays schema-agnostic.
- **Severity law (binding):** `declared_missing` / `undeclared_emitted` / required-attr-missing =
  error; recommended / unknown = warning.

See HANDOFF.md for Auflage 2 (`call_site_visibility` matrix dimension) and Auflage 3
(`payload_access: typed_public | reflection_required`) — both part of the 0.4.0 cut, not covered
elsewhere in this plan.

---

## DUP-2 — Two independent Weaver pipelines projecting the SAME upstream commit (HIGH)

The OTel semconv vocabulary is Weaver-generated **twice**, hand-synced by pin:

- **Qyl.Opentelemetry.SemanticConventions** `src/…SourceGeneration/scripts/templates/registry/weaver.yaml` → C# constants. Pin: `1.41.0` / `e018fe6f…` / Weaver `0.23.0`.
- **typespec-otel-semconv** `weaver.yaml` → TypeSpec `otel-keys.gen.tsp` (vendored into qyl-api-schema as `generated/otel-keys.gen.tsp`). **Same** commit, **same** Weaver version — synced by hand.

**Fix:** hoist the pin (`semconv_commit` + `weaver_version`) into **one** shared file both
pipelines read, or run one Weaver invocation with two templates. The per-language *consumers*
(AOT references the C# package; api-schema references the TypeSpec projection) are correct and
stay. This is the highest silent-drift risk in the fleet.

---

## 2 false alarms — DO NOT TOUCH

- **`QylSemConvRegistry` (autoinstrumentation) is NOT a parallel semconv definition.** `SemConvRegistryGenerator` walks the *referenced* semconv assembly symbols at compile time and bakes a `FrozenSet<string>` index. It adds only ~4 qyl-owned keys (`qyl.instrumentation.domain`, `qyl.conformance.verdict`, `log.severity`). Correct reuse — leave it.
- **qyl umbrella's `typespec-maf` is NOT a rival TypeSpec setup.** It is a *planned, not-yet-built* emitter (no `.tsp` exists in qyl; only the PRD) that will *depend on* api-schema's TCG, not fork it. Nothing to dedupe yet.

---

## Bonus defects found (don't lose these)

1. **qyl: `CLAUDE.md` is a broken symlink → `AGENTS.md`, but no `AGENTS.md` exists.** The agent-instructions file is effectively missing from the umbrella repo. Real defect.
2. **Namespace mismatch (verify before acting):** SemanticConventions analyzers `QYL0103/0106/0107/0108/0109/0405` bind by metadata-name to `Qyl.Instrumentation.Instrumentation.Traced*` attributes — but **no sibling appears to define that namespace** (autoinstrumentation has no `Qyl.Instrumentation`; qyl forbids un-prefixed `[Traced]`). 6 analyzers may police a contract nobody satisfies. **Human-confirm which repo, if any, ships `Qyl.Instrumentation.Instrumentation.TracedAttribute`.**
3. **Version skew:** SemanticConventions consumed at `3.0.0` (qyl) vs `3.0.1` (autoinstrumentation). qyl-api-schema has 4-way release drift (npm `0.2.0`, nupkg `0.1.9`, stray tarballs `0.1.4–0.1.8`, top tag `v0.1.1`) + 5 leftover `.tgz` + a committed `.nupkg` at root.
4. **otelconventions-lint second snapshot:** `qyl-api-schema/emitters/otelconventions-lint/data/otel-attribute-registry.json` is self-labeled "registry 1.40" — already skewed from the fleet's 1.41.0. Same-repo drift.
5. **api-schema `HANDOFF.md` is partially stale:** claims `fail-on-diagnostics` is "typed but dead" (actually removed) and the conformance plan is "shallow" (actually already emits `exporter_ids`/`export_edges`). Treat it as history, not truth.
6. **qyl dead code:** `internal/qyl.instrumentation.generators/Models/Models.cs` `ProviderRegistry.GenAiProviders` is unwired and could drift from semconv `gen_ai.provider.name`. Also `internal/frankenstein/` is a game-asset side-quest with zero telemetry ties — extraction candidate.

---

## TODO (ordered, checkable — execute in cowork)

> **Scope split (per HANDOFF 0.4.0):** the **0.4.0 cut** = Phase C (autoinstrumentation YAML→generate
> into the pinned wire format + Auflage 2/3 fields) **plus** the api-schema HANDOFF.md TODO. Phase A
> + B1 (api-schema ships `Qyl.Api.Contracts ≥0.2.0` ControlGraph types, qyl swaps its hand DTOs) are
> the **LATER cross-repo model-derivation step — NOT 0.4.0**. DUP-2 + the hygiene defects can run
> independently anytime.

**Phase A — api-schema becomes the single contract producer**
- [ ] A1. In `qyl-api-schema`, confirm the TCG models (`DeclaredSignal`, `ConformanceReport`, etc.) are exported into the `Qyl.Api.Contracts` C# package; bump it to `0.2.0` and publish.
- [ ] A2. Resolve the api-schema release-marker drift to a single authoritative version; delete the stray root `.tgz`/`.nupkg`.

**Phase B — qyl consumes instead of re-declaring**
- [ ] B1. In `qyl`, bump `Qyl.Api.Contracts` to `0.2.0`; replace `internal/qyl.conformance/ConformancePlan.cs` + `ConformanceReport.cs` with the generated ControlGraph types.
- [ ] B2. Fix the broken `CLAUDE.md`→`AGENTS.md` symlink (author the missing `AGENTS.md`).
- [ ] B3. Delete dead `ProviderRegistry.GenAiProviders` or wire it to semconv values.

**Phase C — autoinstrumentation: generate the plan from YAML SSOT (per HANDOFF 0.4.0 Auflage 1)**
- [ ] C1. Make `tools/generate-contract-artifacts.py` **generate** `conformance-plan.json` from the YAML SSOT into the wire format `qyl.conformance` pins (do NOT hand-define the shape; do NOT validate against api-schema's schema — that's the later cross-repo step). Hand-built JSON schema stays, made stricter (`additionalProperties:false`) + a wire-roundtrip test. Keep the `qyl-aot-ownership.yaml` lane/evidence layer.
- [ ] C2. **Verify** the Auflage-2/3 fields are present — they ALREADY EXIST in `docs/contracts/qyl-aot-ownership.yaml` + `docs/generated/qyl-aot-contract.resolved.yaml` + `.schema.json` (do NOT re-add). Work is **reclassify only**: move `library_internal` / `reflection_required` items out of Bucket A per the rule. (Corrected vs HANDOFF@ce0559d; current truth is `02a2ab4`.)
- [ ] C3. Align the `Qyl.OpenTelemetry.SemanticConventions` version with qyl (`3.0.0` vs `3.0.1`).

**Phase D — semconv pin de-duplication**
- [ ] D1. Hoist `semconv_commit` + `weaver_version` into one shared source read by both `Qyl.Opentelemetry.SemanticConventions/weaver.yaml` and `typespec-otel-semconv/weaver.yaml`.
- [ ] D2. Refresh or delete `otelconventions-lint/data/otel-attribute-registry.json` (1.40 → 1.41.0 or remove).

**Phase E — verify the namespace contract**
- [ ] E1. Confirm/repair the `Qyl.Instrumentation.Instrumentation.Traced*` analyzer binding (bonus defect #2).

---

## Verification goal (you know you're done when)

1. `grep -r "graph_schema_version\|expected_signals" ` across the fleet returns **one producer**
   (api-schema's emitter) plus **consumers/validators** — no second hand-rolled envelope
   declaration.
2. `qyl/internal/qyl.conformance` contains **no** hand-written plan/report DTOs — only
   `Qyl.Api.Contracts` types.
3. The semconv pin (`e018fe6f` + Weaver `0.23.0`) exists in **exactly one** shared location both
   Weaver pipelines read.
4. All five repos build green; `Qyl.OpenTelemetry.SemanticConventions` is referenced at one
   aligned version fleet-wide.
5. No repo re-declares a concept owned by another (the 2 false alarms stay as-is).

**Hard rule (Alex's): do not release/push any repo until its phase's points are clean.**

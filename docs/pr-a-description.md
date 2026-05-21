# PR-A draft — `OpenTelemetry.SemanticConventions.SourceGeneration` skeleton

> **Status:** DRAFT — pending Alexander's review of the pre-push checklist (§ "Pre-push checklist" below) and a fresh contrib re-cherry-pick. Do not push to `open-telemetry/opentelemetry-dotnet-contrib` until the local fork branch has been rebuilt from the current qyl source-of-truth.
>
> **Source branch:** pending re-cherry-pick to `ANcpLua/opentelemetry-dotnet-contrib` `feat/semconv-srcgen-2856`; the previously staged fork branch is pre-marker-split and must not be pushed as-is.
> **Target:** `open-telemetry/opentelemetry-dotnet-contrib` `main` @ `2e4984b9124ddffff4d76bda53a67b83dbf1d707`.
> **qyl twin:** `O-ANcppLua/qyl` `feat/semconv-srcgen` (private workspace, source-of-truth branch; see [dual-target-mapping.md](https://github.com/O-ANcppLua/qyl/blob/feat/semconv-srcgen/docs/dual-target-mapping.md)).

---

## Suggested PR title

`[OpenTelemetry.SemanticConventions.SourceGeneration] PR-A: marker attribute + attribute-key emitter`

## Suggested PR body

### Summary

Adds the new project `OpenTelemetry.SemanticConventions.SourceGeneration` at `src/OpenTelemetry.SemanticConventions.SourceGeneration/`. It is a Roslyn `IIncrementalGenerator` analyzer that ships as a consumer-facing NuGet under `analyzers/dotnet/cs/`. Consumers apply `[SemanticConventionAttributes("<prefix>")]` or `[SemanticConventionIncubatingAttributes("<prefix>")]` to a `static partial` class and the generator emits the matching attribute-key constants (and enum-value classes) at compile time.

Closes #2856. The prior implementation in #2869 was closed by stale-bot on 2025-09-01; this PR re-opens the work on a fresh slate with the Weaver-toolchain pipeline that #4362 introduced.

PR-A is the foundational slice: paired stable/incubating marker attributes + attribute-key surface only. PR-B (metric-name constants), PR-C (event-name + payload structs), PR-D (typed Meter factory wrappers), and PR-E (typed `Activity` setter extensions) follow the same paired-marker split as stacked PRs.

qyl is intentionally treated as a private-alpha proving ground for this work: it may absorb renames, emitter reshaping, and broader experiments so the upstream contribution can stay small and idiomatic. If qyl's current shape conflicts with the OpenTelemetry/.NET shape maintainers prefer, qyl should bend; the upstream PR should not grow compatibility layers to preserve qyl-local choices.

qyl also contains unrelated private runtime instrumentation generators under `internal/`; those are not part of this proposal.

### Why this shape (and what it is not)

Output shape follows the Java/Python convention (one set of typed accessors per consumer, generated at consumer compile time) rather than the previous in-repo `OpenTelemetry.SemanticConventions/Attributes/*Attributes.cs` shape. The new project does **not** ship a precomputed `.cs` per attribute group; it ships the generator and a single embedded full `resolved-registry.json` for semconv v1.41.0. Consumers reference one NuGet and get exactly the surface they ask for.

- This is consistent with `specification-principles.md` *Be User Driven* and *Be Simple*: one package, paired stable/incubating markers, no fan-out across `Attributes/` files for keys the consumer never references.
- It is additive per `telemetry-stability.md`: the existing `OpenTelemetry.SemanticConventions` 1.x stable surface (the regenerated `Attributes/*Attributes.cs` files from #4362) is untouched. Existing consumers continue to work without code change.
- It does not introduce per-target conditional emit logic: the namespace token flows in via `typeSymbol.ContainingNamespace.ToDisplayString()` at marker-extraction time (see `Extractors/MarkerExtractor.cs:25-29`) and is a single string passed through to the emitter. No `if (target == "contrib") …` branches anywhere in the generator. Documented invariant in the qyl twin's `docs/dual-target-mapping.md` §2.1.
- It is not an instrumentation library. It does not add `AddXxxInstrumentation` glue, OTLP-shaped output, `ILogger` code generation, runtime delivery shims, or ownership of `Meter`, `ActivitySource`, `Logger`, instrumentation scope, versioning, or enablement. Any future direction in those areas must be a separate explicit proposal, not hidden inside this source-generator stack.

### Pipeline (what the new files actually do)

```
open-telemetry/semantic-conventions @ v1.41.0
  -> weaver registry generate
     using scripts/templates/registry/resolved-registry.json.j2
  -> Resources/resolved-registry.json
     (one EmbeddedResource, full semconv v1.41.0 projection)
  -> DTOs in Models/*.cs (resolved-registry-v2 shape; readonly record structs)
  -> AttributesEmitter
  -> .g.cs partial-class completion in the consumer's compilation
```

### Files added

```
src/OpenTelemetry.SemanticConventions.SourceGeneration/
  OpenTelemetry.SemanticConventions.SourceGeneration.csproj       Microsoft.NET.Sdk, netstandard2.0, IsRoslynComponent=true
  README.md                                                        consumer-facing usage
  SemConvAttributesGenerator.cs                                   IIncrementalGenerator entrypoint + RegisterPostInitializationOutput of the marker attribute
  Polyfills.cs                                                    inline EquatableArray<T> + FileWithName (replaces ANcpLua.Roslyn.Utilities.Sources)
  IsExternalInit.cs                                               netstandard2.0 record-support polyfill
  Models/
    Models.cs                                                     RegistryModel, GroupModel, AttributeModel, AttributeTypeModel, EnumMemberModel, StabilityModel, DeprecatedModel, MarkerModel
    Models.Activities.cs                                          (carried for PR-D/E continuity — unused under PR-A scope; remove if maintainers prefer trimming)
    Models.Instruments.cs                                         (carried for PR-B/C/D — see above)
  Extractors/
    MarkerExtractor.cs                                            reads the marker attribute + containing namespace
    JsonReader.cs                                                 lightweight JsonObject/JsonArray/JsonValue parser (no System.Text.Json dependency — IDE-version-clash protection)
    RegistryLoader.cs                                             one-shot embedded-resource loader; lazy-eval'd once per analyzer assembly load
  Emitters/
    AttributesEmitter.cs                                          partial-class completion emit, contrib-shape member region
  Resources/
    resolved-registry.json                                        embedded full v1.41.0 registry projection
  scripts/
    generate.sh                                                   pinned-Weaver regen script (semconv v1.41.0 + Weaver v0.23.0 + commit e018fe6f)
    templates/registry/
      resolved-registry.json.j2                                   custom Jinja template (output shape; not the contrib Attributes Jinja)
      weaver.yaml                                                 weaver config (params: schema_version, semconv_commit, weaver_version)
  build/
    OpenTelemetry.SemanticConventions.SourceGeneration.props      empty placeholder for downstream MSBuild hooks (NU5017 satisfaction)
```

No file under `src/OpenTelemetry.SemanticConventions/` is modified — the new project is strictly additive. `opentelemetry-dotnet-contrib.slnx` is **not** modified in this commit; maintainers can add the project + test entries during review.

### Pins

| Component | Pin | Justification |
|---|---|---|
| `open-telemetry/semantic-conventions` | `v1.41.0` @ `e018fe6f91862f5ed63c082f87697cddac596784` | Aligns with #4362's regen pin; both projects ship the same registry. |
| `open-telemetry/weaver` | `v0.23.0` (Docker image `otel/weaver:v0.23.0`) | Canonical generator per [`open-telemetry/weaver`](https://github.com/open-telemetry/weaver); pinned via `scripts/templates/registry/weaver.yaml`. |
| `Microsoft.CodeAnalysis.CSharp` | `5.3.0` via `VersionOverride` | Latest stable Roslyn; consumer projects need ≥5.3.0 for the analyzer to load. `PrivateAssets="contentfiles;analyzers"` so it flows as a public NuGet dependency (NU5017 satisfaction). |

### Verification status

- **qyl-side build:** clean. `dotnet build packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator.csproj` → 0 warnings, 0 errors.
- **qyl-side tests:** **60/60 passing** on `feat/semconv-srcgen` after the all-surface stability split and metadata-model completion. Includes strict namespace-invariant coverage, stable/incubating snapshot pairs, enum-member stability filtering, generator caching checks, metric requirement/entity metadata coverage, activity context requirement coverage, target-agnostic event coverage, and event-body preservation coverage.
- **qyl-side AOT smoke:** `eng/smoke/qyl.semconv.smoke` (net9.0/osx-arm64, `PublishAot=true`) publishes a native binary with **zero AOT warnings**, runs cleanly, and prints all five PR symbols emitted from the full v1.41.0 registry:
  ```
  const: disk.io.direction
  metric: http.server.request.duration (s)
  event: session.start payload.SessionId=session-abc
  meter-ext: typed factory emitted Histogram<double> for http.server.request.duration
  activity-ext: typed Set* applied method=GET
  ```
- **Embedded registry:** full Weaver `v0.23.0` output over semconv `v1.41.0` model (commit `e018fe6f…`) — **935 groups** (242 attribute groups, 70 spans, 63 entities, 529 metrics, 31 events), **925 catalog attributes**, plus metric/event convenience arrays for the emitters. Generated via the project's pinned `scripts/generate.sh` → `otel/weaver:v0.23.0 registry generate` invocation; byte-reproducible from the pinned semconv commit.
- **full-surface TFM matrix:** **6/6 passing** for `net472`, `netstandard2.0`, `net6.0`, `net8.0`, `net9.0`, and `net10.0`. The matrix binds attributes, metrics, events, meter factories, and activity helpers across stable/incubating surfaces.
- **snapshot separation:** stable/incubating byte snapshots exist for every generated signal family covered by the smoke matrix. The old ambiguous snapshots (`qyl.http.expected.txt`, `qyl.disk.expected.txt`, `qyl.metrics.http-server.expected.txt`, etc.) are removed.
- **contrib-side build:** not refreshed after the stability-intercept pass. The fork branch must be rebuilt from qyl before any upstream push; prior contrib build results apply only to the pre-marker-split branch.
- **Phase 3 compliance audit** (`docs/weaver-audit-phase3-report.md`): **PASS** after the stability-intercept pass. The old seed-registry / `[Experimental]` / PR-A-only-matrix findings are superseded; the generator now also preserves group-level metadata, signal-local requirement levels, notes, examples, event body metadata, and metric/event entity associations from the Weaver projection. Remaining notes are intentional architecture and downstream contrib publication work.

### Architecture divergences disclosed (Phase 3 audit findings)

These are not defects on the qyl source-of-truth — they are deliberate architectural choices that diverge from the existing `OpenTelemetry.SemanticConventions` 1.x track. Disclosed here to protect maintainer review bandwidth (`contrib-review-bandwidth` constraint: review is the scarce resource; surface unusual choices in the PR body, not in a sub-thread the bot doesn't see).

**Findings 2 + 3 + 4 of the Phase 3 audit have been resolved in qyl source-of-truth before this PR opens.** Summary of resolutions:
- *Finding 2* (qyl-additive `[Experimental]` emission on PR-D/PR-E surfaces): **removed**. Verified via direct source-read of `open-telemetry/semantic-conventions-java@02abd594` and `opentelemetry-python@7c4a6b8b` — neither emits per-symbol experimental markers; both rely on surface separation. qyl now matches that model with paired stable/incubating markers across attributes, metrics, events, meters, and activity helpers. Incubating markers are supersets, mirroring Java/Python package behavior.
- *Finding 3* (5-attribute seed registry): **resolved**. `Resources/resolved-registry.json` is now the full Weaver `v0.23.0` output over semconv `v1.41.0` — 935 groups / 925 catalog attributes / 529 metrics / 31 events. Regen pipeline: `scripts/generate.sh` runs `otel/weaver:v0.23.0 registry generate --registry=<semconv 1.41.0 model> --templates=<scripts/templates/registry>` → emits the JSON via a qyl-owned Jinja template `resolved-registry.json.j2`.
- *Finding 4* (`[Experimental]` requires .NET 8+ at consume time): **auto-resolved** by Finding 2's removal. No `[Experimental]` is emitted anywhere, so the netstandard2.0 consumer-compile risk is gone.

The two remaining findings are intentional architecture:

1. **Roslyn-generator output is partial-class completion, not standalone `.cs`.** Contrib's existing `OpenTelemetry.SemanticConventions/Attributes/<Prefix>Attributes.cs` is a sealed `public static class` committed to the repo. This generator emits `static partial class` completions of consumer-declared marker classes. Consequence: the new project's `.cs` files are **not committed**; they are produced per consumer build. This is the headline ergonomic difference vs the existing 1.x package, and is the architectural intent — not a side-effect. (Audit Finding 1.)
2. **Namespace-source is `ContainingNamespace`, not `RootNamespace` MSBuild property.** The earlier design doc hypothesized `AnalyzerConfigOptions.GetBuildProperty("RootNamespace")` as the namespace source; the shipping implementation reads `typeSymbol.ContainingNamespace.ToDisplayString()`. Both satisfy the §2.1 invariant (single string token, no branching emit logic). The `ContainingNamespace` approach is strictly more flexible — one consumer can host multiple marker classes in distinct namespaces. The invariant test (`NamespaceParameterizationTest`) verifies the strict-equality-except-namespace-line property. (Audit Finding 5; doc reconciled in qyl `docs/dual-target-mapping.md` §2.1.)

3. **Incubating markers are supersets.** This matches Java/Python and prevents a breaking move when a symbol is promoted from development to stable. Consumers should normally choose either the stable marker or the incubating marker for a given prefix. If a test intentionally declares both meter/activity projections in one namespace, shared extension methods can be called through the generated static class name to avoid normal C# extension-method ambiguity.

4. **qyl bends toward upstream, not the reverse.** The qyl branch can carry private-alpha churn and extra validation to prove the shape, but the contrib PR should remain a narrow OpenTelemetry/.NET contribution. Do not preserve qyl-specific convenience APIs in upstream if maintainers prefer a smaller or differently named surface.

### Spec-compliance attestation

- `specification-principles.md`: Be User Driven (one requested marker class → typed surface for the consumer's prefix only), Be General (any prefix in the registry), Be Stable (additive vs the existing 1.x track; nothing is renamed or dropped), Be Consistent (matches Java/Python stable/incubating surface split), Be Simple (one csproj, no companion runtime library).
- `telemetry-stability.md`: PR-A is additive — the `[SemanticConventionAttributes("http")]` / `[SemanticConventionIncubatingAttributes("http")]` partial-class pattern adds a new code-gen surface. The existing `OpenTelemetry.SemanticConventions/Attributes/*Attributes.cs` files stay exactly as #4362 emits them.
- `spec-compliance-matrix.md`: PR-A ships attribute keys, which the matrix already counts as supported in the .NET row at the current SHA. No row movement required.
- `glossary.md`: vocabulary spot-checks (audit §A vocabulary pass) found no drift; "attribute", "registry-defined key", "span", "value" used per spec.

---

## Pre-push checklist (decided by Alexander before push)

Items maintainers will likely surface — better to decide upfront and ship clean than burn round-trips. Each item names the proposed default and an alternative; flip if preferred.

1. **Test surface.** *Default: ship PR-A without `test/OpenTelemetry.SemanticConventions.SourceGeneration.Tests/`*; first commit on this branch is the source-only skeleton, and a follow-up commit on the same branch (before push) adds the test project using contrib's xunit v2 + `Microsoft.CodeAnalysis.CSharp.Testing` stack. The qyl test surface (`tests/qyl.opentelemetry.semconv.sourcegen.tests/`, 60 tests) is on xunit.v3 + AwesomeAssertions + `ANcpLua.Roslyn.Utilities.Testing` and cannot be cherry-picked mechanically. *Alternative: defer test project to a stacked PR (PR-A.t).*
2. **Scope cut.** *Default: ship PR-A with the full Models surface (Models.cs + Models.Instruments.cs + Models.Activities.cs) and the full `RegistryLoader.cs` that parses metrics/events/instruments/activities.* This is qyl-shape-preserving and zero-edit; downstream stacked PRs only add emitters, never extend Models. *Alternative: trim `RegistryLoader.cs` to PR-A scope only (drop `ParseMetrics`, `ParseEvents`, `ParseGroups` overloads that touch metric/event/activity records) — smaller PR-A footprint but a substantive code edit that diverges from qyl source-of-truth.*
3. **Polyfill inlining.** *Default: ship the inline `Polyfills.cs` (~80 lines) carrying only the surface the generator consumes.* Reviewers can audit it in one read. *Alternative: depend on a shared polyfill package contrib already references (none currently exists at the right surface), or use `src/Shared/` linked sources — contrib's existing pattern for cross-project polyfills. The shared-source path is more contrib-native but means the generator csproj gets `<Compile Include="$(RepoRoot)\src\Shared\…">` references that complicate cherry-picks back to qyl.*
4. **StyleCop warnings (129).** Mostly mechanical: missing `// <copyright>` headers, multiple types per file in `Models.cs`, file-name vs first-type-name mismatch in `Models.Instruments.cs` / `Models.Activities.cs`, trailing-comma style. *Default: clean these up in a follow-up commit on this branch before push.* *Alternative: ship as-is and accept maintainer-requested cleanup during review.*
5. **`opentelemetry-dotnet-contrib.slnx` entry.** Not added in the current commit. *Default: add as a follow-up commit before push.* *Alternative: leave for maintainer to add during review (they may have a preferred solution-folder placement).*
6. **`OpenTelemetry.SemanticConventions.SourceGeneration.proj` under `build/Projects/`.** Contrib uses these per-project `.proj` files to wire CI builds (see `build/Projects/OpenTelemetry.SemanticConventions.proj`). *Default: add as a follow-up commit before push.*
7. **PR description references to qyl twin.** The body of this PR references `O-ANcppLua/qyl` (private workspace) as the source-of-truth twin. *Default: keep the reference (it's the trail for the cherry-pick mechanism and is informational for maintainers).* *Alternative: drop the qyl reference — keeps the contrib PR self-contained but loses traceability for any future feedback-routing decisions.*

### Single-shot push command (run after checklist items are reconciled)

```bash
cd /Users/ancplua/RiderProjects/opentelemetry-dotnet-contrib/.worktrees/feat-semconv-srcgen-2856
git push -u origin feat/semconv-srcgen-2856

gh pr create \
  --repo open-telemetry/opentelemetry-dotnet-contrib \
  --base main \
  --head ANcpLua:feat/semconv-srcgen-2856 \
  --title "[OpenTelemetry.SemanticConventions.SourceGeneration] PR-A: marker attribute + attribute-key emitter" \
  --body-file /Users/ancplua/RiderProjects/qyl/.agents/worktrees/integration-semconv-srcgen/docs/pr-a-description.md
```
(`gh pr create --body-file` will pick up everything above the **Pre-push checklist** divider. The checklist is review-only and should be stripped before push — easiest is to keep two copies of this file: this one for review, and a stripped one for `--body-file`.)

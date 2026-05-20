# PR-A draft — `OpenTelemetry.SemanticConventions.SourceGeneration` skeleton

> **Status:** DRAFT — pending Alexander's review of the pre-push checklist (§ "Pre-push checklist" below). Do not push to `open-telemetry/opentelemetry-dotnet-contrib` until checklist items decided.
>
> **Source branch:** `ANcpLua/opentelemetry-dotnet-contrib` `feat/semconv-srcgen-2856` @ `f3e86716dcc1c37e9cccd7d4c8ef4904e68edaa9`.
> **Target:** `open-telemetry/opentelemetry-dotnet-contrib` `main` @ `2e4984b9124ddffff4d76bda53a67b83dbf1d707`.
> **qyl twin:** `O-ANcppLua/qyl` `feat/semconv-srcgen` @ `7acf7755…` (private workspace, ships in parallel; see [dual-target-mapping.md](https://github.com/O-ANcppLua/qyl/blob/feat/semconv-srcgen/docs/dual-target-mapping.md)).

---

## Suggested PR title

`[OpenTelemetry.SemanticConventions.SourceGeneration] PR-A: marker attribute + attribute-key emitter`

## Suggested PR body

### Summary

Adds the new project `OpenTelemetry.SemanticConventions.SourceGeneration` at `src/OpenTelemetry.SemanticConventions.SourceGeneration/`. It is a Roslyn `IIncrementalGenerator` analyzer that ships as a consumer-facing NuGet under `analyzers/dotnet/cs/`. Consumers apply `[SemanticConventionAttributes("<prefix>")]` to a `static partial` class and the generator emits the matching attribute-key constants (and enum-value classes) at compile time.

Closes #2856. The prior implementation in #2869 was closed by stale-bot on 2025-09-01; this PR re-opens the work on a fresh slate with the Weaver-toolchain pipeline that #4362 introduced.

PR-A is the foundational slice: marker attribute + attribute-key surface only. PR-B (metric-name constants), PR-C (event-name + payload structs), PR-D (typed Meter factory wrappers), and PR-E (typed `Activity` setter extensions) will follow as stacked PRs on this branch once PR-A is OPEN, per the stacked-PR cadence in the dual-target plan.

### Why this shape (and what it is not)

Output shape follows the Java/Python convention (one set of typed accessors per consumer, generated at consumer compile time) rather than the previous in-repo `OpenTelemetry.SemanticConventions/Attributes/*Attributes.cs` shape. The new project does **not** ship a precomputed `.cs` per attribute group; it ships the generator and a single embedded `resolved-registry.json` (~5 KiB at the PR-A seed-fixture size; full v1.41.0 registry under PR-B). Consumers reference one NuGet and get exactly the attribute-key surface they ask for.

- This is consistent with `specification-principles.md` *Be User Driven* and *Be Simple*: one package, one marker, no fan-out across `Attributes/` files for keys the consumer never references.
- It is additive per `telemetry-stability.md`: the existing `OpenTelemetry.SemanticConventions` 1.x stable surface (the regenerated `Attributes/*Attributes.cs` files from #4362) is untouched. Existing consumers continue to work without code change.
- It does not introduce per-target conditional emit logic: the namespace token flows in via `typeSymbol.ContainingNamespace.ToDisplayString()` at marker-extraction time (see `Extractors/MarkerExtractor.cs:25-29`) and is a single string passed through to the emitter. No `if (target == "contrib") …` branches anywhere in the generator. Documented invariant in the qyl twin's `docs/dual-target-mapping.md` §2.1.

### Pipeline (what the new files actually do)

```
open-telemetry/semantic-conventions @ v1.41.0
  -> weaver registry generate
     using scripts/templates/registry/resolved-registry.json.j2
  -> Resources/resolved-registry.json
     (one EmbeddedResource, ~5 KiB seed; PR-B substitutes the full registry)
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
    resolved-registry.json                                        embedded 5-attribute seed fixture (PR-B substitutes full v1.41.0)
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
- **qyl-side tests:** 44/44 passing on `feat/semconv-srcgen@7acf7755`. Includes a strict invariant test (`NamespaceParameterizationTest`) proving that generator output differs only on the namespace declaration line when run under two distinct consumer namespaces.
- **qyl-side AOT smoke:** `eng/smoke/qyl.semconv.smoke` (net10.0) publishes a native binary and exercises all five PR symbols (`disk.io.direction`, `http.server.request.duration`, `session.start.SessionId`, `Histogram<double>` factory, `SetHttpRequestMethod`) — PR-B–E surfaces, not PR-A, but the marker pipeline and registry loader are the PR-A foundation for that smoke.
- **contrib-side build:** clean (0 errors). 129 StyleCop warnings remain — see *Pre-push checklist* item (4).
- **Phase 3 compliance audit** by `agent/weaver-spec-auditor` (`docs/weaver-audit-phase3-report.md`): **PASS** with 5 documented findings (all non-blocking; surfaced in *Architecture divergences disclosed* below).

### Architecture divergences disclosed (Phase 3 audit findings)

These are not defects on the qyl source-of-truth — they are deliberate architectural choices that diverge from the existing `OpenTelemetry.SemanticConventions` 1.x track. Disclosed here to protect maintainer review bandwidth (`contrib-review-bandwidth` constraint: review is the scarce resource; surface unusual choices in the PR body, not in a sub-thread the bot doesn't see).

1. **Roslyn-generator output is partial-class completion, not standalone `.cs`.** Contrib's existing `OpenTelemetry.SemanticConventions/Attributes/<Prefix>Attributes.cs` is a sealed `public static class` committed to the repo. This generator emits `static partial class` completions of consumer-declared marker classes. Consequence: the new project's `.cs` files are **not committed**; they are produced per consumer build. This is the headline ergonomic difference vs the existing 1.x package, and is the architectural intent — not a side-effect. (Audit Finding 1.)
2. **`[Experimental]` annotations on PR-D/PR-E (not in this PR; flagged for the stacked follow-ups).** When the registry attribute's stability is `development`, `alpha`, `beta`, or `release_candidate`, PR-D's meter factory wrappers and PR-E's `Activity` setters will emit `[global::System.Diagnostics.CodeAnalysis.Experimental("OTEL_SEMCONV_EXPERIMENTAL_<KEY>")]`. The existing `OpenTelemetry.SemanticConventions.Incubating` package (per #4362) leans on the package-name boundary instead. The generator does not have a package-name boundary (one analyzer NuGet, consumer-chosen namespaces), so per-symbol annotations are the only stability signal a consumer sees. *PR-A does not exercise this yet — first appears in PR-D.* (Audit Finding 2.)
3. **PR-A seed registry is a 5-attribute fixture, not the full v1.41.0.** `Resources/resolved-registry.json` is hand-curated for the PR-A test loop (5 catalog attributes + 1 deprecated + 1 development-tier + 2 session attributes + groups for HTTP/disk/network). PR-B substitutes the full Weaver-generated registry. The csproj comment near the `<EmbeddedResource>` and the README disclose this; reviewers should expect a registry diff in PR-B. (Audit Finding 3.)
4. **`[Experimental]` requires .NET 8+ at consume time.** `System.Diagnostics.CodeAnalysis.ExperimentalAttribute` lives in `net8.0` and later. A consumer targeting `netstandard2.0` that applies `[SemanticConventionMeters(…)]` to a partial whose underlying registry attribute is in a non-stable stability tier will fail to compile with CS0246. Mitigation options for PR-D/PR-E: (a) document the constraint in the README and require `net8.0+` consumers for non-stable symbols, or (b) emit an internal `ExperimentalAttribute` polyfill into the consumer compilation on demand. *Decision deferred to PR-D review.* (Audit Finding 4.)
5. **Namespace-source is `ContainingNamespace`, not `RootNamespace` MSBuild property.** The earlier design doc hypothesized `AnalyzerConfigOptions.GetBuildProperty("RootNamespace")` as the namespace source; the shipping implementation reads `typeSymbol.ContainingNamespace.ToDisplayString()`. Both satisfy the §2.1 invariant (single string token, no branching emit logic). The `ContainingNamespace` approach is strictly more flexible — one consumer can host multiple marker classes in distinct namespaces. The invariant test (`NamespaceParameterizationTest`) verifies the strict-equality-except-namespace-line property. (Audit Finding 5; doc reconciled in qyl `docs/dual-target-mapping.md` §2.1.)

### Spec-compliance attestation

- `specification-principles.md`: Be User Driven (one marker → typed surface for the consumer's prefix only), Be General (any prefix in the registry), Be Stable (additive vs the existing 1.x track; nothing is renamed or dropped), Be Consistent (matches Java/Python `*_attributes` generator shape), Be Simple (one csproj, no companion runtime library).
- `telemetry-stability.md`: PR-A is additive — the `[SemanticConventionAttributes("disk")] partial class` pattern adds a new code-gen surface. The existing `OpenTelemetry.SemanticConventions/Attributes/DiskAttributes.cs` stays exactly as #4362 emits it.
- `spec-compliance-matrix.md`: PR-A ships attribute keys, which the matrix already counts as supported in the .NET row at the current SHA. No row movement required.
- `glossary.md`: vocabulary spot-checks (audit §A vocabulary pass) found no drift; "attribute", "registry-defined key", "span", "value" used per spec.

---

## Pre-push checklist (decided by Alexander before push)

Items maintainers will likely surface — better to decide upfront and ship clean than burn round-trips. Each item names the proposed default and an alternative; flip if preferred.

1. **Test surface.** *Default: ship PR-A without `test/OpenTelemetry.SemanticConventions.SourceGeneration.Tests/`*; first commit on this branch is the source-only skeleton, and a follow-up commit on the same branch (before push) adds the test project using contrib's xunit v2 + `Microsoft.CodeAnalysis.CSharp.Testing` stack. The qyl test surface (`tests/qyl.opentelemetry.semconv.sourcegen.tests/`, 44 tests) is on xunit.v3 + AwesomeAssertions + `ANcpLua.Roslyn.Utilities.Testing` and cannot be cherry-picked mechanically. *Alternative: defer test project to a stacked PR (PR-A.t).*
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
  --body-file /Users/ancplua/RiderProjects/qyl/.agents/worktrees/agent-otel-shape-strategist/docs/pr-a-description.md
```
(`gh pr create --body-file` will pick up everything above the **Pre-push checklist** divider. The checklist is review-only and should be stripped before push — easiest is to keep two copies of this file: this one for review, and a stripped one for `--body-file`.)

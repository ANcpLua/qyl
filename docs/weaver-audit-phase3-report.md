# Phase 3 Compliance Audit Report — Tracks A–E

**Auditor.** weaver-spec-auditor (Phase 3 resume — Task #7).
**Audit branch.** `agent/weaver-spec-auditor-phase3` off `agent/gen-test-harness-eng`.
**Audit target tip.** `32c9894f26da607ce1e21f7c044e91d6f7f824bb` (`chore(samples): exercise PR-D meter factories + PR-E activity setters`).
**qyl `main` reference.** `f76ed5e8b46deb766ad2e50c62a72d2c59fd0a25`.

**Pinned upstream references** (refreshed at audit time per charter §"Audit Cadence" step 1):

- `open-telemetry/weaver` @ Phase-0 SHA `3b490b72f2e901d267132a7295ef4800225e26b6` — unchanged for the CLI decision, no re-check required since the canonical CLI is now part of the qyl `generate.sh`.
- `open-telemetry/opentelemetry-dotnet-contrib` `main` advanced from `55978aae` → **`2e4984b9124ddffff4d76bda53a67b83dbf1d707`** (audit time). The two Jinja templates that anchor §B (blobs `1a477cd7…` and `76aebe5f…`) are **unchanged** — re-fetched at the new SHA and confirmed identical to the Phase-0 record.
- `ANcpLua/opentelemetry-dotnet-contrib` PR #4362 head @ `dbd5af1b820aa5079fc091935d90b90eb98ccd55` — unchanged since Phase 0.
- `open-telemetry/semantic-conventions` `v1.41.0` @ `e018fe6f91862f5ed63c082f87697cddac596784` — qyl's pinned version, unchanged.

**Verdict.** **PASS with five non-blocking findings.** The qyl source generator is cherry-pick-ready for upstream contrib once the five findings below are recorded in the PR-A through PR-E descriptions. None of the findings is a code defect; each is an architectural divergence from contrib that is justifiable on the §A spec rules but must be surfaced explicitly to protect upstream maintainer review bandwidth (`contrib-review-bandwidth`, `dual-target-mapping.md` §3.d).

---

## Evidence summary

| Gate | Result | Evidence |
| --- | --- | --- |
| Build clean | PASS | `dotnet build tests/qyl.opentelemetry.semconv.sourcegen.tests/…`: 0 warnings, 0 errors. |
| Test suite | PASS | xUnit.v3 runner output at audit time: **`Total: 36, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`** (run against worktree tip @ `32c9894f`). |
| AOT publish gate | PASS | `dotnet publish eng/smoke/qyl.semconv.smoke -c Release` produced `artifacts/publish/qyl.semconv.smoke/release/qyl.semconv.smoke`; native binary executed and printed all 5 canonical PR symbols (`disk.io.direction`, `http.server.request.duration (s)`, `session.start payload.SessionId=session-abc`, typed `Histogram<double>` factory, `SetHttpRequestMethod(Get)`). |
| §2.1 emit-logic invariant | PASS | `tests/qyl.opentelemetry.semconv.sourcegen.tests/NamespaceParameterizationTest.cs` runs the generator under two distinct consumer namespaces (`ConsumerA.Telemetry` and `ConsumerB.Different.Nested.Path`) and asserts `bodyA.Should().Be(bodyB)` after stripping the namespace line — strict byte-equality of everything except the namespace token. See *Finding 5* for one nuance. |
| §A.2.5 stability gates (PR-E) | PASS | `SemConvActivitiesGeneratorTests.Stability_Gate_Deprecated_Attribute_Annotated_Obsolete` and `…Development_Attribute_Annotated_Experimental` (lines 138 + 161) verify that the seed-fixture's `http.request.method_original` (`stability=deprecated`, `renamed_to=http.request.method`) emits `[global::System.Obsolete("Replaced by http.request.method.")]` and `http.client.duration` (`stability=development`) emits `[global::System.Diagnostics.CodeAnalysis.Experimental("QYL_SEMCONV_EXPERIMENTAL_HTTP_CLIENT_DURATION")]`. Cross-checked against `resolved-registry.json` catalog rows: both stability tiers and `deprecated.reason=renamed`+`renamed_to` are real registry values, not test-only fixtures. |
| §C pin alignment | PASS | `Version.props:16` → `MicrosoftCodeAnalysisCSharpVersion = 5.3.0`. `packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator.csproj` references `Microsoft.CodeAnalysis.CSharp` with `VersionOverride="$(MicrosoftCodeAnalysisCSharpVersion)"` (no literal). `scripts/generate.sh` pins `GENERATOR_VERSION=v0.23.0`, `SEMCONV_VERSION=1.41.0`, `SEMCONV_COMMIT=e018fe6f…`. `scripts/templates/registry/weaver.yaml` carries the same triplet verbatim (`schema_version: "1.41.0"`, `semconv_commit: "e018fe6f…"`, `weaver_version: "0.23.0"`). |

---

## Findings

### Finding 1 — Architectural divergence from contrib: qyl emits a Roslyn source generator, not a Weaver-Jinja → C# pipeline

**Severity.** Documentation finding; not a code defect.

**Spec reference.** §A.1.5 *Be Simple*, §A.1.4 *Be Consistent*, §B *byte-identity audit*.

**Observation.** Contrib's pipeline (verified at `2e4984b9`) is `weaver registry generate` → `SemanticConventionsAttributes.cs.j2` → final `.cs` files **at registry-regen time**. The output is a sealed `public static class` per attribute group, committed to the repo as `src/OpenTelemetry.SemanticConventions/Attributes/<Prefix>Attributes.cs`. qyl ships the same pin (Weaver `v0.23.0`, semconv `1.41.0`) but its Jinja template (`scripts/templates/registry/resolved-registry.json.j2`) emits a **qyl-owned JSON projection** (`Resources/resolved-registry.json`) that a Roslyn source generator reads at **consumer compile time** to emit `static partial class` completions of consumer-declared marker classes. Consequence: qyl's emitted `.cs` files are not committed; they are generated per consumer build and bound to the marker-attribute pattern.

**Impact on §B byte-identity audit.** The §B byte-identity gate as originally framed (Phase 0 charter) compared qyl-emitted `.cs` byte-for-byte against contrib's committed `.cs`. That comparison is **not architecturally meaningful for tracks A–E** as currently implemented:
- contrib emits `public static class HttpAttributes { … }`
- qyl emits `static partial class HttpAttributes { … }` (the user owns the declaration; the generator completes it)

This is forced by the Roslyn marker-attribute pattern: the consumer's class declaration is the public surface, and the generated file is the partial half. The two outputs are **byte-equivalent on the body**, but differ on the class-declaration line and on the header (`// <auto-generated>` cites the generator name vs the Jinja template path). The three snapshot files under `tests/qyl.opentelemetry.semconv.sourcegen.tests/Snapshots/qyl.{disk,http,network}.expected.txt` are the **qyl-shaped baseline** — they correctly encode the partial-class architecture and the contrib `OpenTelemetry.SemanticConventions` namespace, demonstrating that the only intentional structural delta from contrib is `static partial class` vs `public static class`.

**Action.** Document in PR-A's description (cherry-picked from qyl `agent/generator-foundation-eng`) that the Roslyn-generator architecture is the *intent* — contrib maintainers reviewing the PR must understand they are reviewing a generator that emits a partial completion, not a fixed `.cs`. Cite the audit charter §B.3 audit gate explicitly. Per `dual-target-mapping.md` §3.d this is **not** an architectural escalation — the contrib emit logic is preserved at the source-of-truth level (Weaver-Jinja pipeline still produces the registry data); only the *consumer*-side artefact shape changes.

### Finding 2 — `[Experimental]` annotation is qyl-additive (contrib emits no `Experimental`)

**Severity.** Documentation finding; defensible.

**Spec reference.** §A.1.4 *Be Consistent*, §A.2.4 *No tier downgrade*.

**Observation.** qyl's `MetersEmitter` (PR-D) and `ActivityExtensionsEmitter` (PR-E) emit `[global::System.Diagnostics.CodeAnalysis.Experimental("QYL_SEMCONV_EXPERIMENTAL_<KEY>")]` on any meter/activity whose underlying registry attribute has `stability ∈ {development, alpha, beta, release_candidate}` (see `MetersEmitter.cs:171-198`, `ActivityExtensionsEmitter.cs:172-198`). Contrib's generated C# at `main@2e4984b9` (and at PR #4362 head `dbd5af1b`) emits **zero `[Experimental]` annotations** across all `Attributes/*.cs` files — contrib relies on the package-name boundary (`OpenTelemetry.SemanticConventions` stable vs `…Incubating` experimental) to signal stability. Verified via `grep -c Experimental` on contrib `HttpAttributes.cs`, `NetworkAttributes.cs`, `DiskAttributes.cs` (all 0) and on PR #4362's `Incubating/Attributes/Http/HttpAttributes.cs` (0).

**Inconsistency within qyl itself.** Only PR-D + PR-E emit `[Experimental]`. `AttributesEmitter` (PR-A), `MetricsEmitter` (PR-B), and `EventsEmitter` (PR-C) emit `[Obsolete]` for deprecated rows but **do not** emit `[Experimental]` for development-stability rows. There is no test fixture exercising a development-stability *attribute key* / *metric name* / *event name* for PR-A/B/C — the seed registry has `http.client.duration` (development attribute, exercised by PR-E only) and `http.client.request.body.size` (development metric, exercised by PR-D only) but does not exercise an attribute *constant* under PR-A or an event *name* under PR-C in a development tier. Therefore the inconsistency is invisible to the test suite.

**Why this is defensible.**
1. qyl's source generator emits into *consumer-chosen* namespaces. There is no package-name boundary to lean on, so per-symbol `[Experimental]` is the only signal a qyl consumer sees that an attribute/meter/activity is non-stable.
2. The annotation is *additive* per §A.2.2 — qyl does not remove or rename anything contrib emits; it adds a marker contrib chooses not to emit.
3. For typed APIs (Activity setters via PR-E, Meter factories via PR-D) the *method-shape* is what is unstable, so `[Experimental]` carries real meaning. For const-strings (PR-A/B/C) the value is fixed by the spec; only the *meaning* is unstable, which arguably belongs in XML docs (the brief/note), not in an attribute.

**Action.** Two-part:
- **PR-D / PR-E descriptions:** explicitly call out the `[Experimental]` divergence from contrib, citing §A.1.4 (Be Consistent) tension and the justification above. Maintainers reviewing PR-D/PR-E should know contrib does not do this and that the call was deliberate.
- **PR-A / PR-B / PR-C followup (non-blocking):** decide whether to align PR-A/B/C with PR-D/E by adding `[Experimental]` on dev-stability symbols, OR align PR-D/E with PR-A/B/C by *removing* `[Experimental]` and relying on XML docs. The current state — 2 emitters annotate, 3 emitters don't — is the worst of both worlds. *This is a future follow-up, not a Phase 3 fix-blocker.*

### Finding 3 — Seed registry is a 5-attribute fixture, not the full semconv 1.41.0

**Severity.** Documentation finding (already disclosed in csproj comment); blocks full §B byte-identity audit until Phase 4.

**Spec reference.** §B.3 *audit gate*, §A.2.1 *Stable instrumentations cannot rename or drop attributes*.

**Observation.** `packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/Resources/resolved-registry.json` is a 211-line, hand-curated seed (5 catalog attributes for HTTP, 1 each for disk/network, 1 deprecated `http.request.method_original`, 1 development `http.client.duration`, 2 session attributes, plus 4 metric groups and 1 event). The full semconv 1.41.0 registry — emitted by Weaver at `e018fe6f…` — has hundreds of attributes across the Stable + Incubating split (see contrib PR #4362 trees). The generator csproj comment acknowledges this: `"PR-A ships a 5-attribute seed fixture for the build/test loop; Phase 4 substitutes the full Weaver-generated file"`.

**Catalog hygiene note.** The seed registry includes catalog rows that are not referenced from any group (`session.id`, `session.user.id`, `http.client.duration`). These are intentional — they support PR-C event-payload references and PR-D/PR-E stability-gate tests respectively — but the data shape `groups[].attribute_refs` ↔ `catalog[].key` cross-reference is not enforced. A future Phase 4 step that auto-generates the registry from Weaver will eliminate this hand-curation risk.

**Action.** Track the Phase 4 substitution as a known limitation in PR-A's description. The §B byte-identity audit is reframed as: "given the same input registry, the qyl generator must emit a structural superset of contrib's class body (modulo the partial-class architecture)." Snapshot files in `Snapshots/qyl.{disk,http,network}.expected.txt` encode the contract for the seed registry; Phase 4 will extend the snapshot set to the full registry.

### Finding 4 — `[Experimental]` requires .NET 8 / net8.0+; consumers targeting netstandard2.0 may break

**Severity.** Compile-time risk for a subset of consumers; not currently exercised by the smoke gate.

**Spec reference.** §A.1.1 *Be User Driven*, §A.1.5 *Be Simple*.

**Observation.** `System.Diagnostics.CodeAnalysis.ExperimentalAttribute` was introduced in **.NET 8** (it is present in `System.Runtime.dll` for `net8.0+` only). When a consumer applies `[SemanticConventionMeters(...)]` or `[SemanticConventionActivities(...)]` to a partial class **and** the underlying registry attribute carries `stability ∈ {development, alpha, beta, release_candidate}`, the generated `.g.cs` will contain `[global::System.Diagnostics.CodeAnalysis.Experimental("QYL_SEMCONV_EXPERIMENTAL_<KEY>")]`. If the consumer's project targets `netstandard2.0` or `netframework4.x`, this fails to compile with CS0246 ("type or namespace name `Experimental` could not be found") unless a polyfill is shipped.

- The smoke sample `eng/smoke/qyl.semconv.smoke/qyl.semconv.smoke.csproj` targets `net10.0` only, so the smoke gate cannot catch this.
- The contrib-mirror packages `Qyl.OpenTelemetry.SemanticConventions[.Incubating]` declare `<TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>` but **do not consume the new generator** — their `.g.cs` files are pre-existing qyl-Weaver-pipeline output, not Roslyn-generator output. So they are also not exposed.
- None of the three consumer csprojs references `ANcpLua.Roslyn.Utilities.Polyfills` (which carries an `ExperimentalAttribute` polyfill for older TFMs).

**Action.** Two options, **no Phase 3 fix needed** because the only `net10.0` consumer compiles cleanly:
- **(a) Document.** PR-D / PR-E descriptions disclose: "Consumers targeting netstandard2.0 must shim `System.Diagnostics.CodeAnalysis.ExperimentalAttribute`; the recommended path is to reference `ANcpLua.Roslyn.Utilities.Polyfills` or to multi-target only `net8.0+`."
- **(b) Emit a polyfill on demand.** A future Phase 4 enhancement: the generator emits an internal `ExperimentalAttribute` polyfill into the consumer's compilation when the target TFM is netstandard2.0. The contrib `OpenTelemetry.SemanticConventions` package historically does this for `[Obsolete]` (it ships unconditionally because `Obsolete` is in mscorlib since net1.1). The qyl source generator could ship a similar conditional polyfill.

Either option is acceptable. The current state is **not a Phase 3 fix-blocker** because the audit branch's only consumer (`qyl.semconv.smoke`) is `net10.0`. It is a *bug that will surface the first time a netstandard2.0 consumer applies a marker to a partial that resolves to a development-stability symbol* — and the canonical contrib cherry-pick target (`feat/semconv-srcgen-2856`) inherits the netstandard2.0 target, so this *will* matter for cherry-pick.

### Finding 5 — `NamespaceParameterizationTest` proves invariant against *containing namespace*, not `RootNamespace` MSBuild property

**Severity.** Documentation finding; substantive invariant is preserved.

**Spec reference.** `dual-target-mapping.md` §2.1.

**Observation.** `docs/dual-target-mapping.md` §2.1 (otel-shape-strategist, Phase 0 Task #1) states the generator reads `RootNamespace` from `AnalyzerConfigOptions` (`build_property.RootNamespace`) to determine the target namespace. The shipping implementation (`packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Generator/Extractors/MarkerExtractor.cs:25-29`) instead reads `typeSymbol.ContainingNamespace.ToDisplayString()` from the consumer's marker-attribute target — i.e., the namespace the consumer's `[SemanticConventionAttributes("…")]`-decorated class lives in. The test `NamespaceParameterizationTest.Namespace_Change_Only_Affects_Namespace_Declaration` parameterizes over this *containing namespace*, not over `RootNamespace`. The §2.1 *substantive* invariant — "the only diff between two generator runs with different target namespaces must be the namespace declaration line" — is preserved exactly. `bodyA.Should().Be(bodyB)` is strict equality after stripping the namespace line.

**Why this is acceptable (and arguably better).** Reading the consumer's marker-class containing namespace is simpler and more flexible than coupling to MSBuild's `RootNamespace` — it lets one consumer host multiple marker classes in distinct namespaces, which the `RootNamespace` model cannot express. The invariant `dual-target-mapping.md` §2.1 enforces is "emit logic does not branch on target". Both implementations satisfy that invariant (a single string token, no conditional). So this is a *documentation/spec* mismatch, not an emit-logic violation.

**Action.** Reconcile by updating `docs/dual-target-mapping.md` §2 to describe the actual namespace source (`typeSymbol.ContainingNamespace`), or — if the original `RootNamespace` design was intentional — open a follow-up task to switch the implementation. The current state is consistent and the invariant test gives strong confidence; the *doc-vs-code* drift is the only thing to fix.

---

## §A vocabulary pass

The team-lead brief highlighted PR-E (`ActivityExtensionsEmitter`) as the largest XML-doc surface and the highest §A.4 vocabulary-drift risk. Spot-checks:

- **`ActivityExtensionsEmitter.cs:6-20`** doc comment uses *attribute* (line 7, 12), *registry-defined key* (line 9), *span* (line 78 — see *WriteClass*), *typed setter* (line 6). No occurrences of "tag", "label", "field", or "service version". `Activity.SetTag` is the .NET API name (the framework method this generator wraps), and is unavoidable — the docstring at line 9 makes the spec-to-API mapping explicit by using both terms in context.
- **`WriteClass`** (`ActivityExtensionsEmitter.cs:76-79`) emits the class summary: *"Typed setter extensions for OpenTelemetry semantic-convention attributes on a span. Each method invokes `global::System.Diagnostics.Activity.SetTag` with the registry-defined key and a strongly-typed value."* — uses "attribute", "span", "key", "value". Pass.
- **`AttributesEmitter`** class summary: *"Constants for semantic attribute names outlined by the OpenTelemetry specifications."* — matches contrib's wording verbatim. Pass.
- **`MetricsEmitter`** class summary: *"Constants for metric names and descriptors outlined by the OpenTelemetry specifications."* — uses "metric names", "descriptors". Pass on §A.4.4.
- **`MetersEmitter` / `EventsEmitter`** — quick grep found no "tag"/"label"/"service version" mis-use. Pass.

The `<key>` literal in attribute briefs (e.g. `HTTP request headers, <key> being the normalized…`) is **not** a vocabulary issue — it is a registry-source literal; both contrib and qyl rely on `#pragma warning disable CS1570` to suppress the CS1570 warning class. Contrib wraps it as `<c><key></c>` in the brief; qyl's seed registry uses bare `<key>`. This is a **seed-data divergence** that disappears in Phase 4 when the seed is replaced by Weaver output.

---

## §A.3 spec-compliance-matrix note

No upstream-matrix flag was raised during the audit. Tracks A–E ship attribute keys, metric names, event names, typed Meter factories, and typed Activity setters — all listed as supported in the .NET row of the matrix at the audit-time SHA. No row movement requires qyl work.

---

## Conclusion

The Tracks A–E emissions on `agent/gen-test-harness-eng` @ `32c9894f` are **cherry-pick-ready for upstream contrib** with five documented divergences that the strategist will surface in PR-A through PR-E descriptions. There is no defect on qyl that must be fixed before cherry-pick; all five findings are either (a) intentional architecture (Findings 1, 5), (b) defensible additive enhancements over contrib (Finding 2), (c) disclosed scope reductions waiting on Phase 4 (Finding 3), or (d) compile-time risks that the current consumer set does not exercise (Finding 4).

**Verdict: PASS — cherry-pick to contrib unblocked.**

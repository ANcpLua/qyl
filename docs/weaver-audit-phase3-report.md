# Phase 3 Compliance Audit Report — Tracks A-E

**Audit target.** `feat/semconv-srcgen` after the stability-intercept pass.
**Pinned semconv.** `open-telemetry/semantic-conventions` `v1.41.0` @ `e018fe6f91862f5ed63c082f87697cddac596784`.
**Pinned Weaver.** `otel/weaver:v0.23.0`.

**Verdict.** PASS for qyl source-of-truth implementation. The generator now
ships a cohesive Roslyn semantic-conventions surface rather than only const
strings: attributes, metrics, events, meter factories, and activity helpers all
consume the same Weaver-derived registry and the same stable/incubating marker
projection.

## Evidence Summary

| Gate | Result | Evidence |
| --- | --- | --- |
| Full-registry Weaver source | PASS | `scripts/generate.sh` runs `otel/weaver:v0.23.0 registry generate` over the pinned semconv `v1.41.0` model and produces the embedded `Resources/resolved-registry.json`. A post-run `git diff --exit-code -- .../resolved-registry.json` is clean. |
| Stable/incubating split | PASS | Each marker family has a stable and incubating variant. Stable projections emit stable rows plus deprecated migration symbols. Incubating projections emit all stability tiers and are strict supersets, matching Java/Python package behavior. |
| Snapshot tiers | PASS | Byte-identity snapshots are now named by signal and tier, for example `qyl.attributes.http.stable.expected.txt` and `qyl.attributes.http.incubating.expected.txt`. Old ambiguous snapshots (`qyl.http.expected.txt`, `qyl.disk.expected.txt`, etc.) were removed. |
| Tests | PASS | `dotnet test tests/qyl.opentelemetry.semconv.sourcegen.tests/qyl.opentelemetry.semconv.sourcegen.tests.csproj -c Release` passes with 60 tests after the intercept changes. |
| Full-surface TFM matrix | PASS | `eng/smoke/qyl.semconv.smoke.matrix/build-matrix.sh` builds the same consumer fixture for `net472`, `netstandard2.0`, `net6.0`, `net8.0`, `net9.0`, and `net10.0`. The fixture binds attributes, metrics, events, meter factories, and activity helpers across stable/incubating projections. |
| Runtime ownership | PASS | `MetersEmitter` writes extension methods on a consumer-provided `Meter`; no generated global Meter singletons. `ActivityExtensionsEmitter` writes wrappers over a consumer-provided `Activity`; no generated `ActivitySource` ownership. |
| State metric mapping | PASS | The emitted meter helper kind follows the registry `instrument` field directly. `updowncounter` remains `UpDownCounter<T>` and is never guessed as a Gauge. |
| Enum-member stability | PASS | Stable projections filter enum members by member stability, not only by parent attribute stability. Regression coverage locks `http.request.method` so `QUERY` is incubating-only, and `db.system.name` so development database identifiers stay out of the stable marker output. |
| Logger/Event target | PASS with upstream limitation | semconv v1.41.0 event rows do not expose a Logger/Event-vs-ActivityEvent discriminator. `EventsEmitter` emits event names and payload structs and documents the single extension point for routing if upstream adds such a field. |

## Replaced Findings

The earlier Phase 3 report predated the stability-intercept work and is no
longer accurate:

- It reported a seed registry. That is obsolete; the generator now embeds the
  full Weaver output for semconv v1.41.0.
- It reported a metadata-thin registry projection. That is obsolete; the
  embedded projection now preserves all 935 resolved groups across
  `attribute_group`, `span`, `event`, `metric`, and `entity`, and keeps
  signal-local requirement levels, notes, examples, event body metadata, and
  metric/event entity-association slots.
- It reported per-symbol `[Experimental]` emissions. That is obsolete; the
  generator relies on stable/incubating marker projection and emits no
  `[Experimental]` symbols.
- It reported a netstandard2.0 risk caused by `[Experimental]`. That risk is
  gone. The old-TFM event payload risk is handled by the generated
  `IsExternalInit` polyfill.
- It reported only PR-A style const-string portability. That is obsolete; the
  matrix now compiles the complete generated surface.

## Current Review Notes

1. **Partial-class architecture is intentional.** Existing contrib generated
   files are committed `public static class` files. This package is a Roslyn
   generator: the consumer declares a partial marker class and the analyzer
   completes it at compile time. Snapshot output is therefore qyl-shaped by
   design.
2. **Containing namespace is canonical.** The emitted namespace comes from the
   consumer marker class's containing namespace, not from the MSBuild
   `RootNamespace` property. This preserves the no-branching invariant while
   allowing one consumer to host multiple marker classes in different
   namespaces.
3. **Incubating is a superset.** This matches Java/Python and avoids a breaking
   move when a symbol is promoted from development to stable. Consumers should
   normally choose either the stable marker or the incubating marker for a given
   prefix. If both meter/activity projections are declared in the same
   namespace, shared extension methods can be called through the generated
   static class name to avoid extension-method ambiguity.
4. **Contrib branch is not updated by this qyl audit.** The local qyl
   implementation is ready for review after validation, but the downstream
   `opentelemetry-dotnet-contrib` branch still needs a deliberate
   re-cherry-pick and PR-body refresh before any upstream push.

## Conclusion

The qyl source-of-truth generator now satisfies the cohesive scope: full
registry input, data-driven emitters, stable/incubating separation across all
signals, consumer-owned runtime objects, no generated telemetry collection, and
full-surface validation across the supported consumer TFMs.

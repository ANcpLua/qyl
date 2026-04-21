# Qyl.Client — Deferred to Commit 7

`Qyl.Client.csproj` is scaffolded per the TypeSpec-native-emitters mandate (Commit 8) but is
**not currently wired into `qyl.slnx`**. It compiles standalone against the output of
`@typespec/http-client-csharp@1.0.0-alpha.*`, and the alpha emitter has several systemic bugs
that must be fixed (or worked around post-emit) before the package can ship.

## Blockers (against alpha .20260420.8)

| # | Bug | Count | Fix path |
| - | --- | ----- | -------- |
| 1 | `(int)<nullableEnum>?` emitted where `(int?)<nullableEnum>` is meant | 6 | **Fixed** — `core/specs/scripts/patch-emitted-csharp.mjs` rewrites. |
| 2 | `[Experimental(...)]` referenced as `System.ClientModel.Primitives.ExperimentalAttribute` (internal) | 3 | Post-emit sed: strip `[Experimental(...)]` lines. |
| 3 | `SCME0002` experimental diagnostic on `ClientSettings` | 1 | `<NoWarn>SCME0002</NoWarn>` in csproj. |
| 4 | `AggregationFunction`, `TimeBucket` referenced cross-namespace without `using` | 12 | Add `using Qyl.OTel.Metrics;` / `using Qyl.Common.Pagination;` via post-emit patch. |
| 5 | `Qyl.Common.Attribute` collides with `System.Attribute` | 2 | Rename the TypeSpec model or fully-qualify all usage sites via post-emit patch. |
| 6 | `Qyl.OTel.Traces.Trace` collides with `System.Diagnostics.Trace` | 7 | Rename the TypeSpec model or fully-qualify all usage sites via post-emit patch. |

## Why deferred

Commit 6's verification (`dotnet build qyl.slnx` exits 0) passed because `Qyl.Client` was not in
the solution yet. Adding it during Commit 8 surfaced 56 compile errors rooted in the alpha
emitter's namespace resolution. The plan's §7 (ASP.NET controller cutover via
`@typespec/http-server-csharp`) already owns the dedicated "alpha emitter stabilization" window;
fixing `http-client-csharp` belongs in the same session.

## Unblock recipe

1. Extend `core/specs/scripts/patch-emitted-csharp.mjs` with patches #2, #3, #4.
2. Rename TypeSpec models `Attribute` → `TelemetryAttribute` and (if needed) `Trace` → `TraceRecord`
   in `core/specs/models/*.tsp` to eliminate collisions #5 and #6.
3. Re-run `nuke Generate`, add `packages/Qyl.Client/Qyl.Client.csproj` back to `qyl.slnx`, and
   verify `dotnet build packages/Qyl.Client` is clean.
4. Delete this file.

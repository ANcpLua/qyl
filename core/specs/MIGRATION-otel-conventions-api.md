# Migration: ANcpLua.OtelConventions.Api

Replaces the locally-authored OTel signal files under `core/specs/otel/` with
the external TypeSpec library `ANcpLua.OtelConventions.Api` once published.

## Status

Blocked: package is not yet on npm/nuget (HTTP 404 confirmed 2026-05-12).
Indirection point is wired; no further qyl changes needed until publication.

## Indirection point

**`core/specs/otel/otel-conventions.tsp`** — today re-exports six local files.

When the npm package lands, replace the six `import "./…tsp"` lines with:
```typespec
import "@ancplua/otel-conventions-api";
```
All 10 consumer files listed below pick up the swap with no further changes.

## Package coordinates

**npm** — add to `core/specs/package.json`:
```json
"@ancplua/otel-conventions-api": "<version>"
```

**NuGet** (if the library ships as NuGet instead of npm):
```xml
<PackageReference Include="ANcpLua.OtelConventions.Api" Version="x.y.z" />
```

## Consumer files already wired to barrel

| File | Change made |
|---|---|
| `core/specs/main.tsp` | 6 otel imports → 1 barrel |
| `core/specs/api/routes.tsp` | 4 otel imports → 1 barrel |
| `core/specs/api/streaming.tsp` | 3 otel imports → 1 barrel |
| `core/specs/otel/storage.tsp` | `./enums.tsp` → barrel |
| `core/specs/models/system.tsp` | `../otel/resource.tsp` → barrel |
| `core/specs/models/genai.tsp` | 2 otel imports → 1 barrel |
| `core/specs/models/log.tsp` | `../otel/enums.tsp` → barrel |
| `core/specs/models/agent/agent-run.tsp` | `../../otel/enums.tsp` → barrel |
| `core/specs/models/agent/tool-call.tsp` | `../../otel/enums.tsp` → barrel |
| `core/specs/models/agent/workflow-execution.tsp` | `../../otel/enums.tsp` → barrel |

## Files to delete after swap

- `core/specs/otel/enums.tsp`
- `core/specs/otel/resource.tsp`
- `core/specs/otel/span.tsp`
- `core/specs/otel/logs.tsp`
- `core/specs/otel/metrics.tsp`
- `core/specs/otel/profiles.tsp`

`core/specs/otel/storage.tsp` stays — qyl-specific DuckDB storage models
(`SpanRecord`, `LogRecordStorage`, `ProfileRecord`) are not part of the OTel library.

## Byte-identity check

```sh
./eng/build.sh Generate
git diff -- packages/Qyl.Contracts/Generated/ \
            packages/qyl-client/src/generated/ \
            services/qyl.collector/Storage/promoted-columns.g.sql
```

Verified 2026-05-12: 87 artifact files, zero checksum diffs after introducing the barrel.

## Status — 2026-05-12 (swap staged, blocked on publish)

Branch `chore/swap-inlined-otel-for-otel-conventions-api` has performed the
swap:

- `core/specs/otel/otel-conventions.tsp` rewritten to a single
  `import "@o-ancpplua/otel-conventions-api/otel"`.
- Six inlined files deleted (~1,746 LOC removed):
  `enums.tsp`, `resource.tsp`, `span.tsp`, `logs.tsp`, `metrics.tsp`, `profiles.tsp`.
- `core/specs/otel/storage.tsp` kept (qyl-specific DuckDB models under `Qyl.Storage`).
- `core/specs/.npmrc` now declares the `@o-ancpplua` scope on
  `https://npm.pkg.github.com`.
- `core/specs/package.json` pins `@o-ancpplua/otel-conventions-api` at `0.1.0`.

Blocker: the npm package is not yet on GitHub Packages. The first
`npm install` / `npm install --package-lock-only` against this branch will
fail with HTTP 404 until the first release of `@o-ancpplua/otel-conventions-api`
lands. Once the package is published, CI will resolve the dependency and the
TypeSpec layer will pick up the npm-shipped signal models.

Known follow-up (filed in this branch's commit message): the npm package
exposes OTel models under the `ANcpLua.OtelConventions.OTel.*` and
`ANcpLua.OtelConventions.Common` namespaces, whereas qyl-side consumers
currently `using Qyl.OTel.*` and `using Qyl.Common`. Either the npm package's
namespace tree needs to be aligned to `Qyl.*` for qyl's consumption, or every
qyl-side `using` declaration that names the old inlined namespaces has to be
rewritten. That alignment is the next PR on top of this one and is tracked
under the migration so it is not bundled with the file-deletion delta.

## Status — 2026-05-12 (namespace alignment + common/otel-keys swap completed)

The follow-up alignment work has now been completed on the same branch:

- All `using Qyl.OTel.*` rewritten to `using ANcpLua.OtelConventions.OTel.*`
  across `core/specs/**/*.tsp` (signal namespaces sourced from the npm
  package).
- All `using Qyl.Common*` rewritten to `using ANcpLua.OtelConventions.Common*`.
- Bare `Qyl.OTel.Keys.<Domain>.<Ident>` references rewritten to
  `ANcpLua.OtelConventions.OTel.Keys.<Domain>.<Ident>`.
- `core/specs/common/{types,errors,pagination}.tsp` deleted; consumers import
  the npm subpaths `@o-ancpplua/otel-conventions-api/common/{types,errors,pagination}`.
- `core/specs/generated/otel-keys.gen.tsp` deleted; consumers import
  `@o-ancpplua/otel-conventions-api/generated/otel-keys`.
- `eng/semconv/templates/registry/typespec/` removed and
  `eng/semconv/scripts/run-weaver.sh` Pass 3 removed — the producer that
  generates these consts has been lifted into a separate repository
  (`ANcpLua/typespec-otel-semconv`).
- `core/specs/qyl-emit-config.tsp` now decorates the npm-package namespaces
  with `@@csharpNamespace(ANcpLua.OtelConventions.OTel.X, "Qyl.OTel.X")` and
  `@@csharpNamespace(ANcpLua.OtelConventions.Common.X, "Qyl.Common.X")` so the
  qyl-emitted C# downstream keeps using its historical namespaces. The C#
  post-emit patcher (`patch-emitted-csharp.mjs`) and qyl C# downstream code
  therefore need no changes.
- `core/specs/scripts/apply-otel-keys.mjs` reads its key map from the
  npm-installed `node_modules/@o-ancpplua/otel-conventions-api/generated/otel-keys.gen.tsp`
  and emits `ANcpLua.OtelConventions.OTel.Keys.<Domain>.<Ident>` references.
- `core/specs/otel/storage.tsp` deleted; the npm package's
  `ANcpLua.OtelConventions.Storage` namespace is the single source of truth
  for the flattened DuckDB-shaped storage models (`SpanRecord`,
  `LogRecordStorage`, `ProfileRecord`, `SessionSummary`, `TraceNode`,
  `GenAiSpanData`, and the profile child tables). `qyl-emit-duckdb.tsp` now
  decorates the npm-side types; `qyl-emit-config.tsp` maps
  `ANcpLua.OtelConventions.Storage` to C# `Qyl.Storage` so the qyl-emitted
  C# downstream (`packages/Qyl.Contracts/Generated`) keeps its historical
  namespace.
- `core/specs/api/routes.tsp` switches `using Qyl.Storage` →
  `using ANcpLua.OtelConventions.Storage`.

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

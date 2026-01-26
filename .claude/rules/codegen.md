---
paths:
  - "**/*.g.cs"
  - "core/openapi/openapi.yaml"
  - "src/qyl.dashboard/src/types/api.ts"
---

# Generated Code Rules

## Never edit generated files

All files matching `*.g.cs`, `openapi.yaml`, and `api.ts` are generated from TypeSpec.

**To make changes:**
1. Edit TypeSpec files in `core/specs/*.tsp`
2. Run `npm run compile` (generates openapi.yaml)
3. Run `nuke Generate` (generates C# and TypeScript)

**Generated artifacts:**
- `core/openapi/openapi.yaml` ← from TypeSpec
- `src/qyl.protocol/Primitives/Scalars.g.cs` ← from openapi.yaml
- `src/qyl.protocol/Enums/Enums.g.cs` ← from openapi.yaml
- `src/qyl.protocol/Models/*.g.cs` ← from openapi.yaml
- `src/qyl.collector/Storage/DuckDbSchema.g.cs` ← from openapi.yaml
- `src/qyl.dashboard/src/types/api.ts` ← from openapi.yaml

## CI enforcement

The `nuke Generate` target will fail in CI if generated files are stale. Use `--force-generate` locally.

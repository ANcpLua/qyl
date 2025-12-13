# Feature Proposal: TypeSpec/Kiota Code Generation Pipeline

> **Status:** Implemented
> **Date:** 2025-12-11
> **Effort:** ~1h (with Claude)
> **Backend Required:** No (build tooling)

---

## Overview

**Problem:** Multiple disconnected code generators (openapi-typescript, NSwag, datamodel-codegen, quicktype) with
inconsistent outputs and maintenance burden.

**Solution:** Single TypeSpec → OpenAPI → Kiota pipeline generating typed clients for all languages from one source.

**User Story:**

```
As a developer, I want to modify the API schema in one place so that all SDK clients (C#, Python, TypeScript) regenerate automatically.
```

---

## Codebase Context

### Location

```
/Users/ancplua/qyl/core/
├── specs/           # TypeSpec source
├── openapi/         # Generated OpenAPI
├── schemas/         # Generated JSON Schema
└── generated/       # Generated SDK clients
```

### Tech Stack

| Tech     | Version | Notes                                |
|----------|---------|--------------------------------------|
| TypeSpec | 1.7.0   | API-first schema definition          |
| Kiota    | Latest  | Microsoft's OpenAPI client generator |
| OpenAPI  | 3.1.0   | Intermediate representation          |
| NUKE     | 9.x     | Build orchestration                  |

---

## Files Changed

| File                          | Action   | What                                   |
|-------------------------------|----------|----------------------------------------|
| `eng/build/Build.TypeSpec.cs` | Created  | TypeSpec + Kiota build targets         |
| `eng/build/Build.OpenApi.cs`  | Deleted  | Replaced by Build.TypeSpec.cs          |
| `eng/build/Build.cs`          | Modified | IOpenApi → ITypeSpec                   |
| `core/specs/`                 | Created  | TypeSpec source (moved from dashboard) |
| `core/CLAUDE.md`              | Created  | Generator documentation                |

---

## Implementation

### Completed

1. **Build.TypeSpec.cs** - NUKE build component with targets:
    - `TypeSpecInstall` - npm dependencies
    - `TypeSpecCompile` - TypeSpec → OpenAPI + JSON Schema
    - `GenerateCSharp/Python/TypeScript` - Kiota generation
    - `GenerateAll` - All three languages
    - `SyncGeneratedTypes` - Copy to consuming projects
    - `TypeSpecInfo` - Configuration status
    - `TypeSpecClean` - Clean artifacts

2. **Source relocation** - Moved from `src/qyl.dashboard/src/specs/` to `core/specs/`

3. **Documentation** - `core/CLAUDE.md` with complete usage guide

---

## Future Improvements

### WIP: Clean up old location

The old TypeSpec source at `src/qyl.dashboard/src/specs/` still exists. Should be deleted once migration is verified
stable.

**Action:**

```bash
rm -rf src/qyl.dashboard/src/specs
```

### Enhancement: Add more languages

Kiota supports Go, Java, PHP, Ruby, Swift. Add targets as needed.

### Enhancement: CI integration

Add `nuke GenerateAll` to CI pipeline to verify schema changes don't break generation.

### Enhancement: SDK packaging

Create NuGet, PyPI, npm packages from generated clients:

- `Qyl.Core` (NuGet)
- `qyl-client` (PyPI)
- `@qyl/client` (npm)

### Enhancement: Schema validation

Add pre-commit hook or CI step to validate TypeSpec compiles before merge.

### Enhancement: OpenAPI 3.2 upgrade

Currently generates OpenAPI 3.1. When Kiota supports 3.2, upgrade for better SSE streaming support.

---

## Gotchas

- **TypeSpec version mismatch:** Some packages are 1.7.0, others 0.77.0. Use `--legacy-peer-deps` when installing.
- **Kiota discriminator warnings:** Safe to ignore; generation completes successfully.
- **SSE streaming warnings:** OpenAPI 3.1 doesn't fully support streaming schemas. Clients still work.

---

## Test

```bash
# Verify TypeSpec compiles
cd /Users/ancplua/qyl/core/specs && npm run compile

# Verify all targets work
nuke TypeSpecInfo
nuke GenerateAll
```

**Verify:**

- [x] TypeSpec compiles without errors
- [x] OpenAPI generated at core/openapi/openapi.yaml
- [x] C# client generated (183 files)
- [x] Python client generated (169 files)
- [x] TypeScript client generated (70 files)
- [x] Build.cs compiles

---

## Done When

- [x] TypeSpec source at core/specs/
- [x] Build.TypeSpec.cs implements all targets
- [x] All three languages generate successfully
- [x] CLAUDE.md documents the pipeline
- [x] CHANGELOG.md updated

---

## Out of Scope

- SDK package publishing (NuGet, PyPI, npm)
- CI integration
- Pre-commit hooks
- Additional languages beyond C#/Python/TypeScript

---

*Template v2.0 - Optimized for Claude-assisted development*

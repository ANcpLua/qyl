You are validating that all producer SDKs (.NET, Python, TypeScript) respect the
canonical schemas under core/specs (TypeSpec source of truth).

### SCOPE

Includes:

- core/specs/*.tsp (TypeSpec source - 54 files)
- core/openapi/openapi.yaml (Generated OpenAPI 3.1)
- core/schemas/ (Generated JSON Schema)
- core/generated/dotnet (Kiota C# client)
- core/generated/python (Kiota Python client)
- core/generated/typescript (Kiota TypeScript client)

### GOAL

Ensure all producers emit ONLY what is defined, and ALL required fields appear.

### REQUIRED ACTIONS

1. Schema is Source of Truth
  - Producers MUST NOT introduce fields outside schema.
  - Producers MUST emit every required field.

2. Cross-Language Consistency
  - .NET, Python, and TypeScript MUST produce the same logical telemetry shape.
  - Naming MUST be consistent: snake_case across all producers.

3. SemConv Alignment
  - Schema MUST match OTel semconv 1.38.
  - Deprecated fields MUST be removed.

4. Impact on Downstream Systems
  - Changes to TypeSpec MUST trigger regeneration via `nuke GenerateAll`:
    core/openapi/openapi.yaml
    core/generated/dotnet
    core/generated/python
    core/generated/typescript
    src/qyl.dashboard/src/types/generated (via SyncGeneratedTypes)
    src/qyl.collector/Generated (via SyncGeneratedTypes)

5. Dependency Rules
  - core/schema MUST NOT import:
    collectors/*
    instrumentation/*
    dashboard/*
    cli/*

### DEFINITION OF DONE

- Schema matches semconv 1.38.
- All producers conform to schema.
- All generated types aligned across languages.
- No missing or extra fields.
- Changes propagate safely to consumers.

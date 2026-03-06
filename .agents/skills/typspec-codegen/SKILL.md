---
name: typspec-codegen
description: Generate types from TypeSpec schemas. Use after schema changes or when adding new types to qyl.protocol.
---

# TypeSpec Codegen

All types originate in `core/specs/`. Never edit generated files.

## Flow

```text
core/specs/*.tsp → tsp compile → openapi.yaml → C# models (qyl.protocol)
                                               → DuckDB DDL
                                               → TypeScript types (dashboard)
                                               → JSON schemas

eng/semconv/generate-semconv.ts → qyl.protocol/Attributes/Generated/
core/specs/generated/semconv.g.tsp → generated from upstream semconv YAML
```

## Commands

```bash
# Run full codegen pipeline
nuke Generate

# Overwrite existing generated files
nuke Generate --force-generate

# Preview without writing
nuke Generate --dry-run-generate

# Validate generated code compiles
nuke Verify
```

## Never Edit These Files

- `*.g.cs` files — regenerated from TypeSpec
- `core/openapi/openapi.yaml` — generated from TypeSpec
- `qyl.protocol/Attributes/Generated/` — generated from semconv
- `core/specs/generated/semconv.g.tsp` — generated from upstream YAML

## Adding a New Type

1. Define it in `core/specs/*.tsp`
2. Run `nuke Generate --force-generate`
3. Run `nuke Verify`
4. Update consumers (collector, mcp, dashboard) to use the new type
5. Run `nuke` to confirm everything compiles and tests pass

## Adding a New DuckDB Table

1. Define the model in `core/specs/*.tsp`
2. Run `nuke Generate --force-generate` (generates DDL)
3. Run `nuke Verify` (validates DDL against in-memory DuckDB)
4. If modifying an existing table, check if a migration is needed (`SchemaMigrationGenerator.cs`)
5. Update collector storage layer to read/write the table
6. Run `nuke` to confirm

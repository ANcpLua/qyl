# core/ — TypeSpec API Contract Pipeline

> **Status:** Active (TypeSpec → OpenAPI → openapi-typescript)
> **OTel SemConv:** 1.38.0

## Architecture

```
core/specs/*.tsp → core/openapi/openapi.yaml → dashboard/src/types/api.ts
     │                      │
     └─ TypeSpec compile    └─ openapi-typescript
```

**Note:** Kiota client generation was removed (commit 08bf38e). Dashboard types now use openapi-typescript directly.

## Structure

```
core/
├── specs/              # TypeSpec source files
│   ├── main.tsp        # Entry point
│   ├── tspconfig.yaml  # Compiler config
│   ├── api/            # HTTP route definitions
│   ├── common/         # Shared types
│   ├── domains/        # Domain models (genai, etc.)
│   └── otel/           # OpenTelemetry models
├── openapi/            # Generated output
│   └── openapi.yaml    # OpenAPI 3.1 spec (188KB)
└── schemas/            # JSON Schema output
    └── qyl-telemetry   # Schema bundle
```

## Commands

```bash
# Compile TypeSpec → OpenAPI
nuke TypeSpecCompile

# Install TypeSpec dependencies
nuke TypeSpecInstall

# Show TypeSpec status
nuke TypeSpecInfo

# Delete all generated files
nuke TypeSpecClean
```

### Dashboard Type Generation

```bash
cd src/qyl.dashboard
npm run generate:ts   # openapi.yaml → src/types/api.ts
```

## TypeSpec 1.7 Patterns

| Use (1.7) | Reject (pre-1.6) |
|-----------|------------------|
| `@jsonSchema` on scalars | Scalars without schema |
| `@discriminator("field")` | `@discriminated(#{ envelope: "none" })` |
| `@encodedName("application/json", "snake_case")` | camelCase JSON |

## OTel 1.38 GenAI Compliance

**Required attributes:**
```
gen_ai.provider.name      gen_ai.request.model     gen_ai.response.model
gen_ai.operation.name     gen_ai.usage.input_tokens   gen_ai.usage.output_tokens
```

**Deprecated (reject):**
```
gen_ai.system → gen_ai.provider.name
gen_ai.usage.prompt_tokens → gen_ai.usage.input_tokens
gen_ai.usage.completion_tokens → gen_ai.usage.output_tokens
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `@typespec/compiler` | 1.7.0 | Core compiler |
| `@typespec/http` | 1.7.0 | HTTP decorators |
| `@typespec/openapi3` | 1.7.0 | OpenAPI emitter |
| `openapi-typescript` | 7.x | TypeScript types from OpenAPI |

## Troubleshooting

### TypeSpec compile errors
```bash
cd core/specs && npm run compile
```

### Missing npm modules
```bash
cd core/specs && npm install --legacy-peer-deps
```

### Regenerate from scratch
```bash
nuke TypeSpecClean && nuke TypeSpecCompile
```

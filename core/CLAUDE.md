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
├── specs/                    # TypeSpec source files (50+ models)
│   ├── main.tsp              # Entry point with all imports
│   ├── tspconfig.yaml        # Compiler config
│   ├── api/                  # HTTP route definitions
│   │   ├── routes.tsp        # REST endpoints
│   │   └── streaming.tsp     # SSE streaming endpoints
│   ├── common/               # Shared infrastructure
│   │   ├── types.tsp         # Base types, scalars
│   │   ├── errors.tsp        # Error models
│   │   └── pagination.tsp    # Pagination utilities
│   ├── otel/                 # OpenTelemetry core models
│   │   ├── enums.tsp         # SpanKind, StatusCode
│   │   ├── resource.tsp      # Resource attributes
│   │   ├── span.tsp          # Span model
│   │   ├── logs.tsp          # Log records
│   │   └── metrics.tsp       # Metrics model
│   └── domains/              # OTel semantic convention domains
│       ├── ai/               # gen_ai.*, code.*, cli.*
│       ├── security/         # network.*, dns.*, tls.*
│       ├── transport/        # http.*, rpc.*, messaging.*, signalr.*
│       ├── infra/            # host.*, container.*, k8s.*, cloud.*
│       ├── runtime/          # process.*, thread.*, dotnet.*, aspnetcore.*
│       ├── data/             # db.*, file.*, elasticsearch.*, vcs.*
│       ├── observe/          # session.*, browser.*, feature_flag.*
│       ├── ops/              # cicd.*, deployment.*
│       └── identity/         # user.*, geo.*
├── openapi/                  # Generated output
│   └── openapi.yaml          # OpenAPI 3.1 spec
└── schemas/                  # JSON Schema output
    └── qyl-telemetry         # Schema bundle
```

## Domain Coverage

| Domain | Files | OTel Namespaces |
|--------|-------|-----------------|
| AI | 3 | `gen_ai.*`, `code.*`, `cli.*` |
| Security | 4 | `network.*`, `dns.*`, `tls.*`, `security_rule.*` |
| Transport | 7 | `http.*`, `rpc.*`, `messaging.*`, `url.*`, `signalr.*`, `kestrel.*`, `user_agent.*` |
| Infrastructure | 7 | `host.*`, `container.*`, `k8s.*`, `cloud.*`, `faas.*`, `os.*`, `webengine.*` |
| Runtime | 5 | `process.*`, `system.*`, `thread.*`, `dotnet.*`, `aspnetcore.*` |
| Data | 5 | `db.*`, `file.*`, `elasticsearch.*`, `vcs.*`, `artifact.*` |
| Observe | 8 | `session.*`, `browser.*`, `feature_flag.*`, `exception.*`, `otel.*`, `log.*`, `error.*`, `test.*` |
| Ops | 2 | `cicd.*`, `deployment.*` |
| Identity | 2 | `user.*`, `geo.*` |

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

# Direct compilation (for debugging)
cd core/specs && npm run compile
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
| `@typespec/versioning` | 1.7.0 | API versioning |
| `@typespec/sse` | 1.7.0 | Server-Sent Events |
| `@typespec/events` | 1.7.0 | Event definitions |
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

### Stream warnings (expected)
TypeSpec emits warnings about `streams-not-supported` for SSE endpoints - these are informational only (OpenAPI 3.1 limitation, full support in 3.2).

# QYL Code Generation Pipeline

> **Status:** Working
> **Last verified:** 2025-12-11
> **TypeSpec Version:** 1.7.0
> **OTel SemConv:** 1.38
> **.NET Target:** 10.0

---

## TypeSpec 1.7 Modernization Checklist

Before modifying any `.tsp` file, verify you're using 1.7 patterns:

| ✅ Use (1.7) | ❌ Reject (pre-1.6) |
|-------------|---------------------|
| `@jsonSchema` on scalars | Scalars without explicit schema |
| `@discriminator("field")` | `@discriminated(#{ envelope: "none" })` |
| `@example(#{...})` on models | Missing examples |
| `@added(Version)` / `@removed(Version)` | Manual deprecation comments |
| `@encodedName("application/json", "snake_case")` | camelCase JSON fields |

---

## OTel 1.38 GenAI Compliance

### Required Attributes (MUST emit)

```
gen_ai.provider.name      gen_ai.request.model     gen_ai.response.model
gen_ai.operation.name     gen_ai.usage.input_tokens   gen_ai.usage.output_tokens
```

### Deprecated (REJECT)

```
gen_ai.system → gen_ai.provider.name (removed 1.37)
gen_ai.usage.prompt_tokens → gen_ai.usage.input_tokens (removed 1.27)
gen_ai.usage.completion_tokens → gen_ai.usage.output_tokens (removed 1.27)
```

---

## Quick Start

```bash
# Generate all SDK clients
nuke GenerateAll

# Or step by step:
nuke TypeSpecCompile   # TypeSpec → OpenAPI
nuke GenerateCSharp    # OpenAPI → C# client
nuke GeneratePython    # OpenAPI → Python client
nuke GenerateTypeScript # OpenAPI → TypeScript client
```

## Architecture

```
core/
├── specs/                    # TypeSpec source (single source of truth)
│   ├── main.tsp              # Entry point
│   ├── tspconfig.yaml        # Compiler config
│   ├── package.json          # TypeSpec dependencies
│   ├── api/                   # API route definitions
│   ├── common/                # Shared types (pagination, errors)
│   ├── domains/               # Domain models (genai, http, k8s, etc.)
│   └── otel/                  # OpenTelemetry models (spans, logs, metrics)
├── openapi/                  # Generated OpenAPI 3.1 spec
│   └── openapi.yaml          # ~188KB, regenerated on compile
├── schemas/                  # Generated JSON Schema
│   └── qyl-telemetry         # Schema bundle
└── generated/                # Generated SDK clients
    ├── dotnet/               # C# client (Qyl.Core namespace)
    ├── python/               # Python client
    └── typescript/           # TypeScript client
```

## Pipeline

```
TypeSpec → OpenAPI 3.1 → Kiota → C# / Python / TypeScript
```

**Step 1:** TypeSpec compiles `.tsp` files to OpenAPI 3.1 + JSON Schema
**Step 2:** Kiota generates typed HTTP clients from OpenAPI for each language

## Commands

### Generate Everything

```bash
nuke GenerateAll
```

This runs: `TypeSpecInstall` → `TypeSpecCompile` → `GenerateCSharp` + `GeneratePython` + `GenerateTypeScript`

### Individual Targets

| Command | What it does |
|---------|--------------|
| `nuke TypeSpecInstall` | Install TypeSpec npm dependencies |
| `nuke TypeSpecCompile` | Compile TypeSpec → OpenAPI + JSON Schema |
| `nuke GenerateCSharp` | Generate C# client via Kiota |
| `nuke GeneratePython` | Generate Python client via Kiota |
| `nuke GenerateTypeScript` | Generate TypeScript client via Kiota |
| `nuke SyncGeneratedTypes` | Copy to dashboard/collector |
| `nuke TypeSpecInfo` | Show configuration status |
| `nuke TypeSpecClean` | Delete all generated artifacts |

### Manual (without nuke)

```bash
# TypeSpec compile
cd core/specs && npm run compile

# Kiota generate (example: C#)
kiota generate \
  --language csharp \
  --openapi core/openapi/openapi.yaml \
  --output core/generated/dotnet \
  --namespace-name Qyl.Core \
  --class-name QylClient \
  --clean-output
```

## Adding a New Language

Kiota supports: `csharp`, `python`, `typescript`, `java`, `go`, `php`, `ruby`, `swift`

**To add Go:**

1. Add path in `Build.TypeSpec.cs`:
```csharp
AbsolutePath GeneratedGo => RootDirectory / "core" / "generated" / "go";
```

2. Add target in `Build.TypeSpec.cs`:
```csharp
Target GenerateGo => d => d
    .Description("Generate Go client (Kiota)")
    .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
    .Produces(GeneratedGo / "**/*.go")
    .Executes(() =>
    {
        GeneratedGo.CreateOrCleanDirectory();
        ProcessTasks.StartProcess(
            "kiota",
            $"generate --language go --openapi \"{OpenApiOutput}\" " +
            $"--output \"{GeneratedGo}\" --class-name QylClient --clean-output",
            RootDirectory
        ).AssertZeroExitCode();
    });
```

3. Add to `GenerateAll` dependencies:
```csharp
.DependsOn<ITypeSpec>(x => x.GenerateGo)
```

## Modifying the Schema

1. Edit `.tsp` files in `core/specs/`
2. Run `nuke TypeSpecCompile` to validate
3. If valid, run `nuke GenerateAll` to regenerate clients
4. Run `nuke SyncGeneratedTypes` to copy to consuming projects

### TypeSpec File Structure

| Directory | Purpose |
|-----------|---------|
| `api/` | HTTP routes (`@route`, `@get`, `@post`) |
| `common/` | Shared types (`PagedResult`, `ApiError`) |
| `domains/` | Domain models organized by topic |
| `otel/` | OpenTelemetry signal models |

### Example: Add New Endpoint

```typespec
// In api/routes.tsp
@route("/v1/alerts")
@tag("Alerts")
interface AlertsApi {
  @get list(): PagedResult<Alert>;
  @post create(@body alert: CreateAlertRequest): Alert;
}
```

## Dependencies

### TypeSpec (core/specs/package.json)

| Package | Version | Purpose |
|---------|---------|---------|
| `@typespec/compiler` | 1.7.0 | Core compiler |
| `@typespec/http` | 1.7.0 | HTTP decorators |
| `@typespec/rest` | 0.77.0 | REST patterns |
| `@typespec/openapi3` | 1.7.0 | OpenAPI emitter |
| `@typespec/json-schema` | 1.7.0 | JSON Schema emitter |

### Kiota (dotnet tool)

```bash
dotnet tool install -g Microsoft.OpenApi.Kiota
```

## What Kiota Generates

Each generated client provides:

- **Typed request/response models** - Full type safety for API calls
- **Fluent API** - `client.V1.Traces.GetAsync()` style navigation
- **Authentication support** - Bearer token, API key, etc.
- **Serialization** - JSON serialization/deserialization
- **Error handling** - Typed error responses

### Runtime Dependencies

| Language | Required Packages |
|----------|-------------------|
| C# | `Microsoft.Kiota.Abstractions`, `Microsoft.Kiota.Http.HttpClientLibrary`, `Microsoft.Kiota.Serialization.Json` |
| Python | `microsoft-kiota-abstractions`, `microsoft-kiota-http`, `microsoft-kiota-serialization-json` |
| TypeScript | `@microsoft/kiota-abstractions`, `@microsoft/kiota-http-fetchlibrary`, `@microsoft/kiota-serialization-json` |

## Known Warnings

**OpenAPI 3.2 streaming:** TypeSpec emits warnings about SSE streams needing OpenAPI 3.2. These are safe to ignore; clients still work.

**Discriminator issues:** Kiota logs errors about discriminator schemas. Generation completes successfully.

## Output Destinations

| Generated | Sync Destination | Purpose |
|-----------|------------------|---------|
| `core/generated/typescript/` | `src/qyl.dashboard/src/types/generated/` | Dashboard API client |
| `core/generated/dotnet/` | `src/qyl.collector/Generated/` | Collector contracts |

## Troubleshooting

### "Cannot find module @typespec/..."

```bash
cd core/specs && npm install --legacy-peer-deps
```

### "kiota: command not found"

```bash
dotnet tool install -g Microsoft.OpenApi.Kiota
```

### TypeSpec compile errors

```bash
cd core/specs && npm run compile
```

Errors show file:line:column. Fix the `.tsp` file and recompile.

### Regenerate from scratch

```bash
nuke TypeSpecClean && nuke GenerateAll
```

---

## Priority Schema Fixes

Execute these before any other changes:

| Priority | Issue | File | Action |
|----------|-------|------|--------|
| **P0** | `gen_ai.system` wrong version | `genai.tsp:55-57` | Change `@removed(v1_38)` → `@removed(v1_37)` |
| **P0** | Missing exported models | `genai.tsp` | Add `GenAiRequestAttributes`, `GenAiResponseAttributes`, `GenAiUsageAttributes`, `GenAiMessage`, `GenAiCostEstimate` |
| **P0** | Missing `InstrumentKind` enum | `otel/enums.tsp` | Add Counter, UpDownCounter, Histogram, Gauge, Observable* |
| **P1** | Scalars missing `@jsonSchema` | `common/types.tsp` | Add to all 16 scalar types |
| **P1** | K8s metric renames | `domains/infra/k8s.tsp` | Add `k8s.deployment.pod.available`, etc. |

---

## Missing Models (schema.yaml declares, TypeSpec lacks)

### GenAi Domain

```typespec
model GenAiRequestAttributes {
  @encodedName("application/json", "gen_ai.request.model")
  model: string;
  @encodedName("application/json", "gen_ai.request.temperature")
  temperature?: float64;
  @encodedName("application/json", "gen_ai.request.max_tokens")
  maxTokens?: int64;
}

model GenAiResponseAttributes {
  @encodedName("application/json", "gen_ai.response.model")
  model?: string;
  @encodedName("application/json", "gen_ai.response.id")
  id?: string;
  @encodedName("application/json", "gen_ai.response.finish_reasons")
  finishReasons?: GenAiFinishReason[];
}

model GenAiUsageAttributes {
  @encodedName("application/json", "gen_ai.usage.input_tokens")
  inputTokens: TokenCount;
  @encodedName("application/json", "gen_ai.usage.output_tokens")
  outputTokens: TokenCount;
  @encodedName("application/json", "gen_ai.usage.total_tokens")
  totalTokens: TokenCount;
}

model GenAiMessage {
  role: GenAiMessageRole;
  content?: string;
  contentType?: GenAiContentType;
  toolCalls?: GenAiToolCallEvent[];
}

model GenAiCostEstimate {
  @encodedName("application/json", "input_cost_usd")
  inputCostUsd: float64;
  @encodedName("application/json", "output_cost_usd")
  outputCostUsd: float64;
  @encodedName("application/json", "total_cost_usd")
  totalCostUsd: float64;
}
```

### OTel Domain

```typespec
enum InstrumentKind {
  counter: "Counter",
  upDownCounter: "UpDownCounter",
  histogram: "Histogram",
  gauge: "Gauge",
  observableCounter: "ObservableCounter",
  observableGauge: "ObservableGauge",
  observableUpDownCounter: "ObservableUpDownCounter",
}
```

---

## Validation Before Commit

```bash
# 1. TypeSpec compiles
nuke TypeSpecCompile

# 2. All clients generate
nuke GenerateAll

# 3. Sync to consumers
nuke SyncGeneratedTypes

# 4. Verify no schema drift
diff core/openapi/openapi.yaml <previous-version>
```

---

## Cross-Reference

- **Full Architecture**: See `/.claude/TYPESPEC_SCHEMA_ARCHITECT.md`
- **Spec Compliance**: See `/spec-compliance-matrix/MASTER.md`
- **Schema Definition**: See `/spec-compliance-matrix/schema.yaml`
- **UML Reference**: See `/spec-compliance-matrix/UML.md`

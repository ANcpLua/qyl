# qyl API Specification

> Single source of truth for all qyl API types across TypeScript, Python, and C#

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        openapi.yaml (Source of Truth)                       │
│                                                                             │
│  - Defines all API endpoints                                                │
│  - Defines all request/response schemas                                     │
│  - OpenTelemetry Semantic Conventions v1.38 compliant                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
        ▼                           ▼                           ▼
┌───────────────┐         ┌───────────────┐         ┌───────────────┐
│  TypeScript   │         │    Python     │         │      C#       │
│ openapi-ts    │         │ openapi-gen   │         │   NSwag/      │
│               │         │               │         │   Manual      │
└───────────────┘         └───────────────┘         └───────────────┘
        │                           │                           │
        ▼                           ▼                           ▼
┌───────────────┐         ┌───────────────┐         ┌───────────────┐
│ generated/    │         │ generated/    │         │ generated/    │
│ typescript/   │         │ python/       │         │ csharp/       │
│   api.ts      │         │ qyl_client/   │         │ Contracts.cs  │
│   index.ts    │         │   models.py   │         │ Mappers.cs    │
└───────────────┘         └───────────────┘         └───────────────┘
        │                           │                           │
        ▼                           ▼                           ▼
┌───────────────┐         ┌───────────────┐         ┌───────────────┐
│ qyl.dashboard │         │ qyl-python    │         │ qyl.collector │
│ (React 19)    │         │ SDK           │         │ (ASP.NET)     │
└───────────────┘         └───────────────┘         └───────────────┘
```

## Industry Standard

This approach mirrors how enterprise observability tools maintain type alignment:

| Tool          | Schema Source     | Generator                    |
|---------------|-------------------|------------------------------|
| OpenTelemetry | .proto            | protoc (all languages)       |
| Jaeger        | .proto + OpenAPI  | protobuf-ts, openapi-gen     |
| Grafana/Tempo | .proto            | buf.build                    |
| Elastic       | OpenAPI           | openapi-generator            |
| Datadog       | Smithy → OpenAPI  | openapi-generator            |
| Azure Monitor | TypeSpec          | autorest                     |
| **qyl**       | **OpenAPI**       | **openapi-typescript + gen** |

## Quick Start

```bash
# Install dependencies
npm install

# Generate all clients
npm run generate

# Generate TypeScript only
npm run generate:ts

# Generate Python only  
npm run generate:python

# Validate spec
npm run validate

# Preview docs
npm run docs
```

## Generated Files

### TypeScript (`generated/typescript/`)

```typescript
import type { Span, Session, GenAISpanData } from '@qyl/api-spec';

// Full type safety from OpenAPI
const span: Span = await fetch('/api/v1/traces/abc123').then(r => r.json());

// Type guards included
if (isGenAISpan(span)) {
  console.log(span.genai.inputTokens);
}
```

### Python (`generated/python/`)

```python
from qyl_client.models import Span, Session, GenAISpanData

# Pydantic models with validation
span = Span.model_validate(response.json())
print(span.genai.input_tokens if span.genai else "N/A")
```

### C# (`generated/csharp/`)

```csharp
using qyl.Contracts;
using qyl.Mapping;

// DTOs match OpenAPI exactly
SpanDto dto = SpanMapper.ToDto(spanRecord, "my-service");

// Return from controller
return Ok(dto); // JSON matches TypeScript expectations
```

## Integration with qyl.dashboard

### 1. Copy generated types

```bash
# From qyl-api-spec directory
cp generated/typescript/api.ts ../qyl.dashboard/src/types/
cp generated/typescript/index.ts ../qyl.dashboard/src/types/
```

### 2. Update imports

```typescript
// Before (hand-written types)
import type { Span, Session } from './types/telemetry';

// After (generated types)
import type { Span, Session, isGenAISpan } from './types';
```

### 3. Delete old hand-written types

```bash
rm src/qyl.dashboard/src/types/telemetry.ts  # Replaced by generated types
```

## Integration with qyl.collector

### 1. Copy contracts

```bash
cp generated/csharp/Contracts.cs src/qyl.collector/Contracts/
cp generated/csharp/Mappers.cs src/qyl.collector/Mapping/
```

### 2. Update endpoints to return DTOs

```csharp
// Before: returning internal models
[HttpGet("sessions/{sessionId}")]
public IActionResult GetSession(string sessionId)
{
    var summary = _aggregator.GetSession(sessionId);
    return Ok(summary); // ❌ Type mismatch with frontend
}

// After: returning DTOs
[HttpGet("sessions/{sessionId}")]
public IActionResult GetSession(string sessionId)
{
    var summary = _aggregator.GetSession(sessionId);
    return Ok(SessionMapper.ToDto(summary)); // ✅ Matches OpenAPI spec
}
```

## CI Integration (NUKE Build)

```csharp
Target GenerateApiClients => _ => _
    .DependsOn(Restore)
    .Executes(() =>
    {
        // Generate TypeScript
        NpmTasks.NpmRun(s => s
            .SetWorkingDirectory(RootDirectory / "api-spec")
            .SetArguments("generate:ts"));
        
        // Copy to dashboard
        CopyFile(
            RootDirectory / "api-spec/generated/typescript/api.ts",
            RootDirectory / "src/qyl.dashboard/src/types/api.ts");
        CopyFile(
            RootDirectory / "api-spec/generated/typescript/index.ts",
            RootDirectory / "src/qyl.dashboard/src/types/index.ts");
    });
```

## Workflow

1. **Schema Change**: Edit `openapi.yaml`
2. **Validate**: `npm run validate`
3. **Generate**: `npm run generate`
4. **Copy**: Run NUKE target or manually copy
5. **Adapt**: Update mappers if internal models changed
6. **Test**: Types will catch any mismatches at compile time

## Key Benefits

| Benefit | Description |
|---------|-------------|
| **Zero Drift** | Types generated from single source |
| **Multi-Language** | Same spec → TS, Python, C# |
| **Compile-Time Safety** | Type errors caught during build |
| **Documentation** | OpenAPI spec doubles as API docs |
| **Industry Standard** | Same approach as Elastic, Datadog, etc. |
| **OTel Compliant** | GenAI conventions v1.38 |

## Files

```
qyl-api-spec/
├── openapi.yaml              # Source of truth
├── package.json              # Generator scripts
├── README.md                 # This file
└── generated/
    ├── typescript/
    │   ├── api.ts            # Generated (don't edit)
    │   └── index.ts          # Re-exports + helpers
    ├── python/
    │   └── qyl_client/       # Generated Pydantic models
    └── csharp/
        ├── Contracts.cs      # DTOs matching OpenAPI
        └── Mappers.cs        # SpanRecord → SpanDto
```

## Extending the Schema

When adding new endpoints or types:

1. Add to `openapi.yaml`
2. Run `npm run validate` to check syntax
3. Run `npm run generate` to regenerate clients
4. Update C# mappers if needed
5. Copy generated files to projects

The type system will catch any mismatches between backend and frontend at compile time.

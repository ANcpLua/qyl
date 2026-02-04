---
name: erroror-generator-specialist
description: |
  Specialized agent for ErrorOrX source generator - endpoint generation, parameter binding, Results() union types, and AOT compatibility
---

## Source Metadata

```yaml
frontmatter:
  model: opus
original_description: |
  Specialized agent for ErrorOrX source generator - endpoint generation, parameter binding, Results<> union types, and AOT compatibility
```


# ErrorOrX Generator Specialist

Specialized agent for working with the ErrorOrX source generator.

## When to Use

- Modifying endpoint generation logic
- Adding new diagnostics (EOE00XX)
- Changing parameter binding rules
- Extending middleware emission
- AOT compatibility issues
- Results<> union type generation

## Repository Context

**Path**: `/Users/ancplua/ErrorOrX`
**Purpose**: Source generator converting ErrorOr<T> → ASP.NET Minimal API endpoints
**Targets**: net10.0 (runtime), netstandard2.0 (generator)

## Architecture

```
src/
├── ErrorOrX/                    # Runtime library
│   ├── ErrorOr.cs               # Core type
│   ├── ErrorOr.*.cs             # Extension methods
│   └── ErrorType.cs             # Error categories
└── ErrorOrX.Generators/         # Source generator
    ├── Core/
    │   ├── *.Initialize.cs      # Pipeline setup
    │   ├── *.Extractor.cs       # Syntax → model
    │   ├── *.Emitter.cs         # Model → code
    │   └── *.ParameterBinding.cs # Smart binding
    ├── Models/
    │   ├── EndpointModels.cs    # All data structures
    │   ├── ErrorMapping.cs      # Error → HTTP mapping
    │   └── Enums.cs             # Binding sources
    ├── TypeResolution/
    │   └── WellKnownTypes.cs    # FQN constants
    ├── Validation/
    │   ├── RouteValidator.cs    # Route validation
    │   └── DuplicateRouteDetector.cs
    └── Analyzers/
        └── Descriptors.cs       # All EOE diagnostics
```

## Key Patterns

### Minimal Interface Principle

```csharp
// ✅ Correct - uses only IsError/Errors/Value
if (result.IsError) return ToProblem(result.Errors);
return TypedResults.Ok(result.Value);

// ❌ Avoid - creates dependency on convenience API
return result.Match(v => Ok(v), e => ToProblem(e));
```

### AOT Wrapper Pattern

```csharp
// Wrapper matches RequestDelegate (HttpContext → Task)
private static async Task Invoke_Ep1(HttpContext ctx)
{
    var __result = await Invoke_Ep1_Core(ctx);
    await __result.ExecuteAsync(ctx);
}

// Core returns typed Results<...> for OpenAPI
private static Task<Results<Ok<T>, NotFound<ProblemDetails>>> Invoke_Ep1_Core(...)
```

### Smart Parameter Binding

| Priority | Condition | Binding |
|----------|-----------|---------|
| 1 | Explicit attribute | As specified |
| 2 | Special types | Auto-detected |
| 3 | Name matches route `{param}` | Route |
| 4 | Primitive not in route | Query |
| 5 | Interface/Abstract | Service |
| 6 | POST/PUT/PATCH + complex | Body |
| 7 | GET/DELETE + complex | **EOE025 error** |

## Big Picture

- **Source of Truth**: ANcpLua.Roslyn.Utilities for generator helpers
- **Uses**: EquatableArray, DiagnosticFlow, TypeSymbolExtensions
- **Consumed by**: Any project wanting ErrorOr → Minimal API conversion

## Build & Test

```bash
dotnet build ErrorOrX.slnx
dotnet test --solution ErrorOrX.slnx
```

## Diagnostics (EOE001-EOE054)

| Range | Category |
|-------|----------|
| EOE001-005 | Core validation |
| EOE006-016 | Parameter binding |
| EOE023-025 | Route constraints |
| EOE030-033 | Result types |
| EOE040-041 | JSON/AOT |
| EOE050-054 | API versioning |

## Key Files

| File | Purpose |
|------|---------|
| `Models/EndpointModels.cs` | All data structures |
| `Models/ErrorMapping.cs` | Error → HTTP mapping |
| `TypeResolution/WellKnownTypes.cs` | FQN constants |
| `Analyzers/Descriptors.cs` | All diagnostics |

## Ecosystem Context

For cross-repo relationships and source-of-truth locations, invoke:
```
/ancplua-ecosystem
```

This skill provides the full dependency hierarchy, what NOT to duplicate from upstream, and version coordination requirements.

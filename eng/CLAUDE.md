# eng - Build Infrastructure

@import "../CLAUDE.md"

Build tooling and code generation. NOT runtime code.

## Isolation Rule

`eng/` projects are build-time only. Runtime projects in `src/` MUST NOT reference anything under `eng/`.

## Structure

```
eng/
├── build/
│   ├── Build.cs              # Entry point, composite targets
│   ├── Build.Frontend.cs     # IFrontend implementation
│   ├── Build.TypeSpec.cs     # ITypeSpec implementation
│   ├── MtpExtensions.cs      # Microsoft Testing Platform fluent builder
│   ├── Components/           # Nuke interface-based components
│   │   ├── IHasSolution.cs       # Path constants (src, tests, artifacts)
│   │   ├── ICompile.cs           # Solution build targets
│   │   ├── ITest.cs              # MTP test runner
│   │   ├── ICoverage.cs          # Coverage collection + reports
│   │   ├── IGenerate.cs          # OpenAPI → C#/DuckDB generation
│   │   ├── IDockerBuild.cs       # Docker image builds
│   │   ├── IDockerCompose.cs     # Docker Compose orchestration
│   │   ├── IVersionize.cs        # Changelog/release automation
│   │   ├── ITestContainers.cs    # CI testcontainer setup
│   │   ├── IClaudeContext.cs     # CLAUDE.md compilation
│   │   └── CoveragePatterns.cs   # Coverage analysis utilities
│   ├── Context/
│   │   └── BuildPaths.cs         # Centralized path constants
│   └── Domain/CodeGen/
│       ├── OpenApiSchema.cs      # OpenAPI YAML parser
│       ├── GeneratedFileHeaders.cs # Standard file headers
│       ├── GenerationGuard.cs    # Write control (CI vs local)
│       └── Generators/           # IGenerator implementations
│           ├── OpenApiCSharpGenerator.cs  # → protocol/*.g.cs
│           └── OpenApiDuckDbGenerator.cs  # → collector/Storage/*.g.cs
├── MSBuild/                  # Shared .props/.targets
└── build.{sh,cmd}            # Entry scripts
```

## Nuke Components

Build logic is split into interface-based components that compose via `TryDependsOn<T>()`.

| Component | Responsibility | Key Targets |
|-----------|----------------|-------------|
| `IHasSolution` | Path constants, solution reference | (base interface) |
| `ICompile` | Solution build, restore, clean | `Compile`, `Clean` |
| `ITest` | MTP test execution | `Test` |
| `ICoverage` | Coverage collection + HTML reports | `Coverage` |
| `IGenerate` | OpenAPI → C#/DuckDB generation | `Generate` |
| `IFrontend` | Dashboard npm build/dev | `FrontendBuild`, `FrontendDev` |
| `ITypeSpec` | TypeSpec → OpenAPI compilation | `TypeSpecCompile` |
| `IDockerBuild` | Docker image builds | `DockerImageBuild` |
| `IDockerCompose` | Docker Compose up/down/logs | `ComposeUp`, `ComposeDown` |
| `IVersionize` | Changelog generation | `Changelog`, `Release` |
| `ITestContainers` | CI container setup | `SetupTestcontainers` |
| `IClaudeContext` | CLAUDE.md dependency resolution | `GenerateContext` |

### MTP Integration

`MtpExtensions.cs` provides a fluent builder for Microsoft Testing Platform arguments:

```csharp
// Filter by namespace pattern
mtp.FilterNamespace("*.Integration.*")

// Coverage output
mtp.CoverageCobertura(outputPath)

// TRX reporting
mtp.ReportTrx("results.trx")
```

MTP exit code 8 (zero tests matched) is ignored by default.

## Code Generator Architecture (God Schema)

TypeSpec is the single source of truth. All types flow from `core/specs/main.tsp`:

```
core/specs/main.tsp (SSOT)
     │
     └─► ITypeSpec.TypeSpecCompile
              │
              └─► core/openapi/openapi.yaml
                       │
                       ├─► openapi-typescript  → dashboard/src/types/api.ts
                       │
                       └─► IGenerate.Generate
                                │
                                ├─► OpenApiCSharpGenerator  → protocol/*.g.cs
                                └─► OpenApiDuckDbGenerator  → collector/Storage/*.g.cs
```

OpenAPI generators in `eng/build/Domain/CodeGen/Generators/` read from `openapi.yaml` and use x-extensions:

| Extension | Purpose | Example |
|-----------|---------|---------|
| `x-csharp-type` | C# type override | `"long"` for tokens |
| `x-duckdb-type` | DuckDB column type | `"BIGINT"` |
| `x-primitive` | Marks strongly-typed wrapper | `true` |

Generators implement `IOpenApiGenerator`:

```csharp
interface IOpenApiGenerator
{
    string Name { get; }
    IEnumerable<(string RelativePath, string Content)> Generate(
        OpenApiDocument document, BuildPaths paths, string rootNamespace);
}
```

`GenerationGuard` controls write behavior:
- **CI**: Verify-only (fails if generated files don't match)
- **Local**: Write files to disk

## MSBuild Infrastructure

`eng/MSBuild/Shared.props` provides OTel configuration:
- `QylOTelSemConvVersion` = 1.39.0
- Global usings for `System.Diagnostics` and `System.Diagnostics.Metrics`

`eng/build/Directory.Build.props` relaxes analyzer rules for build automation code (RS0030 disabled, AOT not required, XML docs optional).

## SDK Configuration

qyl uses ANcpLua.NET.Sdk 1.6.3 from nuget.org:

| File | Purpose |
|------|---------|
| `global.json` | SDK version pinning |
| `nuget.config` | Package sources (nuget.org only) |
| `Directory.Packages.props` | Central Package Management (CPM) |

### CPM Analyzer Packages

The SDK requires these analyzer entries in CPM:

```xml
<PackageVersion Include="ANcpLua.Analyzers" Version="1.5.3" />
<PackageVersion Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4" />
<PackageVersion Include="JonSkeet.RoslynAnalyzers" Version="1.0.0-beta.6" />
<PackageVersion Include="Microsoft.Sbom.Targets" Version="4.1.5" />
```

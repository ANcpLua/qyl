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
│   │   ├── IGenerate.cs          # Schema code generation
│   │   ├── IDockerBuild.cs       # Docker image builds
│   │   ├── IDockerCompose.cs     # Docker Compose orchestration
│   │   ├── IVersionize.cs        # Changelog/release automation
│   │   ├── ITestContainers.cs    # CI testcontainer setup
│   │   ├── IClaudeContext.cs     # CLAUDE.md compilation
│   │   └── CoveragePatterns.cs   # Coverage analysis utilities
│   ├── Context/
│   │   └── BuildPaths.cs         # Centralized path constants
│   └── Domain/CodeGen/
│       ├── QylSchema.cs          # Schema definitions (SSOT)
│       ├── GenerationGuard.cs    # Write control (CI vs local)
│       └── Generators/           # Code generators
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
| `IGenerate` | QylSchema code generation | `Generate` |
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

## Code Generator Architecture

`eng/build/Domain/CodeGen/` contains the generation pipeline:

```
QylSchema.cs (definitions)
     │
     ├─► CSharpGenerator    → protocol/*.g.cs
     └─► DuckDbGenerator    → collector/Storage/DuckDbSchema.g.cs
```

Generators implement `IGenerator`:

```csharp
interface IGenerator
{
    string Name { get; }
    IEnumerable<(string RelativePath, string Content)> Generate(
        QylSchema schema, BuildPaths paths, string rootNamespace);
}
```

`GenerationGuard` controls write behavior:
- **CI**: Verify-only (fails if generated files don't match)
- **Local**: Write files to disk

## MSBuild Infrastructure

`eng/MSBuild/Shared.props` provides OTel configuration:
- `QylOTelSemConvVersion` = 1.38.0
- Global usings for `System.Diagnostics` and `System.Diagnostics.Metrics`

`eng/build/Directory.Build.props` relaxes analyzer rules for build automation code (RS0030 disabled, AOT not required, XML docs optional).

## SDK Configuration

qyl uses ANcpLua.NET.Sdk 1.6.2 from nuget.org:

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

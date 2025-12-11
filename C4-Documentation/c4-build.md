# C4: Build System

> NUKE build, GitHub Actions, Docker, and tooling

## Overview

The build system uses NUKE for .NET builds, npm for frontend, TypeSpec for schema generation, and Kiota for SDK codegen.
GitHub Actions handles CI/CD with Native AOT publishing.

## Key Classes/Modules

### NUKE Build (`eng/build/`)

| Class            | Purpose                  | Location                     |
|------------------|--------------------------|------------------------------|
| `Build`          | Main build orchestration | `Build.cs`                   |
| `Build.Frontend` | npm targets              | `Build.Frontend.cs`          |
| `Build.TypeSpec` | TypeSpec compilation     | `Build.TypeSpec.cs`          |
| `ICompile`       | .NET compilation         | `Components/ICompile.cs`     |
| `ITest`          | Test execution           | `Components/ITest.cs`        |
| `ICoverage`      | Coverage reporting       | `Components/ICoverage.cs`    |
| `IDockerBuild`   | Docker builds            | `Components/IDockerBuild.cs` |

### Analyzers (`eng/qyl.analyzers/`)

| Class                                   | Purpose                  | Location                                   |
|-----------------------------------------|--------------------------|--------------------------------------------|
| `QylGenAiDeprecatedAttributeAnalyzer`   | Warn on deprecated attrs | `QylGenAiDeprecatedAttributeAnalyzer.cs`   |
| `QylGenAiNonCanonicalAttributeAnalyzer` | Enforce canonical names  | `QylGenAiNonCanonicalAttributeAnalyzer.cs` |

## Dependencies

**Internal:** None (standalone tooling)

**External:** NUKE 10.x, TypeSpec 1.7.0, Kiota 1.29.0, Docker

## Build Flow

```
nuke GenerateAll
    ↓
TypeSpecInstall → TypeSpecCompile
    ↓
openapi.yaml + JSON Schema
    ↓
kiota generate (C#, Python, TypeScript)
    ↓
dotnet build + npm build
    ↓
Native AOT publish
```

## Key Targets

| Target            | Command                             | Description             |
|-------------------|-------------------------------------|-------------------------|
| `Compile`         | `dotnet build`                      | Build all .NET projects |
| `Test`            | `dotnet test`                       | Run unit tests          |
| `FrontendBuild`   | `npm run build`                     | Vite production build   |
| `TypeSpecCompile` | `tsp compile .`                     | TypeSpec → OpenAPI      |
| `GenerateAll`     | All above                           | Full regeneration       |
| `PublishAot`      | `dotnet publish -p:PublishAot=true` | Native AOT binary       |

## GitHub Actions

| Workflow      | Trigger  | Jobs                                     |
|---------------|----------|------------------------------------------|
| `ci.yml`      | push, PR | backend build+test, frontend build+test  |
| `release.yml` | tag v*   | multi-platform AOT build, GitHub release |

## Patterns Used

- **Interface Composition**: NUKE components via interfaces (ICompile, ITest)
- **Target Dependencies**: NUKE target graph for build ordering
- **Roslyn Analyzers**: Custom analyzers enforce GenAI conventions

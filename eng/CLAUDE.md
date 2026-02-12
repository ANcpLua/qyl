# eng - Build System

NUKE 10.1.0 build system with Nuke.Components composition.

## Structure

```
build/
  Build.cs              # Entry point, orchestration targets (Ci, Full, Dev)
  BuildPaths.cs         # IHazSourcePaths, CodegenPaths, IVersionize
  BuildTest.cs          # IQylTest (Nuke.Components.ITest) + MtpExtensions
  BuildCoverage.cs      # ICoverage, exclusion rules, CoverageSummaryConverter
  BuildPipeline.cs      # TypeSpec, frontend, code generation (IPipeline)
  BuildVerify.cs        # Generated code verification (IVerify)
  BuildInfra.cs         # Docker image builds, Compose orchestration (IDocker)
  SchemaGenerator.cs    # OpenAPI -> C#/DuckDB codegen (pure domain, no NUKE deps)

MSBuild/
  Shared.props          # Common properties
  Shared.targets        # Common targets
```

## Component Hierarchy

```
IHazSolution + IHazArtifacts
        |
  IHazSourcePaths          <- qyl project paths (BuildPaths.cs)
    |         |
IQylTest  IPipeline  IDocker  IVerify  IVersionize
    |
 ICoverage
```

All interfaces extend `IHazSourcePaths` for access to `SourceDirectory`, `CollectorDirectory`, `DashboardDirectory`, etc.

## Targets

```
GenerateSemconv + TypeSpecCompile -> Generate -> Compile -> Test
                                                    |
                           FrontendBuild -> DockerImageBuild
```

| Target | Command |
|--------|---------|
| `Full` | `nuke Full` — complete pipeline |
| `Generate` | `nuke Generate --force-generate` — regen types |
| `Compile` | `nuke Compile` — build .NET (Nuke.Components.ICompile) |
| `Test` | `nuke Test` — run tests (Nuke.Components.ITest + MTP) |
| `Coverage` | `nuke Coverage` — tests + Cobertura + reports |
| `UnitTests` | `nuke UnitTests` — unit tests only |
| `IntegrationTests` | `nuke IntegrationTests` — integration tests only |
| `FrontendBuild` | `nuke FrontendBuild` — React app |
| `DockerImageBuild` | `nuke DockerImageBuild` — Docker images |

## NUKE Compat Notes

- .NET 10 SDK: `dotnet test` requires `--project` flag (NUKE uses positional arg — workaround in TestProjectSettings)
- MTP: Clear NUKE's `--logger trx` (conflicts with MTP's `--report-trx`)
- GitVersion: NOT exposed via IHazGitVersion (ICompile.ReportSummary NREs when null). Field on Build class only.

## Docker

```bash
docker build -f src/qyl.collector/Dockerfile -t qyl .
```

Stages: `dashboard` (Node 22) -> `build` (.NET SDK) -> `runtime` (ASP.NET)
Uses Debian base (not Alpine) — DuckDB requires glibc.

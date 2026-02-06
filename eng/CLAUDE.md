# eng - Build System

NUKE build system and MSBuild infrastructure.

## Structure

```
build/
  Build.cs              # Main entry, target orchestration
  BuildCore.cs          # Core targets (Compile, Test)
  BuildInfra.cs         # Infrastructure (Docker, Pack)
  BuildPipeline.cs      # CI/CD pipeline
  BuildTest.cs          # Test targets
  BuildVerify.cs        # Verification targets
  SchemaGenerator.cs    # TypeSpec -> C#/DuckDB/TS codegen

MSBuild/
  Shared.props          # Common properties
  Shared.targets        # Common targets
```

## Targets

```
TypeSpecCompile -> Generate -> Compile -> Test
                                  |
FrontendBuild --------> DockerImageBuild
```

| Target | Command |
|--------|---------|
| `Full` | `nuke Full` — complete build |
| `Generate` | `nuke Generate --force-generate` — regen types |
| `Compile` | `nuke Compile` — build .NET |
| `Test` | `nuke Test` — run tests |
| `Coverage` | `nuke Coverage` — tests + coverage |
| `FrontendBuild` | `nuke FrontendBuild` — React app |
| `DockerImageBuild` | `nuke DockerImageBuild` — Docker image |

## Docker

```bash
docker build -f src/qyl.collector/Dockerfile -t qyl .
```

Stages: `dashboard` (Node 22) -> `build` (.NET SDK) -> `runtime` (ASP.NET)
Uses Debian base (not Alpine) — DuckDB requires glibc.

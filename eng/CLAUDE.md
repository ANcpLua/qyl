# eng/build — NUKE Build System

Build orchestration for qyl. Enforces architecture through target dependencies.

## identity

```yaml
name: eng/build
type: nuke-build
role: architecture-enforcer
location: eng/build/
```

## targets

```yaml
schema:
  - name: TypeSpecCompile
    input: core/specs/*.tsp
    output: core/openapi/openapi.yaml
    command: tsp compile main.tsp
    
  - name: Generate
    depends: [TypeSpecCompile]
    input: core/openapi/openapi.yaml
    outputs:
      - src/qyl.protocol/Primitives/*.g.cs
      - src/qyl.protocol/Enums/*.g.cs
      - src/qyl.protocol/Models/*.g.cs
      - src/qyl.collector/Storage/DuckDbSchema.g.cs
      - src/qyl.dashboard/src/types/api.ts

build:
  - name: Compile
    input: src/**/*.csproj
    output: bin/
    command: dotnet build
    
  - name: DashboardBuild
    input: src/qyl.dashboard/
    output: src/qyl.dashboard/dist/
    command: npm run build
    working-dir: src/qyl.dashboard

embed:
  - name: DashboardEmbed
    depends: [DashboardBuild, Compile]
    action: copy
    source: src/qyl.dashboard/dist/
    target: src/qyl.collector/wwwroot/
    critical: true

package:
  - name: Publish
    depends: [DashboardEmbed]
    output: artifacts/publish/
    command: dotnet publish -c Release
    
  - name: DockerBuild
    depends: [Publish]
    output: ghcr.io/ancplua/qyl:latest
    dockerfile: Dockerfile
    
  - name: Pack
    depends: [Publish]
    output: artifacts/packages/*.nupkg
    creates: dotnet-global-tool

test:
  - name: Test
    command: dotnet test
    
  - name: Coverage
    depends: [Test]
    output: artifacts/coverage/
```

## dependency-graph

```yaml
order:
  1: TypeSpecCompile
  2: Generate
  3: [DashboardBuild, Compile]  # parallel
  4: DashboardEmbed
  5: Publish
  6: [DockerBuild, Pack]  # parallel

critical-path:
  - DashboardEmbed must run after BOTH DashboardBuild AND Compile
  - DockerBuild must include embedded dashboard
  - Pack must include embedded dashboard
```

## parameters

```yaml
flags:
  - name: --configuration
    default: Release
    type: enum [Debug, Release]
    
  - name: --force-generate
    default: false
    type: bool
    description: overwrite generated files
    
  - name: --docker-tag
    default: latest
    type: string
    
  - name: --skip-tests
    default: false
    type: bool
```

## invocation

```yaml
common:
  full-build: nuke Full
  generate-only: nuke Generate --force-generate
  docker-only: nuke DockerBuild
  pack-only: nuke Pack
  ci: nuke CI

development:
  watch-dashboard: cd src/qyl.dashboard && npm run dev
  run-collector: dotnet run --project src/qyl.collector
```

## file-structure

```yaml
eng/:
  build/:
    Build.cs              # Main build class, target definitions
    Build.Schema.cs       # TypeSpecCompile, Generate
    Build.Dashboard.cs    # DashboardBuild, DashboardEmbed
    Build.Docker.cs       # DockerBuild
    Build.Pack.cs         # Pack (global tool)
    Domain/:
      CodeGen/:           # OpenAPI → C#/TS generators
        CSharpScalarGenerator.cs
        CSharpEnumGenerator.cs
        CSharpModelGenerator.cs
        DuckDbSchemaGenerator.cs
        
  MSBuild/:
    BannedSymbols.txt     # Banned API enforcement
```

## generators

```yaml
location: eng/build/Domain/CodeGen/

generators:
  - name: CSharpScalarGenerator
    input: openapi schemas with x-primitive
    output: protocol/Primitives/*.g.cs
    
  - name: CSharpEnumGenerator
    input: openapi enums
    output: protocol/Enums/*.g.cs
    
  - name: CSharpModelGenerator
    input: openapi schemas
    output: protocol/Models/*.g.cs
    
  - name: DuckDbSchemaGenerator
    input: openapi schemas with x-duckdb-table
    output: collector/Storage/DuckDbSchema.g.cs

extensions-used:
  - x-csharp-type
  - x-duckdb-table
  - x-duckdb-column
  - x-duckdb-type
  - x-duckdb-primary-key
  - x-duckdb-index
  - x-primitive
  - x-promoted
```

## enforcement

```yaml
rules:
  - dashboard-must-embed: DashboardEmbed target ensures dist/ → wwwroot/
  - no-manual-generation: all *.g.cs from Generate target
  - docker-includes-dashboard: DockerBuild depends on DashboardEmbed
  - pack-includes-dashboard: Pack depends on DashboardEmbed
```

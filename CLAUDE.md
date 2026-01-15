# qyl

AI Observability Platform. Observe everything. Judge nothing. Document perfectly.

## identity

```yaml
name: qyl
type: observability-platform
domain: gen_ai telemetry
distribution:
  primary: docker
  secondary: dotnet-global-tool
```

## architecture

```yaml
components:
  collector:
    runtime: dotnet-10
    ports:
      http: 5100
      grpc: 4317
    serves:
      - otlp-ingestion
      - rest-api
      - sse-streaming
      - static-files (dashboard)
    storage: duckdb
    
  dashboard:
    runtime: node-22
    framework: react-19
    build-tool: vite-7
    output: dist/
    embedding: collector/wwwroot/
    
  mcp:
    runtime: dotnet-10
    protocol: stdio
    connects-to: collector (http)
    
  protocol:
    runtime: dotnet-10
    constraints: bcl-only
    role: shared-types
```

## dependencies

```yaml
flow:
  - from: dashboard
    to: collector
    via: http (rest/sse)
    
  - from: mcp
    to: collector
    via: http (rest)
    
  - from: collector
    to: protocol
    via: project-reference
    
  - from: mcp
    to: protocol
    via: project-reference

forbidden:
  - from: mcp
    to: collector
    via: project-reference
    reason: must remain http-only for decoupling
    
  - from: dashboard
    to: any-dotnet
    reason: pure frontend, no runtime dependency
```

## schema

```yaml
source-of-truth: core/specs/main.tsp
outputs:
  - path: core/openapi/openapi.yaml
    generator: typespec
    
  - path: src/qyl.protocol/**/*.g.cs
    generator: nuke-generate
    
  - path: src/qyl.collector/Storage/DuckDbSchema.g.cs
    generator: nuke-generate
    
  - path: src/qyl.dashboard/src/types/api.ts
    generator: openapi-typescript

rule: never-edit-generated-files
```

## build

```yaml
system: nuke
targets:
  - name: TypeSpecCompile
    input: core/specs/*.tsp
    output: core/openapi/openapi.yaml
    
  - name: Generate
    depends: [TypeSpecCompile]
    input: core/openapi/openapi.yaml
    output: [protocol/*.g.cs, collector/Storage/*.g.cs, dashboard/src/types/api.ts]
    
  - name: DashboardBuild
    input: src/qyl.dashboard/
    output: src/qyl.dashboard/dist/
    command: npm run build
    
  - name: Compile
    input: src/**/*.csproj
    output: bin/
    
  - name: DashboardEmbed
    depends: [DashboardBuild, Compile]
    action: copy dist/ → collector/wwwroot/
    
  - name: Publish
    depends: [DashboardEmbed]
    output: artifacts/publish/
    
  - name: DockerBuild
    depends: [Publish]
    output: ghcr.io/ancplua/qyl:latest
    
  - name: Pack
    depends: [Publish]
    output: artifacts/packages/*.nupkg
```

## tech-stack

```yaml
runtime:
  dotnet: "10.0"
  csharp: "14"
  node: "22"
  
packages:
  sdk: ANcpLua.NET.Sdk@latest
  storage: DuckDB.NET.Data.Full
  grpc: Grpc.AspNetCore
  otel: OpenTelemetry.SemanticConventions@1.39
  
frontend:
  react: "19"
  vite: "7"
  tailwind: "4"
  tanstack-query: "5"
  radix-ui: latest
  
testing:
  framework: xunit-v3
  runner: microsoft-testing-platform
```

## commands

```yaml
development:
  collector: dotnet run --project src/qyl.collector
  dashboard: cd src/qyl.dashboard && npm run dev
  
build:
  full: nuke Full
  generate: nuke Generate --force-generate
  docker: nuke DockerBuild
  pack: nuke Pack
  
test:
  all: dotnet test
  coverage: nuke Coverage
```

## conventions

```yaml
files:
  generated: "*.g.cs"
  tests: "*.Tests.cs"
  
naming:
  spans-table: spans
  sessions-table: sessions
  logs-table: logs
  
api:
  base-path: /api/v1
  otlp-path: /v1/traces
  live-path: /api/v1/live
```

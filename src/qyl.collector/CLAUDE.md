# qyl.collector

Backend service: OTLP ingestion, REST API, SSE streaming, DuckDB storage, dashboard hosting.

## identity

```yaml
name: qyl.collector
type: aspnetcore-web-api
sdk: ANcpLua.NET.Sdk.Web
role: primary-runtime
what-users-see: docker-image
```

## ports

```yaml
http:
  port: 5100
  env: QYL_PORT
  serves: [rest-api, sse, static-files, otlp-http]
  
grpc:
  port: 4317
  env: QYL_GRPC_PORT
  serves: [otlp-grpc]
```

## endpoints

```yaml
otlp:
  - path: /v1/traces
    method: POST
    protocol: otlp-http-json
    
  - port: 4317
    protocol: otlp-grpc
    service: TraceService

rest:
  - path: /api/v1/sessions
    method: GET
    returns: SessionSummary[]
    
  - path: /api/v1/sessions/{id}
    method: GET
    returns: SessionSummary
    
  - path: /api/v1/sessions/{id}/spans
    method: GET
    returns: SpanRecord[]
    
  - path: /api/v1/traces/{traceId}
    method: GET
    returns: TraceNode

streaming:
  - path: /api/v1/live
    method: GET
    protocol: sse
    returns: SpanRecord (stream)

static:
  - path: /*
    serves: wwwroot/
    fallback: index.html
```

## storage

```yaml
engine: duckdb
location:
  development: ./qyl.duckdb
  docker: /data/qyl.duckdb
  env: QYL_DATA_PATH

tables:
  - name: spans
    primary-key: span_id
    indexes: [trace_id, session_id, start_time_unix_nano, service_name]
    
  - name: sessions
    primary-key: session_id
    
  - name: logs
    primary-key: log_id
    indexes: [trace_id, session_id, time_unix_nano, severity_number]

schema-source: DuckDbSchema.g.cs
schema-rule: never-edit-manually
```

## dashboard-embedding

```yaml
source: ../qyl.dashboard/dist/
target: wwwroot/
copy-timing: build-time
mechanism: nuke-target (DashboardEmbed)

csproj-config: |
  <ItemGroup>
    <None Include="..\qyl.dashboard\dist\**\*" 
          CopyToOutputDirectory="PreserveNewest"
          LinkBase="wwwroot" />
  </ItemGroup>

program-cs: |
  app.UseStaticFiles();
  app.MapFallbackToFile("index.html");
```

## dependencies

```yaml
project-references:
  - qyl.protocol
  
packages:
  - DuckDB.NET.Data.Full
  - Grpc.AspNetCore
  - Google.Protobuf
  
forbidden:
  - qyl.dashboard (build artifact, not dependency)
  - qyl.mcp (http client, not reference)
```

## patterns

```yaml
channel:
  type: bounded
  capacity: 10000
  full-mode: wait
  usage: span-ingestion-buffer

json:
  serializer: System.Text.Json
  options: static-readonly (CA1869)
  
time:
  provider: TimeProvider.System
  format: unix-nano (int64)
  
locking:
  sync: Lock (dotnet-9+)
  async: SemaphoreSlim
```

## configuration

```yaml
environment-variables:
  - name: QYL_PORT
    default: 5100
    type: int
    
  - name: QYL_GRPC_PORT
    default: 4317
    type: int
    
  - name: QYL_DATA_PATH
    default: /data/qyl.duckdb
    type: string

future: dashboard-based-config (not env vars)
```

## dockerfile

```yaml
base: mcr.microsoft.com/dotnet/aspnet:10.0
expose: [5100, 4317]
workdir: /app
entrypoint: ["dotnet", "qyl.collector.dll"]
volume: /data
```

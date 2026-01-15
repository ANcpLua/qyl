# qyl.collector

Backend service. This IS qyl from user perspective.

## identity

```yaml
name: qyl.collector
type: aspnetcore-webapi
sdk: ANcpLua.NET.Sdk.Web
role: primary-runtime
user-sees: docker-image or global-tool
```

## ports

```yaml
http:
  port: 5100
  env: QYL_PORT
  
grpc:
  port: 4317
  env: QYL_GRPC_PORT
```

## endpoints

```yaml
otlp-ingestion:
  - path: /v1/traces
    method: POST
    content-type: application/json
    protocol: otlp-http
    
  - port: 4317
    service: opentelemetry.proto.collector.trace.v1.TraceService
    method: Export
    protocol: otlp-grpc

rest-api:
  base: /api/v1
  endpoints:
    - GET /sessions
    - GET /sessions/{id}
    - GET /sessions/{id}/spans
    - GET /traces/{traceId}
    - GET /stats/tokens
    - GET /stats/latency

streaming:
  - path: /api/v1/live
    protocol: sse
    content-type: text/event-stream
    
static:
  - path: /*
    source: wwwroot/
    fallback: index.html
```

## storage

```yaml
engine: duckdb
package: DuckDB.NET.Data.Full

location:
  env: QYL_DATA_PATH
  default-dev: ./qyl.duckdb
  default-docker: /data/qyl.duckdb

schema:
  source: Storage/DuckDbSchema.g.cs
  generator: SchemaGenerator.GenerateDuckDb()
  
tables:
  spans:
    primary-key: span_id
    indexes:
      - idx_spans_trace_id (trace_id)
      - idx_spans_session_id (session_id)
      - idx_spans_start_time (start_time_unix_nano)
      - idx_spans_service_name (service_name)
    promoted-columns:
      - session_id
      - service_name
      - gen_ai_provider_name
      - gen_ai_request_model
      - gen_ai_response_model
      - gen_ai_input_tokens
      - gen_ai_output_tokens
      - gen_ai_temperature
      - gen_ai_stop_reason
      - gen_ai_tool_name
      - gen_ai_tool_call_id
      - gen_ai_cost_usd
    json-blobs:
      - attributes_json
      - resource_json
      
  sessions:
    primary-key: session_id
    aggregates:
      - span_count
      - error_count
      - total_input_tokens
      - total_output_tokens
      - total_cost_usd
      
  logs:
    primary-key: log_id
    indexes:
      - idx_logs_trace_id
      - idx_logs_session_id
      - idx_logs_time
      - idx_logs_severity
```

## dashboard-embedding

```yaml
source: ../qyl.dashboard/dist/
target: wwwroot/
timing: build-time (not runtime)
mechanism: nuke DashboardEmbed target

csproj: |
  <ItemGroup>
    <None Include="..\qyl.dashboard\dist\**\*" 
          CopyToOutputDirectory="PreserveNewest"
          LinkBase="wwwroot" />
  </ItemGroup>

program-cs: |
  app.UseStaticFiles();
  app.MapFallbackToFile("index.html");
```

## patterns

```yaml
channel:
  purpose: span ingestion buffer
  type: Channel.CreateBounded<SpanRecord>
  capacity: 10000
  full-mode: BoundedChannelFullMode.Wait
  code: |
    private readonly Channel<SpanRecord> _channel = 
        Channel.CreateBounded<SpanRecord>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

sse:
  code: |
    app.MapGet("/api/v1/live", async (HttpContext ctx, CancellationToken ct) =>
    {
        ctx.Response.ContentType = "text/event-stream";
        await foreach (var span in _channel.Reader.ReadAllAsync(ct))
        {
            await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(span, s_options)}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    });

json:
  rule: static readonly options (CA1869)
  code: |
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

time:
  provider: TimeProvider.System
  format: unix nanoseconds (int64)
  
locking:
  sync: |
    private readonly Lock _lock = new();
    using (_lock.EnterScope()) { /* sync only */ }
    
  async: |
    private readonly SemaphoreSlim _asyncLock = new(1, 1);
    await _asyncLock.WaitAsync(ct);
    try { await DoWork(ct); }
    finally { _asyncLock.Release(); }
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
  - qyl.dashboard
  - qyl.mcp
```

## dockerfile

```yaml
base: mcr.microsoft.com/dotnet/aspnet:10.0
expose: [5100, 4317]
workdir: /app
volume: /data
entrypoint: ["dotnet", "qyl.collector.dll"]

multi-stage: |
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  # restore, build, publish
  
  FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
  COPY --from=build /app/publish .
  # wwwroot/ already included from DashboardEmbed
```

# qyl

**Question Your Logs** — AI Observability Platform

Landing page: <https://ancplua.github.io/qyl/>

## What qyl Does

|                 |                                                                         |
|-----------------|-------------------------------------------------------------------------|
| **Collects**    | OTLP receiver (gRPC 4317 / HTTP 5100) with idempotent upsert ingestion  |
| **Stores**      | DuckDB 1.5.0 columnar storage with single-writer, ON CONFLICT semantics |
| **Instruments** | Roslyn source generators for zero-config GenAI + DB telemetry           |
| **Queries**     | MCP server with 77 tools — traces, errors, logs, metrics, GenAI, triage |
| **Visualizes**  | Real-time dashboard with SSE streaming                                  |
| **Automates**   | Loom agent for triage, RCA, fix generation, and code review             |

## Tech Stack

| Layer    | Technology                                      |
|----------|-------------------------------------------------|
| Runtime  | .NET 10.0, C# 14                                |
| Frontend | React 19, Vite 7, Tailwind CSS 4, Base UI 1.3.0 |
| Storage  | DuckDB 1.5.0 (Debian, glibc required)           |
| Protocol | OpenTelemetry SDK 1.15.0, Semconv 1.40          |
| Schema   | TypeSpec -> OpenAPI -> C# / DuckDB / TypeScript |
| Build    | NUKE 10.1.0, MSBuild, ANcpLua.NET.Sdk           |
| MCP      | ModelContextProtocol C# SDK 1.2.0               |
| Agents   | Microsoft Agent Framework 1.3.0                 |

## Projects

| Project                            | Type            | Purpose                                              |
|------------------------------------|-----------------|------------------------------------------------------|
| `qyl.collector`                    | Application     | OTLP ingest, REST API, SSE streaming, DuckDB storage |
| `qyl.mcp`                          | Application     | MCP tool surface (stdio + Streamable HTTP)           |
| `qyl.loom`                         | Application     | Standalone agent exe — triage, RCA, fix, code review |
| `qyl.dashboard`                    | Frontend        | React 19 SPA with TanStack Table/Query, ECharts 6    |
| `qyl.contracts`                    | Library         | BCL-only shared types (no external dependencies)     |
| `qyl.instrumentation`              | Library         | .NET instrumentation SDK with OTel setup             |
| `qyl.instrumentation.generators`   | Roslyn Analyzer | Source generator for GenAI/DB interceptors           |
| `qyl.collector.storage.generators` | Roslyn Analyzer | DuckDB storage source generators                     |

## Architecture

```text
                   ┌──────────────────┐
                   │   qyl.collector   │
                   │ OTLP ingest + API │
                   │  DuckDB storage   │
                   └─────┬────┬────┬──┘
                         │    │    │
          ┌──────────────┘    │    └──────────────┐
          │                   │                   │
          v                   v                   v
   ┌─────────────┐    ┌─────────────┐    ┌──────────────┐
   │   qyl.mcp    │    │  qyl.loom   │    │qyl.dashboard │
   │ 77 MCP tools │    │ Agent exe   │    │  React SPA   │
   │ stdio + HTTP │    │ Triage/RCA  │    │  SSE stream  │
   └─────────────┘    └─────────────┘    └──────────────┘

   qyl.collector owns DuckDB (single-writer).
   qyl.mcp and qyl.loom talk to collector over HTTP only.
   qyl.dashboard connects via REST + SSE.
```

## Quick Start

**Hosted**

<https://qyl-api-production.up.railway.app>

**Docker**

```bash
docker build -f services/qyl.collector/Dockerfile -t qyl .
docker run -d -p 5100:5100 -p 4317:4317 -v ~/.qyl:/data qyl
```

**From Source**

```bash
git clone --recurse-submodules https://github.com/ANcpLua/qyl.git
cd qyl
dotnet run --project services/qyl.collector
```

If you cloned without `--recurse-submodules`, initialize the OpenTelemetry
semantic-conventions pin once:

```bash
git submodule update --init .tools/semconv-upstream
```

**Dashboard**

```bash
cd services/qyl.dashboard && npm install && npm run dev
```

**MCP Server**

```bash
QYL_COLLECTOR_URL=http://localhost:5100 dotnet run --project services/qyl.mcp
```

## Instrument Your .NET App

```csharp
builder.AddQylServiceDefaults();
```

This auto-instruments `IChatClient` calls (Microsoft.Extensions.AI) with full OTel 1.40 GenAI semantic conventions —
token usage, latency, model info, finish reasons.

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:5100"
```

## Use with Any OTel App

qyl accepts standard OTLP from any language/framework:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:5100"
export OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf"
```

| Port | Protocol | Purpose                        |
|------|----------|--------------------------------|
| 5100 | HTTP     | REST API, Dashboard, OTLP/HTTP |
| 4317 | gRPC     | OTLP/gRPC ingestion            |
| 4318 | HTTP     | OTLP HTTP/protobuf ingestion   |

## MCP Tool Surface

qyl.mcp exposes 77 tools across 9 skill families:

| Skill     | Examples                                              |
|-----------|-------------------------------------------------------|
| Inspect   | Traces, spans, errors, logs, services, GenAI sessions |
| Health    | Storage stats, system context                         |
| Analytics | Conversation analytics, user journeys, satisfaction   |
| Agent     | `use_qyl` meta-agent, RCA, summaries, fix generation  |
| Build     | Project management, API keys, retention               |
| Anomaly   | Baselines, anomaly detection, period comparison       |
| Loom      | Triage, fix pipeline, regressions, code review        |
| Apps      | Trace explorer, error explorer, query studio          |
| Debug     | Rider MCP proxy, JetBrains discovery                  |

Transports: stdio (local) and Streamable HTTP (remote, Claude Web UI, API connectors).

## Agent Runtime (MAF)

`qyl.loom` is a [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) 1.3.0 consumer. Curated
`Qyl.*` facade extensions over MAF live in [`MAF.Advanced.Patterns`](https://github.com/Alexander-Nachtmann/MAF.Advanced.Patterns)
(5 packages: core + Azure + Foundry + Foundry.Hosting + OpenAI) — qyl is moving to consume those packages instead of
duplicating extensions in-tree (tracked in
[MAF.Advanced.Patterns#1](https://github.com/Alexander-Nachtmann/MAF.Advanced.Patterns/issues/1) /
[qyl#173](https://github.com/Alexander-Nachtmann/qyl/issues/173)).

Triage, RCA, fix generation, and code review agents share one composition shape:

```csharp
var agent = llm.AsAIAgent(new ChatClientAgentOptions
    {
        Name = "AutofixRcaAgent",
        Description = "Root-cause analysis for a qyl error issue.",
        ChatOptions = new ChatOptions { Instructions = AutofixPrompts.RootCauseAnalysis },
    })
    .AsBuilder()
    .UseQylAgentTelemetry()            // emits on 'qyl.agent' ActivitySource
    .Build();

var response = await agent.RunAsync(userMessage, cancellationToken: ct);
```

Multi-step flows use `WorkflowBuilder` with fan-out edges and are observed via `InProcessExecution.RunStreamingAsync`

+ `WatchStreamAsync`:

```csharp
var workflow = new WorkflowBuilder(start)
    .AddEdge(start, gatherContext)
    .AddEdge(gatherContext, rca)
    .AddFanOutEdge(rca, [impact, issueTriage, solutionPlan])
    .AddEdge(solutionPlan, diffGen)
    .AddEdge(diffGen, confidence)
    .AddEdge(confidence, policyGate)
    .WithOutputFrom(policyGate)
    .Build();

await foreach (var evt in (await InProcessExecution.RunStreamingAsync(workflow, input)).WatchStreamAsync(ct))
{
    // observe per-executor completions, emit SSE, update LoomRunState
}
```

| Surface                   | qyl usage                                                                                                   |
|---------------------------|-------------------------------------------------------------------------------------------------------------|
| Standalone agent          | `llm.AsAIAgent(options)` — every executor under `services/qyl.loom/Autofix/Workflow/Executors/`             |
| Streaming                 | `AutofixAgentService`, `ExplorationOrchestrator`                                                            |
| Workflow graph            | `AutofixWorkflowFactory`, `ExplorationWorkflowFactory`                                                      |
| Tool factory              | `AIFunctionFactory.Create` via `internal/qyl.instrumentation/Instrumentation/Loom/LoomToolFactoryBridge.cs` |
| MCP tool registration     | `[QylSkill]` + `[QylCapability]` — emitted by `internal/qyl.mcp.generators/`                                |
| Telemetry (`IChatClient`) | `innerClient.WithQylTelemetry("qyl.genai")`                                                                 |
| Telemetry (`AIAgent`)     | `agent.AsBuilder().UseQylAgentTelemetry().Build()`                                                          |

Full entry-point catalogue and rules live in [`CLAUDE.md`](CLAUDE.md) under "MAF agent composition".

## TypeSpec-First Design

All types are defined in TypeSpec and generated downstream:

```text
core/specs/*.tsp
       | (tsp compile)
core/openapi/openapi.yaml
       | (nuke Generate)
   +---+---+-------+--------+
   |       |       |        |
  C#    DuckDB    TS    JSON Schema
```

Never edit `*.g.cs` or `api.ts` — edit TypeSpec and regenerate.

## Environment Variables

| Variable                       | Default               | Purpose                                  |
|--------------------------------|-----------------------|------------------------------------------|
| `QYL_PORT`                     | 5100                  | HTTP API port                            |
| `QYL_GRPC_PORT`                | 4317                  | OTLP/gRPC port (0=disable)               |
| `QYL_OTLP_PORT`                | 4318                  | OTLP HTTP/protobuf port (0=disable)      |
| `QYL_DATA_PATH`                | ./qyl.duckdb          | DuckDB file location                     |
| `QYL_TOKEN`                    | (none)                | Auth token                               |
| `QYL_COLLECTOR_URL`            | http://localhost:5100 | Collector URL for `qyl.mcp` / `qyl.loom` |
| `QYL_MCP_TRANSPORT`            | stdio                 | MCP transport (`stdio` or `http`)        |
| `QYL_MCP_TOKEN`                | (none)                | MCP bearer-token auth                    |
| `QYL_SKILLS`                   | all non-debug         | Enabled MCP skill families               |
| `QYL_AGENT_API_KEY`            | (none)                | LLM API key for agent-backed tools       |
| `QYL_AGENT_MODEL`              | (none)                | LLM model for agent-backed tools         |
| `QYL_MAX_RETENTION_DAYS`       | 30                    | Telemetry retention                      |
| `QYL_MAX_SPAN_COUNT`           | 1000000               | Max spans before cleanup                 |
| `QYL_MAX_LOG_COUNT`            | 500000                | Max logs before cleanup                  |
| `QYL_CLEANUP_INTERVAL_SECONDS` | 300                   | Cleanup interval                         |

## Development

```bash
# Full build (TypeSpec -> Docker)
nuke Full

# Regenerate types from TypeSpec
nuke Generate --force-generate

# Run tests
dotnet test

# Dashboard dev server (hot reload)
cd services/qyl.dashboard && npm run dev
```

## Project Structure

```text
core/                                    # TypeSpec schemas (source of truth)
eng/                                     # NUKE build system + MSBuild props
packages/                                # NuGet-shipped libraries (Qyl.*)
services/
  qyl.collector/                        # OTLP ingest, REST API, DuckDB storage
  qyl.mcp/                              # MCP tool surface
  qyl.loom/                             # Standalone agent exe (triage/RCA/fix)
  qyl.loom.patterns/                    # MAF composition cookbook
  qyl.dashboard/                        # React 19 SPA
internal/
  qyl.instrumentation/                  # .NET instrumentation SDK
  qyl.instrumentation.generators/       # Roslyn: GenAI/DB interceptors
  qyl.collector.storage.generators/     # Roslyn: DuckDB storage
  qyl.mcp.generators/                   # Roslyn: MCP tool manifest emission
docs/                                   # Architecture sketches + superpowers
tests/                                  # xUnit v3 + MTP tests
```

## Documentation

| Document                             | Purpose                                                             |
|--------------------------------------|---------------------------------------------------------------------|
| `AGENTS.md`                          | Conventions + composition cheat sheet for agents in this repo       |
| `CLAUDE.md`                          | Per-package conventions, MAF entry-points, MCP/agent telemetry      |
| `docs/attributes/`                   | Compile-time attribute catalogues                                   |
| `docs/superpowers/`                  | Long-form notes and historical design plans                         |

## License

MIT

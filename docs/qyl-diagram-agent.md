# qyl Architecture Diagram Agent

You are a technical documentation agent. Your job is to produce **three architecture diagrams** for the qyl observability platform, matching the visual style and information density of the OpenTelemetry Demo architecture docs (https://opentelemetry.io/docs/demo/architecture/).

## Output Format

Produce each diagram as a **Mermaid diagram** in its own fenced code block, plus a brief legend/description beneath. The diagrams will be rendered on the qyl landing page (qyl.info) and in the GitHub README.

---

## Diagram 1: Service Diagram

Show qyl's internal service topology — every component, how they communicate, and what protocol they use.

### Components to include

| Component | Tech | Role |
|---|---|---|
| **OTLP Receiver** | ASP.NET Core (gRPC + HTTP) | Ingests traces, metrics, logs via OTLP gRPC (:4317) and OTLP HTTP (:4318) |
| **DuckDB Storage** | DuckDB 1.4.4 | Single persistent store — traces, metrics, logs, GenAI spans, convergence data |
| **MCP Server** | ASP.NET Core, Streamable HTTP | 54+ tools, deployed at mcp.qyl.info, OAuth 2.1 |
| **Loom RCA Engine** | C# Agent | 5-stage pipeline: context → root cause → solution → diff → confidence |
| **ErrorFingerprinter** | C# | Groups errors by stack trace / message similarity |
| **IssueService** | C# | Issue lifecycle management, triage |
| **TriagePipelineService** | C# | Automated issue routing and prioritization |
| **AutofixOrchestrator** | C# | Generates code fix suggestions |
| **AnomalyDetector** | C# | Statistical anomaly detection on metrics/error rates |
| **SessionReplay** | C# + TypeScript | Captures and replays user sessions |
| **AI Summarization** | C# (OpenAI SDK) | Error summarization, trace summarization |
| **Meta-Agent** | C# | Orchestrates other agents, tool routing |
| **React Frontend** | React + TypeScript + Tailwind + shadcn/ui | Dashboard, trace explorer, issue views |
| **Convergence Metrics** | C# | Embedding distance + cluster analysis for LLM agent convergence |
| **GenAI Cost Tracker** | C# | Token usage and cost aggregation per provider/model |

### Edges / Protocols

- Frontend ↔ Backend: HTTP/REST + SSE (AG-UI protocol for Loom handoff)
- OTLP Receiver → DuckDB: Direct write (no Kafka, no intermediate queue)
- Loom → DuckDB: Read telemetry context
- Loom → MCP Server: Code context via AG-UI/MCP
- All AI agents → OpenAI SDK: LLM calls
- MCP Server → DuckDB: All 54+ tools query DuckDB
- External clients → MCP Server: Streamable HTTP + OAuth 2.1

### Style guidance

- Color-code by domain: ingestion (blue), storage (dark), AI/agents (purple), frontend (green), external (orange)
- Label every edge with protocol (gRPC, HTTP, SSE, TCP, direct)
- Show DuckDB as a cylinder (database shape)
- Show external callers (Claude Desktop, IDE plugins, CI/CD) as dashed-border boxes

---

## Diagram 2: Telemetry Data Flow

Show the path of telemetry data from instrumented applications through qyl to storage and visualization.

### Flow

```
Instrumented App
  ├── OTLP gRPC (:4317)
  └── OTLP HTTP (:4318)
        ↓
  OTLP Receiver (ASP.NET Core)
        ↓
  Pipeline Processors
  ├── ErrorFingerprinter (groups errors)
  ├── GenAI Span Enricher (extracts token counts, costs, provider info)
  ├── Convergence Calculator (embedding distances, cluster metrics)
  └── Anomaly Scorer (statistical deviation)
        ↓
  DuckDB Writer
  ├── traces table
  ├── metrics table
  ├── logs table
  ├── gen_ai_spans table
  ├── convergence_episodes table
  └── service_instances table (Tier 1) / services view (Tier 2)
        ↓
  Consumers
  ├── React Dashboard (HTTP)
  ├── MCP Server (54+ tools, Streamable HTTP)
  ├── Loom RCA Engine (background + interactive SSE)
  └── REST API
```

### Key difference from OTel Demo

The OTel Demo fans out to Prometheus + Jaeger + OpenSearch + Grafana. qyl replaces ALL of that with DuckDB as the single backend. Emphasize this architectural simplification visually — show a single storage cylinder where the Demo has three colored backend boxes.

---

## Diagram 3: Loom RCA Pipeline (Detail)

Show Loom's 5-stage pipeline and the dual-trigger execution model.

### Stages

1. **Context Assembly** — IssueContextBuilder gathers: error event, stack trace, related traces from DuckDB, code context via MCP
2. **Root Cause Analysis** — LLM analyzes assembled context, identifies probable root cause
3. **Solution Generation** — LLM proposes fix strategy
4. **Diff Generation** — LLM produces concrete code diff
5. **Confidence Scoring** — Evaluates confidence in the analysis + fix

### Dual trigger

- **Background (headless)**: Triggered automatically on new high-severity issues. Runs all 5 stages silently. Stores result with `LoomStatus` in DuckDB.
- **Interactive (SSE)**: User clicks "Attach & Continue Chat". Hydrates background result into conversation via AG-UI protocol. Each stage emits SSE tool-call events as stage signals.

### Hero moment

Show the handoff: background session completes → user sees "Attach & Continue Chat" button → SSE session resumes from stored state → user can ask follow-up questions in real-time.

---

## General instructions

- Use `graph TD` (top-down) for Diagrams 1 and 2, `graph LR` (left-right) for Diagram 3
- Include a Service Legend with language/tech color coding
- Add subgraph labels for logical groupings
- Every node must have a tooltip-friendly label (no abbreviations without expansion)
- Validate that every component mentioned in the qyl SKILL.md tool inventory is represented
- If you're unsure about a component's existence or connections, leave a `%% TODO: verify` comment rather than guessing

## What NOT to include

- Sentry bridge (optional integration, not core architecture)
- CI/CD internals
- Build tooling (MSBuild SDK, Roslyn analyzers)
- Development-only infrastructure

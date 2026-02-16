# qyl Roadmap

**Philosophy:** Fully-instrumented adapters. If a customer uses it, qyl observes it — zero config, idempotent, complete
or doesn't exist.

---

## P0 - Core Adapters

### Microsoft Agent Framework (`Microsoft.Agents.AI`)

The new Microsoft agent framework replaces Semantic Kernel for agentic workloads.
qyl advantage: `OpenTelemetryAgent` already emits OTel GenAI semconv — qyl collects it natively.

| Adapter                                       | Notes                                                                |
|-----------------------------------------------|----------------------------------------------------------------------|
| `IChatClient` interception                    | Already done via source generator                                    |
| `AIAgent` / `ChatClientAgent` workflow traces | Observe full agent pipelines, not just LLM calls                     |
| `AIContextProvider` integration               | qyl as a context provider — inject trace context into agent sessions |
| Workflow checkpoint observability             | Track workflow state, superstep boundaries, checkpoint/resume events |
| `DelegatingAIAgent` telemetry                 | Compose with `OpenTelemetryAgent` or replace it entirely             |

### EF Core Interception (complete partial)

| Adapter                       | Notes                                       |
|-------------------------------|---------------------------------------------|
| Source generator interception | Complete the partial — full db.* attributes |

## P1 - Product

### Dashboard

| Feature               | Notes                                                  |
|-----------------------|--------------------------------------------------------|
| Tool definitions view | Customers need to see what tools their AI agent called |
| Theme selection       | Light/Dark/System — table stakes                       |
| Keyboard shortcuts    | R/C/S/T/M navigation                                   |
| Text visualizer       | JSON/XML formatting in log viewer                      |
| Clear telemetry       | Reset dev environment                                  |

### MCP Server

| Feature           | Notes                                                   |
|-------------------|---------------------------------------------------------|
| MCP auth          | x-mcp-api-key header — required for any real deployment |
| MCP config dialog | Dashboard UI to connect AI agents to qyl                |

### Onboarding

| Feature          | Notes                                              |
|------------------|----------------------------------------------------|
| Starter template | `dotnet new qyl` — zero to observing in 60 seconds |

## Anti-Goals

| Feature                       | Why Not                                           |
|-------------------------------|---------------------------------------------------|
| AppHost orchestration         | qyl is standalone collector, not orchestrator     |
| Console logs / stdout capture | qyl doesn't run apps — apps send OTLP             |
| Resource management           | No resources concept — apps are external          |
| Deploy CLI (Azure/AWS/K8s)    | Customers deploy Docker containers — not our job  |
| HTTP Protobuf OTLP            | gRPC + JSON covers 99% of clients                 |
| TLS termination               | Reverse proxy handles this (nginx/Caddy/cloud LB) |
| JSON config file              | Env vars work natively on every platform          |
| Semantic Kernel adapter       | Replaced by Microsoft Agent Framework             |

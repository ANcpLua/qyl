# qyl Mintlify Documentation Site Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a comprehensive Mintlify documentation site for qyl, an OpenTelemetry backend for AI observability.

**Architecture:** Mintlify-native MDX documentation with OpenAPI-generated API reference pages. Structure follows Diátaxis framework: tutorials for onboarding, how-to guides for tasks, reference for API/types, explanation for architecture. TypeSpec-generated OpenAPI spec powers auto-generated endpoint docs.

**Tech Stack:** Mintlify, MDX, docs.json configuration, OpenAPI 3.0 (from TypeSpec), TypeSpec extensions (x-mint)

---

## Prerequisites

- Mintlify CLI installed: `npm install -g mintlify`
- Access to existing `core/openapi/openapi.yaml` (188KB, auto-generated from TypeSpec)
- Familiarity with qyl architecture from root `CLAUDE.md`

---

## Task 1: Initialize Mintlify Project

**Files:**
- Create: `docs/docs.json`
- Create: `docs/index.mdx`
- Create: `docs/favicon.svg`

**Step 1: Create docs.json configuration**

```json
{
  "$schema": "https://mintlify.com/docs.json",
  "name": "qyl",
  "logo": {
    "dark": "/logo/qyl-dark.svg",
    "light": "/logo/qyl-light.svg"
  },
  "favicon": "/favicon.svg",
  "colors": {
    "primary": "#0D9373",
    "light": "#07C983",
    "dark": "#0D9373",
    "anchors": {
      "from": "#0D9373",
      "to": "#07C983"
    }
  },
  "topbarLinks": [
    {
      "name": "GitHub",
      "url": "https://github.com/ancplua/qyl"
    }
  ],
  "topbarCtaButton": {
    "name": "Dashboard",
    "url": "http://localhost:5173"
  },
  "tabs": [
    {
      "name": "Documentation",
      "url": "/"
    },
    {
      "name": "API Reference",
      "url": "/api-reference"
    }
  ],
  "navigation": {
    "groups": [
      {
        "group": "Getting Started",
        "pages": ["index", "quickstart", "installation"]
      },
      {
        "group": "Architecture",
        "pages": [
          "architecture/overview",
          "architecture/collector",
          "architecture/dashboard",
          "architecture/mcp",
          "architecture/protocol"
        ]
      },
      {
        "group": "Guides",
        "pages": [
          "guides/instrumenting-apps",
          "guides/querying-telemetry",
          "guides/ai-agent-integration",
          "guides/dashboard-usage"
        ]
      },
      {
        "group": "Schema",
        "pages": [
          "schema/types",
          "schema/genai-attributes",
          "schema/otel-compliance"
        ]
      }
    ],
    "anchors": [
      {
        "name": "API Reference",
        "icon": "code",
        "url": "/api-reference"
      }
    ]
  },
  "footerSocials": {
    "github": "https://github.com/ancplua/qyl"
  },
  "api": {
    "baseUrl": "http://localhost:5100",
    "auth": {
      "method": "bearer"
    }
  },
  "openapi": "openapi.yaml"
}
```

**Step 2: Create landing page**

Create `docs/index.mdx`:

```mdx
---
title: "qyl Documentation"
description: "OpenTelemetry backend for AI observability with gen_ai.* semantic conventions"
---

# Welcome to qyl

qyl is an OpenTelemetry backend purpose-built for AI observability. It captures, stores, and visualizes telemetry from AI applications using OpenTelemetry's `gen_ai.*` semantic conventions.

## What qyl does

<CardGroup cols={2}>
  <Card title="OTLP Ingestion" icon="download">
    Accepts traces via OTLP HTTP (5100) and gRPC (4317)
  </Card>
  <Card title="GenAI Attributes" icon="robot">
    Extracts gen_ai.* attributes for token tracking and cost attribution
  </Card>
  <Card title="Real-Time Dashboard" icon="chart-line">
    React 19 SPA with live session and span visualization
  </Card>
  <Card title="MCP Integration" icon="plug">
    AI agents query their own traces via Model Context Protocol
  </Card>
</CardGroup>

## Architecture

```
User App ──OTLP──► qyl.collector ──DuckDB──► Storage
                        │                       │
                        │                       └──► REST/SSE ──► qyl.dashboard
                        └──► REST ──► qyl.mcp ──stdio──► Claude
```

## Quick links

<CardGroup cols={2}>
  <Card title="Quick Start" icon="rocket" href="/quickstart">
    Get running in 5 minutes
  </Card>
  <Card title="API Reference" icon="code" href="/api-reference">
    REST API documentation
  </Card>
  <Card title="GenAI Attributes" icon="sparkles" href="/schema/genai-attributes">
    OTel v1.39 gen_ai.* coverage
  </Card>
  <Card title="MCP Tools" icon="terminal" href="/guides/ai-agent-integration">
    Claude Code integration
  </Card>
</CardGroup>
```

**Step 3: Create placeholder favicon**

Create `docs/favicon.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
  <rect width="100" height="100" rx="20" fill="#0D9373"/>
  <text x="50" y="70" font-family="Arial" font-size="60" font-weight="bold" fill="white" text-anchor="middle">q</text>
</svg>
```

**Step 4: Run Mintlify dev to verify**

Run: `cd docs && mintlify dev`
Expected: Local server starts at http://localhost:3000, shows landing page

**Step 5: Commit**

```bash
git add docs/docs.json docs/index.mdx docs/favicon.svg
git commit -m "feat(docs): initialize Mintlify documentation site"
```

---

## Task 2: Create Quick Start Guide

**Files:**
- Create: `docs/quickstart.mdx`

**Step 1: Write quickstart content**

Create `docs/quickstart.mdx`:

```mdx
---
title: "Quick Start"
description: "Get qyl running and visualize your first AI traces in 5 minutes"
---

# Quick Start

This guide walks you through running qyl locally and sending your first OpenTelemetry traces.

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- A terminal

## Step 1: Start the collector

```bash
cd src/qyl.collector
dotnet run
```

The collector starts on:
- **HTTP**: http://localhost:5100
- **gRPC**: localhost:4317

## Step 2: Start the dashboard

In a new terminal:

```bash
cd src/qyl.dashboard
npm install
npm run dev
```

Open http://localhost:5173 to view the dashboard.

## Step 3: Send test traces

Use any OTLP-compatible SDK. Here's an example with .NET:

```csharp
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("my-app")
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4317");
        options.Protocol = OtlpExportProtocol.Grpc;
    })
    .Build();

var activitySource = new ActivitySource("my-app");

using var activity = activitySource.StartActivity("process-request");
activity?.SetTag("gen_ai.system", "openai");
activity?.SetTag("gen_ai.request.model", "gpt-4");
activity?.SetTag("gen_ai.usage.input_tokens", 150);
activity?.SetTag("gen_ai.usage.output_tokens", 500);
```

## Step 4: View traces

Open the dashboard at http://localhost:5173. You should see:
- Your session in the sessions list
- Spans with extracted GenAI attributes
- Token counts and timing information

## Next steps

<CardGroup cols={2}>
  <Card title="API Reference" icon="code" href="/api-reference">
    Query traces programmatically
  </Card>
  <Card title="GenAI Attributes" icon="sparkles" href="/schema/genai-attributes">
    Learn about supported attributes
  </Card>
</CardGroup>
```

**Step 2: Verify page renders**

Run: `cd docs && mintlify dev`
Expected: Navigate to /quickstart, page renders correctly

**Step 3: Commit**

```bash
git add docs/quickstart.mdx
git commit -m "feat(docs): add quick start guide"
```

---

## Task 3: Create Installation Guide

**Files:**
- Create: `docs/installation.mdx`

**Step 1: Write installation content**

Create `docs/installation.mdx`:

```mdx
---
title: "Installation"
description: "Install and configure qyl for development or production"
---

# Installation

qyl consists of three components. Install what you need for your use case.

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Node.js | 20+ |
| npm | 10+ |

## Clone the repository

```bash
git clone https://github.com/ancplua/qyl.git
cd qyl
```

## Install dependencies

### Collector (required)

```bash
cd src/qyl.collector
dotnet restore
```

### Dashboard (optional)

```bash
cd src/qyl.dashboard
npm install
```

### MCP Server (optional)

```bash
cd src/qyl.mcp
dotnet restore
```

## Configuration

### Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `QYL_HTTP_PORT` | 5100 | HTTP API port |
| `QYL_GRPC_PORT` | 4317 | OTLP gRPC port |
| `QYL_DB_PATH` | `./data/qyl.duckdb` | DuckDB file path |

### appsettings.json

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5100"
      },
      "Grpc": {
        "Url": "http://localhost:4317",
        "Protocols": "Http2"
      }
    }
  }
}
```

## Verify installation

```bash
# Start collector
cd src/qyl.collector
dotnet run

# In another terminal, check health
curl http://localhost:5100/health
```

Expected response:

```json
{"status": "healthy"}
```

## Next steps

<Card title="Quick Start" icon="rocket" href="/quickstart">
  Send your first traces
</Card>
```

**Step 2: Verify page renders**

Run: `cd docs && mintlify dev`
Expected: Navigate to /installation, page renders correctly

**Step 3: Commit**

```bash
git add docs/installation.mdx
git commit -m "feat(docs): add installation guide"
```

---

## Task 4: Create Architecture Overview

**Files:**
- Create: `docs/architecture/overview.mdx`
- Create: `docs/architecture/collector.mdx`
- Create: `docs/architecture/dashboard.mdx`
- Create: `docs/architecture/mcp.mdx`
- Create: `docs/architecture/protocol.mdx`

**Step 1: Create architecture directory**

```bash
mkdir -p docs/architecture
```

**Step 2: Write architecture overview**

Create `docs/architecture/overview.mdx`:

```mdx
---
title: "Architecture Overview"
description: "Understand qyl's component architecture and data flow"
---

# Architecture Overview

qyl is designed as a modular system with clear separation of concerns.

## Component diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Applications                         │
│              (Instrumented with OpenTelemetry SDKs)             │
└─────────────────────────────────────────────────────────────────┘
                               │
                    OTLP (HTTP/gRPC)
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                        qyl.collector                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ OTLP Ingest  │  │ GenAI Extract│  │  REST/SSE API        │  │
│  │ HTTP + gRPC  │──│ gen_ai.*     │──│  /api/v1/sessions    │  │
│  └──────────────┘  └──────────────┘  │  /api/v1/traces      │  │
│                           │          │  /api/v1/live (SSE)  │  │
│                           ▼          └──────────────────────┘  │
│                    ┌──────────────┐           │                │
│                    │   DuckDB     │           │                │
│                    │   Storage    │───────────┘                │
│                    └──────────────┘                            │
└─────────────────────────────────────────────────────────────────┘
         │                                           │
    HTTP REST                                   HTTP REST
         │                                           │
         ▼                                           ▼
┌──────────────────┐                    ┌──────────────────────┐
│    qyl.mcp       │                    │   qyl.dashboard      │
│   ┌──────────┐   │                    │  ┌────────────────┐  │
│   │ MCP Tools│   │                    │  │   React 19     │  │
│   │ search   │   │                    │  │   TanStack Q5  │  │
│   │ sessions │   │                    │  │   Tailwind 4   │  │
│   └──────────┘   │                    │  └────────────────┘  │
└──────────────────┘                    └──────────────────────┘
         │                                           │
    stdio MCP                                   Browser
         │                                           │
         ▼                                           ▼
┌──────────────────┐                    ┌──────────────────────┐
│  Claude / AI     │                    │      Developer       │
│     Agents       │                    │                      │
└──────────────────┘                    └──────────────────────┘
```

## Components

| Component | Type | Purpose |
|-----------|------|---------|
| [qyl.collector](/architecture/collector) | Web API | OTLP ingestion, storage, REST API |
| [qyl.dashboard](/architecture/dashboard) | React SPA | Real-time visualization |
| [qyl.mcp](/architecture/mcp) | Console | AI agent integration via MCP |
| [qyl.protocol](/architecture/protocol) | Library | Shared types and contracts |

## Data flow

1. **Ingestion**: Apps send OTLP traces to collector (HTTP:5100 or gRPC:4317)
2. **Processing**: Collector extracts `gen_ai.*` attributes into structured fields
3. **Storage**: Spans persisted to embedded DuckDB (columnar, fast analytics)
4. **Query**: Dashboard and MCP query via REST API
5. **Stream**: Live updates via Server-Sent Events (SSE)

## Dependency rules

```
dashboard ──HTTP──► collector ◄──HTTP── mcp
                        │
                        ▼
                    protocol
```

<Warning>
  **Critical**: `qyl.mcp` communicates with `qyl.collector` via HTTP only. No `ProjectReference` allowed.
</Warning>

## Tech stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 / C# 14 |
| Storage | DuckDB (embedded columnar) |
| OTel | Semantic Conventions v1.39.0 |
| Frontend | React 19, Vite 6, Tailwind 4 |
| Testing | xUnit v3 + MTP |
```

**Step 3: Write collector architecture**

Create `docs/architecture/collector.mdx`:

```mdx
---
title: "Collector Architecture"
description: "OTLP ingestion, DuckDB storage, and REST API"
---

# Collector Architecture

The collector is the core of qyl, handling telemetry ingestion, storage, and query APIs.

## Responsibilities

- Accept OTLP traces via HTTP (port 5100) and gRPC (port 4317)
- Extract `gen_ai.*` attributes into dedicated columns
- Store spans in embedded DuckDB
- Expose REST API for queries
- Stream live updates via SSE

## Directory structure

```
src/qyl.collector/
├── Api/                    # REST endpoint handlers
│   ├── QylEndpointExtensions.cs
│   ├── SessionsEndpoint.cs
│   ├── TracesEndpoint.cs
│   └── LiveEndpoint.cs
├── Ingestion/              # OTLP processing
│   ├── OtlpJsonSpanParser.cs
│   ├── GenAiAttributeExtractor.cs
│   └── SpanProcessor.cs
├── Storage/                # DuckDB persistence
│   ├── DuckDbStore.cs
│   ├── DuckDbSchema.g.cs   # Generated
│   └── Queries/
└── Program.cs
```

## Ingestion pipeline

```
OTLP Request
     │
     ▼
┌─────────────────┐
│ OtlpJsonSpan    │  Parse OTLP JSON/Protobuf
│ Parser          │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ GenAi Attribute │  Extract gen_ai.system, tokens, etc.
│ Extractor       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ SpanRecord      │  Create typed record
│ Builder         │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ DuckDbStore     │  INSERT into spans table
│                 │
└─────────────────┘
```

## Storage schema

DuckDB table structure:

| Column | Type | Description |
|--------|------|-------------|
| `span_id` | VARCHAR(16) | Unique span identifier |
| `trace_id` | VARCHAR(32) | Trace correlation |
| `parent_span_id` | VARCHAR(16) | Parent for hierarchy |
| `name` | VARCHAR | Operation name |
| `start_time_unix_nano` | UBIGINT | Start timestamp |
| `end_time_unix_nano` | UBIGINT | End timestamp |
| `duration_ns` | UBIGINT | Computed duration |
| `kind` | TINYINT | Span kind (0-4) |
| `status_code` | TINYINT | Status (0=Unset, 1=Ok, 2=Error) |
| `gen_ai_system` | VARCHAR | AI system (openai, anthropic, etc.) |
| `gen_ai_request_model` | VARCHAR | Model requested |
| `gen_ai_response_model` | VARCHAR | Model used |
| `gen_ai_input_tokens` | INTEGER | Input token count |
| `gen_ai_output_tokens` | INTEGER | Output token count |
| `gen_ai_total_tokens` | INTEGER | Total tokens |
| `attributes_json` | JSON | All span attributes |
| `resource_json` | JSON | Resource attributes |

## API endpoints

See [API Reference](/api-reference) for complete endpoint documentation.
```

**Step 4: Write dashboard architecture**

Create `docs/architecture/dashboard.mdx`:

```mdx
---
title: "Dashboard Architecture"
description: "React 19 SPA for real-time telemetry visualization"
---

# Dashboard Architecture

The dashboard provides real-time visualization of AI observability data.

## Tech stack

| Library | Version | Purpose |
|---------|---------|---------|
| React | 19 | UI framework |
| Vite | 6 | Build tool |
| Tailwind CSS | 4 | Styling |
| TanStack Query | 5 | Data fetching |
| TanStack Router | 1 | Routing |

## Directory structure

```
src/qyl.dashboard/
├── src/
│   ├── components/         # Reusable UI components
│   ├── features/           # Feature modules
│   │   ├── sessions/
│   │   ├── traces/
│   │   └── spans/
│   ├── hooks/              # Custom React hooks
│   ├── types/
│   │   └── api.ts          # Generated from OpenAPI
│   ├── lib/                # Utilities
│   └── main.tsx
├── package.json
└── vite.config.ts
```

## Data fetching

Uses TanStack Query for server state:

```typescript
// Example: Fetch sessions
const { data: sessions } = useQuery({
  queryKey: ['sessions'],
  queryFn: () => fetch('/api/v1/sessions').then(r => r.json()),
});
```

## Real-time updates

SSE subscription for live spans:

```typescript
useEffect(() => {
  const eventSource = new EventSource('/api/v1/live/spans');
  eventSource.onmessage = (event) => {
    const span = JSON.parse(event.data);
    queryClient.invalidateQueries(['spans']);
  };
  return () => eventSource.close();
}, []);
```

## Type generation

Types are auto-generated from OpenAPI:

```bash
npm run generate:ts
# Generates src/types/api.ts from core/openapi/openapi.yaml
```
```

**Step 5: Write MCP architecture**

Create `docs/architecture/mcp.mdx`:

```mdx
---
title: "MCP Architecture"
description: "Model Context Protocol server for AI agent integration"
---

# MCP Architecture

The MCP server enables AI agents like Claude to query their own telemetry.

## What is MCP?

Model Context Protocol (MCP) is a standard for AI agents to interact with external tools. qyl implements an MCP server that exposes telemetry query tools.

## Available tools

| Tool | Description |
|------|-------------|
| `search_sessions` | Find sessions by criteria |
| `get_session` | Get session details and spans |
| `search_traces` | Query traces with filters |
| `get_span` | Get specific span details |

## Directory structure

```
src/qyl.mcp/
├── Tools/                  # MCP tool implementations
│   ├── SearchSessionsTool.cs
│   ├── GetSessionTool.cs
│   └── SearchTracesTool.cs
├── Client/                 # HTTP client to collector
│   └── CollectorClient.cs
└── Program.cs
```

## Communication

```
Claude ──stdio──► qyl.mcp ──HTTP──► qyl.collector
```

<Warning>
  MCP communicates with collector via HTTP REST API only. No direct database access.
</Warning>

## Configuration

Add to Claude Code settings:

```json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/qyl.mcp"],
      "env": {
        "QYL_COLLECTOR_URL": "http://localhost:5100"
      }
    }
  }
}
```
```

**Step 6: Write protocol architecture**

Create `docs/architecture/protocol.mdx`:

```mdx
---
title: "Protocol Architecture"
description: "Shared types and contracts across all components"
---

# Protocol Architecture

The protocol library defines shared types used by all qyl components.

## Design principles

1. **BCL Only**: No external package dependencies
2. **Leaf Dependency**: Other projects depend on protocol, never reverse
3. **Immutable Types**: Records with init-only properties
4. **Strongly-Typed IDs**: Prevent primitive obsession

## Strongly-typed IDs

| Type | Underlying | Format |
|------|------------|--------|
| `SessionId` | `Guid` | `550e8400-e29b-41d4-a716-446655440000` |
| `TraceId` | `ActivityTraceId` | 32-char hex |
| `SpanId` | `ActivitySpanId` | 16-char hex |
| `UnixNano` | `ulong` | Nanoseconds since Unix epoch |

## Core models

### SpanRecord

```csharp
public sealed record SpanRecord
{
    public required SpanId SpanId { get; init; }
    public required TraceId TraceId { get; init; }
    public SpanId? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required UnixNano StartTimeUnixNano { get; init; }
    public required UnixNano EndTimeUnixNano { get; init; }
    public required SpanKind Kind { get; init; }
    public required StatusCode StatusCode { get; init; }
    public GenAiSpanData? GenAi { get; init; }
}
```

### GenAiSpanData

Extracted `gen_ai.*` attributes:

```csharp
public sealed record GenAiSpanData
{
    public string? System { get; init; }           // gen_ai.system
    public string? RequestModel { get; init; }     // gen_ai.request.model
    public string? ResponseModel { get; init; }    // gen_ai.response.model
    public int? InputTokens { get; init; }         // gen_ai.usage.input_tokens
    public int? OutputTokens { get; init; }        // gen_ai.usage.output_tokens
    public decimal? CostUsd { get; init; }         // gen_ai.usage.cost
}
```

## Type ownership rules

| Owner | Types |
|-------|-------|
| `qyl.protocol` | All shared types (SpanRecord, SessionSummary, TraceNode, etc.) |
| `qyl.collector` | DuckDbStore, OtlpJsonSpanParser (internal only) |
| `qyl.dashboard` | React components, generated API types |
| `qyl.mcp` | MCP tool implementations |
```

**Step 7: Verify all pages render**

Run: `cd docs && mintlify dev`
Expected: Navigate to each architecture page, all render correctly

**Step 8: Commit**

```bash
git add docs/architecture/
git commit -m "feat(docs): add architecture documentation"
```

---

## Task 5: Copy and Configure OpenAPI Spec

**Files:**
- Copy: `core/openapi/openapi.yaml` → `docs/openapi.yaml`

**Step 1: Copy OpenAPI spec to docs**

```bash
cp core/openapi/openapi.yaml docs/openapi.yaml
```

**Step 2: Verify API reference generates**

Run: `cd docs && mintlify dev`
Expected: Navigate to /api-reference, see auto-generated endpoint docs

**Step 3: Commit**

```bash
git add docs/openapi.yaml
git commit -m "feat(docs): add OpenAPI spec for API reference"
```

---

## Task 6: Create API Reference Overview

**Files:**
- Create: `docs/api-reference/index.mdx`
- Update: `docs/docs.json` (add API reference navigation)

**Step 1: Create API reference directory**

```bash
mkdir -p docs/api-reference
```

**Step 2: Write API overview**

Create `docs/api-reference/index.mdx`:

```mdx
---
title: "API Reference"
description: "REST API for querying sessions, traces, and spans"
---

# API Reference

The qyl collector exposes a REST API for querying telemetry data.

## Base URL

```
http://localhost:5100
```

## Authentication

Most endpoints require Bearer token authentication:

```bash
curl -H "Authorization: Bearer <token>" \
  http://localhost:5100/api/v1/sessions
```

## Endpoints

### Sessions

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/sessions` | List sessions |
| GET | `/api/v1/sessions/{sessionId}` | Get session details |
| GET | `/api/v1/sessions/{sessionId}/spans` | Get session spans |

### Traces

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/v1/traces` | List traces |
| GET | `/v1/traces/{traceId}` | Get trace details |
| GET | `/v1/traces/{traceId}/spans` | Get trace spans |
| POST | `/v1/traces/search` | Search traces |

### Logs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/v1/logs` | Query logs |
| POST | `/v1/logs/search` | Search logs |
| POST | `/v1/logs/aggregate` | Aggregate logs |

### Streaming

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/live` | SSE stream for all events |
| GET | `/api/v1/live/spans` | SSE stream for spans |

## Pagination

List endpoints support cursor-based pagination:

```bash
curl "http://localhost:5100/api/v1/sessions?limit=10&cursor=abc123"
```

Response includes `nextCursor` for subsequent pages.

## Error responses

All errors follow this format:

```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Session not found"
  }
}
```
```

**Step 3: Update docs.json to add API reference tab**

The API reference is auto-generated from OpenAPI. Add specific pages to navigation in `docs/docs.json` under the `tabs` section:

```json
{
  "tabs": [
    {
      "name": "Documentation",
      "url": "/"
    },
    {
      "name": "API Reference",
      "url": "/api-reference",
      "openapi": "openapi.yaml"
    }
  ]
}
```

**Step 4: Verify API reference**

Run: `cd docs && mintlify dev`
Expected: API Reference tab shows auto-generated endpoint docs from OpenAPI

**Step 5: Commit**

```bash
git add docs/api-reference/ docs/docs.json
git commit -m "feat(docs): add API reference overview"
```

---

## Task 7: Create Guides Section

**Files:**
- Create: `docs/guides/instrumenting-apps.mdx`
- Create: `docs/guides/ai-agent-integration.mdx`

**Step 1: Create guides directory**

```bash
mkdir -p docs/guides
```

**Step 2: Write instrumenting apps guide**

Create `docs/guides/instrumenting-apps.mdx`:

```mdx
---
title: "Instrumenting Applications"
description: "Add OpenTelemetry instrumentation to send traces to qyl"
---

# Instrumenting Applications

This guide shows how to instrument your applications to send traces to qyl.

## .NET Applications

### Install packages

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

### Configure tracing

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("my-app")
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4317");
        options.Protocol = OtlpExportProtocol.Grpc;
    })
    .Build();
```

### Add GenAI attributes

For AI operations, add semantic convention attributes:

```csharp
using var activity = activitySource.StartActivity("llm.call");

// System identification
activity?.SetTag("gen_ai.system", "openai");
activity?.SetTag("gen_ai.operation.name", "chat");

// Model information
activity?.SetTag("gen_ai.request.model", "gpt-4");
activity?.SetTag("gen_ai.response.model", "gpt-4-0613");

// Token usage
activity?.SetTag("gen_ai.usage.input_tokens", 150);
activity?.SetTag("gen_ai.usage.output_tokens", 500);
activity?.SetTag("gen_ai.usage.total_tokens", 650);
```

## Python Applications

### Install packages

```bash
pip install opentelemetry-api opentelemetry-sdk opentelemetry-exporter-otlp
```

### Configure tracing

```python
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter

provider = TracerProvider()
processor = BatchSpanProcessor(OTLPSpanExporter(endpoint="localhost:4317"))
provider.add_span_processor(processor)
trace.set_tracer_provider(provider)

tracer = trace.get_tracer("my-app")
```

### Add GenAI attributes

```python
with tracer.start_as_current_span("llm.call") as span:
    span.set_attribute("gen_ai.system", "openai")
    span.set_attribute("gen_ai.request.model", "gpt-4")
    span.set_attribute("gen_ai.usage.input_tokens", 150)
    span.set_attribute("gen_ai.usage.output_tokens", 500)
```

## Supported attributes

See [GenAI Attributes](/schema/genai-attributes) for the complete list of supported `gen_ai.*` attributes.
```

**Step 3: Write AI agent integration guide**

Create `docs/guides/ai-agent-integration.mdx`:

```mdx
---
title: "AI Agent Integration"
description: "Enable Claude and other AI agents to query telemetry via MCP"
---

# AI Agent Integration

qyl includes an MCP (Model Context Protocol) server that lets AI agents query their own telemetry.

## What is MCP?

MCP is a protocol that allows AI agents to call external tools. With qyl's MCP server, Claude can:

- Search its previous sessions
- Inspect specific traces and spans
- Analyze token usage patterns
- Debug issues in AI workflows

## Setup

### Prerequisites

1. qyl collector running at http://localhost:5100
2. Claude Code installed

### Configure Claude Code

Add to your Claude Code settings (`~/.claude/settings.json`):

```json
{
  "mcpServers": {
    "qyl": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/qyl/src/qyl.mcp"],
      "env": {
        "QYL_COLLECTOR_URL": "http://localhost:5100"
      }
    }
  }
}
```

### Verify connection

In Claude Code, run:

```
/mcp
```

You should see `qyl` listed with its available tools.

## Available tools

### search_sessions

Find sessions by criteria:

```
Search for my sessions from the last hour
```

### get_session

Get detailed session information:

```
Show me the spans from session 550e8400-e29b-41d4-a716-446655440000
```

### search_traces

Query traces with filters:

```
Find traces where gen_ai.system is "anthropic"
```

## Example queries

### Debug a slow request

```
Claude, find my traces from the last 10 minutes where duration > 5 seconds
```

### Analyze token usage

```
Show me the total tokens used in my session today
```

### Inspect errors

```
Find traces with status_code = ERROR from my last session
```
```

**Step 4: Verify guides render**

Run: `cd docs && mintlify dev`
Expected: Navigate to /guides/, both pages render correctly

**Step 5: Commit**

```bash
git add docs/guides/
git commit -m "feat(docs): add instrumentation and MCP integration guides"
```

---

## Task 8: Create Schema Reference

**Files:**
- Create: `docs/schema/types.mdx`
- Create: `docs/schema/genai-attributes.mdx`
- Create: `docs/schema/otel-compliance.mdx`

**Step 1: Create schema directory**

```bash
mkdir -p docs/schema
```

**Step 2: Write types reference**

Create `docs/schema/types.mdx`:

```mdx
---
title: "Type Reference"
description: "Strongly-typed IDs and core data models"
---

# Type Reference

qyl uses strongly-typed IDs to prevent primitive obsession and improve type safety.

## Strongly-typed IDs

### SessionId

Uniquely identifies a telemetry session.

| Property | Value |
|----------|-------|
| Underlying type | `Guid` |
| Format | UUID v4 |
| Example | `550e8400-e29b-41d4-a716-446655440000` |

### TraceId

OpenTelemetry trace identifier.

| Property | Value |
|----------|-------|
| Underlying type | `ActivityTraceId` |
| Format | 32-character hex |
| Example | `0af7651916cd43dd8448eb211c80319c` |

### SpanId

OpenTelemetry span identifier.

| Property | Value |
|----------|-------|
| Underlying type | `ActivitySpanId` |
| Format | 16-character hex |
| Example | `b7ad6b7169203331` |

### UnixNano

Timestamp in nanoseconds since Unix epoch.

| Property | Value |
|----------|-------|
| Underlying type | `ulong` |
| Format | Nanoseconds |
| Example | `1704067200000000000` |

## Core models

### SpanRecord

Complete span representation:

```csharp
public sealed record SpanRecord
{
    public required SpanId SpanId { get; init; }
    public required TraceId TraceId { get; init; }
    public SpanId? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required UnixNano StartTimeUnixNano { get; init; }
    public required UnixNano EndTimeUnixNano { get; init; }
    public required SpanKind Kind { get; init; }
    public required StatusCode StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public GenAiSpanData? GenAi { get; init; }
    public IReadOnlyDictionary<string, object>? Attributes { get; init; }
    public IReadOnlyDictionary<string, object>? Resource { get; init; }
}
```

### SessionSummary

Aggregated session metrics:

```csharp
public sealed record SessionSummary
{
    public required SessionId SessionId { get; init; }
    public required UnixNano StartTime { get; init; }
    public required UnixNano EndTime { get; init; }
    public required int SpanCount { get; init; }
    public required int ErrorCount { get; init; }
    public int? TotalInputTokens { get; init; }
    public int? TotalOutputTokens { get; init; }
}
```
```

**Step 3: Write GenAI attributes reference**

Create `docs/schema/genai-attributes.mdx`:

```mdx
---
title: "GenAI Attributes"
description: "OpenTelemetry gen_ai.* semantic conventions supported by qyl"
---

# GenAI Attributes

qyl extracts and indexes `gen_ai.*` attributes from OpenTelemetry spans for fast querying.

## Supported attributes

Based on OpenTelemetry Semantic Conventions v1.39.0.

### System identification

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.system` | string | AI system (openai, anthropic, azure, etc.) |
| `gen_ai.operation.name` | string | Operation type (chat, completion, embedding) |

### Request attributes

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.request.model` | string | Model requested |
| `gen_ai.request.max_tokens` | int | Max tokens requested |
| `gen_ai.request.temperature` | float | Sampling temperature |
| `gen_ai.request.top_p` | float | Nucleus sampling parameter |
| `gen_ai.request.top_k` | int | Top-k sampling parameter |
| `gen_ai.request.stop_sequences` | string[] | Stop sequences |
| `gen_ai.request.frequency_penalty` | float | Frequency penalty |
| `gen_ai.request.presence_penalty` | float | Presence penalty |

### Response attributes

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.response.model` | string | Model actually used |
| `gen_ai.response.id` | string | Response ID |
| `gen_ai.response.finish_reasons` | string[] | Completion reasons |

### Token usage

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.usage.input_tokens` | int | Input token count |
| `gen_ai.usage.output_tokens` | int | Output token count |
| `gen_ai.usage.total_tokens` | int | Total tokens (computed if not provided) |

### Cost tracking

| Attribute | Type | Description |
|-----------|------|-------------|
| `gen_ai.usage.cost` | decimal | Cost in USD |

## Indexed columns

These attributes are extracted to dedicated DuckDB columns for fast filtering:

- `gen_ai_system`
- `gen_ai_request_model`
- `gen_ai_response_model`
- `gen_ai_input_tokens`
- `gen_ai_output_tokens`
- `gen_ai_total_tokens`
- `gen_ai_cost_usd`

All other attributes remain in `attributes_json` for flexible querying.
```

**Step 4: Write OTel compliance reference**

Create `docs/schema/otel-compliance.mdx`:

```mdx
---
title: "OTel Compliance"
description: "OpenTelemetry specification compliance and semantic convention coverage"
---

# OTel Compliance

qyl implements OpenTelemetry specifications for interoperability with the observability ecosystem.

## Protocol support

| Protocol | Version | Status |
|----------|---------|--------|
| OTLP/gRPC | 1.0 | Supported |
| OTLP/HTTP JSON | 1.0 | Supported |
| OTLP/HTTP Protobuf | 1.0 | Planned |

## Semantic conventions

Based on OpenTelemetry Semantic Conventions v1.39.0.

### Supported domains

| Domain | Prefix | Coverage |
|--------|--------|----------|
| GenAI | `gen_ai.*` | Full |
| HTTP | `http.*` | Full |
| Database | `db.*` | Full |
| RPC | `rpc.*` | Full |
| Messaging | `messaging.*` | Full |
| Network | `network.*` | Partial |
| Process | `process.*` | Partial |

### GenAI coverage

All `gen_ai.*` attributes from v1.39.0 are supported:

- System identification (`gen_ai.system`, `gen_ai.operation.name`)
- Request parameters (`gen_ai.request.*`)
- Response data (`gen_ai.response.*`)
- Token usage (`gen_ai.usage.*`)

## Data model

qyl stores spans in the OTLP data model:

- `SpanId` (16-char hex)
- `TraceId` (32-char hex)
- `ParentSpanId` (optional)
- `Name` (operation name)
- `Kind` (Internal, Server, Client, Producer, Consumer)
- `StartTimeUnixNano` / `EndTimeUnixNano`
- `Status` (Unset, Ok, Error)
- `Attributes` (key-value pairs)
- `Resource` (service metadata)
- `Events` (span events)
- `Links` (span links)
```

**Step 5: Verify schema pages render**

Run: `cd docs && mintlify dev`
Expected: Navigate to /schema/, all pages render correctly

**Step 6: Commit**

```bash
git add docs/schema/
git commit -m "feat(docs): add schema reference documentation"
```

---

## Task 9: Create Logo Assets

**Files:**
- Create: `docs/logo/qyl-light.svg`
- Create: `docs/logo/qyl-dark.svg`

**Step 1: Create logo directory**

```bash
mkdir -p docs/logo
```

**Step 2: Create light mode logo**

Create `docs/logo/qyl-light.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40">
  <text x="10" y="30" font-family="system-ui, -apple-system, sans-serif" font-size="28" font-weight="700" fill="#0D9373">qyl</text>
</svg>
```

**Step 3: Create dark mode logo**

Create `docs/logo/qyl-dark.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40">
  <text x="10" y="30" font-family="system-ui, -apple-system, sans-serif" font-size="28" font-weight="700" fill="#07C983">qyl</text>
</svg>
```

**Step 4: Verify logos display**

Run: `cd docs && mintlify dev`
Expected: Logo appears in header, correct color for light/dark mode

**Step 5: Commit**

```bash
git add docs/logo/
git commit -m "feat(docs): add logo assets"
```

---

## Task 10: Final Integration and Verification

**Files:**
- Update: `docs/docs.json` (final navigation structure)

**Step 1: Update final docs.json**

Update `docs/docs.json` with complete navigation:

```json
{
  "$schema": "https://mintlify.com/docs.json",
  "name": "qyl",
  "logo": {
    "dark": "/logo/qyl-dark.svg",
    "light": "/logo/qyl-light.svg"
  },
  "favicon": "/favicon.svg",
  "colors": {
    "primary": "#0D9373",
    "light": "#07C983",
    "dark": "#0D9373",
    "anchors": {
      "from": "#0D9373",
      "to": "#07C983"
    }
  },
  "topbarLinks": [
    {
      "name": "GitHub",
      "url": "https://github.com/ancplua/qyl"
    }
  ],
  "topbarCtaButton": {
    "name": "Dashboard",
    "url": "http://localhost:5173"
  },
  "tabs": [
    {
      "name": "Documentation",
      "url": "/"
    },
    {
      "name": "API Reference",
      "url": "/api-reference",
      "openapi": "openapi.yaml"
    }
  ],
  "navigation": {
    "groups": [
      {
        "group": "Getting Started",
        "pages": ["index", "quickstart", "installation"]
      },
      {
        "group": "Architecture",
        "pages": [
          "architecture/overview",
          "architecture/collector",
          "architecture/dashboard",
          "architecture/mcp",
          "architecture/protocol"
        ]
      },
      {
        "group": "Guides",
        "pages": [
          "guides/instrumenting-apps",
          "guides/ai-agent-integration"
        ]
      },
      {
        "group": "Schema",
        "pages": [
          "schema/types",
          "schema/genai-attributes",
          "schema/otel-compliance"
        ]
      },
      {
        "group": "API Reference",
        "pages": ["api-reference/index"]
      }
    ]
  },
  "footerSocials": {
    "github": "https://github.com/ancplua/qyl"
  },
  "api": {
    "baseUrl": "http://localhost:5100",
    "auth": {
      "method": "bearer"
    }
  },
  "openapi": "openapi.yaml"
}
```

**Step 2: Full verification**

Run: `cd docs && mintlify dev`

Verify:
- [ ] Landing page loads at /
- [ ] Quick Start at /quickstart
- [ ] Installation at /installation
- [ ] All architecture pages load
- [ ] All guides load
- [ ] All schema pages load
- [ ] API Reference tab shows OpenAPI-generated docs
- [ ] Logo displays correctly
- [ ] Navigation works in all sections

**Step 3: Final commit**

```bash
git add docs/
git commit -m "feat(docs): complete Mintlify documentation site"
```

---

## Summary

This plan creates a complete Mintlify documentation site for qyl with:

- **14 MDX pages** covering getting started, architecture, guides, and schema
- **Auto-generated API reference** from existing OpenAPI spec
- **Proper navigation structure** following Diátaxis framework
- **Brand assets** (logos, favicon, colors)

Total estimated files: 17 (14 MDX + 2 logos + 1 favicon + 1 config)

---

Plan complete and saved to `docs/plans/2026-01-15-mintlify-docs-site.md`. Two execution options:

**1. Subagent-Driven (this session)** - I dispatch fresh subagent per task, review between tasks, fast iteration

**2. Parallel Session (separate)** - Open new session with executing-plans, batch execution with checkpoints

Which approach?

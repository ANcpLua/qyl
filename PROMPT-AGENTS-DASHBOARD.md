# Prompt: Build the Agents Overview Dashboard for qyl.dashboard

## What you are building

You are redesigning the `/agents` route in qyl.dashboard to match the quality and feature set of a production-grade AI agent observability dashboard. The reference is Vercel/Browserbase's "Agents" Insights page — the gold standard for agentic telemetry UX.

This is NOT a greenfield build. qyl.dashboard already has:
- `/agents` page (AgentRunsPage) with agent run list and tool call sequences
- `/genai` page with token tracking, cost analysis, provider stats
- `/traces` page with span hierarchy and waterfall timeline
- Full OTel 1.39 semantic conventions (57+ GenAI attributes) in `src/lib/semconv.ts`
- DuckDB backend with `spans`, `agent_runs`, `tool_calls` tables
- TanStack Query for data fetching, Recharts for charts, Radix UI + shadcn components
- SSE live streaming via `/api/v1/live`
- React 19 + Vite + TypeScript + Tailwind CSS 4

Your job is to reason through what changes are needed to transform the existing `/agents` page into a complete agent observability experience that looks like a real product team built it — not a prototype.

---

## Where the traces come from (instrumentation sources)

Before building the dashboard, understand what data flows into qyl and how:

### Source 1: Vercel AI SDK (`ai` npm package)

The Vercel AI SDK emits OpenTelemetry spans when `experimental_telemetry` is enabled:

```typescript
import { generateText } from "ai";

const result = await generateText({
  model: anthropic("claude-sonnet-4-5-20250929"),
  prompt: "...",
  experimental_telemetry: { isEnabled: true },
});
```

This produces spans with these exact names and attributes:
- **`ai.generateText`** — root LLM call span
  - `gen_ai.request.model`: "claude-sonnet-4-5-20250929"
  - `gen_ai.usage.input_tokens`: 1423
  - `gen_ai.usage.output_tokens`: 892
  - `gen_ai.response.finish_reasons`: ["stop"]
  - `ai.operationId`: "ai.generateText"
- **`ai.generateText.doGenerate`** — actual provider API call
  - `gen_ai.response.model`: "claude-sonnet-4-5-20250929"
  - `ai.response.avgCompletionTokensPerSecond`: 45.2
- **`ai.toolCall`** — each tool invocation
  - `ai.toolCall.name`: "searchDatabase"
  - `ai.toolCall.id`: "call_abc123"
  - `ai.toolCall.args`: '{"query": "..."}'

Same pattern for `streamText`, `generateObject`, `streamObject`. Each produces nested spans.

### Source 2: Claude Code (CLI agent sessions)

Claude Code emits OTLP traces when configured with an endpoint. Each session produces:
- **`runner.start-build`** — root span for the entire session
- **`claude-code - invoke_agent (claude-haiku-4-5)`** — agent invocation span
  - `gen_ai.agent.name`: "claude-code"
  - `gen_ai.request.model`: "claude-haiku-4-5"
  - Duration shown as bar in waterfall (e.g., "1.03m")
- **`chat claude-haiku-4... - 527k Tokens ($0.0314)`** — LLM completion span
  - Tokens and cost embedded in span name
  - `gen_ai.usage.input_tokens` + `gen_ai.usage.output_tokens`
  - `gen_ai.response.cost_usd`: 0.0314
- **`Read - execute_tool`** — tool execution (0.05-0.12ms typical)
- **`Bash - execute_tool`** — shell command execution
- **`Edit - execute_tool`** — file edit
- **`Write - execute_tool`** — file write
- **`TodoWrite - execute_tool`** — task tracking
- **`KillShell - execute_tool`** — process termination
- **`BashOutput - execute_tool`** — shell output capture
- **`generateText - 14k Tokens ($0.0149)`** — nested LLM call (sub-agent)
- **`generate_text_claud... - 14k Tokens ($0.0149)`** — provider-level call

The nesting is: `runner.start-build` → `invoke_agent` → tool calls + nested `invoke_agent` for sub-agents → their own tool calls. This is how multi-agent orchestration appears as a trace tree.

### Source 3: Any OTLP-compatible AI framework

OpenLLMetry, LangSmith, LiteLLM, Traceloop — anything that exports `gen_ai.*` attributes over OTLP gRPC (:4317) or HTTP (:5100) flows into qyl's DuckDB.

### What qyl captures and stores

All traces arrive via OTLP and land in these DuckDB tables:

| Table | What it stores | Key columns |
|-------|---------------|-------------|
| `spans` | Every span from every trace | `span_id`, `trace_id`, `parent_span_id`, `name`, `kind`, `start_time_unix_nano`, `end_time_unix_nano`, `status_code`, `attributes` (JSON) |
| `agent_runs` | Extracted agent invocations | `run_id`, `trace_id`, `agent_name`, `model`, `status`, `input_tokens`, `output_tokens`, `total_cost`, `tool_call_count` |
| `tool_calls` | Individual tool executions | `call_id`, `run_id`, `tool_name`, `arguments_json`, `result_json`, `status`, `duration_ns`, `sequence_number` |
| `logs` | Structured log records | `trace_id`, `span_id`, `severity_number`, `body`, `attributes` |

The write path: OTLP → parse protobuf → batch insert (100 spans/batch) → DuckDB. Visible in <1ms.

### Attribute extraction pattern

The dashboard must extract structured data from the `attributes` JSON column. The attributes are stored as a JSON array of `{"key": "...", "value": {"stringValue": "..."}}` objects (OTLP format). To query:

```sql
-- Extract model name from span attributes
SELECT
  span_id,
  json_extract_string(attr, '$.value.stringValue') as model
FROM spans, unnest(json_extract(attributes, '$[*]')) as t(attr)
WHERE json_extract_string(attr, '$.key') = 'gen_ai.request.model'
```

Or if attributes are pre-flattened to a JSON object:
```sql
SELECT attributes->>'gen_ai.request.model' as model FROM spans
```

Check which format qyl uses before writing queries.

---

## The exact reference UI (pixel-level from screenshots)

### Page header

```
Agents                                    [fullscreen icon]
─────────────────────────────────────────────────────────
Overview    Models    Tools              ← tab bar
```

"Agents" is the page title (large, white, left-aligned). Three tabs below it. "Overview" is active (underlined or highlighted). A fullscreen toggle icon sits top-right.

### Filters bar (below tabs)

```
[My Projects ▾]  [All Envs ▾]  [Oct 18, 2025-Jan 1 ▾]  🔍 Search for spans, users, tags, and more
```

Four elements in a row. Dropdowns have chevrons. Date range shows the exact selected range. Search is a full-width input with placeholder text and search icon.

### Overview grid (exact layout from screenshots)

The grid is NOT a uniform 3x2. It is:

```
┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────┐
│      Traffic        │ │      Duration       │ │   Issues    │
│                     │ │                     │ │             │
│  Runs ☑ Error Rate  │ │  avg(span.duration) │ │ N Error:    │
│  ☑ Releases ☑      │ │  ☑ p95(span.dur.)   │ │   aborted   │
│                     │ │  ☑ Releases ☑       │ │ N TypeError │
│  [bar chart with    │ │                     │ │   :fetch... │
│   purple bars +     │ │  [line chart with   │ │ ● TypeError │
│   red error rate    │ │   green avg line +  │ │   :fetch... │
│   line overlay +    │ │   blue p95 line]    │ │ N Reference │
│   right Y-axis %]   │ │                     │ │   Error:... │
│                     │ │                     │ │ N Error:    │
│  Nov 1st  Dec 1st   │ │  Nov 1st  Jan 1st   │ │   failed to │
│          Jan 1st    │ │                     │ │   pi...     │
└─────────────────────┘ └─────────────────────┘ └─────────────┘

┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────────────┐
│     LLM Calls       │ │    Tokens Used      │ │    Tool Calls       │
│                     │ │                     │ │                     │
│  [stacked bar chart │ │  [stacked bar chart │ │  [stacked bar chart │
│   by model]         │ │   by model]         │ │   by tool name]     │
│                     │ │                     │ │                     │
│  Nov 1st  Dec 1st   │ │  Nov 1st  Dec 1st   │ │  Nov 1st  Dec 1st   │
│          Jan 1st    │ │          Jan 1st    │ │          Jan 1st    │
│                     │ │                     │ │                     │
│ ■ claude-haiku  880 │ │ ■ claude-haiku 355m │ │ ■ TodoWrite   2.7k │
│ ■ gpt-5-codex  368 │ │ ■ claude-opus  117m │ │ ■ Read        2.6k │
│ ■ claude-sonn. 216 │ │ ■ claude-sonn.  68m │ │ ■ Edit        2.0k │
└─────────────────────┘ └─────────────────────┘ └─────────────────────┘
```

Key visual details:
- Top row: Traffic and Duration are wide (roughly 40% each), Issues is narrow (roughly 20%) and is a LIST not a chart
- Bottom row: three equal-width panels
- Each chart has checkbox toggles in the legend (☑ Runs, ☑ Error Rate, ☑ Releases)
- Bar charts use purple/blue for primary series, red for errors
- Model legends show colored squares (■) with model name + total count
- Token counts use compact notation: "355m" = 355 million, "117m" = 117 million
- Tool call counts: "2.7k" = 2,700
- Issues panel shows severity icon (N = error, ● = warning) + truncated error message + colored status dot (green = resolved, red = active)

### Trace list table (below the grid)

```
TRACE ID    AGENTS / TRACE ROOT    ROOT DURATION    ERRORS    LLM CALLS    TOOL CALLS    TOTAL TOKENS    TOTAL COST    TIMESTAMP ↓
─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
e68d3888    [claude-code]          2.15min          0         0            7             0               —             2wk ago
10ad645a    GET /api/projects/[id] 1.93hr           0         0            1             0               —             2wk ago
14b71d6f    [claude-code]          6.19min          72        0            4             0               —             2wk ago
b3089e4f    GET /api/projects/[i.. 4.67min          48        2            1             29k             $0.0151       2wk ago
6edbf211    POST /api/projects/[.. 5.15min          104       0            5             0               —             2wk ago
71098dc3    /                      2.79min          40        0            2             0               —             2wk ago
6ee7316c    GET /api/projects/[i.. 43.41min         8         0            8             0               —             2wk ago
ec81f29c    GET /api/projects/[i.. 3.89min          0         1            0             15k             $0.0157       2wk ago
96be7a85    POST /api/runner/ev..  6.20min          0         1            0             210k            $0.0101       2wk ago
```

Key details:
- TRACE ID is monospace, purple/link colored, 8 characters
- Agent names appear as dark badges: `[claude-code]` with rounded corners, dark background
- Non-agent traces show their HTTP route as plain text
- ERRORS column: numbers are colored (red/orange when > 0, muted when 0)
- LLM CALLS: green-tinted numbers
- TOTAL COST: "$X.XXXX" format or "—" dash when zero/unknown
- TIMESTAMP: relative time, right-aligned
- Column header "TIMESTAMP ↓" shows active sort direction
- Table rows are hoverable with subtle highlight

### Abbreviated Trace View (slide-in panel from right)

When you click a trace ID, a panel slides in covering roughly 60% of the page width:

```
✕ Close    Abbreviated Trace
─────────────────────────────────────────────────────────────────

AI Spans                                    │ Span
                                            │
runner.start-build                          │ ID: 03afcd7872fa4f19  📋
                                            │
┌─ claude-code - invoke_agent               │ gen_ai.invoke_agent
│  (claude-haiku-4-5)          1.03m ───    │ 1.03min
│                                           │
│  ┌─ chat claude-haiku-4... -              │ Agent Name   claude-code
│  │  527k Tokens ($0.0314)    59.16s ──    │ Model        claude-h...
│                                           │ Tokens       106 in + 6.2...
│  ● Read - execute_tool        0.11ms      │ Cost         $0.0314
│  ● Read - execute_tool        0.06ms      │ Available Tools > [ 18 items ]
│  ● Bash - execute_tool        0.06ms      │
│  ● Read - execute_tool        0.05ms      │ ▼ Output
│  ● Read - execute_tool        0.05ms      │   Response
│  ● Bash - execute_tool        0.05ms      │   Build complete! Created a c...
│  ● Read - execute_tool        0.06ms      │   professional light design. T...
│  ● Read - execute_tool        0.05ms      │   interactive demo with state...
│  ● Read - execute_tool        0.05ms      │   grid, and test information c...
│  ● Read - execute_tool        0.03ms      │   ready on port 3200.
│  ● TodoWrite - execute_tool   0.12ms      │
│  ● Bash - execute_tool        0.05ms      │ ▼ Attributes
│  ● TodoWrite - execute_tool   0.07ms      │   🔍 Search
│  ● Bash - execute_tool        0.05ms      │   span
│  ● Edit - execute_tool        0.07ms      │     description    i...
│  ● TodoWrite - execute_tool   0.05ms      │     duration       6...
│  ● Bash - execute_tool        0.05ms      │     name           g...
│  ● BashOutput - execute_tool  0.04ms      │     op             ...
│  ● KillShell - execute_tool   0.05ms      │     self_time      ...
│  ● TodoWrite - execute_tool   0.04ms      │
│  ● TodoWrite - execute_tool   0.05ms      │
│                                           │
│  ■ generateText               2.56s ──    │
│    14k Tokens ($0.0149)                   │
│  ■ generate_text_claud...     2.56s ──    │
│    14k Tokens ($0.0149)                   │
│                                           │
│  ┌─ claude-code - invoke_agent 2.56s ──   │
│  │  (claude-haiku-4-5)                    │
│  │  ● chat claude-haiku-4..  12.89ms      │
│  │    14k Tokens (<$0.01)                 │
```

Key visual details:
- "✕ Close" button + "Abbreviated Trace" header at top
- Left panel: "AI Spans" header, hierarchical tree with indentation
- Green dots (●) for successful tool calls, duration right-aligned in gray
- Agent invocation spans shown as bordered blocks with model name in parentheses
- LLM chat spans show token count + cost inline: "527k Tokens ($0.0314)"
- Nested agent calls show the recursive structure (agent → tools → sub-agent → sub-tools)
- Duration bars: horizontal bars proportional to time (like a Gantt chart / flame graph)
- Right panel: Span metadata card with labeled fields
- "Available Tools > [ 18 items ]" is expandable
- Output section shows the LLM response text, collapsible
- Attributes section has a search input and shows key-value pairs

### Left sidebar (model filter, visible in trace view)

When the trace view is open, the left sidebar shows a model breakdown:

```
■ claude-haiku-4-5
■ gpt-5-codex
■ claude-sonnet-4-5

TRACE ID    AGENTS / TRACE ROOT
e68d3888    [claude-code]
10ad645a    GET /api/projects/[id]
14b71d6f    [claude-code]
b3089e4f    GET /api/projects/[i...
6edbf211    POST /api/projects/[...
71098dc3    /
6ee7316c    GET /api/projects/[i...
```

Model names with colored squares act as filters. The trace list is compressed to just ID + route.

---

## The target experience (reason through each)

### 1. Agents Overview Tab

A single page with 6 synchronized panels, all respecting the same filters:

**Filters bar** (top):
- Project selector (dropdown, "My Projects" default)
- Environment selector (dropdown, "All Envs" default)
- Date range picker (presets: 24h, 7d, 30d, custom range)
- Search bar ("Search for spans, users, tags, and more")

**Panel layout** (2-wide + sidebar top row, 3 equal bottom row):

| Panel | X-axis | Y-axis | Series | Notes |
|-------|--------|--------|--------|-------|
| Traffic | Time buckets | Count | Runs (bar), Error Rate (line, right axis), Releases (vertical markers) | Dual-axis chart. Error rate as percentage overlay. Purple bars, red line |
| Duration | Time buckets | Duration | avg(span.duration) (line), p95(span.duration) (line), Releases (markers) | Green + blue lines, legend toggleable |
| Issues | — | — | Top 5 errors by frequency | Card list, not chart. Severity icon (N/●) + error message + status dot. Clickable → `/issues/:id` |
| LLM Calls | Time buckets | Count | Stacked bars by model | Legend below: colored square + model name + total count |
| Tokens Used | Time buckets | Token count | Stacked bars by model | Same model breakdown. Compact notation: "355m", "117m", "68m" |
| Tool Calls | Time buckets | Count | Stacked bars by tool name | Top N tools by frequency: TodoWrite, Read, Edit, Bash, etc. |

**Reasoning questions you must answer:**
- What SQL queries against DuckDB produce each panel's data? Think about `time_bucket` or `date_trunc` for grouping. Check DuckDB docs for the exact function.
- How do you compute error rate? `COUNT(CASE WHEN status_code = 2 THEN 1 END)::FLOAT / COUNT(*) * 100` per bucket.
- How do you get p95 duration? DuckDB has `quantile_cont(duration, 0.95)`.
- Where do "Releases" come from? qyl doesn't have deploy tracking yet. Either skip release markers or design a minimal deploy annotation table (`version`, `timestamp`, `service`).
- How do you determine which model was used per span? Extract `gen_ai.request.model` from the attributes JSON. Check if qyl stores attributes as OTLP array format or pre-flattened JSON object.
- The filter state must be shared across all panels via URL search params (`?project=X&env=Y&from=Z&to=W`). TanStack Query keys include filter values so cache invalidates on filter change.

### 2. Trace List Table (below the charts)

Columns:
| Column | Source | Format |
|--------|--------|--------|
| TRACE ID | `trace_id` (first 8 chars) | Monospace, purple link color, clickable |
| AGENTS / TRACE ROOT | Root span `gen_ai.agent.name` or span `name` | Dark badge for agents, plain text for HTTP routes |
| ROOT DURATION | `end_time - start_time` of root span | "2.15min", "1.93hr", "43.41min" |
| ERRORS | Count of spans with `status_code = 2` in trace | Red when > 0, muted gray "0" otherwise |
| LLM CALLS | Count of spans with `gen_ai.*` operation names | Green-tinted number |
| TOOL CALLS | Count of `*execute_tool` spans | Plain number |
| TOTAL TOKENS | Sum of `gen_ai.usage.input_tokens + output_tokens` | "29k", "210k", "15k" or "0" |
| TOTAL COST | Sum of `gen_ai.response.cost_usd` across spans | "$0.0151" or "—" |
| TIMESTAMP | Root span `start_time` | Relative: "2wk ago", "3h ago", sorted descending |

**Reasoning questions:**
- This requires a backend endpoint that aggregates per-trace. The SQL must GROUP BY trace_id and compute all columns in a single query. Sketch it before implementing.
- Pagination: cursor-based (keyed on `start_time DESC, trace_id`) is more stable for time-series data than offset.
- Agent detection: a trace is an "agent trace" when the root span (or first child) has `gen_ai.agent.name` set. HTTP route traces show the span name directly.

### 3. Abbreviated Trace View (slide-in panel)

When clicking a trace ID, a panel slides in from the right (60% width):

**Left: AI Spans waterfall**
- Show ONLY AI-relevant spans from the trace. Filter criteria:
  - Span name contains `invoke_agent` → agent invocation
  - Span name contains `execute_tool` → tool call
  - Span name contains `chat` or `generateText` or `generate_text` → LLM completion
  - Span has `gen_ai.usage.input_tokens` attribute → LLM call
- Tree structure preserved — indentation shows parent-child
- Each tool call span: green dot (●) + name (e.g., "Read - execute_tool") + duration right-aligned
- Agent invocation spans: bordered block with model name in parentheses, duration as horizontal bar
- LLM completion spans: pink/red colored, show "527k Tokens ($0.0314)" inline
- Nested agents visible: agent → tools → sub-agent → sub-tools (recursive)

**Right: Span detail panel**
When a span is selected (clicked) in the waterfall:
- **Span ID**: hex string with copy button
- **Span name**: e.g., `gen_ai.invoke_agent`
- **Duration**: "1.03min"
- **Agent Name**: extracted from `gen_ai.agent.name` attribute
- **Model**: extracted from `gen_ai.request.model` attribute
- **Tokens**: "106 in + 6.2k out" (from `gen_ai.usage.input_tokens` + `gen_ai.usage.output_tokens`)
- **Cost**: "$0.0314" (from `gen_ai.response.cost_usd`)
- **Available Tools**: expandable "> [ 18 items ]" — list of unique `gen_ai.tool.name` values from child spans
- **Output section** (collapsible): LLM response text from `gen_ai.output.messages` or response content
- **Attributes section**: searchable key-value table of ALL span attributes

**Reasoning questions:**
- Can you reuse the existing TracesPage waterfall component with an AI-span filter? Or does the "abbreviated" concept (hiding HTTP/DB spans, showing only the AI decision chain) require a new component?
- The waterfall must show duration bars proportional to time — this is a horizontal bar/flame graph, not just a list
- Use `semconv.ts` attribute constants for extraction — don't hardcode attribute key strings

### 4. Models Tab

Model-level analytics:
- Table: Model name, Total calls, Total tokens (input/output split), Total cost, Avg duration, Error rate
- Time-series: calls over time stacked by model
- Token distribution: input vs output per model

Query: aggregate `spans` table grouped by extracted `gen_ai.request.model`.

### 5. Tools Tab

Tool usage analytics:
- Table: Tool name, Total calls, Avg duration, Error rate, Top agents using it
- Time-series: tool calls over time stacked by tool
- Latency distribution per tool

Query: aggregate `tool_calls` table or spans with `execute_tool` in name, grouped by tool name.

---

## Backend changes needed

Think through each new endpoint:

1. **`GET /api/v1/agents/overview/traffic`** — Time-bucketed run counts + error rate
2. **`GET /api/v1/agents/overview/duration`** — Time-bucketed avg + p95 duration
3. **`GET /api/v1/agents/overview/issues`** — Top N errors by frequency
4. **`GET /api/v1/agents/overview/llm-calls`** — Time-bucketed LLM call counts by model
5. **`GET /api/v1/agents/overview/tokens`** — Time-bucketed token sums by model
6. **`GET /api/v1/agents/overview/tool-calls`** — Time-bucketed tool call counts by tool name
7. **`GET /api/v1/agents/traces`** — The trace list with all aggregated columns (most complex query)
8. **`GET /api/v1/agents/models`** — Model breakdown aggregations
9. **`GET /api/v1/agents/tools`** — Tool usage aggregations

Separate endpoints (not one giant call) so the frontend can fetch all 6 overview panels in parallel — 6 small fast queries > 1 large slow query.

**Time bucketing** — All time-series endpoints need a `bucket` parameter (auto, 1h, 1d, 1w). Auto-detection logic: if range < 24h → 1h buckets, < 7d → 6h buckets, < 30d → 1d buckets, else → 1w buckets.

**Common query params**: `from` (unix ms), `to` (unix ms), `project` (optional), `env` (optional), `search` (optional full-text).

---

## Visual quality requirements

This must look like a product, not a hackathon project:

- **Dark theme** with deep background (#0a0a0f or similar), not pure black
- **Purple accent** for primary data (bars, links, active states). Red for errors. Green for success dots
- **Chart styling**: no gridlines on background, subtle axis labels (gray, small), smooth transitions on data load. X-axis: "Nov 1st", "Dec 1st", "Jan 1st" format
- **Number formatting**: "355m" not "355000000", "$0.0151" not "0.015099...", "2.15min" not "129000ms", "29k" not "29000"
- **Loading states**: skeleton placeholders matching panel shapes, not spinners
- **Empty states**: meaningful messages ("No agent runs in this time range"), not blank panels
- **Responsive**: panels reflow to single column on mobile, charts maintain aspect ratio
- **Transitions**: panels fade in sequentially (staggered 50ms), not all at once
- **Typography**: monospace for trace IDs, token counts, costs, durations. Proportional for labels and descriptions
- **Checkbox legends**: each chart series has a toggleable checkbox (☑ Runs, ☑ Error Rate, ☑ Releases). Unchecking hides that series
- **Table hover**: subtle row highlight on hover, no border change
- **Agent badges**: dark rounded pill with light text (e.g., `claude-code` on dark gray background)

---

## What NOT to do

- Don't build a generic dashboard builder. Build these specific panels with these specific queries.
- Don't abstract prematurely. If the Traffic panel and LLM Calls panel share 60% of their code, that's fine — let them diverge where they need to.
- Don't add features not in the reference (no alerting, no SLOs, no comparison mode). Ship the core first.
- Don't fake data. Every number must come from a real DuckDB query. If the query is slow, optimize it — don't cache stale results.
- Don't use `any` types. Every API response has a TypeScript interface. If the backend doesn't return what you need, change the backend.
- Don't guess attribute formats. Read one actual span from DuckDB (`SELECT attributes FROM spans LIMIT 1`) to see how attributes are stored before writing extraction queries.

---

## Execution order

Reason through dependencies:

1. **Verify data shape** — Query DuckDB directly to confirm attribute storage format, available span names, and what data exists
2. **Backend endpoints** — you can't build UI without data. Start with trace list (most complex), then overview panels
3. **Overview panels** — the 6-panel grid is the hero of the page
4. **Trace list table** — the most used feature (people scan traces)
5. **Trace detail slide-in** — the deep-dive experience (waterfall + span detail)
6. **Models and Tools tabs** — secondary analytics
7. **Polish** — loading states, empty states, transitions, responsive, skeleton placeholders

For each step: implement, verify the data is correct by checking DuckDB directly, then move to the next.

---

## The bar

When someone opens `/agents` for the first time, their reaction should be: "This is a real observability product." Not "This is a good open-source project." Not "This is impressive for one person." Just: this is real.

The difference between those reactions is: correct data from real queries, sub-second panel loads, professional typography, meaningful empty states, and zero broken layouts at any viewport width.

# qyl.bot — AI Chat Analytics for GitHub Copilot Extensions

## Problem

AI-powered assistants (GitHub Copilot extensions, MCP agents, website widgets) generate thousands of conversations.
Without analytics, teams can't answer:

- Where does the AI fail to help users? (**Coverage Gaps**)
- What do users care about most? (**Top Questions**)
- Which knowledge sources matter? (**Source Analytics**)
- Are answers improving over time? (**User Satisfaction**)
- What does an individual user's journey look like? (**User Tracking**)
- What happened in a specific conversation? (**Conversations**)

qyl.bot solves this with **OpenTelemetry** — the GitHub Copilot extension already emits GenAI spans through
qyl.copilot's instrumentation layer, and qyl.collector already stores them in DuckDB. The analytics modules query what's
already there.

## Architecture

### Primary Integration: GitHub Copilot Extensions (Free)

qyl.copilot is already a GitHub Copilot agent. It handles chat via `QylCopilotAdapter` and instruments every interaction
through `CopilotSpanRecorder`. The spans flow into qyl.collector via OTLP.

```
GitHub Copilot (VS Code / JetBrains / GitHub.com)
        |
        | Copilot Extensions Protocol
        v
+------------------+
|  qyl.copilot     |  QylCopilotAdapter + CopilotSpanRecorder
+--------+---------+
         | OTLP spans (gen_ai.* attributes)
         v
+------------------+
|  qyl.collector   |  Ingests + stores in DuckDB
+--------+---------+
         |
         v
+------------------+
|     DuckDB       |  spans table (gen_ai columns already exist)
+--------+---------+
         |
    +----+----+----+----+----+----+
    |    |    |    |    |    |    |
    v    v    v    v    v    v    v
  Conv  Gaps  Top  Src  Sat  User
              Q    Ana       Track
```

### Future Integrations (When API Keys Available)

The same analytics work for any OTLP source. When Claude API keys are available, qyl.bot serves additional frontends —
but the backend analytics are identical.

| Frontend                     | Protocol                     | Status                    |
|------------------------------|------------------------------|---------------------------|
| GitHub Copilot Extensions    | Copilot Extensions API       | Now (free)                |
| qyl.mcp (AI agents)          | MCP over stdio               | Now                       |
| qyl.browser (website widget) | OTLP over HTTP               | Ready                     |
| Claude Code CLI              | OTLP logs + metrics (native) | Future (requires API key) |

### Business-Neutral Instrumentation

qyl.bot uses only generic `gen_ai.*` attributes from the OTel GenAI semantic conventions. No vendor-specific
extensions (`openai.*`, `aws.bedrock.*`, `azure.*`). This means:

- Same analytics regardless of which LLM provider backs the copilot
- `gen_ai.provider.name` is set factually but carries no business semantics
- Switching providers requires zero analytics changes

---

## The 6 Analytics Modules

### 1. Conversations

**Purpose:** Browse and review all raw AI conversations.

**How it works:**

- Groups spans by `gen_ai.conversation.id` (or fallback `session_id` / `trace_id`) to reconstruct threads
- Each conversation shows: user prompts, AI responses, tool calls, sources cited, timestamps
- Filter by time range, user, model, error status
- Excludes off-topic and duplicate conversations from analytics

**MCP Tool:** `qyl.list_conversations`

```
Input:  { period: "2026-02", page: 1, pageSize: 20, filter?: { hasErrors?, userId?, model? } }
Output: { conversations: [{ conversationId, firstQuestion, turnCount, startTime, duration, tokenCount, hasErrors, userId }], total, page }
```

**MCP Tool:** `qyl.get_conversation`

```
Input:  { conversationId: "abc-123" }
Output: { turns: [{ role, content, timestamp, tokens, model, toolCalls?, sources?, feedback? }] }
```

**API Endpoints:**

- `GET /api/v1/analytics/conversations?period=2026-02&page=1`
- `GET /api/v1/analytics/conversations/{conversationId}`

**DuckDB Query Pattern:**

```sql
SELECT
    COALESCE(gen_ai_conversation_id, session_id, trace_id) AS conversation_id,
    MIN(start_time_unix_nano) AS started_at,
    MAX(end_time_unix_nano) AS ended_at,
    COUNT(*) AS turn_count,
    SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
    COALESCE(SUM(gen_ai_usage_input_tokens), 0) AS total_input_tokens,
    COALESCE(SUM(gen_ai_usage_output_tokens), 0) AS total_output_tokens,
    MAX(enduser_id) AS user_id
FROM spans
WHERE gen_ai_operation_name IS NOT NULL
  AND start_time_unix_nano >= ?
  AND start_time_unix_nano < ?
GROUP BY conversation_id
ORDER BY started_at DESC
```

---

### 2. Coverage Gaps

**Purpose:** Identify common topics where the AI assistant fails to provide conclusive answers, revealing documentation
and product gaps.

**How it works:**

For a selected time period, qyl analyzes all "uncertain" conversations — those where the AI struggled. It groups
recurring failure patterns into clusters, each with:

- **Finding:** What users asked about and why the AI failed
- **Recommendation:** How to fix the gap (add docs, mark unsupported, etc.)

**Uncertainty signals** (detected from OTel spans):

| Signal                  | How Detected                                                                   | Semconv Attribute                                          |
|-------------------------|--------------------------------------------------------------------------------|------------------------------------------------------------|
| Error responses         | `status_code = 2` on gen_ai spans                                              | `error.type` present                                       |
| High latency / timeouts | Duration > 2x median for similar `gen_ai.operation.name`                       | `gen_ai.client.operation.duration`                         |
| Tool call failures      | Execute-tool spans with error status                                           | `gen_ai.tool.name` + `error.type`                          |
| Empty completions       | `gen_ai.usage.output_tokens = 0` or very low                                   | `gen_ai.usage.output_tokens`                               |
| Excessive retries       | Multiple inference spans in same `gen_ai.conversation.id` with similar prompts | `gen_ai.conversation.id` grouping                          |
| Token anomalies         | Input+output tokens > 3x median (model struggling)                             | `gen_ai.usage.input_tokens` + `gen_ai.usage.output_tokens` |
| Repeated tool calls     | Same `gen_ai.tool.name` called 3+ times in one conversation                    | `gen_ai.tool.name` + `gen_ai.tool.call.id`                 |
| Generation stopped      | `finish_reasons` includes user-initiated stop                                  | `gen_ai.response.finish_reasons`                           |
| Low evaluation score    | `gen_ai.evaluation.score.value < 0.5`                                          | `gen_ai.evaluation.score.value`                            |
| Negative feedback       | `qyl.feedback.reaction = "downvote"`                                           | `qyl.feedback.reaction` + `qyl.feedback.incorrect`         |

**Clustering approach:**

- Group uncertain conversations by span name patterns and error types
- Use DuckDB string similarity functions for lightweight topic grouping
- Each cluster gets a count, sample conversations, and common attributes

**MCP Tool:** `qyl.get_coverage_gaps`

```
Input:  { period: "weekly" | "monthly" | "quarterly", offset?: 0 }
Output: {
  conversationsProcessed: 226,
  gapsIdentified: 8,
  gaps: [{
    topic: "Dark-light theme support",
    conversationCount: 7,
    finding: "Seven questions ask how to enable dark theme...",
    recommendation: "Explicitly state whether a built-in dark/light mode API exists...",
    status: "to_review" | "doc_issue" | "feature_request" | "done",
    sampleConversationIds: ["abc", "def"]
  }]
}
```

**API Endpoint:**

- `GET /api/v1/analytics/coverage-gaps?period=monthly&offset=0`
- `PATCH /api/v1/analytics/coverage-gaps/{gapId}/status` (update tracking status)

**DuckDB Query Pattern:**

```sql
WITH uncertain AS (
    SELECT
        COALESCE(gen_ai_conversation_id, session_id, trace_id) AS conversation_id,
        span_name,
        string_agg(DISTINCT gen_ai_provider_name, ', ') AS providers,
        COUNT(*) AS span_count,
        SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END) AS error_count,
        COALESCE(SUM(gen_ai_usage_input_tokens + gen_ai_usage_output_tokens), 0) AS total_tokens,
        MAX(duration_ms) AS max_duration_ms
    FROM spans
    WHERE gen_ai_operation_name IS NOT NULL
      AND start_time_unix_nano BETWEEN ? AND ?
      AND (
          status_code = 2
          OR gen_ai_usage_output_tokens = 0
          OR duration_ms > (SELECT percentile_disc(0.95) WITHIN GROUP (ORDER BY duration_ms)
                           FROM spans WHERE gen_ai_operation_name IS NOT NULL)
      )
    GROUP BY conversation_id, span_name
)
SELECT
    span_name AS topic,
    COUNT(DISTINCT conversation_id) AS conversation_count,
    array_agg(DISTINCT conversation_id) AS sample_ids
FROM uncertain
GROUP BY span_name
HAVING COUNT(DISTINCT conversation_id) >= 3
ORDER BY conversation_count DESC
```

---

### 3. Top Questions

**Purpose:** Understand the most common topics users ask about across all conversations, regardless of answer quality.

**How it works:**

For a selected time period, qyl analyzes ALL conversations (not just uncertain ones) to identify recurring themes.
Clusters similar questions into broader topics.

**MCP Tool:** `qyl.get_top_questions`

```
Input:  { period: "weekly" | "monthly" | "quarterly", offset?: 0, minConversations?: 5 }
Output: {
  conversationsProcessed: 742,
  clustersIdentified: 29,
  clusters: [{
    topic: "API Authentication Setup",
    conversationCount: 45,
    description: "Queries about API keys, OAuth setup, and token management",
    sampleConversationIds: ["abc", "def", "ghi"],
    languages: ["en", "de", "ja"]
  }]
}
```

**API Endpoint:**

- `GET /api/v1/analytics/top-questions?period=monthly&offset=0`

**DuckDB Query Pattern:**

```sql
SELECT
    span_name AS topic,
    COUNT(DISTINCT COALESCE(gen_ai_conversation_id, session_id, trace_id)) AS conversation_count,
    array_agg(DISTINCT COALESCE(gen_ai_conversation_id, session_id, trace_id)) AS sample_ids
FROM spans
WHERE gen_ai_operation_name IS NOT NULL
  AND start_time_unix_nano BETWEEN ? AND ?
  AND span_kind = 'CLIENT'
GROUP BY span_name
HAVING COUNT(DISTINCT COALESCE(gen_ai_conversation_id, session_id, trace_id)) >= ?
ORDER BY conversation_count DESC
```

**"Copy for LLM" feature:** Each cluster serializes as structured text for pasting into an LLM for deeper analysis — the
MCP tool output is already LLM-consumable.

---

### 4. Source Analytics

**Purpose:** Show which parts of knowledge sources are most important for answering user questions.

**How it works:**

- Tracks which documents/sources the AI references in answers via `gen_ai.data_source.id`
- Ranks sources by citation frequency
- Identifies "dead" sources (indexed but never cited)

**MCP Tool:** `qyl.get_source_analytics`

```
Input:  { period: "monthly", offset?: 0 }
Output: {
  sources: [{
    sourceId: "api-reference",
    citationCount: 234,
    topQuestions: ["How to authenticate", "Rate limiting"]
  }],
  deadSources: [{ sourceId: "legacy-migration", lastCited: null }]
}
```

**API Endpoint:**

- `GET /api/v1/analytics/source-analytics?period=monthly`

---

### 5. User Satisfaction

**Purpose:** Track user satisfaction with AI answers over time.

**How it works:**

- Uses `gen_ai.evaluation.score.value` from evaluation spans (0.0-1.0 quality score)
- Uses `qyl.feedback.reaction` from feedback events (upvote/downvote)
- Calculates satisfaction rate per period (week/month/quarter)
- Tracks trends: improving, declining, stable
- Breaks down by model, topic, source

**Feedback flow:**

```
User gives feedback in Copilot chat
    |
    v
qyl.copilot emits span event with qyl.feedback.reaction = "upvote" | "downvote"
    |  OTLP
    v
qyl.collector ingests as span attribute
    |
    v
Analytics query aggregates feedback per period
```

**MCP Tool:** `qyl.get_satisfaction`

```
Input:  { period: "monthly", offset?: 0 }
Output: {
  totalFeedback: 456,
  upvotes: 389,
  downvotes: 67,
  satisfactionRate: 0.853,
  trend: "improving",
  byModel: [{ model: "gpt-4o", rate: 0.91 }, ...],
  byTopic: [{ topic: "Authentication", rate: 0.72, downvotes: 12 }, ...]
}
```

**API Endpoint:**

- `GET /api/v1/analytics/satisfaction?period=monthly`

---

### 6. User Tracking

**Purpose:** Understand individual user journeys and identify power users.

**How it works:**

- Uses `enduser.id` attribute on spans (OTel semantic convention)
- Anonymous tracking via `gen_ai.conversation.id` when no user ID is set
- Cross-platform identity: same `enduser.id` across Copilot, MCP, website
- Tracks: conversation count, topics asked, satisfaction, retention

**Identity resolution:**

```
Anonymous visit (gen_ai.conversation.id: "conv-abc")
    | user identified via Copilot auth
    v
Identified visit (enduser.id: "user@example.com", gen_ai.conversation.id: "conv-abc")
    | qyl links both
    v
Single user profile with full history
```

**MCP Tool:** `qyl.list_users`

```
Input:  { period: "monthly", page: 1, pageSize: 20 }
Output: {
  users: [{
    userId: "user@example.com",
    conversationCount: 23,
    firstSeen: "2026-01-15T10:00:00Z",
    lastSeen: "2026-02-14T16:30:00Z",
    satisfactionRate: 0.87,
    topTopics: ["Authentication", "Deployment"]
  }],
  total: 1250
}
```

**MCP Tool:** `qyl.get_user_journey`

```
Input:  { userId: "user@example.com" }
Output: {
  conversations: [{ conversationId, date, topic, turnCount, satisfied }],
  totalTokens: 45000,
  frequentTopics: ["Auth", "API", "Billing"],
  retentionDays: 30
}
```

**API Endpoints:**

- `GET /api/v1/analytics/users?period=monthly`
- `GET /api/v1/analytics/users/{userId}/journey`

---

## OTel GenAI Semantic Conventions Reference

qyl.bot uses only the generic, business-neutral `gen_ai.*` attributes. No vendor-specific extensions. GenAI conventions
are in **Development** status (semconv v1.39). Instrumentations on v1.36.0 SHOULD NOT change defaults and SHOULD use
`OTEL_SEMCONV_STABILITY_OPT_IN=gen_ai_latest_experimental` to opt into latest.

### Four Signal Types

| Signal          | Purpose                                              | Example                                     |
|-----------------|------------------------------------------------------|---------------------------------------------|
| **Model spans** | Client calls to GenAI models (inference, embeddings) | `gen_ai.operation.name = "chat"`            |
| **Agent spans** | Higher-level agent/framework operations              | `gen_ai.operation.name = "invoke_agent"`    |
| **Events**      | Detailed input/output capture                        | `gen_ai.client.inference.operation.details` |
| **Metrics**     | Aggregated latency and token usage                   | `gen_ai.client.operation.duration`          |

### Span Hierarchy for AI Chat

```
invoke_agent (gen_ai.operation.name = "invoke_agent")    <- pipeline span
├── embeddings (gen_ai.operation.name = "embeddings")     <- query embedding
├── db.client (db.system.name = "duckdb")                 <- content retrieval
└── chat (gen_ai.operation.name = "chat")                 <- answer generation
    └── execute_tool (gen_ai.operation.name = "execute_tool")  <- tool calling
```

#### Inference Span (Answer Generation)

Every LLM call creates a `CLIENT` span:

```json
{
  "gen_ai.operation.name": "chat",
  "gen_ai.provider.name": "openai",
  "gen_ai.request.model": "gpt-4o",
  "gen_ai.response.model": "gpt-4o-2025-01-15",
  "gen_ai.request.max_tokens": 2000,
  "gen_ai.request.top_p": 1.0,
  "gen_ai.response.id": "chatcmpl-abc123",
  "gen_ai.usage.input_tokens": 150,
  "gen_ai.usage.output_tokens": 420,
  "gen_ai.response.finish_reasons": ["stop"],
  "error.type": null
}
```

**Content capture** (sensitive, may contain PII — instrumentations MAY offer filtering/truncation):

- `gen_ai.input.messages` — full chat history following JSON schema (user/system/assistant messages)
- `gen_ai.output.messages` — model response parts (text, tool_calls, reasoning)
- `gen_ai.system_instructions` — out-of-band system instructions (separate from in-history system role)

Content can live on the span directly or as a `gen_ai.client.inference.operation.details` event.

**Metrics** (auto-derived):

- `gen_ai.client.operation.duration` — P50/P95/P99 latency per model/operation
- `gen_ai.client.token.usage` with `gen_ai.token.type = "input" | "output"`

#### Embeddings Span

```json
{
  "gen_ai.operation.name": "embeddings",
  "gen_ai.provider.name": "openai",
  "gen_ai.request.model": "text-embedding-3-small",
  "gen_ai.embeddings.dimension.count": 1536,
  "gen_ai.request.encoding_formats": ["float"],
  "gen_ai.usage.input_tokens": 42
}
```

#### Retrieval Span (Vector DB / Content Store)

Database client span conventions:

```json
{
  "db.system.name": "duckdb",
  "db.namespace": "qyl",
  "db.collection.name": "spans",
  "db.operation.name": "SELECT",
  "db.query.summary": "SELECT nearest docs from knowledge_base"
}
```

Link retrieval to the logical data source with `gen_ai.data_source.id` on parent spans.

#### Agent Span (Pipeline Wrapper)

Wraps the full retrieve-generate cycle:

```json
{
  "gen_ai.operation.name": "invoke_agent",
  "gen_ai.provider.name": "qyl.copilot",
  "gen_ai.conversation.id": "thread-abc-123",
  "gen_ai.data_source.id": "docs-v2"
}
```

Child spans: retrieval (DB), embedding, inference, tool execution. Gives end-to-end latency with breakdown.

#### Tool Call Spans

Multi-span flow:

1. **Inference span 1**: model responds with `finish_reasons: ["tool_calls"]`, output contains tool_call parts
2. **Execute-tool span** (kind: `INTERNAL`):
    - `gen_ai.operation.name = "execute_tool"`
    - `gen_ai.tool.name` — tool name (e.g. "search_docs")
    - `gen_ai.tool.call.id` — correlates to the model's tool_call request
    - `gen_ai.tool.type` — `"function"` (client-side), `"extension"` (agent-side), or `"datastore"` (retrieval)
    - `gen_ai.tool.description` — human-readable tool description (recommended)
    - Opt-in: `gen_ai.tool.call.arguments` (input params), `gen_ai.tool.call.result` (output) — both sensitive
3. **Inference span 2**: model gets tool results, responds with `finish_reasons: ["stop"]`

Span name SHOULD be `execute_tool {gen_ai.tool.name}`.

#### Evaluation Span (Quality / Hallucination Monitoring)

For online or offline answer grading:

```json
{
  "gen_ai.evaluation.name": "answer_relevance",
  "gen_ai.evaluation.score.value": 0.85,
  "gen_ai.evaluation.score.label": "good",
  "gen_ai.evaluation.explanation": "Answer addressed the question but missed edge case"
}
```

Attach to the `invoke_agent` span or as a dedicated evaluation span in the same trace.

### Attribute Reference

#### Core (Required — set at span creation time for sampling decisions)

| Attribute                        | Type     | Example                                                                        |
|----------------------------------|----------|--------------------------------------------------------------------------------|
| `gen_ai.operation.name`          | string   | `"chat"`, `"embeddings"`, `"invoke_agent"`, `"execute_tool"`, `"create_agent"` |
| `gen_ai.provider.name`           | string   | `"openai"`, `"anthropic"`, `"gcp.vertex_ai"`, `"aws.bedrock"`                  |
| `gen_ai.request.model`           | string   | `"gpt-4o"`                                                                     |
| `gen_ai.response.model`          | string   | `"gpt-4o-2025-01-15"`                                                          |
| `gen_ai.usage.input_tokens`      | int      | `150`                                                                          |
| `gen_ai.usage.output_tokens`     | int      | `420`                                                                          |
| `gen_ai.response.finish_reasons` | string[] | `["stop"]`, `["tool_calls"]`                                                   |
| `error.type`                     | string   | `"timeout"`, `"rate_limit"`, `"_OTHER"`                                        |
| `server.address`                 | string   | `"api.openai.com"`                                                             |
| `server.port`                    | int      | `443`                                                                          |

#### Conversation & Agent

| Attribute                | Type   | Example                                   |
|--------------------------|--------|-------------------------------------------|
| `gen_ai.conversation.id` | string | `"thread-abc-123"`                        |
| `gen_ai.data_source.id`  | string | `"docs-v2"`                               |
| `gen_ai.output.type`     | string | `"text"`, `"json"`, `"image"`, `"speech"` |
| `gen_ai.tool.name`       | string | `"search_docs"`                           |
| `gen_ai.tool.call.id`    | string | `"call_abc123"`                           |
| `gen_ai.tool.type`       | string | `"function"`, `"code_interpreter"`        |
| `enduser.id`             | string | `"user@example.com"`                      |

#### Content (sensitive — opt-in only)

| Attribute                    | Type | Notes                                                  |
|------------------------------|------|--------------------------------------------------------|
| `gen_ai.input.messages`      | json | Chat history, follows JSON schema, likely contains PII |
| `gen_ai.output.messages`     | json | Response parts (text, tool_calls, reasoning)           |
| `gen_ai.system_instructions` | json | Out-of-band system instructions                        |

#### Evaluation

| Attribute                       | Type   | Example              |
|---------------------------------|--------|----------------------|
| `gen_ai.evaluation.name`        | string | `"answer_relevance"` |
| `gen_ai.evaluation.score.value` | float  | `0.85`               |
| `gen_ai.evaluation.score.label` | string | `"good"` / `"poor"`  |
| `gen_ai.evaluation.explanation` | string | `"Missed edge case"` |

#### Custom qyl Extensions

| Attribute                 | Type   | Example                   | Purpose           |
|---------------------------|--------|---------------------------|-------------------|
| `qyl.feedback.reaction`   | string | `"upvote"` / `"downvote"` | User Satisfaction |
| `qyl.feedback.comment`    | string | User's feedback text      | Coverage Gaps     |
| `qyl.feedback.irrelevant` | bool   | `true`                    | Coverage Gaps     |
| `qyl.feedback.incorrect`  | bool   | `true`                    | Coverage Gaps     |

### Advanced Patterns

**Simple chat completion:** One `CLIENT` span with core attributes. Content optionally captured via
`gen_ai.input.messages` / `gen_ai.output.messages` or as event.

**Multimodal chat:** Same span, but message parts include `type = "blob" | "uri" | "file"` with optional `modality` /
`mime_type`.

**Multiple choices:** Single span, `gen_ai.response.finish_reasons` is array with one entry per choice.

**Reasoning traces:** Parts with `type = "reasoning"` alongside `type = "text"` in `gen_ai.output.messages`.

**System instructions:** Separate from chat history via `gen_ai.system_instructions`.

**Built-in tools:** Single span; output format for built-in tools like `code_interpreter` is not yet normatively
specified.

---

## Implementation Phases

### Phase 1: Copilot Instrumentation Enhancement

Enrich `CopilotSpanRecorder` to emit the full GenAI semconv attributes on every copilot interaction. The spans already
flow to DuckDB — this phase ensures they carry enough data for the analytics modules.

**Key additions to `CopilotSpanRecorder`:**

- `gen_ai.conversation.id` (from copilot thread ID)
- `gen_ai.operation.name` (`"chat"`, `"invoke_agent"`)
- `gen_ai.usage.input_tokens` / `gen_ai.usage.output_tokens`
- `gen_ai.response.finish_reasons`
- `gen_ai.data_source.id` (when workflows reference knowledge sources)
- `enduser.id` (from Copilot auth context)
- `error.type` (on failures)

### Phase 2: Analytics API Endpoints

Add REST endpoints to qyl.collector. All queries run against existing `spans` table — no schema changes needed.

**New endpoints:**

- `GET /api/v1/analytics/conversations`
- `GET /api/v1/analytics/conversations/{id}`
- `GET /api/v1/analytics/coverage-gaps`
- `GET /api/v1/analytics/top-questions`
- `GET /api/v1/analytics/source-analytics`
- `GET /api/v1/analytics/satisfaction`
- `GET /api/v1/analytics/users`
- `GET /api/v1/analytics/users/{id}/journey`

### Phase 3: MCP Tools

Add 8 MCP tools to qyl.mcp that call the Phase 2 API endpoints:

1. `qyl.list_conversations` — browse all conversations
2. `qyl.get_conversation` — single conversation detail
3. `qyl.get_coverage_gaps` — uncertain answer clusters
4. `qyl.get_top_questions` — topic clusters
5. `qyl.get_source_analytics` — knowledge source usage
6. `qyl.get_satisfaction` — feedback aggregation
7. `qyl.list_users` — user activity overview
8. `qyl.get_user_journey` — individual user history

### Phase 4: Dashboard Pages

React pages in qyl.dashboard matching the 6 analytics modules. Each page consumes the Phase 2 API endpoints.

### Phase 5: Semantic Clustering via Embeddings

Upgrade from heuristic clustering (span name patterns) to embedding-based semantic clustering for richer Coverage Gaps
and Top Questions.

**Embedding models:**

- **Primary:** `all-mpnet-base-v2` — best quality for offline clustering (768 dimensions)
- **Query-optimized:** `multi-qa-mpnet-base-cos-v1` — tuned for question similarity (Top Questions)
- **Fast path:** `all-MiniLM-L6-v2` — low-latency for real-time MCP tool queries (384 dimensions)
- Fine-tune on domain data (Sentence-Transformers) using `gen_ai.source.*` attributes and `qyl.feedback.*` signals

**Feature-to-model mapping:**

| Feature            | Model                                                          | Why                                                           |
|--------------------|----------------------------------------------------------------|---------------------------------------------------------------|
| Coverage Gaps      | `all-mpnet-base-v2` (or fine-tuned)                            | Semantic clustering of uncertain/low-confidence spans         |
| Top Questions      | `multi-qa-mpnet-base-cos-v1` or fine-tuned `all-mpnet-base-v2` | Question-to-question similarity                               |
| Source Analytics   | `all-mpnet-base-v2`                                            | Match conversation topics to knowledge sources                |
| User Satisfaction  | None (structured data)                                         | DuckDB aggregation on `qyl.feedback.*` — no embeddings needed |
| User Tracking      | None (structured data)                                         | DuckDB aggregation on `enduser.id` — no embeddings needed     |
| MCP tool queries   | `all-MiniLM-L6-v2`                                             | Fast embedding at query time when agents analyze gaps         |
| Conversation dedup | `all-mpnet-base-v2`                                            | Semantic similarity for excluding duplicate questions         |

**Clustering pipeline:**

1. Extract user prompts from `gen_ai.input.messages` on inference spans
2. Embed via `all-mpnet-base-v2` → 768-dim vectors (batch, offline)
3. Cluster with HDBSCAN (density-based, handles noise, no predefined k)
4. Evaluate with silhouette score + cluster stability over time
5. Store cluster assignments back to DuckDB for fast dashboard queries

**Cluster quality metrics (sensors, not judges):**

- Silhouette score — how well each conversation fits its cluster
- Davies-Bouldin index — cluster separation quality
- Cluster drift over time — are new topics emerging?
- Cluster stability — are clusters consistent across periods?
- Outlier rate — how many conversations don't fit any cluster?

These metrics are system hygiene signals for the analytics engine, not end-user-facing scores.

---

## Future: Claude Code Integration

Claude Code has native OpenTelemetry support. When API keys become available, qyl.collector ingests Claude Code
telemetry with zero custom instrumentation — just environment variables.

**Priority:** After all Copilot work is finished and minmaxed. Copilot is free; Claude API requires a key.

### Setup

```bash
# Enable telemetry collection
export CLAUDE_CODE_ENABLE_TELEMETRY=1

# Point to qyl.collector
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc

# Enable exporters
export OTEL_METRICS_EXPORTER=otlp
export OTEL_LOGS_EXPORTER=otlp

# Optional: capture prompt content for Coverage Gaps / Top Questions
export OTEL_LOG_USER_PROMPTS=1
export OTEL_LOG_TOOL_DETAILS=1
```

### Configuration Reference

| Variable                       | Purpose                              | Default |
|--------------------------------|--------------------------------------|---------|
| `CLAUDE_CODE_ENABLE_TELEMETRY` | Enable OTel collection               | off     |
| `OTEL_EXPORTER_OTLP_ENDPOINT`  | Collector endpoint                   | —       |
| `OTEL_EXPORTER_OTLP_PROTOCOL`  | `grpc`, `http/json`, `http/protobuf` | —       |
| `OTEL_METRIC_EXPORT_INTERVAL`  | Metrics export interval (ms)         | 60000   |
| `OTEL_LOGS_EXPORT_INTERVAL`    | Logs export interval (ms)            | 5000    |
| `OTEL_LOG_USER_PROMPTS`        | Log user prompt content              | off     |
| `OTEL_LOG_TOOL_DETAILS`        | Log MCP/tool names                   | off     |
| `OTEL_EXPORTER_OTLP_HEADERS`   | Auth headers (e.g. Bearer token)     | —       |

### What This Enables

Once Claude Code telemetry flows into qyl.collector, all 6 analytics modules work automatically:

- **Conversations:** Every Claude Code session becomes a browsable conversation
- **Coverage Gaps:** Identify where Claude Code fails to help developers
- **Top Questions:** See what developers ask Claude Code most often
- **User Satisfaction:** Track answer quality across Claude Code sessions
- **User Tracking:** Individual developer journey through Claude Code
- **Source Analytics:** Which codebase files/docs Claude Code references most

No additional code needed — Claude Code emits standard OTLP logs and metrics, qyl.collector already ingests OTLP.

---

## What qyl.bot Adds

1. **Self-improving AI** — the copilot can call `qyl.get_coverage_gaps` via MCP and identify what docs to write
2. **Cross-platform by default** — same analytics for Copilot, MCP agents, website widget, any OTLP source
3. **Token economics** — tracks cost per conversation, per user, per topic
4. **Full trace correlation** — coverage gaps link to complete distributed traces, not just chat logs
5. **No vendor lock-in** — standard OTel, standard DuckDB, standard REST API
6. **Zero additional infrastructure** — analytics query what qyl.collector already stores

---

## Scaling Blueprint

For teams deploying qyl.bot at scale:

1. **Instrument once** — qyl.copilot already emits GenAI spans; just enrich the attributes
2. **Analytics automatically** — conversations, gaps, satisfaction flow into DuckDB from day one
3. **Self-improving** — MCP agents call `qyl.get_coverage_gaps` to identify documentation gaps
4. **Zero headcount scaling** — same infrastructure serves 10 or 100,000 users
5. **Cost visibility** — know exactly what each conversation costs via token tracking

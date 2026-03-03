# OTel Semantic Conventions Reference

> Merged from: `gen-ai-semconv-model.md` (attribute definitions) + `architecture/semconv-pipeline.md` (generation pipeline)

---

## Part 1: GenAI Attribute Model

# GenAI Semantic Conventions — Full Attribute Model

This document is the complete OpenTelemetry semantic conventions reference for GenAI and Copilot SDK instrumentation as implemented in qyl. It covers every span, event, metric, and MCP attribute that the platform captures, normalises, and stores. Use it as the authoritative mapping between upstream OTel semconv and what qyl records in DuckDB.

Upstream sources:
- [gen_ai spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/)
- [gen_ai agent spans](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-agent-spans/)
- [gen_ai events](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-events/)
- [gen_ai metrics](https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-metrics/)
- [MCP](https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp/)

---

## 1. Spans

### 1.1 Inference Spans

An inference span covers a single model call — from request serialisation to the last response token received. Span name follows the pattern `{gen_ai.system} {gen_ai.operation.name}`, e.g. `openai chat`.

**Requirement levels:** `Required` = must be present for qyl to process the span correctly. `Recommended` = captured when available. `Opt-In` = disabled unless explicitly configured.

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `gen_ai.system` | string | Required | Identifies the provider. Use a registered identifier or `_OTHER` for unknown providers. | `openai`, `anthropic`, `azure.ai.inference`, `_OTHER` |
| `gen_ai.operation.name` | string | Required | The operation being performed. | `chat`, `text_completion`, `embeddings` |
| `gen_ai.request.model` | string | Required | Model name as sent in the request. | `gpt-4o`, `claude-3-5-sonnet-20241022` |
| `gen_ai.response.model` | string | Recommended | Actual model name returned in the response (may differ from request if routing or aliasing occurs). | `gpt-4o-2024-08-06` |
| `gen_ai.request.max_tokens` | int | Recommended | Maximum tokens requested. | `4096`, `16384` |
| `gen_ai.request.temperature` | double | Recommended | Sampling temperature. Range: 0–2. | `0.7`, `1.0` |
| `gen_ai.request.top_p` | double | Recommended | Nucleus sampling probability mass. Range: 0–1. | `0.95` |
| `gen_ai.request.stop_sequences` | string[] | Opt-In | Stop sequences sent to the model. | `["<|endoftext|>"]`, `["Human:", "Assistant:"]` |
| `gen_ai.request.frequency_penalty` | double | Opt-In | Penalty for token frequency. Range: -2–2. | `0.5` |
| `gen_ai.request.presence_penalty` | double | Opt-In | Penalty for token presence. Range: -2–2. | `0.3` |
| `gen_ai.response.id` | string | Recommended | Unique identifier for the response returned by the provider. | `chatcmpl-abc123` |
| `gen_ai.response.finish_reasons` | string[] | Recommended | Reason(s) the model stopped generating. | `["stop"]`, `["length"]`, `["tool_calls"]`, `["content_filter"]` |
| `gen_ai.response.input_tokens` | int | Recommended | Number of tokens in the prompt/input. Used for cost accounting. | `512` |
| `gen_ai.response.output_tokens` | int | Recommended | Number of tokens in the completion/output. Used for cost accounting. | `128` |
| `gen_ai.output_type` | string | Recommended | Content type of the model output. | `text`, `json`, `image`, `speech`, `tool_calls` |

**Note on `gen_ai.output_type`:** When the response contains both text and tool calls (a common pattern with function-calling models), record `tool_calls`. The actual content is carried in events (see section 2).

### 1.2 Embeddings Spans

Embeddings spans share the inference span schema above with `gen_ai.operation.name = "embeddings"`. Additional attributes:

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `gen_ai.request.encoding_formats` | string[] | Recommended | Requested output encoding formats for the embedding vector. | `["float"]`, `["base64"]` |

### 1.3 Retrieval Spans

Retrieval spans model vector-store lookups that feed context into a generation call. Span name: `{gen_ai.system} retrieve` or the name of the retriever component.

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `gen_ai.system` | string | Required | Provider or store identifier. | `pinecone`, `azure.ai.search`, `_OTHER` |
| `gen_ai.operation.name` | string | Required | Always `retrieve` for retrieval operations. | `retrieve` |
| `gen_ai.request.model` | string | Recommended | Embedding model used to encode the query (if applicable). | `text-embedding-3-small` |
| `db.system` | string | Recommended | Underlying vector database type. Follows `db.*` semconv. | `chroma`, `qdrant`, `redis` |
| `db.collection.name` | string | Recommended | Collection or index name queried. | `knowledge-base-v2` |

### 1.4 Execute Tool Spans

Tool-execution spans represent the actual invocation of a tool the model requested. They are children of the inference span that produced the tool call. Span name: `execute_tool {tool.name}`.

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `gen_ai.system` | string | Required | Provider that requested the tool. | `openai`, `anthropic` |
| `gen_ai.operation.name` | string | Required | Always `execute_tool`. | `execute_tool` |
| `gen_ai.tool.name` | string | Required | The name of the tool being invoked, as it was declared to the model. | `get_weather`, `run_query`, `read_file` |
| `gen_ai.tool.call.id` | string | Recommended | Provider-assigned identifier for this specific tool call within the response. Correlates with the `gen_ai.tool.message` event. | `call_abc123` |
| `gen_ai.tool.description` | string | Opt-In | Description sent to the model for this tool. | `"Returns current weather for a city."` |

---

## 2. Agent Spans

Agent-framework spans wrap multi-step reasoning processes. They nest inference and tool-execution spans as children. qyl uses these to reconstruct full agent run timelines in the session view.

### 2.1 Agent Lifecycle Span

The root span for an agent run. Span name: `{agent.name} {operation}` or simply the agent name.

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `gen_ai.agent.id` | string | Recommended | Stable identifier for the agent definition (not the run instance). | `copilot-coding-agent`, `research-agent-v2` |
| `gen_ai.agent.name` | string | Required | Human-readable name of the agent. | `Coding Agent`, `Research Agent` |
| `gen_ai.agent.description` | string | Opt-In | What the agent does. Often the system prompt summary. | `"Assists developers with code review and refactoring."` |

### 2.2 Agent Operation Types

The following operation names are used in `gen_ai.operation.name` when instrumenting agent frameworks. Each produces a distinct child span under the agent lifecycle span.

| Operation Name | Description |
|---|---|
| `planning` | Agent is decomposing the user goal into sub-tasks. |
| `tool_calling` | Agent is selecting and calling one or more tools. |
| `reflection` | Agent is evaluating its own prior output before continuing. |
| `handoff` | Agent is delegating to a sub-agent or handing off to a human. |

### 2.3 Handoff Span Attributes

Handoff spans have additional attributes that record the delegation target.

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `gen_ai.agent.id` | string | Recommended | ID of the *target* agent receiving the handoff. | `specialist-agent-sql` |
| `gen_ai.agent.name` | string | Recommended | Name of the target agent. | `SQL Specialist` |

---

## 3. Events

Events are OTel log records attached to inference spans. They carry the actual prompt and completion content — the data that is too large and sensitive to put in span attributes. qyl captures events when content capture is enabled (opt-in, off by default).

All GenAI events share the following log record fields:

| Field | Value |
|---|---|
| `event.name` | One of the names listed below |
| `event.domain` | `gen_ai` |

### 3.1 Event Types

| Event Name | Emitted By | Description |
|---|---|---|
| `gen_ai.system.message` | Instrumentation | Captures the system prompt sent to the model. |
| `gen_ai.user.message` | Instrumentation | Captures a user turn in the conversation. |
| `gen_ai.assistant.message` | Instrumentation | Captures an assistant (model) turn, including tool calls made. |
| `gen_ai.tool.message` | Instrumentation | Captures the result returned to the model after a tool call. |
| `gen_ai.choice` | Instrumentation | Captures a single completion choice from the model response. |

### 3.2 Event Body Schema

Event bodies are JSON-encoded. The shape varies by event type.

**`gen_ai.system.message`**

```json
{
  "role": "system",
  "content": "You are a helpful assistant."
}
```

**`gen_ai.user.message`**

```json
{
  "role": "user",
  "content": "How do I reverse a string in Python?"
}
```

Content may also be an array for multi-modal inputs:

```json
{
  "role": "user",
  "content": [
    { "type": "text", "text": "What is in this image?" },
    { "type": "image_url", "image_url": { "url": "data:image/png;base64,..." } }
  ]
}
```

**`gen_ai.assistant.message`**

```json
{
  "role": "assistant",
  "content": "Here is how you reverse a string...",
  "tool_calls": [
    {
      "id": "call_abc123",
      "type": "function",
      "function": {
        "name": "run_code",
        "arguments": "{\"code\": \"print('hello'[::-1])\"}"
      }
    }
  ]
}
```

**`gen_ai.tool.message`**

```json
{
  "role": "tool",
  "id": "call_abc123",
  "content": "olleh"
}
```

**`gen_ai.choice`**

```json
{
  "index": 0,
  "finish_reason": "stop",
  "message": {
    "role": "assistant",
    "content": "The reversed string is 'olleh'."
  }
}
```

### 3.3 Privacy and Safety

Event capture is **opt-in** and disabled by default in qyl. When enabled, content is stored in the `span_events` table. Scrubbing rules (regex-based PII redaction) can be applied at the collector before storage. Do not enable event capture in production without reviewing your data retention policy.

---

## 4. Metrics

All GenAI metrics are histograms. They are recorded by the instrumentation layer — not by qyl — and arrive via OTLP. qyl stores them in the `metrics` table with full attribute dimension preservation.

### 4.1 `gen_ai.client.token.usage`

Billable token accounting per model call.

| Field | Value |
|---|---|
| Instrument | Histogram |
| Unit | `{token}` |
| Description | Measures the number of input and output tokens used. |

| Attribute | Type | Description | Example Values |
|---|---|---|---|
| `gen_ai.system` | string | Provider identifier. | `openai` |
| `gen_ai.operation.name` | string | Operation performed. | `chat` |
| `gen_ai.request.model` | string | Requested model. | `gpt-4o` |
| `gen_ai.response.model` | string | Actual model used. | `gpt-4o-2024-08-06` |
| `gen_ai.token.type` | string | Whether these are `input` or `output` tokens. | `input`, `output` |
| `server.address` | string | Inference endpoint host. | `api.openai.com` |
| `server.port` | int | Inference endpoint port. | `443` |

### 4.2 `gen_ai.client.operation.duration`

End-to-end latency of a model call as observed by the client, from first byte sent to last byte received.

| Field | Value |
|---|---|
| Instrument | Histogram |
| Unit | `s` |
| Description | GenAI operation duration from client perspective. |

| Attribute | Type | Description | Example Values |
|---|---|---|---|
| `gen_ai.system` | string | Provider identifier. | `anthropic` |
| `gen_ai.operation.name` | string | Operation performed. | `chat` |
| `gen_ai.request.model` | string | Requested model. | `claude-3-5-sonnet-20241022` |
| `gen_ai.response.model` | string | Actual model used. | `claude-3-5-sonnet-20241022` |
| `gen_ai.response.finish_reasons` | string[] | How the call ended. | `["stop"]` |
| `error.type` | string | Error class name if the call failed. | `RateLimitError`, `TimeoutError` |
| `server.address` | string | Inference endpoint host. | `api.anthropic.com` |
| `server.port` | int | Inference endpoint port. | `443` |

### 4.3 `gen_ai.client.operation.time_to_first_chunk`

Time from when the client sends the request to when it receives the first streaming chunk. Measures perceived responsiveness for streaming calls.

| Field | Value |
|---|---|
| Instrument | Histogram |
| Unit | `s` |
| Description | Time to first streaming chunk received by the client (TTFC). Only applies to streaming calls. |

Attributes are identical to `gen_ai.client.operation.duration`.

### 4.4 `gen_ai.server.time_to_first_token`

Time from when the model begins processing to when it emits its first output token. This is a server-side metric and requires provider support. When available it separates queuing/scheduling latency from generation latency.

| Field | Value |
|---|---|
| Instrument | Histogram |
| Unit | `s` |
| Description | Time to first token generated by the model (TTFT). Server-side signal. |

| Attribute | Type | Description | Example Values |
|---|---|---|---|
| `gen_ai.system` | string | Provider identifier. | `openai` |
| `gen_ai.request.model` | string | Requested model. | `gpt-4o` |
| `gen_ai.response.model` | string | Actual model used. | `gpt-4o-2024-08-06` |
| `server.address` | string | Inference endpoint host. | `api.openai.com` |
| `server.port` | int | Inference endpoint port. | `443` |

### 4.5 `gen_ai.server.time_per_output_token`

Time per output token generated (TPOT), also a server-side metric. Combined with TTFT, gives throughput benchmarks useful when selecting between model variants.

| Field | Value |
|---|---|
| Instrument | Histogram |
| Unit | `s` |
| Description | Average time to generate each output token, excluding time to first token. |

Attributes are identical to `gen_ai.server.time_to_first_token`.

---

## 5. MCP (Model Context Protocol)

MCP spans instrument the communication channel between an agent and its tool servers. They are children of the inference or agent span that triggers the MCP call.

### 5.1 MCP Span Name

```
mcp.{operation_name}
```

Examples: `mcp.tools/call`, `mcp.resources/read`, `mcp.prompts/get`, `mcp.initialize`.

### 5.2 MCP Span Attributes

| Attribute | Type | Requirement Level | Description | Example Values |
|---|---|---|---|---|
| `mcp.method.name` | string | Required | Full MCP method name as defined in the MCP specification. | `tools/call`, `resources/read`, `prompts/get`, `initialize` |
| `mcp.request.id` | string | Required | Request identifier from the MCP JSON-RPC message. | `req-42`, `1` |
| `mcp.session.id` | string | Recommended | Session identifier for the MCP connection. Correlates all requests in a single agent session. | `session-abc123` |
| `mcp.transport` | string | Recommended | Transport mechanism used. | `stdio`, `http`, `sse` |
| `mcp.client.id` | string | Recommended | Identifier for the MCP client making the request. | `copilot-agent-v2` |
| `mcp.server.name` | string | Recommended | Name of the MCP server as declared in its manifest. | `filesystem`, `github`, `postgres` |
| `mcp.server.version` | string | Recommended | Version of the MCP server. | `1.2.0` |
| `mcp.tool.name` | string | Recommended | Name of the tool being called. Present when `mcp.method.name = tools/call`. | `read_file`, `execute_query` |
| `mcp.resource.uri` | string | Recommended | URI of the resource being accessed. Present when `mcp.method.name = resources/read`. | `file:///workspace/main.py` |
| `mcp.resource.name` | string | Opt-In | Human-readable name of the resource. | `main.py` |
| `mcp.prompt.name` | string | Recommended | Name of the prompt template. Present when `mcp.method.name = prompts/get`. | `code-review`, `summarise` |
| `mcp.response.error_code` | string | Recommended | JSON-RPC error code string if the call failed. | `-32601`, `TOOL_NOT_FOUND` |

### 5.3 MCP Context Propagation

MCP spans participate in W3C Trace Context propagation. The MCP client injects `traceparent` and `tracestate` headers (or equivalent transport-layer fields) into each request. The MCP server extracts them and creates child spans. This ensures the full agent → model → tool chain appears as a single trace in qyl.

When MCP servers do not support context propagation, the agent instrumentation creates a synthetic parent link using `gen_ai.tool.call.id` to maintain logical correlation.

---

## 6. Provider-Specific Attributes

These attributes extend the core `gen_ai.*` schema with provider-specific fields. They are stored verbatim in the `span_attributes` column. qyl normalises the core fields but preserves provider extensions intact.

### 6.1 OpenAI

| Attribute | Type | Description | Example Values |
|---|---|---|---|
| `openai.request.response_format` | string | Response format requested. | `json_object`, `text`, `json_schema` |
| `openai.request.seed` | int | Seed used for deterministic sampling. | `42`, `12345` |
| `openai.request.service_tier` | string | Service tier requested by the client. | `default`, `flex` |
| `openai.response.service_tier` | string | Service tier used for this response. | `default`, `flex` |
| `openai.response.system_fingerprint` | string | Fingerprint representing the backend configuration. Changes indicate a backend update. | `fp_44709d6fcb` |

### 6.2 Anthropic

| Attribute | Type | Description | Example Values |
|---|---|---|---|
| `anthropic.request.thinking.enabled` | boolean | Whether extended thinking was requested. | `true`, `false` |
| `anthropic.request.thinking.budget_tokens` | int | Token budget allocated for extended thinking. | `8000` |
| `anthropic.response.thinking_tokens` | int | Tokens consumed by the thinking block, not counted as output for pricing. | `3200` |
| `anthropic.response.stop_sequence` | string | The specific stop sequence that ended generation (when `finish_reason = stop_sequence`). | `"Human:"` |

---

## 7. Resource Attributes

Resource attributes describe the service emitting the telemetry, not the model call itself. They are set once on the `TracerProvider` and attached to every span.

| Attribute | Type | Description | Example Values |
|---|---|---|---|
| `service.name` | string | Name of the instrumented application. | `my-copilot-app` |
| `service.version` | string | Application version. | `1.4.2` |
| `service.namespace` | string | Logical grouping of services. | `production`, `staging` |
| `gen_ai.system` | string | May be set as a resource attribute when all calls in the process use a single provider. Overridden by span-level value when both are present. | `openai` |
| `server.address` | string | Host of the inference endpoint. Used at resource level when the endpoint is fixed per process. | `api.openai.com` |
| `server.port` | int | Port of the inference endpoint. | `443` |

---

## 8. Attribute Value Enumerations

### `gen_ai.system` Registered Values

| Value | Provider |
|---|---|
| `openai` | OpenAI (including Azure OpenAI using the OpenAI SDK) |
| `anthropic` | Anthropic |
| `azure.ai.inference` | Azure AI Inference (non-OpenAI models via Azure) |
| `azure.ai.openai` | Azure OpenAI (Azure-specific SDK path) |
| `google.ai.gemini` | Google AI Gemini (via Google SDK) |
| `google.vertexai` | Google Vertex AI |
| `aws.bedrock` | Amazon Bedrock |
| `cohere` | Cohere |
| `mistral_ai` | Mistral AI |
| `ibm.watsonx.ai` | IBM watsonx.ai |
| `_OTHER` | Any provider not listed above |

### `gen_ai.operation.name` Values

| Value | Used In |
|---|---|
| `chat` | Chat completion requests |
| `text_completion` | Legacy text completion requests |
| `embeddings` | Embedding generation requests |
| `retrieve` | Vector store retrieval |
| `execute_tool` | Tool execution spans |
| `planning` | Agent planning steps |
| `tool_calling` | Agent tool selection/invocation |
| `reflection` | Agent self-evaluation |
| `handoff` | Agent delegation |

### `gen_ai.response.finish_reasons` Values

| Value | Meaning |
|---|---|
| `stop` | Model completed naturally at an end-of-turn boundary. |
| `length` | Generation stopped because `max_tokens` was reached. |
| `tool_calls` | Model stopped to request tool execution. |
| `content_filter` | Generation stopped by a safety or content filter. |
| `stop_sequence` | Generation stopped because a stop sequence was matched. |
| `error` | Generation stopped due to an error. |

---

## 9. qyl Storage Mapping

This section shows how the above attributes map to qyl's internal DuckDB schema. It is here for platform engineers; application developers can skip it.

| OTel Field | qyl Column | Table | Notes |
|---|---|---|---|
| Span `gen_ai.system` | `gen_ai_system` | `spans` | Extracted at ingest; also indexed. |
| Span `gen_ai.operation.name` | `gen_ai_operation` | `spans` | — |
| Span `gen_ai.request.model` | `gen_ai_request_model` | `spans` | — |
| Span `gen_ai.response.model` | `gen_ai_response_model` | `spans` | — |
| Span `gen_ai.response.input_tokens` | `input_tokens` | `spans` | Summed into session token totals. |
| Span `gen_ai.response.output_tokens` | `output_tokens` | `spans` | Summed into session token totals. |
| Span `gen_ai.agent.id` | `agent_id` | `spans` | Used to group spans into agent runs. |
| Span `gen_ai.agent.name` | `agent_name` | `spans` | — |
| Span `mcp.session.id` | `mcp_session_id` | `spans` | Joins to the session view. |
| Metric `gen_ai.client.token.usage` | — | `metrics` | Stored with full attribute dimensions. |
| Metric `gen_ai.client.operation.duration` | — | `metrics` | Bucketed per OTel histogram schema. |
| Event `gen_ai.user.message` | `body` (JSON) | `span_events` | Only when content capture is enabled. |
| Provider `openai.*` | `attributes` (JSON) | `spans` | Stored in the raw attribute blob. |
| Provider `anthropic.*` | `attributes` (JSON) | `spans` | Stored in the raw attribute blob. |

---

## Related

- [ADR-003: .NET Premium SDK](../decisions/ADR-003-nuget-first-instrumentation.md) — how `gen_ai.*` interceptors are emitted by source generators
- [Semconv Pipeline](otel-semconv-reference.md#part-2-semconv-generation-pipeline) — pipeline from OTLP ingest to DuckDB storage


---

## Part 2: Semconv Generation Pipeline

``# Semantic Conventions Pipeline

How qyl keeps its telemetry attributes in sync with the OpenTelemetry standard.

## What It Does

One generator reads the official OTel semantic conventions and produces typed constants for every layer of qyl — dashboard, collector, storage, SDK, and API schemas. When OTel releases new conventions (e.g. v1.40), we bump one version number and regenerate.

## Pipeline

```
@opentelemetry/semantic-conventions (npm package)
                 │
                 ▼
    eng/semconv/generate-semconv.ts
                 │
       ┌─────────┼──────────┬──────────────┬─────────────┐
       ▼         ▼          ▼              ▼             ▼
   TypeScript    C#      C# UTF-8      TypeSpec       DuckDB
   (Dashboard)  (SDK)   (Hot paths)   (API Schema)  (Storage)
```

## Outputs

| Output | File | Consumer | Purpose |
|--------|------|----------|---------|
| TypeScript | `src/qyl.dashboard/src/lib/semconv.ts` | Dashboard | Attribute keys for UI filters, labels |
| C# | `src/qyl.servicedefaults/.../SemanticConventions.g.cs` | .NET SDK | String constants for instrumentation |
| C# UTF-8 | `src/qyl.servicedefaults/.../SemanticConventions.Utf8.g.cs` | Collector | `ReadOnlySpan<byte>` for zero-allocation OTLP parsing |
| TypeSpec | `core/specs/generated/semconv.g.tsp` | API codegen | Typed models for OpenAPI/JSON schema |
| DuckDB SQL | `src/qyl.collector/Storage/promoted-columns.g.sql` | Collector | Column definitions for promoted attributes |

## How To Run

```bash
# All outputs
cd eng/semconv && npm run generate

# Single output
npm run generate:ts    # TypeScript only
npm run generate:cs    # C# only
npm run generate:utf8  # C# UTF-8 only
npm run generate:tsp   # TypeSpec only
npm run generate:sql   # DuckDB only

# Via NUKE (recommended — runs as part of full pipeline)
nuke Generate --force-generate
```

## How To Update OTel Version

1. Edit `eng/semconv/package.json`:
   ```json
   "@opentelemetry/semantic-conventions": "1.40.0"
   ```
2. `cd eng/semconv && npm install`
3. `npm run generate`
4. Review generated diffs
5. Run `dotnet build` + `dotnet test` to verify compatibility

## Attribute Filtering

Not all OTel attributes are relevant. The generator filters by prefix:

| Category | Prefixes |
|----------|----------|
| AI | `gen_ai`, `code`, `openai`, `azure` |
| Transport | `http`, `rpc`, `messaging`, `url`, `signalr`, `kestrel` |
| Data | `db`, `file`, `vcs`, `artifact`, `elasticsearch` |
| Infrastructure | `cloud`, `container`, `k8s`, `host`, `os`, `faas` |
| Security | `network`, `tls`, `dns` |
| Runtime | `process`, `thread`, `system`, `dotnet`, `aspnetcore` |
| Identity | `user`, `client`, `server`, `service`, `telemetry` |
| Observe | `browser`, `session`, `exception`, `error`, `log`, `otel` |
| Ops | `cicd`, `deployment` |

To add a new prefix: edit `CONFIG.includePrefixes` in `generate-semconv.ts`.

## Adding a New Language

To support a new output language (e.g. Python, Go):

1. Add a new generator function in `generate-semconv.ts`:
   ```typescript
   function generatePython(data: ParsedData): string { ... }
   ```
2. Add output path in `CONFIG.outputs`:
   ```typescript
   python: "../../sdks/python/qyl/semconv.py"
   ```
3. Add CLI flag (`--py-only`) and wire it in `main()`
4. Add npm script in `package.json`:
   ```json
   "generate:py": "tsx generate-semconv.ts --py-only"
   ```

The `ParsedData` structure already contains everything needed — attribute names, values, enum groups, and type hints. Each new language just needs a formatting function.

## Architecture Decisions

- **npm as source**: OTel publishes conventions as an npm package with TypeScript declarations. Parsing `.d.ts` files is more reliable than parsing YAML (which has breaking format changes between versions).
- **Single generator**: One script, five outputs. No drift between layers — if the dashboard knows about `gen_ai.system`, so does the collector, the SDK, and the storage.
- **Compile-time only**: All generated files are constants. No runtime dependency on the generator. The npm package is a dev dependency only.
- **Prefix filtering**: OTel has 500+ attributes. qyl only promotes the ones relevant to its use cases. Adding new domains is one line in the config.

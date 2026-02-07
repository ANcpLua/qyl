# Codex Telemetry Mapper

## Why This Exists

OpenAI's Codex CLI exports OpenTelemetry data using **custom `codex.*` prefixed attributes** instead of the standard [OTel GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/). This creates a compatibility problem: qyl's GenAI dashboard expects standard semantic conventions to display token usage, model information, and conversation tracking.

This mapper transforms Codex telemetry at ingestion time to standard GenAI semantic conventions.

## The Problem

### What Codex Emits

Per [Codex Advanced Configuration](https://developers.openai.com/codex/config-advanced#observability-and-telemetry), Codex emits these custom events:

| Event | Description |
|-------|-------------|
| `codex.conversation_starts` | Model, reasoning settings, sandbox/approval policy |
| `codex.api_request` | Attempt, status/success, duration, error details |
| `codex.sse_event` | Stream event kind, success/failure, duration, token counts |
| `codex.user_prompt` | Length; content redacted unless enabled |
| `codex.tool_decision` | Approved/denied decision |
| `codex.tool_result` | Duration, success, output snippet |

### What OTel GenAI Semconv Specifies

Per [OpenTelemetry Semantic Conventions for GenAI](https://opentelemetry.io/docs/specs/semconv/gen-ai/):

> The Semantic Conventions define a common set of (semantic) attributes which provide meaning to data when collecting, producing and consuming it.

For OpenAI specifically, per [OTel OpenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/openai/):

> `gen_ai.provider.name` MUST be set to "openai"

Required/recommended attributes include:

| Attribute | Requirement | Description |
|-----------|-------------|-------------|
| `gen_ai.provider.name` | Required | Must be "openai" |
| `gen_ai.operation.name` | Required | Operation type (chat, execute_tool, etc.) |
| `gen_ai.request.model` | Required | Model name requested |
| `gen_ai.response.model` | Recommended | Model name that responded |
| `gen_ai.usage.input_tokens` | Recommended | Input/prompt tokens |
| `gen_ai.usage.output_tokens` | Recommended | Output/completion tokens |
| `gen_ai.conversation.id` | Conditionally Required | Session/thread identifier |
| `gen_ai.response.finish_reasons` | Recommended | Why generation stopped |

### The Gap

Codex (an OpenAI product) does not follow the OTel semantic conventions that were designed for OpenAI operations. This means:

1. **qyl's GenAI dashboard** cannot recognize Codex spans as AI operations
2. **Token usage tracking** doesn't work (attributes have wrong names)
3. **Model analytics** are missing (no `gen_ai.request.model`)
4. **Conversation grouping** fails (no `gen_ai.conversation.id`)

## The Solution

### Transformation Approach

The `CodexTelemetryMapper` intercepts OTLP ingestion and transforms Codex attributes to GenAI semconv **before storage**:

```
OTLP Request → Codex Detection → Attribute Transform → Storage
```

### Attribute Mappings

| Codex Attribute | GenAI Semconv |
|-----------------|---------------|
| `codex.model` | `gen_ai.request.model` |
| `codex.conversation_id` | `gen_ai.conversation.id` |
| `codex.thread_id` | `gen_ai.conversation.id` |
| `codex.input_tokens` | `gen_ai.usage.input_tokens` |
| `codex.output_tokens` | `gen_ai.usage.output_tokens` |
| `codex.finish_reason` | `gen_ai.response.finish_reasons` |
| `codex.tool_name` | `gen_ai.tool.name` |
| `codex.error_type` | `error.type` |
| (always added) | `gen_ai.provider.name = "openai"` |

### Operation Name Derivation

| Codex Span Name | GenAI Operation |
|-----------------|-----------------|
| `codex.conversation_starts` | `chat` |
| `codex.api_request` | `chat` |
| `codex.sse_event` | `chat` |
| `codex.user_prompt` | `chat` |
| `codex.tool_decision` | `execute_tool` |
| `codex.tool_result` | `execute_tool` |

## Design Decisions

### 1. Preprocessing (not post-processing)

Transformations happen at ingestion before storage. This ensures:
- All queries see normalized data
- No duplicate storage of raw + transformed
- Consistent dashboard experience

### 2. In-place Mutation

Attributes are transformed in the existing objects rather than creating copies:
- Memory efficient
- No allocation pressure during high-volume ingestion

### 3. Constants from GenAiAttributes

All attribute names use constants from `qyl.protocol.Attributes.GenAiAttributes`:
- Single source of truth
- No string duplication
- Compile-time verification

### 4. Idempotent

The mapper won't overwrite existing GenAI attributes:
- Safe to run multiple times
- Preserves any semconv-compliant attributes already present

### 5. Fast Path Detection

Quick prefix check (`codex.`) before full attribute scanning:
- Non-Codex spans pass through with minimal overhead
- Only Codex spans incur transformation cost

## Integration Points

### HTTP OTLP (`/v1/traces`)

```csharp
// Program.cs — POST /v1/traces handler
var batch = new SpanBatch(spans).WithCodexTransformations();
```

### gRPC OTLP (TraceService)

```csharp
// TraceServiceImpl.cs — Export method
var batch = new SpanBatch(spans).WithCodexTransformations();
```

## Sources

### Primary References

1. **OpenTelemetry Semantic Conventions for GenAI**
   https://opentelemetry.io/docs/specs/semconv/gen-ai/
   - Defines core `gen_ai.*` attribute namespace
   - Specifies required vs recommended attributes
   - Describes span structure for AI operations

2. **OpenTelemetry Semantic Conventions for OpenAI**
   https://opentelemetry.io/docs/specs/semconv/gen-ai/openai/
   - Mandates `gen_ai.provider.name = "openai"`
   - OpenAI-specific attribute extensions

3. **Codex Advanced Configuration - Observability and Telemetry**
   https://developers.openai.com/codex/config-advanced#observability-and-telemetry
   - Documents `codex.*` event schema
   - OTLP export configuration options

### Attribute Specifications

4. **GenAI Span Attributes**
   https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/
   - Full attribute table with requirement levels
   - `gen_ai.operation.name` well-known values: `chat`, `execute_tool`, `embeddings`, `create_agent`, `invoke_agent`
   - Token usage attributes (`gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`)

5. **Execute Tool Span Conventions**
   https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/#execute-tool
   - `gen_ai.tool.call.id` - unique identifier for tool call
   - `gen_ai.tool.name` - name of the tool being executed
   - `gen_ai.tool.description` - tool description
   - Directly relevant to `codex.tool_decision` and `codex.tool_result` mapping

6. **Provider Name Values**
   https://opentelemetry.io/docs/specs/semconv/attributes-registry/gen-ai/
   - Well-known values: `openai`, `anthropic`, `aws.bedrock`, `azure.ai`, `cohere`, `google.genai`, `groq`, `mistral`, `perplexity`, `vertex_ai`, `ibm.watsonx`, `deepseek`
   - Custom providers use their identifier

### Supplementary References

7. **OpenTelemetry Semantic Conventions README**
   https://opentelemetry.io/docs/specs/semconv/

8. **GenAI Events**
   https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-events/
   - Content recording via events (alternative to span attributes)
   - `gen_ai.content.prompt` and `gen_ai.content.completion` events

9. **MCP Semantic Conventions (Experimental)**
   https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp/
   - May apply to Codex tool calls in future

## Attribute Requirement Levels

Per OTel semconv, attributes have requirement levels that guide our mapping priority:

| Level | Meaning | Our Approach |
|-------|---------|--------------|
| **Required** | MUST be present | Always map if source exists |
| **Conditionally Required** | Required when condition met | Map when Codex provides equivalent |
| **Recommended** | SHOULD be present | Map for dashboard functionality |
| **Opt-In** | Only when explicitly enabled | Not mapped (content redaction) |

### Required Attributes (Always Mapped)

- `gen_ai.provider.name` - hardcoded to `"openai"`
- `gen_ai.operation.name` - derived from span name
- `gen_ai.request.model` - from `codex.model`

### Conditionally Required (Mapped When Available)

- `gen_ai.conversation.id` - from `codex.conversation_id` or `codex.thread_id`

### Recommended (Mapped for Dashboard)

- `gen_ai.usage.input_tokens` - from `codex.input_tokens`
- `gen_ai.usage.output_tokens` - from `codex.output_tokens`
- `gen_ai.response.finish_reasons` - from `codex.finish_reason`
- `gen_ai.tool.name` - from `codex.tool_name`

## Future Considerations

- **Feature request to OpenAI**: Codex should natively emit GenAI semantic conventions
- **MCP semantic conventions**: OTel is developing [MCP-specific semconv](https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp/) which may apply to Codex tool calls
- **Metrics mapping**: Current implementation covers traces; metrics may need similar treatment
- **Content events**: Could map `codex.user_prompt` content to `gen_ai.content.prompt` events when `log_user_prompt = true`

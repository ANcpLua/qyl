# qyl. Specification Compliance Matrix

Implementation status for qyl. AI Observability Platform across supported languages.

## Legend

| Symbol | Meaning |
|--------|---------|
| `+` | Implemented |
| `-` | Not implemented |
| `?` | Unknown/Partial |
| `N/A` | Not applicable |

---

## Core Components

| Component | .NET | TypeScript | Python |
|-----------|------|------------|--------|
| qyl.collector | + | N/A | - |
| qyl.dashboard | N/A | + | N/A |
| qyl.agents.telemetry | + | N/A | - |
| qyl.providers.gemini | + | N/A | - |
| qyl.mcp.server | + | N/A | - |
| qyl.sdk.aspnetcore | + | N/A | - |

---

## OpenTelemetry Traces

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| **TracerProvider** | | | |
| Create TracerProvider | + | N/A | - |
| Get a Tracer | + | N/A | - |
| Associate Tracer with InstrumentationScope | + | N/A | - |
| Safe for concurrent calls | + | N/A | - |
| Shutdown | + | N/A | - |
| ForceFlush | + | N/A | - |
| **Span Operations** | | | |
| Create root span | + | N/A | - |
| Create with default parent (active span) | + | N/A | - |
| Create with parent from Context | + | N/A | - |
| UpdateName | + | N/A | - |
| User-defined start timestamp | + | N/A | - |
| End | + | N/A | - |
| End with timestamp | + | N/A | - |
| IsRecording | + | N/A | - |
| Set status (Unset, Ok, Error) | + | N/A | - |
| Safe for concurrent calls | + | N/A | - |
| **SpanContext** | | | |
| IsValid | + | + | - |
| IsRemote | + | + | - |
| Conforms to W3C TraceContext spec | + | + | - |
| **Span Attributes** | | | |
| SetAttribute | + | + | - |
| String type | + | + | - |
| Boolean type | + | + | - |
| Double floating-point type | + | + | - |
| Signed int64 type | + | + | - |
| Array of primitives (homogeneous) | + | + | - |
| **Span Events** | | | |
| AddEvent | + | + | - |
| Add order preserved | + | + | - |
| **Span Links** | | | |
| Links recorded on span creation | + | + | - |
| Links order preserved | + | + | - |

---

## OpenTelemetry Metrics

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| MeterProvider | - | N/A | - |
| Counter instrument | - | N/A | - |
| Histogram instrument | - | N/A | - |
| Gauge instrument | - | N/A | - |
| UpDownCounter instrument | - | N/A | - |
| Async instruments | - | N/A | - |
| Views | - | N/A | - |
| Aggregations | - | N/A | - |
| Exemplars | - | N/A | - |

---

## OpenTelemetry Logs

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| LoggerProvider | - | + | - |
| Logger.Emit(LogRecord) | - | + | - |
| SimpleLogRecordProcessor | - | N/A | - |
| BatchLogRecordProcessor | - | N/A | - |
| OTLP exporter | - | N/A | - |
| Trace Context Injection | - | + | - |

---

## GenAI Semantic Conventions (v1.38.0)

| Attribute | .NET | TypeScript | Python |
|-----------|------|------------|--------|
| **Provider** | | | |
| gen_ai.provider.name | + | + | - |
| **Operations** | | | |
| gen_ai.operation.name | + | + | - |
| Values: chat | + | + | - |
| Values: generate_content | + | + | - |
| Values: text_completion | + | + | - |
| Values: embeddings | + | + | - |
| Values: invoke_agent | + | + | - |
| Values: execute_tool | + | + | - |
| Values: create_agent | + | + | - |
| **Agent** | | | |
| gen_ai.agent.id | + | + | - |
| gen_ai.agent.name | + | + | - |
| gen_ai.agent.description | + | + | - |
| **Conversation** | | | |
| gen_ai.conversation.id | + | + | - |
| **Messages** | | | |
| gen_ai.system_instructions | + | + | - |
| gen_ai.input.messages | + | + | - |
| gen_ai.output.messages | + | + | - |
| gen_ai.output.type | + | + | - |
| **Request Parameters** | | | |
| gen_ai.request.model | + | + | - |
| gen_ai.request.temperature | + | + | - |
| gen_ai.request.top_k | + | + | - |
| gen_ai.request.top_p | + | + | - |
| gen_ai.request.presence_penalty | + | + | - |
| gen_ai.request.frequency_penalty | + | + | - |
| gen_ai.request.max_tokens | + | + | - |
| gen_ai.request.stop_sequences | + | + | - |
| gen_ai.request.choice.count | + | + | - |
| gen_ai.request.seed | + | + | - |
| gen_ai.request.encoding_formats | + | + | - |
| **Response** | | | |
| gen_ai.response.id | + | + | - |
| gen_ai.response.model | + | + | - |
| gen_ai.response.finish_reasons | + | + | - |
| **Usage** | | | |
| gen_ai.usage.input_tokens | + | + | - |
| gen_ai.usage.output_tokens | + | + | - |
| **Token Type** | | | |
| gen_ai.token.type | + | + | - |
| **Tool Integration** | | | |
| gen_ai.tool.definitions | + | + | - |
| gen_ai.tool.name | + | + | - |
| gen_ai.tool.description | + | + | - |
| gen_ai.tool.type | + | + | - |
| gen_ai.tool.call.id | + | + | - |
| gen_ai.tool.call.arguments | + | + | - |
| gen_ai.tool.call.result | + | + | - |
| **Data Source** | | | |
| gen_ai.data_source.id | + | + | - |
| **Embeddings** | | | |
| gen_ai.embeddings.dimension.count | + | + | - |
| **Evaluation** | | | |
| gen_ai.evaluation.name | + | + | - |
| gen_ai.evaluation.score.value | + | + | - |
| gen_ai.evaluation.score.label | + | + | - |
| gen_ai.evaluation.explanation | + | + | - |
| **Deprecated (Semconv 1.38)** | | | |
| gen_ai.system (deprecated) | + | + | - |
| gen_ai.prompt (deprecated) | + | + | - |
| gen_ai.completion (deprecated) | + | + | - |
| gen_ai.usage.prompt_tokens (deprecated) | + | + | - |
| gen_ai.usage.completion_tokens (deprecated) | + | + | - |
| **Error** | | | |
| error.type | + | + | - |
| error.message | + | + | - |

---

## AI Provider Support

| Provider | .NET | TypeScript | Python |
|----------|------|------------|--------|
| Google Gemini | + | N/A | - |
| OpenAI | ? | N/A | - |
| Anthropic | - | N/A | - |
| Azure OpenAI | - | N/A | - |
| Ollama | - | N/A | - |

---

## Microsoft.Extensions.AI

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| IChatClient interface | + | N/A | N/A |
| ChatMessage abstraction | + | N/A | N/A |
| Tool/Function support | + | N/A | N/A |
| Streaming responses | + | N/A | N/A |

---

## Microsoft.Agents.AI

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| AIAgent creation | + | N/A | N/A |
| AIAgentBuilder pattern | + | N/A | N/A |
| AgentWorkflowBuilder | + | N/A | N/A |
| Sequential workflows | + | N/A | N/A |
| UseOpenTelemetry instrumentation | + | N/A | N/A |
| AIFunctionFactory tool execution | + | N/A | N/A |
| DevUI integration | + | N/A | N/A |

---

## Model Context Protocol (MCP)

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| **Transport** | | | |
| stdio transport | + | N/A | - |
| HTTP/SSE transport | + | N/A | - |
| **Server** | | | |
| Tool registration | + | N/A | - |
| Tool discovery (manifest) | + | N/A | - |
| Tool invocation | + | N/A | - |
| InputSchema (JSON Schema) | + | N/A | - |
| **Collector MCP Tools** | | | |
| qyl.search_agent_runs | + | N/A | - |
| qyl.get_agent_run | + | N/A | - |
| qyl.get_token_usage | + | N/A | - |
| qyl.list_errors | + | N/A | - |
| qyl.get_latency_stats | + | N/A | - |
| get_sessions | + | N/A | - |
| get_trace | + | N/A | - |
| get_spans | + | N/A | - |
| get_genai_stats | + | N/A | - |
| search_errors | + | N/A | - |
| get_storage_stats | + | N/A | - |
| archive_old_data | + | N/A | - |

---

## Collector Features

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| **API Endpoints** | | | |
| POST /api/login | + | N/A | - |
| POST /api/logout | + | N/A | - |
| GET /api/auth/check | + | N/A | - |
| GET /api/v1/sessions | + | N/A | - |
| GET /api/v1/sessions/{id}/spans | + | N/A | - |
| GET /api/v1/traces/{id} | + | N/A | - |
| GET /api/v1/live (SSE) | + | N/A | - |
| GET /mcp/manifest | + | N/A | - |
| POST /mcp/tools/call | + | N/A | - |
| POST /api/v1/ingest | + | N/A | - |
| POST /v1/traces (OTLP) | + | N/A | - |
| POST /api/v1/feedback | + | N/A | - |
| GET /health | + | N/A | - |
| **Authentication** | | | |
| Token-based auth | + | + | - |
| Bearer token support | + | + | - |
| Cookie persistence | + | + | - |
| Fixed-time comparison | + | N/A | - |
| **Storage** | | | |
| DuckDB embedded | + | N/A | - |
| Parquet cold tier | + | N/A | - |
| Schema normalization (1.28→1.38) | + | N/A | - |
| Background write channel | + | N/A | - |
| **Real-time** | | | |
| SSE streaming | + | + | - |
| Pub/sub hub | + | N/A | - |
| Session filtering | + | + | - |
| Backpressure handling | + | N/A | - |
| Reconnection with backoff | N/A | + | - |

---

## Dashboard Features

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| **Pages** | | | |
| TracesPage (waterfall) | N/A | + | N/A |
| LogsPage (virtualized) | N/A | + | N/A |
| MetricsPage (charts) | N/A | + | N/A |
| GenAIPage (token stats) | N/A | + | N/A |
| ResourcesPage (services) | N/A | + | N/A |
| SettingsPage | N/A | + | N/A |
| **Data Fetching** | | | |
| TanStack Query integration | N/A | + | N/A |
| SSE live stream hook | N/A | + | N/A |
| Automatic cache invalidation | N/A | + | N/A |
| **Visualization** | | | |
| Recharts (Line/Area/Bar) | N/A | + | N/A |
| Span waterfall | N/A | + | N/A |
| Token cost calculation | N/A | + | N/A |
| **UI Components** | | | |
| Tailwind v4 (@theme) | N/A | + | N/A |
| Radix UI primitives | N/A | + | N/A |
| TanStack Virtual (10k+ rows) | N/A | + | N/A |
| Copy-to-clipboard | N/A | + | N/A |

---

## Exporters

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| **OTLP** | | | |
| OTLP/gRPC Exporter | + | N/A | - |
| OTLP/HTTP binary Protobuf | + | N/A | - |
| OTLP/HTTP JSON | - | N/A | - |
| Concurrent sending | - | N/A | - |
| Retryable responses | - | N/A | - |
| **Console** | | | |
| Standard output (logging) | + | + | - |

---

## Context Propagation

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| Create Context Key | + | + | - |
| Get value from Context | + | + | - |
| Set value for Context | + | + | - |
| Get current Context | + | + | - |
| TraceContext Propagator | + | + | - |
| B3 Propagator | - | - | - |
| Composite Propagator | + | N/A | - |

---

## Resource

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| Create from Attributes | + | + | - |
| Create empty | + | + | - |
| Retrieve attributes | + | + | - |
| Default service.name | + | + | - |
| Resource detector interface | + | N/A | - |

---

## Environment Variables

| Variable | .NET | TypeScript | Python |
|----------|------|------------|--------|
| QYL_PORT | + | N/A | - |
| QYL_TOKEN | + | N/A | - |
| QYL_DATA_PATH | + | N/A | - |
| OTEL_SEMCONV_STABILITY_OPT_IN | + | N/A | - |
| OTEL_RESOURCE_ATTRIBUTES | + | N/A | - |
| OTEL_SERVICE_NAME | + | N/A | - |
| OTEL_EXPORTER_OTLP_* | + | N/A | - |

---

## Deployment

| Feature | .NET | TypeScript | Python |
|---------|------|------------|--------|
| Native AOT compilation | + | N/A | N/A |
| Docker multi-stage build | + | + | - |
| Minimal image (~25MB) | + | N/A | - |
| Health check endpoints | + | N/A | - |
| docker-compose support | + | + | - |

---

## Summary Statistics

| Category | .NET | TypeScript | Python |
|----------|------|------------|--------|
| GenAI Semantic Conventions | 47/47 | 47/47 | 0/47 |
| OpenTelemetry Traces | 22/22 | 14/22 | 0/22 |
| OpenTelemetry Metrics | 0/9 | 0/9 | 0/9 |
| OpenTelemetry Logs | 0/5 | 2/5 | 0/5 |
| MCP Tools | 12/12 | 0/12 | 0/12 |
| Provider Support | 1/5 | 0/5 | 0/5 |

---

## References

- [OpenTelemetry GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
- [Microsoft.Agents.AI](https://github.com/microsoft/agent-framework)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai)
- [Mscc.GenerativeAI.Microsoft](https://github.com/mscraftsman/generative-ai)

---

*Last updated: 2025-12-09*
*Semantic Conventions Version: v1.38.0*

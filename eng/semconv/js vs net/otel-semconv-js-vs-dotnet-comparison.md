# OpenTelemetry Semantic Conventions v1.40.0

Canonical reference for `@opentelemetry/semantic-conventions` v1.40.0 (npm).
Schema URL: `https://opentelemetry.io/schemas/1.40.0`

This is the single source of truth. The .NET `OpenTelemetry.SemanticConventions` NuGet package (v1.0.0-rc9.9, frozen at spec v1.13.0) is dead and irrelevant.

---

## 1. Package

| Dimension | Value |
|---|---|
| Package | `@opentelemetry/semantic-conventions` |
| Version | **1.40.0** |
| Semconv spec | **v1.40.0** (2026-02-19) |
| Install | `npm install @opentelemetry/semantic-conventions` |
| Stable import | `import { ATTR_*, METRIC_* } from '@opentelemetry/semantic-conventions'` |
| Incubating import | `import { ATTR_*, METRIC_* } from '@opentelemetry/semantic-conventions/incubating'` |
| Total stable exports | ~195 constants (8% of total) |
| Total incubating exports | ~2,208 constants (92% of total) |

---

## 2. Naming Convention

All constants follow a single pattern:

| Type | Pattern | Example |
|---|---|---|
| Attribute key | `ATTR_` + SCREAMING_SNAKE | `ATTR_HTTP_REQUEST_METHOD` = `"http.request.method"` |
| Metric name | `METRIC_` + SCREAMING_SNAKE | `METRIC_HTTP_SERVER_REQUEST_DURATION` = `"http.server.request.duration"` |
| Enum value | `{ATTR_PREFIX}_VALUE_` + MEMBER | `HTTP_REQUEST_METHOD_VALUE_GET` = `"GET"` |

### In C# (v1.40.0 compliant)

Use string literals matching v1.40.0 names, or generate constants from the JS package:

```csharp
activity?.SetTag("http.request.method", "GET");
activity?.SetTag("http.response.status_code", 200);
activity?.SetTag("url.full", "https://example.com/api");
activity?.SetTag("gen_ai.provider.name", "anthropic");
activity?.SetTag("gen_ai.request.model", "claude-opus-4-6");
```

Or use generated constants (from `generate-semconv.ts`):

```csharp
activity?.SetTag(HttpAttributes.RequestMethod, "GET");
activity?.SetTag(HttpAttributes.ResponseStatusCode, 200);
activity?.SetTag(UrlAttributes.Full, "https://example.com/api");
activity?.SetTag(GenAiAttributes.ProviderName, "anthropic");
```

---

## 3. Stable Attributes (v1.40.0)

### HTTP

| Constant | Value |
|---|---|
| `ATTR_HTTP_REQUEST_METHOD` | `http.request.method` |
| `ATTR_HTTP_REQUEST_METHOD_ORIGINAL` | `http.request.method_original` |
| `ATTR_HTTP_REQUEST_RESEND_COUNT` | `http.request.resend_count` |
| `ATTR_HTTP_RESPONSE_STATUS_CODE` | `http.response.status_code` |
| `ATTR_HTTP_ROUTE` | `http.route` |

### URL

| Constant | Value |
|---|---|
| `ATTR_URL_FULL` | `url.full` |
| `ATTR_URL_PATH` | `url.path` |
| `ATTR_URL_QUERY` | `url.query` |
| `ATTR_URL_SCHEME` | `url.scheme` |
| `ATTR_URL_TEMPLATE` | `url.template` |

### Network

| Constant | Value |
|---|---|
| `ATTR_NETWORK_PEER_ADDRESS` | `network.peer.address` |
| `ATTR_NETWORK_PEER_PORT` | `network.peer.port` |
| `ATTR_NETWORK_PROTOCOL_NAME` | `network.protocol.name` |
| `ATTR_NETWORK_PROTOCOL_VERSION` | `network.protocol.version` |
| `ATTR_NETWORK_TRANSPORT` | `network.transport` |
| `ATTR_NETWORK_TYPE` | `network.type` |
| `ATTR_NETWORK_LOCAL_ADDRESS` | `network.local.address` |
| `ATTR_NETWORK_LOCAL_PORT` | `network.local.port` |

### Server / Client

| Constant | Value |
|---|---|
| `ATTR_SERVER_ADDRESS` | `server.address` |
| `ATTR_SERVER_PORT` | `server.port` |
| `ATTR_CLIENT_ADDRESS` | `client.address` |
| `ATTR_CLIENT_PORT` | `client.port` |

### DB

| Constant | Value |
|---|---|
| `ATTR_DB_SYSTEM_NAME` | `db.system.name` |
| `ATTR_DB_COLLECTION_NAME` | `db.collection.name` |
| `ATTR_DB_NAMESPACE` | `db.namespace` |
| `ATTR_DB_OPERATION_NAME` | `db.operation.name` |
| `ATTR_DB_QUERY_TEXT` | `db.query.text` |
| `ATTR_DB_QUERY_SUMMARY` | `db.query.summary` |
| `ATTR_DB_OPERATION_BATCH_SIZE` | `db.operation.batch.size` |
| `ATTR_DB_RESPONSE_STATUS_CODE` | `db.response.status_code` |
| `ATTR_DB_RESPONSE_RETURNED_ROWS` | `db.response.returned_rows` |

### Service / Telemetry

| Constant | Value |
|---|---|
| `ATTR_SERVICE_NAME` | `service.name` |
| `ATTR_SERVICE_VERSION` | `service.version` |
| `ATTR_SERVICE_NAMESPACE` | `service.namespace` |
| `ATTR_SERVICE_INSTANCE_ID` | `service.instance.id` |
| `ATTR_TELEMETRY_SDK_NAME` | `telemetry.sdk.name` |
| `ATTR_TELEMETRY_SDK_VERSION` | `telemetry.sdk.version` |
| `ATTR_TELEMETRY_SDK_LANGUAGE` | `telemetry.sdk.language` |
| `ATTR_TELEMETRY_DISTRO_VERSION` | `telemetry.distro.version` |

### Error / Exception

| Constant | Value |
|---|---|
| `ATTR_ERROR_TYPE` | `error.type` |
| `ATTR_EXCEPTION_TYPE` | `exception.type` |
| `ATTR_EXCEPTION_MESSAGE` | `exception.message` |
| `ATTR_EXCEPTION_STACKTRACE` | `exception.stacktrace` |
| `ATTR_EXCEPTION_ESCAPED` | `exception.escaped` |

### Code

| Constant | Value |
|---|---|
| `ATTR_CODE_FUNCTION_NAME` | `code.function.name` |
| `ATTR_CODE_FILE_PATH` | `code.file.path` |
| `ATTR_CODE_LINE_NUMBER` | `code.line.number` |
| `ATTR_CODE_COLUMN_NUMBER` | `code.column.number` |
| `ATTR_CODE_STACKTRACE` | `code.stacktrace` |

### OTel

| Constant | Value |
|---|---|
| `ATTR_OTEL_SCOPE_NAME` | `otel.scope.name` |
| `ATTR_OTEL_SCOPE_VERSION` | `otel.scope.version` |
| `ATTR_OTEL_STATUS_CODE` | `otel.status_code` |
| `ATTR_OTEL_STATUS_DESCRIPTION` | `otel.status_description` |

### User Agent

| Constant | Value |
|---|---|
| `ATTR_USER_AGENT_ORIGINAL` | `user_agent.original` |

### Deployment

| Constant | Value |
|---|---|
| `ATTR_DEPLOYMENT_ENVIRONMENT_NAME` | `deployment.environment.name` |

---

## 4. Stable Metrics (v1.40.0)

### HTTP

| Constant | Value |
|---|---|
| `METRIC_HTTP_CLIENT_REQUEST_DURATION` | `http.client.request.duration` |
| `METRIC_HTTP_SERVER_REQUEST_DURATION` | `http.server.request.duration` |

### ASP.NET Core

| Constant | Value |
|---|---|
| `METRIC_ASPNETCORE_ROUTING_MATCH_ATTEMPTS` | `aspnetcore.routing.match_attempts` |
| `METRIC_ASPNETCORE_DIAGNOSTICS_EXCEPTIONS` | `aspnetcore.diagnostics.exceptions` |
| `METRIC_ASPNETCORE_RATE_LIMITING_ACTIVE_REQUEST_LEASES` | `aspnetcore.rate_limiting.active_request_leases` |
| `METRIC_ASPNETCORE_RATE_LIMITING_REQUEST_LEASE_DURATION` | `aspnetcore.rate_limiting.request_lease.duration` |
| `METRIC_ASPNETCORE_RATE_LIMITING_QUEUED_REQUESTS` | `aspnetcore.rate_limiting.queued_requests` |
| `METRIC_ASPNETCORE_RATE_LIMITING_REQUEST_TIME_IN_QUEUE` | `aspnetcore.rate_limiting.request.time_in_queue` |
| `METRIC_ASPNETCORE_RATE_LIMITING_REQUESTS` | `aspnetcore.rate_limiting.requests` |

### Kestrel

| Constant | Value |
|---|---|
| `METRIC_KESTREL_ACTIVE_CONNECTIONS` | `kestrel.active_connections` |
| `METRIC_KESTREL_ACTIVE_TLS_HANDSHAKES` | `kestrel.active_tls_handshakes` |
| `METRIC_KESTREL_CONNECTION_DURATION` | `kestrel.connection.duration` |
| `METRIC_KESTREL_QUEUED_CONNECTIONS` | `kestrel.queued_connections` |
| `METRIC_KESTREL_QUEUED_REQUESTS` | `kestrel.queued_requests` |
| `METRIC_KESTREL_REJECTED_CONNECTIONS` | `kestrel.rejected_connections` |
| `METRIC_KESTREL_TLS_HANDSHAKE_DURATION` | `kestrel.tls_handshake.duration` |
| `METRIC_KESTREL_UPGRADED_CONNECTIONS` | `kestrel.upgraded_connections` |

### SignalR

| Constant | Value |
|---|---|
| `METRIC_SIGNALR_SERVER_ACTIVE_CONNECTIONS` | `signalr.server.active_connections` |
| `METRIC_SIGNALR_SERVER_CONNECTION_DURATION` | `signalr.server.connection.duration` |

### .NET Runtime

| Constant | Value |
|---|---|
| `METRIC_DOTNET_DNS_LOOKUP_DURATION` | `dotnet.dns.lookup.duration` |
| `METRIC_DOTNET_HTTP_CLIENT_ACTIVE_REQUESTS` | `dotnet.http.client.active_requests` |
| `METRIC_DOTNET_HTTP_CLIENT_CONNECTION_DURATION` | `dotnet.http.client.connection.duration` |
| `METRIC_DOTNET_HTTP_CLIENT_OPEN_CONNECTIONS` | `dotnet.http.client.open_connections` |
| `METRIC_DOTNET_HTTP_CLIENT_REQUEST_TIME_IN_QUEUE` | `dotnet.http.client.request.time_in_queue` |

### JVM

| Constant | Value |
|---|---|
| `METRIC_JVM_BUFFER_COUNT` | `jvm.buffer.count` |
| `METRIC_JVM_BUFFER_MEMORY_LIMIT` | `jvm.buffer.memory.limit` |
| `METRIC_JVM_BUFFER_MEMORY_USED` | `jvm.buffer.memory.used` |
| `METRIC_JVM_CLASS_COUNT` | `jvm.class.count` |
| `METRIC_JVM_CLASS_LOADED` | `jvm.class.loaded` |
| `METRIC_JVM_CLASS_UNLOADED` | `jvm.class.unloaded` |
| `METRIC_JVM_CPU_RECENT_UTILIZATION` | `jvm.cpu.recent_utilization` |
| `METRIC_JVM_CPU_TIME` | `jvm.cpu.time` |
| `METRIC_JVM_GC_DURATION` | `jvm.gc.duration` |
| `METRIC_JVM_MEMORY_COMMITTED` | `jvm.memory.committed` |
| `METRIC_JVM_MEMORY_LIMIT` | `jvm.memory.limit` |
| `METRIC_JVM_MEMORY_USED` | `jvm.memory.used` |

### DB

| Constant | Value |
|---|---|
| `METRIC_DB_CLIENT_OPERATION_DURATION` | `db.client.operation.duration` |

---

## 5. Incubating Domains (top 15 by attribute count)

These live under `@opentelemetry/semantic-conventions/incubating`.

| Domain | Attributes | Metrics | Events |
|---|---|---|---|
| `k8s.*` | 77 | 30+ | - |
| `gen_ai.*` | 56 | 5 | 7 |
| `aws.*` | 53 | - | - |
| `messaging.*` | 46 | 3 | - |
| `cloud.*` | 30 | - | - |
| `container.*` | 25 | - | - |
| `host.*` | 24 | - | - |
| `process.*` | 22 | 8 | - |
| `http.*` | 20 | - | - |
| `network.*` | 18 | - | - |
| `db.*` | 16 | 6 | - |
| `system.*` | 16 | 30+ | - |
| `os.*` | 14 | - | - |
| `tls.*` | 14 | - | - |
| `rpc.*` | 12 | 3 | - |

### gen_ai Deep Dive

Key attributes (v1.40.0 names):

| Attribute | Value |
|---|---|
| `gen_ai.provider.name` | Provider identity (was `gen_ai.system` before v1.37) |
| `gen_ai.request.model` | Model requested |
| `gen_ai.response.model` | Model that responded |
| `gen_ai.request.max_tokens` | Max tokens requested |
| `gen_ai.request.temperature` | Temperature |
| `gen_ai.request.top_p` | Top-p |
| `gen_ai.request.top_k` | Top-k |
| `gen_ai.request.seed` | Seed (was `gen_ai.openai.request.seed` before v1.30) |
| `gen_ai.usage.input_tokens` | Input tokens (was `gen_ai.usage.prompt_tokens` before v1.27) |
| `gen_ai.usage.output_tokens` | Output tokens (was `gen_ai.usage.completion_tokens` before v1.27) |
| `gen_ai.operation.name` | Operation (chat, text_completion, embeddings) |
| `gen_ai.request.encoding_formats` | Encoding formats |

Provider enum values for `gen_ai.provider.name`:

`anthropic`, `openai`, `azure_ai_inference`, `az.ai.openai`, `aws.bedrock`, `gcp.vertex_ai`, `cohere`, `deepseek`, `groq`, `ibm_watsonx_ai`, `mistral_ai`, `perplexity`, `xai`

### MCP Deep Dive

| Attribute | Value |
|---|---|
| `gen_ai.mcp.method` | MCP method name |
| `gen_ai.mcp.transport` | Transport type |
| `gen_ai.mcp.session.id` | Session ID |
| `gen_ai.mcp.notification.method` | Notification method |

Method enum values: `completion/complete`, `initialize`, `logging/setLevel`, `ping`, `prompts/get`, `prompts/list`, `resources/list`, `resources/read`, `resources/subscribe`, `resources/templates/list`, `resources/unsubscribe`, `roots/list`, `sampling/createMessage`, `tools/call`, `tools/list`

---

## 6. Stable/Incubating Split Mechanism

The npm package uses Node.js subpath exports:

```json
{
  "exports": {
    ".": "./build/src/index.js",
    "./incubating": "./build/src/incubating/index.js"
  }
}
```

- `import { ATTR_HTTP_REQUEST_METHOD } from '@opentelemetry/semantic-conventions'` — stable only
- `import { ATTR_GEN_AI_PROVIDER_NAME } from '@opentelemetry/semantic-conventions/incubating'` — incubating (re-exports stable too)

Tree-shaking works: bundlers only include the constants you import.

---

## 7. Generator

`generate-semconv.ts` parses `@opentelemetry/semantic-conventions` v1.40.0 `.d.ts` files and emits:

| Output | Format | Use |
|---|---|---|
| `semconv.ts` | TypeScript flat constants | Dashboard/frontend |
| `SemanticConventions.g.cs` | C# `const string` classes | Backend instrumentation |
| `SemanticConventions.Utf8.g.cs` | C# `ReadOnlySpan<byte>` | Zero-allocation OTLP parsing |
| `semconv.g.tsp` | TypeSpec models + unions | API spec codegen |
| `promoted-columns.g.sql` | DuckDB column definitions | Telemetry storage |
| Protocol facades | C# domain classes | Per-domain typed constants |

All outputs use v1.40.0 names exclusively. No shims, no fallbacks, no deprecated names.

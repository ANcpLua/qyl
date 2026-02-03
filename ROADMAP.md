# qyl Roadmap - Aspire-Inspired Features

**Philosophy:** Aspire with persistence, zero-config GenAI, Railway simplicity.

## Legend

- [x] Done
- [~] Partial
- [ ] TODO
- [—] Won't do (different approach)

---

## 1. Core Platform

### Storage & Persistence

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Persistent telemetry | [x] | DuckDB (columnar) | In-memory only |
| Idempotent ingestion | [x] | `ON CONFLICT DO UPDATE` | None |
| Data survives restart | [x] | DuckDB file | Lost on restart |
| Volumes/Bind mounts | [—] | Not needed (DuckDB is the volume) | WithDataVolume |

### OTLP Ingestion

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| gRPC OTLP (4317) | [x] | Grpc.AspNetCore | Same |
| HTTP OTLP (5100) | [x] | JSON endpoint | Same |
| HTTP Protobuf | [ ] | TODO | Supported |
| OTLP CORS | [ ] | TODO for browser apps | Dashboard:Otlp:Cors |

---

## 2. Dashboard

### Core Views

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Traces page | [x] | React | Blazor |
| Trace detail with spans | [x] | Expandable tree | Same |
| Structured logs | [x] | Filterable table | Same |
| Console logs | [ ] | TODO | Per-resource stdout |
| Metrics page | [~] | Ingestion yes, UI? | Charts + table view |
| Resources page | [—] | No AppHost concept | Lists all resources |
| Resource graph view | [ ] | Could add trace graph | Visual dependencies |

### GenAI Telemetry

| Feature | Status | qyl Approach | Aspire 13.1 |
|---------|--------|--------------|-------------|
| GenAI span visualization | [x] | Native support | GenAI visualizer |
| Token usage display | [x] | gen_ai.usage.* | Same |
| Model info | [x] | gen_ai.request.model | Same |
| Tool definitions view | [ ] | TODO | New in 13.1 |
| Video/audio preview | [ ] | TODO | New in 13.1 |
| Message content capture | [x] | Via interceptor | Manual env var |

### UX Features

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Real-time streaming | [x] | SSE | SignalR |
| Theme selection | [ ] | TODO (dark only?) | Light/Dark/System |
| Keyboard shortcuts | [ ] | TODO | R/C/S/T/M navigation |
| Text visualizer | [ ] | TODO | JSON/XML formatting |
| Pause telemetry | [ ] | TODO | Per-type pause |
| Clear telemetry | [ ] | TODO | Per-resource or all |
| Download logs | [ ] | TODO | Console log download |
| Filter by resource | [x] | Query params | Dropdown |
| Health check status | [ ] | TODO | With last run time |
| Polyglot icons | [ ] | TODO | Language-specific icons |

---

## 3. MCP Server Integration

| Feature | Status | qyl Approach | Aspire 13.1 |
|---------|--------|--------------|-------------|
| MCP endpoint | [x] | qyl.mcp project | Dashboard built-in |
| list_traces | [x] | Via HTTP API | MCP tool |
| list_structured_logs | [x] | Via HTTP API | MCP tool |
| list_resources | [—] | No resources concept | MCP tool |
| list_console_logs | [ ] | TODO | MCP tool |
| execute_resource_command | [—] | No resources | MCP tool |
| API key auth | [ ] | TODO | x-mcp-api-key header |
| MCP config dialog | [ ] | TODO | Dashboard UI |

---

## 4. Auto-Instrumentation

### GenAI Interceptors

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| IChatClient interception | [x] | Source generator | Manual UseOpenTelemetry() |
| OpenAI SDK | [x] | Source generator | Manual |
| Anthropic SDK | [x] | Source generator | Manual |
| Azure.AI.Inference | [x] | Source generator | Manual |
| Ollama | [x] | Source generator | Not supported |
| Semantic Kernel | [~] | Partial | Not supported |
| Zero-config setup | [x] | AddQylServiceDefaults() | Multiple steps |

### Database Interceptors

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| EF Core interception | [~] | Source generator | OTel.Instrumentation |
| Connection tracking | [x] | db.* attributes | Same |
| Query capture | [ ] | TODO (opt-in) | Not by default |

### Other Instrumentation

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| HTTP client/server | [—] | Use OTel packages | Same |
| gRPC | [—] | Use OTel packages | Same |
| [Traced] attribute | [x] | Source generator | None |
| Custom meters | [x] | [Meter] attribute | None |

---

## 5. Authentication & Security

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Dashboard auth | [ ] | TODO | BrowserToken/OIDC |
| OTLP auth | [ ] | TODO | ApiKey/Certificate |
| MCP auth | [ ] | TODO | ApiKey |
| Unsecured mode | [x] | Default (dev) | Explicit flag |
| TLS termination | [ ] | TODO | Built-in (13.1) |
| Dev cert trust | [ ] | TODO | Auto-configured |

---

## 6. Configuration

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Env var config | [x] | QYL_* vars | ASPIRE_* vars |
| JSON config file | [ ] | TODO | appsettings.json |
| Telemetry limits | [ ] | TODO (hardcoded) | MaxLogCount etc. |
| OTLP endpoint config | [x] | OTEL_EXPORTER_OTLP_ENDPOINT | Same |

---

## 7. Deployment

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Docker image | [x] | Single container | Single container |
| docker-compose | [x] | Included | Optional |
| Railway deploy | [x] | One-click | Not supported |
| Azure deploy | [ ] | TODO | aspire deploy |
| AWS deploy | [ ] | TODO | Community |
| Kubernetes | [ ] | TODO | aspire publish |

---

## 8. Developer Experience

### CLI Commands

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Init command | [—] | Not needed (standalone) | aspire init |
| Update command | [—] | Not needed | aspire update |
| Run command | [x] | dotnet run / docker | aspire run |
| Deploy command | [ ] | TODO | aspire deploy |
| Add integration | [—] | NuGet reference | aspire add |

### Templates

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| Starter template | [ ] | TODO | aspire-starter |
| Python template | [—] | Works with any OTel | aspire-py-starter |
| JS template | [—] | Works with any OTel | aspire-ts-cs-starter |

---

## 9. TypeSpec-First (qyl Unique)

| Feature | Status | Notes |
|---------|--------|-------|
| TypeSpec schemas | [x] | core/specs/*.tsp |
| OpenAPI generation | [x] | tsp compile |
| C# codegen | [x] | SchemaGenerator |
| DuckDB schema gen | [x] | SchemaGenerator |
| TypeScript types | [x] | openapi-typescript |
| JSON Schema | [x] | TypeSpec emitter |

---

## 10. Polyglot Support

| Feature | Status | qyl Approach | Aspire |
|---------|--------|--------------|--------|
| .NET apps | [x] | Source generator | First-class |
| Python apps | [x] | Standard OTel SDK | AddUvicornApp |
| Node.js apps | [x] | Standard OTel SDK | AddJavaScriptApp |
| Go apps | [x] | Standard OTel SDK | Community |
| Java apps | [x] | Standard OTel SDK | Community |
| Any OTel app | [x] | OTLP endpoint | Same |

**qyl advantage:** We don't need language-specific hosting APIs because we're a standalone collector. Any app that speaks OTLP works.

---

## Priority Queue

### P0 - Must Have

1. [ ] Dashboard auth (at least basic token)
2. [ ] Metrics UI (we ingest, need to display)
3. [ ] Telemetry limits config
4. [ ] OTLP CORS for browser apps

### P1 - Should Have

5. [ ] Theme selection (light/dark)
6. [ ] Keyboard shortcuts
7. [ ] GenAI tool definitions view
8. [ ] MCP auth

### P2 - Nice to Have

9. [ ] Console logs (stdout capture)
10. [ ] Resource graph (trace visualization)
11. [ ] Health check UI
12. [ ] Download logs

### P3 - Future

13. [ ] Azure/AWS deploy commands
14. [ ] Kubernetes manifests
15. [ ] Video/audio preview in GenAI

---

## Anti-Goals (Won't Do)

These are Aspire features we explicitly **won't** implement because qyl has a different philosophy:

| Feature | Why Not |
|---------|---------|
| AppHost orchestration | qyl is standalone collector, not orchestrator |
| Resource start/stop | No resource management |
| aspire init/add | Use standard dotnet/npm/pip |
| Lifecycle hooks | No app lifecycle |
| Publishing callbacks | No deployment orchestration |
| WithReference API | Use env vars directly |
| Single-file AppHost | Use docker-compose |

---

## Summary

**qyl = Aspire Dashboard + DuckDB + Zero-Config GenAI**

```
What Aspire does:           What qyl does:
─────────────────           ──────────────
Orchestrates apps     →     Collects telemetry
In-memory storage     →     Persistent DuckDB
Manual GenAI setup    →     Auto-instrumentation
AppHost required      →     Standalone collector
Complex deployment    →     docker run / Railway
```

The goal is **simplicity**: one container, persistent storage, zero-config GenAI tracing.

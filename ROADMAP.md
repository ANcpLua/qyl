# qyl Roadmap

**Philosophy:** Aspire with persistence, zero-config GenAI, Railway simplicity.

---

## P1 - Should Have

### Dashboard UX

| Feature | Notes |
|---------|-------|
| Theme selection | Light/Dark/System |
| Keyboard shortcuts | R/C/S/T/M navigation |
| Text visualizer | JSON/XML formatting |
| Pause telemetry | Per-type pause |
| Clear telemetry | Per-resource or all |

### GenAI Telemetry

| Feature | Notes |
|---------|-------|
| Tool definitions view | New in Aspire 13.1 |
| MCP auth | x-mcp-api-key header |

## P2 - Nice to Have

### Dashboard

| Feature | Notes |
|---------|-------|
| Console logs | Per-resource stdout capture |
| Resource graph view | Trace visualization |
| Health check status | With last run time |
| Download logs | Console log download |

### Ingestion

| Feature | Notes |
|---------|-------|
| HTTP Protobuf OTLP | Binary protobuf over HTTP |

### MCP Server

| Feature | Notes |
|---------|-------|
| list_console_logs | Depends on console log capture |
| MCP config dialog | Dashboard UI for MCP setup |

### Auto-Instrumentation

| Feature | Notes |
|---------|-------|
| Semantic Kernel | Partial — needs completion |
| EF Core interception | Source generator — partial |
| Query capture | Opt-in db.statement capture |

### Security

| Feature | Notes |
|---------|-------|
| TLS termination | Built-in TLS |
| Dev cert trust | Auto-configured dev certs |

### Configuration

| Feature | Notes |
|---------|-------|
| JSON config file | appsettings.json alternative to env vars |

## P3 - Future

| Feature | Notes |
|---------|-------|
| Azure deploy | aspire deploy equivalent |
| AWS deploy | Community equivalent |
| Kubernetes manifests | aspire publish equivalent |
| Video/audio preview | GenAI multimodal content |
| Starter template | dotnet new qyl-starter |
| Deploy CLI command | qyl deploy |

---

## Anti-Goals

| Feature | Why Not |
|---------|---------|
| AppHost orchestration | qyl is standalone collector, not orchestrator |
| Resource start/stop | No resource management |
| aspire init/add | Use standard dotnet/npm/pip |
| Lifecycle hooks | No app lifecycle |
| Publishing callbacks | No deployment orchestration |
| WithReference API | Use env vars directly |

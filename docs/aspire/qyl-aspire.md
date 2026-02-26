# qyl ↔ Aspire 13.x Coverage

Aspire registry: `aspire.md`
qyl constraints: net10.0, Linux, cloud-agnostic, no Azure, no Windows, no mobile

File paths verified against disk as of 2026-02-22.

Status key:
- **DONE** — fully implemented, file paths verified
- **PARTIAL** — core concept present, specific gap stated
- **NOT APPLICABLE** — outside qyl's design scope (Azure, IDE-specific, mobile, CLI tooling)
- **NOT PRESENT** — in scope but not implemented

---

## Summary Table

| ASP-ID | Feature | qyl Status | Notes |
|--------|---------|------------|-------|
| ASP-001 | Polyglot platform | DONE | qyl.hosting orchestrates .NET/Node/Python/Vite |
| ASP-002 | AddPythonApp / AddPythonModule / AddPythonExecutable | DONE | PythonResource |
| ASP-003 | AddUvicornApp (ASGI) | DONE | Dedicated `UvicornResource` class exists in PythonResource.cs with WithUv() and Type="uvicorn" |
| ASP-004 | Python package management (uv/pip) | PARTIAL | `WithUv()` / `UseUv` property implemented for explicit opt-in; no auto-detection from pyproject.toml / requirements.txt |
| ASP-005 | Automatic Dockerfile generation (Python) | NOT PRESENT | qyl.hosting runs locally; no container build system |
| ASP-006 | Python version detection | NOT PRESENT | No .python-version / pyproject.toml inspection |
| ASP-007 | VS Code debugging for Python | NOT APPLICABLE | IDE tooling, not qyl's concern |
| ASP-008 | Unified JS app model (AddJavaScriptApp) | DONE | NodeResource |
| ASP-009 | Package manager flexibility (npm/yarn/pnpm) | NOT PRESENT | NodeResource uses npm; no auto-detect |
| ASP-010 | Customizing scripts and arguments | PARTIAL | NodeResource exposes script configuration |
| ASP-011 | Vite support (AddViteApp) | DONE | ViteResource |
| ASP-012 | Dynamic Dockerfile generation (JS) | NOT PRESENT | qyl.hosting runs locally; no container build |
| ASP-013 | Node support (AddNodeApp) | DONE | NodeResource |
| ASP-014 | JS/TS starter template | NOT APPLICABLE | Template system, not qyl's concern |
| ASP-015 | Vite HTTPS runtime configuration | NOT PRESENT | ViteResource doesn't manage TLS certs |
| ASP-016 | AppHost SDK modernization | DONE | QylApp / QylAppBuilder replace AppHost |
| ASP-017 | Single-file AppHost / C# file-based apps | DONE | QylApp.CreateBuilder() is minimal-file pattern |
| ASP-018 | Polyglot connection properties | PARTIAL | Resource-to-resource refs work; no JDBC injection |
| ASP-019 | Simplified service URL env vars | PARTIAL | Resources inject URLs; no API_HTTP / API_HTTPS convention |
| ASP-020 | Named references | NOT PRESENT | No named alias API |
| ASP-021 | Endpoint reference enhancements | NOT PRESENT | Basic endpoint model only |
| ASP-022 | Network identifiers | NOT PRESENT | No network identifier API |
| ASP-023 | aspire init command | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003) |
| ASP-024 | aspire new curated templates | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003) |
| ASP-025 | aspire update / self-update | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003) |
| ASP-026 | CLI channel control | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003) |
| ASP-027 | Auto running-instance detection | NOT PRESENT | QylRunner doesn't check for running instances |
| ASP-028 | --skip-path install option | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003) |
| ASP-029 | CLI config model (.aspire/settings.json) | NOT APPLICABLE | qyl uses env vars and QYL_* settings |
| ASP-030 | aspire config CLI commands | NOT APPLICABLE | qyl uses env vars |
| ASP-031 | aspire mcp init | EXCEEDS | qyl.mcp is always available; no init step needed |
| ASP-032 | MCP tooling (list_integrations, list_apphosts) | EXCEEDS | qyl.mcp has 15+ tools; richer than Aspire's 4 |
| ASP-033 | Dashboard MCP endpoints | EXCEEDS | Full telemetry via MCP; not just resource status |
| ASP-034 | Dashboard parameters tab | DONE | qyl.dashboard shows resource configuration |
| ASP-035 | GenAI Visualizer | EXCEEDS | AgentRunsPage with waterfall, cost/token, tool sequencing |
| ASP-036 | Interaction services (dynamic inputs) | NOT PRESENT | No interactive resource parameter prompts |
| ASP-037 | Cross-language certificate trust | NOT PRESENT | No dev-cert injection across runtimes |
| ASP-038 | TLS termination APIs | NOT PRESENT | No WithHttpsCertificate-style APIs |
| ASP-039 | aspire do pipeline system | NOT APPLICABLE | qyl uses QylRunner + Docker; not a general pipeline |
| ASP-040 | Container files as build artifacts | NOT APPLICABLE | No multi-container artifact copy support |
| ASP-041 | Docker Compose improvements | NOT APPLICABLE | qyl doesn't generate Compose files |
| ASP-042 | OTel SDK integration updates | EXCEEDS | qyl.servicedefaults is qyl's OTel SDK layer; more complete than Aspire's defaults |

**Result: 9 DONE, 5 EXCEEDS, 4 PARTIAL, 12 NOT PRESENT, 12 NOT APPLICABLE**

---

## Detail: ASP-001 — Polyglot platform

**Status: DONE**

qyl.hosting provides a resource orchestration model that mirrors Aspire's polyglot approach.

| Resource Type | File |
|---|---|
| IQylResource (base interface) | `src/qyl.hosting/Resources/IQylResource.cs` |
| ProjectResource (.NET) | `src/qyl.hosting/Resources/ProjectResource.cs` |
| NodeResource (JS/Node) | `src/qyl.hosting/Resources/NodeResource.cs` |
| ViteResource (Vite frontend) | `src/qyl.hosting/Resources/ViteResource.cs` |
| PythonResource (Python) | `src/qyl.hosting/Resources/PythonResource.cs` |
| ContainerResource (Docker) | `src/qyl.hosting/Resources/ContainerResource.cs` |
| QylApp / QylAppBuilder | `src/qyl.hosting/QylApp.cs`, `QylAppBuilder.cs` |
| QylRunner (orchestration loop) | `src/qyl.hosting/QylRunner.cs` |

---

## Detail: ASP-002 — AddPythonApp / AddPythonModule / AddPythonExecutable

**Status: DONE**

`PythonResource` handles Python processes. No separate Module/Executable subtypes — one resource
type covers all Python workload patterns (script, module, ASGI).

File: `src/qyl.hosting/Resources/PythonResource.cs`

---

## Detail: ASP-003 — AddUvicornApp (ASGI)

**Status: DONE**

A dedicated `UvicornResource` class exists alongside `PythonResource` with explicit ASGI wiring.

File: `src/qyl.hosting/Resources/PythonResource.cs`

`UvicornResource` has `Type = "uvicorn"`, `WithUv()` method, and its own constructor. No separate
file — both `PythonResource` and `UvicornResource` are defined in `PythonResource.cs`.

---

## Detail: ASP-004 — Python package management (uv/pip)

**Status: PARTIAL**

`WithUv()` / `UseUv` property is implemented on both `PythonResource` and `UvicornResource` for
explicit uv opt-in.

File: `src/qyl.hosting/Resources/PythonResource.cs`

**Gap:** No auto-detection from `pyproject.toml`, `requirements.txt`, or `.python-version`. No `pip` runner.

## Detail: ASP-005..006 — Dockerfile generation, Python version detection

**Status: NOT PRESENT**

`PythonResource` starts a Python process locally. It does not:
- Generate Dockerfiles from `pyproject.toml`/`requirements.txt`
- Inspect `.python-version` for interpreter selection

---

## Detail: ASP-008 — Unified JavaScript application model

**Status: DONE**

`NodeResource` handles Node.js applications with script configuration.

File: `src/qyl.hosting/Resources/NodeResource.cs`

---

## Detail: ASP-011 — Vite support

**Status: DONE**

`ViteResource` is a dedicated resource type for Vite development servers with HMR and proxy support.

File: `src/qyl.hosting/Resources/ViteResource.cs`

---

## Detail: ASP-013 — Node support

**Status: DONE**

`NodeResource` handles Node.js processes.

File: `src/qyl.hosting/Resources/NodeResource.cs`

---

## Detail: ASP-016..017 — AppHost SDK / Single-file AppHost

**Status: DONE**

qyl.hosting's `QylApp.CreateBuilder()` + `QylAppBuilder` provide the same minimal-file host setup.
No separate csproj SDK boilerplate required.

| File | Role |
|---|---|
| `src/qyl.hosting/QylApp.cs` | Entry point builder |
| `src/qyl.hosting/QylAppBuilder.cs` | Builder pattern (add resources, configure) |
| `src/qyl.hosting/QylRunner.cs` | Orchestration runner (starts/stops resources) |

---

## Detail: ASP-018..019 — Polyglot connection properties / service URL env vars

**Status: PARTIAL**

Resources can reference each other and inject URLs. There is no formal `WithReference()` API
injecting standardized JDBC strings, `HostName`/`Port` properties, or `API_HTTP`/`API_HTTPS`
env var conventions.

---

## Detail: ASP-031..033 — MCP tooling

**Status: EXCEEDS**

Aspire 13.1 adds 4 MCP tools (`list_integrations`, `get_integration_docs`, `list_apphosts`,
`select_apphost`). qyl.mcp has 60+ individual tool methods across 14 tool files.

| qyl.mcp Tools | File |
|---|---|
| TelemetryTools | `src/qyl.mcp/Tools/TelemetryTools.cs` |
| StructuredLogTools | `src/qyl.mcp/Tools/StructuredLogTools.cs` |
| GenAiTools | `src/qyl.mcp/Tools/GenAiTools.cs` |
| AgentTools | `src/qyl.mcp/Tools/AgentTools.cs` |
| IssueTools | `src/qyl.mcp/Tools/IssueTools.cs` |
| BuildTools | `src/qyl.mcp/Tools/BuildTools.cs` |
| ReplayTools | `src/qyl.mcp/Tools/ReplayTools.cs` |
| SearchTools | `src/qyl.mcp/Tools/SearchTools.cs` |
| WorkflowTools | `src/qyl.mcp/Tools/WorkflowTools.cs` |
| AnalyticsTools | `src/qyl.mcp/Tools/AnalyticsTools.cs` |
| StorageTools | `src/qyl.mcp/Tools/StorageTools.cs` |
| ConsoleTools | `src/qyl.mcp/Tools/ConsoleTools.cs` |
| CopilotTools | `src/qyl.mcp/Tools/CopilotTools.cs` |
| WorkspaceTools | `src/qyl.mcp/Tools/WorkspaceTools.cs` |

---

## Detail: ASP-034 — Dashboard parameters tab

**Status: DONE**

qyl.dashboard React app shows resource configuration and parameters.

File: `src/qyl.dashboard/src/`

---

## Detail: ASP-035 — GenAI Visualizer

**Status: EXCEEDS**

Aspire 13.1 adds tool definitions/evaluations and audio/video preview to its GenAI Visualizer.
qyl has a full agent run dashboard with trace waterfall, token/cost analytics, and tool call
sequencing with sequence numbers.

| qyl Implementation | File |
|---|---|
| Agent runs page | `src/qyl.dashboard/src/pages/AgentRunsPage.tsx` |
| Agent run detail (waterfall) | `src/qyl.dashboard/src/pages/AgentRunDetailPage.tsx` |
| agent_runs + tool_calls storage | `src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs` |

---

## Detail: ASP-042 — OTel SDK integration updates

**Status: EXCEEDS**

Aspire 13.1 updates its bundled OTel SDK version. qyl.servicedefaults is qyl's equivalent of
Aspire's OTel defaults — with source-generated compile-time interceptors ([Traced], [GenAi], [Db]),
GenAI instrumentation, and full OTel SemConv v1.39 compliance.

| qyl Implementation | File |
|---|---|
| Service defaults / OTel wiring | `src/qyl.servicedefaults/` |
| Compile-time interceptors | `src/qyl.servicedefaults.generator/` |
| GenAI instrumentation | `src/qyl.servicedefaults/Instrumentation/GenAi/GenAiInstrumentation.cs` |
| Telemetry source generators | `src/qyl.instrumentation.generators/` |

---

## qyl-Only Features (no Aspire equivalent)

| Feature | File |
|---|---|
| Build failure capture (MSBuild binlog → DuckDB) | `src/qyl.collector/BuildFailures/` |
| OTLP PII scrubbing at ingestion boundary | `src/qyl.collector/Ingestion/` |
| Error fingerprinting + GenAI-aware grouping | `src/qyl.collector/Errors/ErrorFingerprinter.cs` |
| Autofix orchestration with policy gates | `src/qyl.collector/Autofix/AutofixOrchestrator.cs` |
| Workflow engine (nodes, checkpoints, shared state, routing) | `src/qyl.collector/Workflow/` |
| qyl.watch (live terminal span viewer) | `src/qyl.watch/` |
| qyl.watchdog (process anomaly detection) | `src/qyl.watchdog/` |
| qyl.browser (Web Vitals + interactions OTLP SDK) | `src/qyl.browser/` |

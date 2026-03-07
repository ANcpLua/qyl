# qyl vs Aspire 13.x Coverage

> Merged from: `aspire/aspire.md` (feature registry) + `aspire/qyl-aspire.md` (coverage comparison)

---

## Part 1: Feature Catalog

# Aspire 13.x Feature Registry

Pinned commit: `252619a35e515c291172ce5c8e32f32159b81f12`
Date: 2026-02-22
Sources:

- https://github.com/microsoft/aspire.dev/commit/252619a35e515c291172ce5c8e32f32159b81f12
- https://aspire.dev/whats-new/aspire-13/
- https://aspire.dev/whats-new/aspire-13-1/
- https://aspire.dev/reference/cli/configuration/

Replaces: README.aspire-feature-catalog.md, aspire-13-coverage-notes.md, aspire-13-release-notes.md,
aspire-13-1-release-notes.md, ASPIRE-13x-JIRA-THEORETICAL-TASKS.md, aspire-api-browser.md,
aspire-cli-configuration.md, ASPIRE.md

IDs: ASP-001..042 (fresh numbering, clean categories)

---

## Feature Catalog (CSV)

```csv
"id","feature_name","category","version_introduced","docs_url","qyl_relevance"
"ASP-001","Polyglot platform — Python/JS as first-class citizens alongside .NET","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#polyglot-platform","relevant"
"ASP-002","AddPythonApp / AddPythonModule / AddPythonExecutable","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#flexible-python-application-models","relevant"
"ASP-003","AddUvicornApp for ASGI applications (FastAPI/Uvicorn)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#uvicorn-integration-for-asgi-applications","relevant"
"ASP-004","Python package management (uv/pip auto-detect, WithUv/WithPip)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#python-package-management","relevant"
"ASP-005","Automatic Dockerfile generation for Python workloads","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#automatic-dockerfile-generation","relevant"
"ASP-006","Python version detection (.python-version / pyproject.toml)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#python-version-detection","relevant"
"ASP-007","VS Code debugging support for Python resources","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#vs-code-debugging-support","not-applicable"
"ASP-008","Unified JavaScript application model (AddJavaScriptApp)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#unified-javascript-application-model","relevant"
"ASP-009","Package manager flexibility (npm/yarn/pnpm auto-detect)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#package-manager-flexibility","relevant"
"ASP-010","Customizing scripts and passing arguments (package.json / API)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#customizing-scripts","relevant"
"ASP-011","Vite support (AddViteApp)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#vite-support","relevant"
"ASP-012","Dynamic Dockerfile generation for JS workloads","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#dynamic-dockerfile-generation","relevant"
"ASP-013","Node support (AddNodeApp)","polyglot","13.0","https://aspire.dev/whats-new/aspire-13/#node-support","relevant"
"ASP-014","JavaScript/TypeScript starter template (aspire-ts-cs-starter)","polyglot","13.1","https://aspire.dev/whats-new/aspire-13-1/#javascript-frontend-starter","relevant"
"ASP-015","Vite HTTPS runtime configuration (no manual vite.config changes)","polyglot","13.1","https://aspire.dev/whats-new/aspire-13-1/#javascript-frontend-starter","relevant"
"ASP-016","AppHost SDK modernization (Aspire.AppHost.Sdk/13.0.0, implicit Aspire.Hosting.AppHost)","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#apphost-project-structure","relevant"
"ASP-017","Single-file AppHost and C# file-based apps (#:sdk / AddCSharpApp)","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#single-file-apphost","relevant"
"ASP-018","Polyglot connection properties (WithReference, URI/JDBC/property injection)","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#polyglot-connection-properties","relevant"
"ASP-019","Simplified service URL environment variables (API_HTTP / API_HTTPS)","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#simplified-service-url-environment-variables","relevant"
"ASP-020","Named references (WithReference named aliases)","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#named-references","relevant"
"ASP-021","Endpoint reference enhancements","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#endpoint-reference-enhancements","relevant"
"ASP-022","Network identifiers for container networking","apphost","13.0","https://aspire.dev/whats-new/aspire-13/#network-identifiers","relevant"
"ASP-023","aspire init command (initialize existing repositories)","cli","13.0","https://aspire.dev/whats-new/aspire-13/#aspire-init-command","not-applicable"
"ASP-024","aspire new curated starter templates","cli","13.0","https://aspire.dev/whats-new/aspire-13/#aspire-new-curated-starter-templates","not-applicable"
"ASP-025","aspire update improvements and self-update","cli","13.0","https://aspire.dev/whats-new/aspire-13/#aspire-update-improvements","not-applicable"
"ASP-026","CLI channel control (--channel flag, persistent after aspire update --self)","cli","13.1","https://aspire.dev/whats-new/aspire-13-1/#cli-channel-control","not-applicable"
"ASP-027","Auto running-instance detection (aspire run terminates existing instances)","cli","13.1","https://aspire.dev/whats-new/aspire-13-1/#running-instance-detection","not-applicable"
"ASP-028","--skip-path installation option (install without PATH modification)","cli","13.1","https://aspire.dev/whats-new/aspire-13-1/#skip-path-install","not-applicable"
"ASP-029","CLI config model (.aspire/settings.json + ~/.aspire/globalsettings.json)","cli","13.x","https://aspire.dev/reference/cli/configuration/","not-applicable"
"ASP-030","aspire config list/get/set/delete commands","cli","13.x","https://aspire.dev/reference/cli/configuration/","not-applicable"
"ASP-031","aspire mcp init — MCP configuration for agent environments","mcp","13.1","https://aspire.dev/whats-new/aspire-13-1/#mcp-initialization","relevant"
"ASP-032","MCP CLI tooling (list_integrations, get_integration_docs, list_apphosts, select_apphost)","mcp","13.1","https://aspire.dev/whats-new/aspire-13-1/#mcp-tooling","relevant"
"ASP-033","Dashboard MCP endpoints (resource status, logs, traces via MCP)","mcp","13.1","https://aspire.dev/whats-new/aspire-13-1/#dashboard-mcp-endpoints","relevant"
"ASP-034","Dashboard parameters tab (resource parameters as dedicated tab)","dashboard","13.1","https://aspire.dev/whats-new/aspire-13-1/#dedicated-parameters-tab","relevant"
"ASP-035","GenAI Visualizer (tool definitions, evaluations, audio/video preview, log linking)","dashboard","13.1","https://aspire.dev/whats-new/aspire-13-1/#genai-visualizer","relevant"
"ASP-036","Interaction services (dynamic inputs, comboboxes, custom choices)","dashboard","13.0","https://aspire.dev/whats-new/aspire-13/#interaction-services-dynamic-inputs-and-comboboxes","relevant"
"ASP-037","Cross-language certificate trust (Python, Node, Container auto-trust)","security","13.0","https://aspire.dev/whats-new/aspire-13/#certificate-trust","relevant"
"ASP-038","TLS termination APIs (WithHttpsDeveloperCertificate, WithHttpsCertificate, etc.)","security","13.1","https://aspire.dev/whats-new/aspire-13-1/#tls-termination-support","relevant"
"ASP-039","aspire do pipeline system — Build/Publish/Deploy with dependency graph","pipelines","13.0","https://aspire.dev/whats-new/aspire-13/#aspire-do-pipeline","not-applicable"
"ASP-040","Container files as build artifacts (PublishWithContainerFiles)","pipelines","13.0","https://aspire.dev/whats-new/aspire-13/#container-files","not-applicable"
"ASP-041","Docker Compose publishing improvements (portable bind-mount placeholders, ConfigureEnvFile)","pipelines","13.1","https://aspire.dev/whats-new/aspire-13-1/#docker-compose-improvements","not-applicable"
"ASP-042","OpenTelemetry SDK integration updates in Aspire","observability","13.1","https://aspire.dev/whats-new/aspire-13-1/#opentelemetry-updates","relevant"
```

---

## Exclusion Registry (CSV)

Features intentionally excluded from qyl relevance analysis. qyl is cloud-agnostic, Linux-only, no Azure, no Windows, no
mobile.

```csv
"id","feature_name","reason_excluded"
"ASP-EX-001","Azure Redis API rename (AddAzureRedisEnterprise -> AddAzureManagedRedis)","Azure-specific, qyl has no Azure Redis dependency"
"ASP-EX-002","Azure connection properties standardization (HostName/Port/JdbcConnectionString)","Azure service APIs — qyl manages its own connection model"
"ASP-EX-003","Azure App Service deployment slots (WithDeploymentSlot, ClearDefaultRoleAssignments)","Azure App Service deployment — qyl is cloud-agnostic"
"ASP-EX-004","Container Registry resource (ContainerRegistryResource, ACR push pipeline)","Azure Container Registry — qyl is cloud-agnostic"
"ASP-EX-005","Explicit ACR control for Azure Container Apps deployments","Azure Container Apps — qyl is cloud-agnostic"
"ASP-EX-006","Deployment new architecture + local state management (--clear-cache)","Azure deployment state — qyl is cloud-agnostic"
"ASP-EX-007","Azure tenant selection and multi-tenant provisioning governance","Azure-specific"
"ASP-EX-008","Azure App Service Dashboard (default on) + Application Insights integration","Azure App Service and Azure Monitor — qyl is cloud-agnostic"
"ASP-EX-009","Azure Functions integration (AddAzureFunctionsProject, stable in 13.1)","Azure Functions — qyl is cloud-agnostic"
"ASP-EX-010",".NET MAUI orchestration (Aspire.Hosting.Maui, device/emulator resources)","Mobile-specific, Windows/Mac only — qyl is Linux server-side"
"ASP-EX-011","VS Code Aspire extension operations (launch config, deployment actions)","IDE-specific tooling — qyl has no VS Code extension dependency"
```

---

## Abstraction Tags (CSV)

```csv
"id","tag","intent","member_ids"
"TAG-001","PolyglotRuntime","Hosting Python, Node, Vite, and .NET resources in one orchestrator","ASP-001;ASP-002;ASP-003;ASP-004;ASP-005;ASP-006;ASP-008;ASP-009;ASP-010;ASP-011;ASP-012;ASP-013"
"TAG-002","AppHostModel","AppHost structure, connection injection, endpoint and network model","ASP-016;ASP-017;ASP-018;ASP-019;ASP-020;ASP-021;ASP-022"
"TAG-003","CliWorkflow","CLI init/new/run/update/config lifecycle","ASP-023;ASP-024;ASP-025;ASP-026;ASP-027;ASP-028;ASP-029;ASP-030"
"TAG-004","McpIntegration","MCP tooling for AI-assisted development and agent observability","ASP-031;ASP-032;ASP-033"
"TAG-005","DashboardUX","Dashboard UI enhancements for resources, GenAI, parameters","ASP-034;ASP-035;ASP-036"
"TAG-006","SecurityTls","Certificate trust and TLS configuration across languages","ASP-037;ASP-038"
"TAG-007","DeliveryPipeline","Build/publish/deploy pipeline system and container artifact flows","ASP-039;ASP-040;ASP-041"
"TAG-008","Observability","OTel SDK integration, telemetry, traces/logs/metrics","ASP-042"
```

---

## Part 2: qyl Coverage

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

| ASP-ID  | Feature                                              | qyl Status     | Notes                                                                                                                   |
|---------|------------------------------------------------------|----------------|-------------------------------------------------------------------------------------------------------------------------|
| ASP-001 | Polyglot platform                                    | DONE           | qyl.hosting orchestrates .NET/Node/Python/Vite                                                                          |
| ASP-002 | AddPythonApp / AddPythonModule / AddPythonExecutable | DONE           | PythonResource                                                                                                          |
| ASP-003 | AddUvicornApp (ASGI)                                 | DONE           | Dedicated `UvicornResource` class exists in PythonResource.cs with WithUv() and Type="uvicorn"                          |
| ASP-004 | Python package management (uv/pip)                   | PARTIAL        | `WithUv()` / `UseUv` property implemented for explicit opt-in; no auto-detection from pyproject.toml / requirements.txt |
| ASP-005 | Automatic Dockerfile generation (Python)             | NOT PRESENT    | qyl.hosting runs locally; no container build system                                                                     |
| ASP-006 | Python version detection                             | NOT PRESENT    | No .python-version / pyproject.toml inspection                                                                          |
| ASP-007 | VS Code debugging for Python                         | NOT APPLICABLE | IDE tooling, not qyl's concern                                                                                          |
| ASP-008 | Unified JS app model (AddJavaScriptApp)              | DONE           | NodeResource                                                                                                            |
| ASP-009 | Package manager flexibility (npm/yarn/pnpm)          | NOT PRESENT    | NodeResource uses npm; no auto-detect                                                                                   |
| ASP-010 | Customizing scripts and arguments                    | PARTIAL        | NodeResource exposes script configuration                                                                               |
| ASP-011 | Vite support (AddViteApp)                            | DONE           | ViteResource                                                                                                            |
| ASP-012 | Dynamic Dockerfile generation (JS)                   | NOT PRESENT    | qyl.hosting runs locally; no container build                                                                            |
| ASP-013 | Node support (AddNodeApp)                            | DONE           | NodeResource                                                                                                            |
| ASP-014 | JS/TS starter template                               | NOT APPLICABLE | Template system, not qyl's concern                                                                                      |
| ASP-015 | Vite HTTPS runtime configuration                     | NOT PRESENT    | ViteResource doesn't manage TLS certs                                                                                   |
| ASP-016 | AppHost SDK modernization                            | DONE           | QylApp / QylAppBuilder replace AppHost                                                                                  |
| ASP-017 | Single-file AppHost / C# file-based apps             | DONE           | QylApp.CreateBuilder() is minimal-file pattern                                                                          |
| ASP-018 | Polyglot connection properties                       | PARTIAL        | Resource-to-resource refs work; no JDBC injection                                                                       |
| ASP-019 | Simplified service URL env vars                      | PARTIAL        | Resources inject URLs; no API_HTTP / API_HTTPS convention                                                               |
| ASP-020 | Named references                                     | NOT PRESENT    | No named alias API                                                                                                      |
| ASP-021 | Endpoint reference enhancements                      | NOT PRESENT    | Basic endpoint model only                                                                                               |
| ASP-022 | Network identifiers                                  | NOT PRESENT    | No network identifier API                                                                                               |
| ASP-023 | aspire init command                                  | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003)                                             |
| ASP-024 | aspire new curated templates                         | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003)                                             |
| ASP-025 | aspire update / self-update                          | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003)                                             |
| ASP-026 | CLI channel control                                  | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003)                                             |
| ASP-027 | Auto running-instance detection                      | NOT PRESENT    | QylRunner doesn't check for running instances                                                                           |
| ASP-028 | --skip-path install option                           | NOT APPLICABLE | NOT APPLICABLE — qyl uses standard OTLP env vars + optional NuGet (ADR-003)                                             |
| ASP-029 | CLI config model (.aspire/settings.json)             | NOT APPLICABLE | qyl uses env vars and QYL_* settings                                                                                    |
| ASP-030 | aspire config CLI commands                           | NOT APPLICABLE | qyl uses env vars                                                                                                       |
| ASP-031 | aspire mcp init                                      | EXCEEDS        | qyl.mcp is always available; no init step needed                                                                        |
| ASP-032 | MCP tooling (list_integrations, list_apphosts)       | EXCEEDS        | qyl.mcp has 15+ tools; richer than Aspire's 4                                                                           |
| ASP-033 | Dashboard MCP endpoints                              | EXCEEDS        | Full telemetry via MCP; not just resource status                                                                        |
| ASP-034 | Dashboard parameters tab                             | DONE           | qyl.dashboard shows resource configuration                                                                              |
| ASP-035 | GenAI Visualizer                                     | EXCEEDS        | AgentRunsPage with waterfall, cost/token, tool sequencing                                                               |
| ASP-036 | Interaction services (dynamic inputs)                | NOT PRESENT    | No interactive resource parameter prompts                                                                               |
| ASP-037 | Cross-language certificate trust                     | NOT PRESENT    | No dev-cert injection across runtimes                                                                                   |
| ASP-038 | TLS termination APIs                                 | NOT PRESENT    | No WithHttpsCertificate-style APIs                                                                                      |
| ASP-039 | aspire do pipeline system                            | NOT APPLICABLE | qyl uses QylRunner + Docker; not a general pipeline                                                                     |
| ASP-040 | Container files as build artifacts                   | NOT APPLICABLE | No multi-container artifact copy support                                                                                |
| ASP-041 | Docker Compose improvements                          | NOT APPLICABLE | qyl doesn't generate Compose files                                                                                      |
| ASP-042 | OTel SDK integration updates                         | EXCEEDS        | qyl.servicedefaults is qyl's OTel SDK layer; more complete than Aspire's defaults                                       |

**Result: 9 DONE, 5 EXCEEDS, 4 PARTIAL, 12 NOT PRESENT, 12 NOT APPLICABLE**

---

## Detail: ASP-001 — Polyglot platform

**Status: DONE**

qyl.hosting provides a resource orchestration model that mirrors Aspire's polyglot approach.

| Resource Type                  | File                                             |
|--------------------------------|--------------------------------------------------|
| IQylResource (base interface)  | `src/qyl.hosting/Resources/IQylResource.cs`      |
| ProjectResource (.NET)         | `src/qyl.hosting/Resources/ProjectResource.cs`   |
| NodeResource (JS/Node)         | `src/qyl.hosting/Resources/NodeResource.cs`      |
| ViteResource (Vite frontend)   | `src/qyl.hosting/Resources/ViteResource.cs`      |
| PythonResource (Python)        | `src/qyl.hosting/Resources/PythonResource.cs`    |
| ContainerResource (Docker)     | `src/qyl.hosting/Resources/ContainerResource.cs` |
| QylApp / QylAppBuilder         | `src/qyl.hosting/QylApp.cs`, `QylAppBuilder.cs`  |
| QylRunner (orchestration loop) | `src/qyl.hosting/QylRunner.cs`                   |

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

| File                               | Role                                          |
|------------------------------------|-----------------------------------------------|
| `src/qyl.hosting/QylApp.cs`        | Entry point builder                           |
| `src/qyl.hosting/QylAppBuilder.cs` | Builder pattern (add resources, configure)    |
| `src/qyl.hosting/QylRunner.cs`     | Orchestration runner (starts/stops resources) |

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

| qyl.mcp Tools      | File                                      |
|--------------------|-------------------------------------------|
| TelemetryTools     | `src/qyl.mcp/Tools/TelemetryTools.cs`     |
| StructuredLogTools | `src/qyl.mcp/Tools/StructuredLogTools.cs` |
| GenAiTools         | `src/qyl.mcp/Tools/GenAiTools.cs`         |
| AgentTools         | `src/qyl.mcp/Tools/AgentTools.cs`         |
| IssueTools         | `src/qyl.mcp/Tools/IssueTools.cs`         |
| BuildTools         | `src/qyl.mcp/Tools/BuildTools.cs`         |
| ReplayTools        | `src/qyl.mcp/Tools/ReplayTools.cs`        |
| SearchTools        | `src/qyl.mcp/Tools/SearchTools.cs`        |
| WorkflowTools      | `src/qyl.mcp/Tools/WorkflowTools.cs`      |
| AnalyticsTools     | `src/qyl.mcp/Tools/AnalyticsTools.cs`     |
| StorageTools       | `src/qyl.mcp/Tools/StorageTools.cs`       |
| ConsoleTools       | `src/qyl.mcp/Tools/ConsoleTools.cs`       |
| CopilotTools       | `src/qyl.mcp/Tools/CopilotTools.cs`       |
| WorkspaceTools     | `src/qyl.mcp/Tools/WorkspaceTools.cs`     |

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

| qyl Implementation              | File                                                  |
|---------------------------------|-------------------------------------------------------|
| Agent runs page                 | `src/qyl.dashboard/src/pages/AgentRunsPage.tsx`       |
| Agent run detail (waterfall)    | `src/qyl.dashboard/src/pages/AgentRunDetailPage.tsx`  |
| agent_runs + tool_calls storage | `src/qyl.collector/Storage/DuckDbSchema.AgentRuns.cs` |

---

## Detail: ASP-042 — OTel SDK integration updates

**Status: EXCEEDS**

Aspire 13.1 updates its bundled OTel SDK version. qyl.servicedefaults is qyl's equivalent of
Aspire's OTel defaults — with source-generated compile-time interceptors ([Traced], [GenAi], [Db]),
GenAI instrumentation, and full OTel SemConv v1.40 compliance.

| qyl Implementation             | File                                                                    |
|--------------------------------|-------------------------------------------------------------------------|
| Service defaults / OTel wiring | `src/qyl.servicedefaults/`                                              |
| Compile-time interceptors      | `src/qyl.servicedefaults.generator/`                                    |
| GenAI instrumentation          | `src/qyl.servicedefaults/Instrumentation/GenAi/GenAiInstrumentation.cs` |
| Telemetry source generators    | `src/qyl.instrumentation.generators/`                                   |

---

## qyl-Only Features (no Aspire equivalent)

| Feature                                                     | File                                               |
|-------------------------------------------------------------|----------------------------------------------------|
| Build failure capture (MSBuild binlog → DuckDB)             | `src/qyl.collector/BuildFailures/`                 |
| OTLP PII scrubbing at ingestion boundary                    | `src/qyl.collector/Ingestion/`                     |
| Error fingerprinting + GenAI-aware grouping                 | `src/qyl.collector/Errors/ErrorFingerprinter.cs`   |
| Autofix orchestration with policy gates                     | `src/qyl.collector/Autofix/AutofixOrchestrator.cs` |
| Workflow engine (nodes, checkpoints, shared state, routing) | `src/qyl.collector/Workflow/`                      |
| qyl.watch (live terminal span viewer)                       | `src/qyl.watch/`                                   |
| qyl.watchdog (process anomaly detection)                    | `src/qyl.watchdog/`                                |
| qyl.browser (Web Vitals + interactions OTLP SDK)            | `src/qyl.browser/`                                 |

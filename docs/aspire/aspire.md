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

Features intentionally excluded from qyl relevance analysis. qyl is cloud-agnostic, Linux-only, no Azure, no Windows, no mobile.

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

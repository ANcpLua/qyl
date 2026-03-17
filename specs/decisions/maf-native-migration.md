# v2 Amendment: MAF Native Migration

> **For agentic workers:** Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Delete `qyl.agents` and `qyl.workflows` projects, strip LLM dependencies from the collector, move OTel wrappers to SDK, clean up dashboard and MCP consumers.

**Architecture:** qyl is an observability platform. MAF provides agent construction (`AddAIAgent()`), workflow orchestration (`DeclarativeWorkflowBuilder`), and transport (`MapAGUI()`). qyl's wrappers are shim layers. Delete them. Keep only what MAF doesn't provide: `InstrumentedChatClient` and `InstrumentedAIFunction` (OTel span decorators).

**Verified facts** (3x deep-think, 2026-03-15):
- `WorkflowExecution` and `WorkflowStatus` are defined in `qyl.contracts` — they survive.
- `DuckDbStore.Workflows.cs` uses its own `WorkflowExecutionRecord` — no `qyl.workflows` dependency.
- `CodingAgentRunRecord`, `LoomSettingsRecord`, `CodingAgentProvider` are defined in collector's `CodingAgent/CodingAgentProvider.cs` — used by Loom, must be relocated before deletion.
- `ClaudeCodeSession` etc. are defined in collector's `ClaudeCode/ClaudeCodeEndpoints.cs` — no surviving consumer outside that file + `DuckDbStore.ClaudeCode.cs`. Delete both.
- `IExecutionStore` is in `qyl.workflows` — only consumer is `DuckDbExecutionStore.cs`. Delete both.
- 12 of 36 deleted endpoints have zero LLM dependencies (they're pure DuckDB queries). Loom already has its own copies of the CodingAgent and Workflow endpoints. ClaudeCode query endpoints are deliberately removed from the server (they can be re-added to Loom if needed).

**Tech Stack:** .NET 10, C# 14, Microsoft.Agents.AI rc3, xUnit v3 + MTP, NUKE build

---

## File Map

### Files to delete

```
src/qyl.agents/                                    # entire project (~2,200 lines)
src/qyl.workflows/                                 # entire project (~500 lines)
src/qyl.collector/Copilot/                          # 3 files — LLM-dependent (AG-UI, chat, tools)
src/qyl.collector/ClaudeCode/                       # 2 files — orphaned (Loom can re-add if needed)
src/qyl.collector/CodingAgent/                      # 3 files — Loom has its own copies
src/qyl.collector/Workflow/                         # 4 files — Loom has its own copies
src/qyl.collector/Storage/DuckDbStore.ClaudeCode.cs # storage for deleted ClaudeCode endpoints
src/qyl.collector/Storage/DuckDbExecutionStore.cs   # implements deleted IExecutionStore
```

### Files to create

```
src/qyl.contracts/Loom/CodingAgentProvider.cs                        # relocated from collector
src/qyl.instrumentation/Instrumentation/GenAi/InstrumentedChatClient.cs  # moved from qyl.agents
src/qyl.instrumentation/Instrumentation/GenAi/InstrumentedAIFunction.cs  # moved from qyl.agents
src/qyl.instrumentation/Instrumentation/GenAi/ChatClientExtensions.cs    # moved from qyl.agents
```

### Files to modify

```
# Collector
src/qyl.collector/Program.cs                        # remove all agent/workflow/copilot/claude-code registrations
src/qyl.collector/qyl.collector.csproj               # remove ProjectRefs + PackageRefs
src/qyl.collector/Storage/DuckDbStore.CodingAgent.cs # using → Qyl.Contracts.Loom
src/qyl.collector/Autofix/AutofixOrchestrator.cs     # using → Qyl.Contracts.Loom

# Loom
src/qyl.loom/qyl.loom.csproj                        # remove qyl.agents + qyl.workflows refs
src/qyl.loom/Identity/GlobalUsings.cs                # line 36: Qyl.Collector.CodingAgent → Qyl.Contracts.Loom

# Instrumentation
src/qyl.instrumentation/qyl.instrumentation.csproj   # add Microsoft.Extensions.AI PackageRef
src/qyl.instrumentation/Instrumentation/QylServiceDefaultsExtensions.cs  # add MAF OTel source names

# Solution
qyl.slnx                                             # remove qyl.agents + qyl.workflows

# MCP (consumer cleanup)
src/qyl.mcp/Tools/CopilotTools.cs                   # delete or gut (endpoints gone)
src/qyl.mcp/Tools/ClaudeCodeTools.cs                 # delete or gut (endpoints gone)
src/qyl.mcp/Tools/InvestigateTools.cs                # remove HttpAgentProvider dependency
src/qyl.mcp/Agents/HttpAgentProvider.cs              # delete (proxied to copilot/chat)

# Dashboard (consumer cleanup)
src/qyl.dashboard/src/hooks/use-copilot.ts           # delete
src/qyl.dashboard/src/hooks/use-llm-status.ts        # delete
src/qyl.dashboard/src/hooks/use-claude-code-hooks.ts # delete
src/qyl.dashboard/src/hooks/use-workflows.ts         # delete
src/qyl.dashboard/src/components/copilot/            # delete directory
src/qyl.dashboard/src/pages/WorkflowRunsPage.tsx     # delete
src/qyl.dashboard/src/pages/WorkflowRunDetailPage.tsx # delete (if exists)
src/qyl.dashboard/src/App.tsx                        # remove deleted routes
src/qyl.dashboard/src/components/layout/Sidebar.tsx  # remove deleted nav items

# Tests
tests/qyl.collector.tests/ArchitectureTests.cs       # update
tests/qyl.collector.tests/qyl.collector.tests.csproj  # remove qyl.agents ProjectRef

# Docs
specs/04-agents.md                                    # mark SUPERSEDED
specs/09-workflows.md                                 # mark SUPERSEDED
.claude/rules/loom.md                                 # update
.claude/rules/collector.md                            # update
CHANGELOG.md                                          # add migration entry
```

---

## Task 1: Fix OTel Source Name Gap

**Files:** `src/qyl.instrumentation/Instrumentation/QylServiceDefaultsExtensions.cs`

- [ ] Add `"Experimental.Microsoft.Agents.AI"` to `SGenAiActivitySources` and `SGenAiMeterNames` arrays
- [ ] `nuke` — build succeeds
- [ ] Commit: `fix(instrumentation): add MAF experimental OTel source name`

---

## Task 2: Relocate Shared Types + Move OTel Wrappers

### 2a: Relocate CodingAgent types to qyl.contracts

**Why:** `CodingAgentProvider`, `CodingAgentProviderNames`, `CodingAgentRunRecord`, `LoomSettingsRecord` are defined in `src/qyl.collector/CodingAgent/CodingAgentProvider.cs`. Loom uses them via `global using Qyl.Collector.CodingAgent;`. Collector's `DuckDbStore.CodingAgent.cs` and `AutofixOrchestrator.cs` also use them. Must relocate before deleting `CodingAgent/`.

- [ ] Create `src/qyl.contracts/Loom/CodingAgentProvider.cs` — copy all 4 types, change namespace to `Qyl.Contracts.Loom`
- [ ] Update `src/qyl.collector/Storage/DuckDbStore.CodingAgent.cs` — change `using Qyl.Collector.CodingAgent;` to `using Qyl.Contracts.Loom;`
- [ ] Update `src/qyl.collector/Autofix/AutofixOrchestrator.cs` — same using change
- [ ] Update `src/qyl.loom/Identity/GlobalUsings.cs` line 36 — change `global using Qyl.Collector.CodingAgent;` to `global using Qyl.Contracts.Loom;`
- [ ] `nuke` — build succeeds (both old and new type locations exist temporarily)

### 2b: Move InstrumentedChatClient + InstrumentedAIFunction to SDK

- [ ] Copy `InstrumentedChatClient.cs` to `src/qyl.instrumentation/Instrumentation/GenAi/`. Namespace: `Qyl.Instrumentation.GenAi`. Replace `CopilotInstrumentation.ActivitySource` with `ActivitySources.GenAiSource`. Replace `CopilotMetrics` calls with `GenAiInstrumentation` equivalents (verify methods exist first).
- [ ] Copy `InstrumentedAIFunction.cs` same way. Replace `CopilotInstrumentation.ActivitySource` with `ActivitySources.GenAiSource`.
- [ ] Copy `ChatClientExtensions.cs` same way. Update type references.
- [ ] Add `Microsoft.Extensions.AI` PackageReference to `qyl.instrumentation.csproj` if missing.
- [ ] `nuke` — build succeeds

- [ ] Commit: `feat(instrumentation): relocate shared types and move OTel wrappers to SDK`

---

## Task 3: Purge Collector Directories + Storage Partials

**Dependency order matters.** Types are already relocated (Task 2). Now delete the source directories and associated storage.

### 3a: Clean Program.cs

- [ ] Remove `using` statements: `Qyl.Agents`, `Qyl.Agents.Agents`, `Qyl.Agents.Auth`, `Qyl.Workflows`, `Qyl.Workflows.Workflows`, `Qyl.Collector.Copilot`, `Qyl.Collector.ClaudeCode`, `Qyl.Collector.CodingAgent`, `Qyl.Collector.Workflow`
- [ ] Remove registrations: `ClaudeCodeHooksService` (L146), `IExecutionStore`/`DuckDbExecutionStore` (L183-185), `ObservabilityTools.Create` (L187-189), `AddQylAgents`/`AddQylWorkflows`/`AddQylAgui`/`CopilotAuthOptions`/`AddQylAgentTelemetry` (L191-201), `WorkflowRunService` (L258)
- [ ] Remove endpoint mappings: `MapCopilotEndpoints` (L395), AG-UI block (L397-400), `MapClaudeCodeEndpoints` (L402), `MapCodingAgentEndpoints` (L860), `MapLoomSettingsEndpoints` (L862), `MapWorkflowEndpoints`/`MapWorkflowRunEndpoints`/`MapWorkflowEventEndpoints` (L865-867)

### 3b: Clean collector csproj

- [ ] Remove `<ProjectReference>` to `qyl.agents` and `qyl.workflows`
- [ ] Remove `<PackageReference>` for `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`
- [ ] Verify whether `Microsoft.Extensions.AI` PackageRef can be removed (check remaining usages)

### 3c: Delete directories and orphaned storage

```bash
rm -rf src/qyl.collector/Copilot/
rm -rf src/qyl.collector/ClaudeCode/
rm -rf src/qyl.collector/CodingAgent/
rm -rf src/qyl.collector/Workflow/
rm src/qyl.collector/Storage/DuckDbStore.ClaudeCode.cs
rm src/qyl.collector/Storage/DuckDbExecutionStore.cs
```

**Keep:** `DuckDbStore.CodingAgent.cs` (uses relocated types from contracts), `DuckDbStore.Workflows.cs` (uses own `WorkflowExecutionRecord`, no qyl.workflows dep).

- [ ] `nuke` — build succeeds
- [ ] Commit: `refactor(collector): purge LLM-dependent and orphaned directories from server`

---

## Task 4: Delete qyl.agents + qyl.workflows Projects

- [ ] Remove from `qyl.slnx`: `<Project Path="src/qyl.agents/qyl.agents.csproj"/>` and `<Project Path="src/qyl.workflows/qyl.workflows.csproj"/>`
- [ ] Remove from `src/qyl.loom/qyl.loom.csproj`: ProjectReferences to qyl.agents and qyl.workflows
- [ ] Verify no remaining `using Qyl.Agents.*` or `using Qyl.Workflows.*` in Loom (grep)
- [ ] `rm -rf src/qyl.agents/ src/qyl.workflows/`
- [ ] `nuke` — build succeeds
- [ ] Commit: `refactor: delete qyl.agents and qyl.workflows projects`

---

## Task 5: Clean Up MCP + Dashboard Consumers

### 5a: MCP tools + registrations

- [ ] Delete `src/qyl.mcp/Agents/HttpAgentProvider.cs` and `src/qyl.mcp/Agents/IAgentProvider.cs`
- [ ] Gut `src/qyl.mcp/Tools/CopilotTools.cs` — remove tools that call `/api/v1/copilot/*` (3 tools dead)
- [ ] Gut `src/qyl.mcp/Tools/ClaudeCodeTools.cs` — remove tools that call `/api/v1/claude-code/*` (5 tools dead)
- [ ] Update `src/qyl.mcp/Tools/InvestigateTools.cs` — remove `HttpAgentProvider`/`IAgentProvider` usage (1 tool dead)
- [ ] Update `src/qyl.mcp/Agents/McpToolRegistry.cs` — remove `typeof(ClaudeCodeTools)` from `ToolTypes` array (L31)
- [ ] Update `src/qyl.mcp/Skills/SkillRegistrationExtensions.cs` — remove `CopilotTools` (L117), `ClaudeCodeTools` (L123), `InvestigateTools` (L83) registrations
- [ ] Update `src/qyl.mcp/Program.cs` — remove `CopilotTools` (L178), `ClaudeCodeTools` (L183), `HttpAgentProvider`/`IAgentProvider` (L203-211) registrations
- [ ] Update `src/qyl.mcp/Clear.cs` — update `global using Qyl.Collector.CodingAgent` to `global using Qyl.Contracts.Loom` and fix any remaining references to deleted types

### 5b: Dashboard hooks and pages

- [ ] Delete hooks: `use-copilot.ts`, `use-llm-status.ts`, `use-claude-code-hooks.ts`
- [ ] Delete `use-llm-config.ts` (only consumer after CopilotPanel deletion is SettingsPage LLM section, which is also being removed)
- [ ] Delete components: `src/qyl.dashboard/src/components/copilot/` directory
- [ ] Delete pages: `WorkflowRunsPage.tsx`, `WorkflowRunDetailPage.tsx`
- [ ] Update `DashboardLayout.tsx` — remove `useCopilotStatus`, `CopilotButton`, `CopilotPanel` imports and rendering (**build-breaker if missed**)
- [ ] Update `App.tsx` — remove routes for deleted pages
- [ ] Update `Sidebar.tsx` — remove nav items for deleted pages
- [ ] Update `pages/index.ts` — remove `WorkflowRunsPage`, `WorkflowRunDetailPage` exports
- [ ] Update `SettingsPage.tsx` — remove LLM config section, Claude Code hooks section, Loom settings section
- [ ] Update `IssueDetailPage.tsx` — remove or update coding agent results section
- [ ] Update `use-coding-agents.ts` — remove `useCodingAgentRuns` (calls deleted `/api/v1/fix-runs/*/coding-agents`) and `useLoomSettings` (calls deleted `/api/v1/Loom/settings`), or update URLs to Loom endpoints
- [ ] Update `hooks/index.ts` — remove deleted hook exports
- [ ] `npm run build` in `src/qyl.dashboard/` — frontend compiles

- [ ] Commit: `refactor: clean up MCP tools and dashboard for MAF migration`

---

## Task 6: Update Architecture Tests

- [ ] Delete `Agents_Should_Not_Depend_On_Loom_Or_Collector` test
- [ ] Delete `Workflows_Should_Not_Depend_On_Loom_Or_Collector` test
- [ ] Remove `"Qyl.Agents"` and `"Qyl.Workflows"` from contracts dependency list
- [ ] Remove qyl.agents AND qyl.workflows ProjectReferences from test csproj
- [ ] Add `Server_Should_Not_Depend_On_Agent_Framework` test (assert no dependency on `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Hosting`, `GitHub.Copilot.SDK`)
- [ ] `nuke test` — all tests pass
- [ ] Commit: `test(architecture): replace agents/workflows tests with server-no-LLM invariant`

---

## Task 7: Update Specs, Rules, CHANGELOG

- [ ] `specs/04-agents.md` — mark SUPERSEDED, reference MAF native APIs. Only list APIs qyl actually uses: `AddAIAgent()`, `DeclarativeWorkflowBuilder`, `MapAGUI()`. Do NOT list `MapA2A()`, `HandoffsWorkflowBuilder`, `AgentWorkflowBuilder.BuildSequential()` as "replacements" — they are capabilities qyl has not wired.
- [ ] `specs/09-workflows.md` — mark SUPERSEDED, reference MAF workflow APIs
- [ ] `.claude/rules/loom.md` — replace "Loom uses AIAgent (via QylAgentBuilder)" with "Loom uses IChatClient + AIAgent from MAF directly". Update ownership boundary to remove agents/workflows from ProjectReference list.
- [ ] `.claude/rules/collector.md` — replace AG-UI line with "Server has zero LLM dependencies"
- [ ] `README.md` — update project table and directory tree to remove qyl.agents and qyl.workflows
- [ ] `CHANGELOG.md` — add Removed entries under Unreleased
- [ ] Commit: `docs: update specs and rules for MAF native migration`

---

## Task 8: Final Verification

- [ ] `nuke` — clean build, zero errors
- [ ] `nuke test` — all tests pass
- [ ] `qyl.slnx` has no references to qyl.agents or qyl.workflows
- [ ] `src/qyl.agents/` and `src/qyl.workflows/` directories do not exist
- [ ] `dotnet list src/qyl.collector/qyl.collector.csproj package --include-transitive | grep -i "Microsoft.Agents"` — zero output
- [ ] Dashboard compiles and loads without console errors on startup
- [ ] Commit fixups if needed

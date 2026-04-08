## Problem
qyl needs a repo-grounded answer to one question: where are we still hand-rolling agent runtime, orchestration, workflow state, and hosted execution instead of using Microsoft Agent Framework layers? The current docs and package surface imply more MAF adoption than the production code actually has.

## Key Insight
Production qyl uses MAF almost nowhere. The real runtime stack is mostly `IChatClient` + custom `BackgroundService` loops + custom stores/endpoints/parsers/session state, while actual `Microsoft.Agents.AI` usage is confined to the sample at `samples/maf-agent-qyl/Program.cs`. The repo is therefore not "partially on MAF" so much as "MAF-shaped in design, custom in execution."

## Analysis
- Constraint: collector is intentionally forbidden from taking MAF runtime and provider SDK dependencies; architecture tests already ban `Microsoft.Agents.AI*` from the collector, so any MAF-native adoption must live in Loom or samples, not in collector runtime code.
- Tradeoff: the current custom stack keeps qyl's product-specific semantics explicit, but it also re-owns framework problems: hosted polling, session handoff, streaming orchestration, tool discovery, JSON extraction, workflow persistence, and status transitions.
- Risk: the biggest risk is architectural drift, not compilation failure. Specs like `src/qyl.loom/specs/loom.md` still describe direct MAF agent construction, while `src/qyl.loom/` actually runs on `Qyl.Agents` plus raw `IChatClient` services and `src/qyl.collector/Autofix/*` still owns custom orchestration.
- Evidence: sample-only MAF runtime usage lives in `samples/maf-agent-qyl/Program.cs`; production Loom uses `Qyl.Agents` in `src/qyl.loom/Agents/LoomGodAnalyzerServer.cs` and `src/qyl.loom/Agents/LoomGodAnalyzerHostingExtensions.cs`; collector and Loom background automation use `BackgroundService` loops in `src/qyl.collector/Autofix/AutofixAgentService.cs`, `src/qyl.collector/Autofix/TriagePipelineService.cs`, `src/qyl.collector/Autofix/RegressionDetectionService.cs`, `src/qyl.loom/Autofix/AutofixAgentService.cs`, `src/qyl.loom/TriagePipelineService.cs`, and `src/qyl.loom/RegressionDetectionService.cs`.
- Overlap: custom agent wrappers instead of MAF agents. `LoomDiagnostician`, `LoomStrategist`, `LoomInsightService`, and `CodeReviewService` call `IChatClient` directly and manually assemble prompts, streaming, and parsing in `src/qyl.collector/Autofix/*.cs`. These are bounded-agent responsibilities implemented without `AIAgent`.
- Overlap: custom orchestration instead of MAF workflows. `LoomOrchestrator` manually sequences diagnostician then strategist and emits `StreamUpdate` SSE events in `src/qyl.collector/Autofix/LoomOrchestrator.cs` and `src/qyl.collector/Autofix/LoomEndpoints.cs`. `AutofixAgentService` manually executes the 5-stage fix pipeline step by step in both collector and standalone Loom.
- Overlap: custom session state instead of framework durability/session abstractions. `LoomSessionStore` in `src/qyl.collector/Autofix/LoomSessionStore.cs` owns issue-keyed in-memory state, message history, diagnosis handoff, and solution handoff. This is explicit and readable, but it is still runtime/session infrastructure that a framework usually owns.
- Overlap: custom workflow persistence without a workflow engine. `src/qyl.collector/Storage/DuckDbSchema.WorkflowRuns.cs` and `src/qyl.collector/Storage/DuckDbStore.Workflows.cs` define `workflow_runs`, `workflow_nodes`, `workflow_checkpoints`, `workflow_events`, and helpers, yet the concrete runtime evidence is thin: `AutofixOrchestrator` only inserts `workflow_executions`, and no production code was found driving `workflow_runs` / `workflow_nodes` as a real engine.
- Overlap: custom handoff and coding-agent packaging instead of a framework-native delegation model. `AgentHandoffService` in `src/qyl.collector/Autofix/AgentHandoffService.cs` serializes run context, step outputs, timeout state, and result lifecycle manually.
- Overlap: custom embedded agent loop in MCP instead of MAF agent construction. `src/qyl.mcp/Tools/UseQylTools.cs` and `src/qyl.mcp/Tools/RcaTools.cs` build tool-calling agents with `ChatClientBuilder.UseFunctionInvocation(...)` plus reflection-based tool discovery in `src/qyl.mcp/Agents/McpToolRegistry.cs`. This is a self-implemented agent runtime on top of `Microsoft.Extensions.AI`, not MAF.
- Non-overlap: A2A / AGUI / DurableTask / Workflows.Declarative / Purview / CopilotStudio are effectively absent from production qyl. The only references I found are central package-version placeholders, sample/docs mentions, or architecture bans. There is no meaningful runtime adoption to migrate away from there because there is almost nothing there.
- Recommendation: keep domain logic, delete infrastructure duplication. Keep qyl-specific concepts like `FixRunRecord`, `PolicyGate`, issue-context building, and code-review prompts. Delete or replace custom runtime layers where they re-solve scheduling, orchestration, session handoff, or tool invocation if Loom is going to be MAF-native. If Loom is not going to be MAF-native, update specs to stop claiming that it is.

## Implementation Plan

| Phase | Step | Action | Checkpoint | Files |
|-------|------|--------|------------|-------|
| 1 | 1.1 | Document current-state overlap map and evidence | Report exists and names the concrete overlap zones with file-backed evidence | docs/superpowers/plans/2026-04-02-maf-overlap-analysis.md |
| 1 | 1.2 | Delete dead central MAF package versions not referenced by any project | `rg` finds only the live `Microsoft.Agents.AI.Hosting` central version entry | Version.props, Directory.Packages.props |
| 1 | 1.3 | Align collector dependency wording with the actual ban boundary | Architecture test summary no longer claims "zero LLM dependencies" | tests/qyl.collector.tests/ArchitectureTests.cs |
| 2 | 2.1 | Decide the real target for Loom runtime ownership | Either Loom is declared MAF-native and migration work starts, or specs are rewritten to endorse the custom runtime explicitly | src/qyl.loom/specs/loom.md, specs/00-architecture.md, specs/decisions/maf-native-migration.md |
| 2 | 2.2 | Move orchestration ownership out of collector if Loom stays the intelligence plane | Collector keeps storage/endpoints; Loom owns agents, workflow execution, and provider selection | src/qyl.collector/Autofix/*.cs, src/qyl.loom/*.cs |
| 2 | 2.3 | Replace raw `IChatClient` agent shells with a single runtime model | No duplicated prompt-wrapper agents across collector, Loom, and MCP | src/qyl.collector/Autofix/*.cs, src/qyl.loom/*.cs, src/qyl.mcp/Tools/*.cs |

## Checkpoints (for TodoWrite)
- [ ] CP1: The repo contains a persistent MAF overlap report with concrete file-backed evidence.
- [ ] CP2: Central package management contains only live MAF package-version entries.
- [ ] CP3: Collector boundary wording no longer overstates the dependency ban.

## Confidence
high -- package references, namespace imports, runtime entrypoints, and orchestrator/background-service files all point to the same conclusion: production qyl is custom-first, with MAF runtime usage limited to the sample.

## Risks
- The report does not itself remove custom runtime duplication from collector or Loom; it only makes the gap explicit.
- Some specs still claim direct MAF usage in Loom, so documentation drift remains until a follow-up reconciles target state versus actual state.
- `Qyl.Agents` may intentionally cover part of the same surface as MAF, which means a future migration decision must compare those two frameworks explicitly rather than assuming "custom" always means "repo-local code."

## Questions Before Starting
- None - ready to proceed.

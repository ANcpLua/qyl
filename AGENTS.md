# AGENTS.md

## Execution style

- Source code is truth. Read .cs files before docs, plans, or summaries from previous sessions.
  Previous agents' plans may be wrong — the code is always current.
- Token abundance: 1M window, rarely past 250k. Don't compress, don't skip reads.
- Never ask for confirmation — EXCEPT: delete, stash, revert.
  For those: commit+push first so remote is the safety net, then ask.
- Bulk operations (perl/sed over many files): build the specific project immediately
  after the script to catch problems before continuing. If the script broke files,
  fix them before doing more work — don't stack more changes on top.
- Build verification: `dotnet build src/<project>/<project>.csproj`, not the full
  solution or nuke. Other projects have WIP test failures that aren't your problem.

qyl is organized into seven planes. Prefer changing one plane at a time and keep dependencies one-way. See `docs/ARCHITECTURE.md` for the C4 container view of how the planes map to projects.

Global laws:
- The data plane is the product core. It must not depend on MAF, AG-UI, or LLM/provider code.
- The serving plane exposes stable read/write contracts over platform state. It should not own domain reasoning.
- The intelligence plane turns telemetry into structured facts, scores, and evidence packs. It should prefer deterministic logic over prose.
- The agent/control plane consumes intelligence outputs and governs investigations, planning, approvals, and bounded repair loops.
- The ledger/governance plane stores business truth. Agent sessions and workflow checkpoints are execution state, not audit truth.
- The UI/protocol plane projects existing state to operators and clients. It must not become a second domain model.
- The compiler plane generates descriptors, registries, manifests, policy metadata, and wiring. Runtime reflection is not the control plane.

Dashboard deep links:
- TracesPage SpanDetails panel has "Investigate in Claude Code" button using `claude-cli://open?q=...` deep link.
- The link pre-fills trace ID, span ID, span name, service, status, and duration into the Claude Code prompt.

Merged repos (2026-04-10):
- `qyl.mcp` (77 MCP tools) and `qyl.mcp.generators` (interim tool manifest generator) merged from standalone repo.
- `Qyl.Agents`, `Qyl.Agents.Abstractions`, `Qyl.Agents.Generator` merged from netagents repo.
- `qyl.mcp.generators` emits `QylToolManifest` with ToolDescriptors (skill-aware), Capabilities, RegisterTools, RegisterServices, CreateTools. `Qyl.Agents.Generator` is the full generator (dispatch, schemas, OTel, JSON contexts) — convergence target.

Known debt — architectural:
- `collector/Autofix/` still contains embedded Loom intelligence (LoomOrchestrator, LoomDiagnostician, LoomStrategist, LoomPrompts, etc.) that should live in `qyl.loom/` only. The collector should expose data-plane endpoints, not own LLM orchestration.
- `collector/AgentRuns/` is correct — pure read-only DuckDB queries for agent run observability.
- `LoomToolEnvelope<T>` — use `LoomToolEnvelope.Ok(data)` and `LoomToolEnvelope.Fail<T>(error)` (non-generic companion class), NOT `LoomToolEnvelope<T>.Ok/Fail`.

Compiler plane — single source of truth (2026-04-12):
- `[QylSkill(QylSkillKind.X)]` on each `[McpServerToolType]` class is the only place skill ownership is declared.
- `[QylCapability("id", QylCapabilityRole.Starting/FollowUp)]` on tool methods links tools to capabilities at compile time.
- `[QylCapabilityDefinition("id", QylSkillKind.X)]` on marker classes in `Capabilities/Definitions/` defines capability metadata.
- The generator produces everything else: `RegisterTools()`, `RegisterServices()`, `Capabilities[]`, `ToolDescriptors[]`.
- Do NOT manually add tools to DI, MCP registration, or skill catalogs — the generator handles it from the attribute.
- Tools without `[QylSkill]` (e.g. `CapabilityTools`, `ArtifactTools`) keep manual registration and are excluded from generated methods.

Agent bounded autonomy (2026-04-12):
- `InvestigationLineage` (AsyncLocal) enforces max depth (3), root spawn budget (10), cycle detection.
- `UseQylTools` and `RcaTools` call `InvestigationLineage.TryEnter()` before starting investigations.
- Env overrides: `QYL_AGENT_MAX_DEPTH`, `QYL_AGENT_MAX_SPAWNS`.

Project map (8 real projects — see `QYL_GROUND_TRUTH` from SessionStart hook for the full list and forbidden ghost projects):
- **Platform:** qyl.collector (OTLP ingest, REST API, DuckDB), qyl.contracts (BCL-only types)
- **MCP:** qyl.mcp (77 tools, stdio + Streamable HTTP), qyl.mcp.generators (Roslyn generator — skill-aware manifests + capabilities)
- **Loom:** qyl.loom (standalone agent exe — triage, RCA, fix, code review; HTTP-only to collector)
- **SDK:** qyl.instrumentation, qyl.instrumentation.generators, qyl.collector.storage.generators
- **Frontend:** qyl.dashboard (React 19, Vite 7, Tailwind CSS 4, Base UI 1.3.0, lucide-react)

Reference docs:
- `docs/ARCHITECTURE.md` — C4 Context / Container / Component diagrams
- `docs/THREAT_MODEL.md` — 20 attacker stories with P0–P3 prioritization
- `docs/OPEN_WORK.md` — consolidated open work items from the former specs/ tree
- `docs/aot-assessment.md`, `docs/attribute.md`, `docs/generator.md`, `docs/emitters.md` — generator ecosystem reference

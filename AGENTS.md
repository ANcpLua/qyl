# AGENTS.md

qyl is organized into seven planes. Prefer changing one plane at a time and keep dependencies one-way.

Read the plane doc that matches the files you touch:
- `planes/data-plane.md`
- `planes/serving-plane.md`
- `planes/intelligence-plane.md`
- `planes/agent-control-plane.md`
- `planes/ledger-governance-plane.md`
- `planes/ui-protocol-plane.md`
- `planes/compiler-plane.md`

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
- `qyl.mcp.generators` is an interim generator. `Qyl.Agents.Generator` is the full generator (dispatch, schemas, OTel, JSON contexts). See `docs/plans/2026-04-10-complete-cp2-cp3-post-merge.md` for the convergence plan.

Known debt — architectural:
- **Triple roster:** ~60 tool classes are manually listed in 3 places that drift: `SkillRegistrationExtensions.WithSkillTools()`, `QylSkillCatalog.SkillMap`, and DI registration in `QylMcpServiceCollectionExtensions`. The generator already knows all tool types at compile time. Fix: CP2 in `docs/plans/2026-04-10-complete-cp2-cp3-post-merge.md`.
- **Capability references unvalidated:** `QylCapabilityCatalog` hardcodes tool names as strings with no check against the generated manifest. Fix: CP3 test in same plan doc.
- `collector/Autofix/` still contains embedded Loom intelligence (LoomOrchestrator, LoomDiagnostician, LoomStrategist, LoomPrompts, etc.) that should live in `qyl.loom/` only. The collector should expose data-plane endpoints, not own LLM orchestration.
- `collector/AgentRuns/` is correct — pure read-only DuckDB queries for agent run observability.
- `LoomToolEnvelope<T>` — use `LoomToolEnvelope.Ok(data)` and `LoomToolEnvelope.Fail<T>(error)` (non-generic companion class), NOT `LoomToolEnvelope<T>.Ok/Fail`.

Project map (13 projects):
- **Platform:** qyl.collector (OTLP ingest, REST API, DuckDB), qyl.contracts (BCL-only types)
- **MCP:** qyl.mcp (77 tools, stdio + Streamable HTTP), qyl.mcp.generators (interim Roslyn generator)
- **Loom:** qyl.loom (standalone agent exe — triage, RCA, fix, code review)
- **SDK:** qyl.instrumentation, qyl.instrumentation.generators, qyl.collector.storage.generators
- **Agents:** Qyl.Agents (runtime), Qyl.Agents.Abstractions (attributes), Qyl.Agents.Generator (unified generator)
- **Frontend:** qyl.dashboard (React 19, Vite 7, Tailwind CSS 4, Base UI 1.3.0)

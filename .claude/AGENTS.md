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

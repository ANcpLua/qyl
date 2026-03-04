# qyl Scope Taxonomy

Use these labels in roadmap/spec docs to avoid mixing local implementation
status with external/comparative research notes.

| Label | Definition | Typical Evidence |
|---|---|---|
| `IMPLEMENTED-IN-QYL` | Capability exists in this repository and is runnable/testable locally. | Code path + endpoint mapping + storage/UI/tool references in `src/**`. |
| `CONTEXT-ONLY` | Comparative or market/reference information retained for planning context. Not a local ship gate. | External docs/blogs/specs, competitor feature timelines. |
| `EXTERNAL-CLOSED` | Known unknowns in closed-source external systems; cannot be verified from this repo. | Public mention of component exists, but implementation inaccessible. |
| `NOT-PLANNED` | Explicitly excluded from qyl architecture/roadmap. | Decision note, ADR, or roadmap statement. |

## Usage Rules

1. Every feature matrix row should carry one scope label.
2. Avoid `OUT-OF-SCOPE` as a catch-all; choose a precise label above.
3. `CONTEXT-ONLY` and `EXTERNAL-CLOSED` can coexist when needed.
4. If a feature moves from context to implementation, relabel to `IMPLEMENTED-IN-QYL`
   and attach local evidence.

## Seer Application

See [seer-scope-reconciliation](../roadmap/seer-scope-reconciliation.md) for
the concrete migration from broad omission language to this taxonomy.


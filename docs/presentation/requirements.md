# Presentation — Requirements

Extracted from [loom-design.md §22](../roadmap/loom-design.md#22-requirements-registry).

## Requirements

| ID      | Capability             | Domain    | Scope          | Evidence                                           | Verification                                      |
|---------|------------------------|-----------|----------------|----------------------------------------------------|--------------------------------------------------|
| QYL-009 | Aspire 13.x Comparison | —         | `CONTEXT-ONLY` | [261?](../roadmap/loom-design.md#219-aspire-coverage--extended-matrix-from-aspire-coverage-md) | Keep matrix current as Aspire evolves |
| QYL-010 | Hosting Resource Model  | Hosting   | `IMPLEMENTED-IN-QYL` | `QylApp`, `QylAppBuilder`, `QylRunner`             | 6 resource types can be provisioned and disposed     |
| QYL-014 | CI/CD Improvements     | —         | `CONTEXT-ONLY` | [262?](../roadmap/loom-design.md#2110-cicd-improvements-from-suggested-improvementsyaml) | SHA-pinned actions and CI hygiene changes verified   |

## Acceptance Criteria

- [ ] Dashboard and hosting pages cover implemented workflow surfaces and remain stable across releases.
- [ ] QylApp/QylRunner orchestration can start/stop supported service resources.
- [ ] CI/CD hygiene items are implemented before they become blockers in release workflows.
- [ ] Scope-only requirements remain marked `CONTEXT-ONLY` with explicit rationale.

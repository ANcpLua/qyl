# Presentation Slice

Presentation in qyl includes dashboard surfaces, web hosting resources, and operating posture for how users
interact with observability outputs.

## Domain Objects

| Object            | Description                                          | src/ Mapping                    |
|-------------------|------------------------------------------------------|---------------------------------|
| Dashboard Surfaces | Monitoring and workflow pages for traces, issues, and AI analytics | `qyl.dashboard/`, `qyl.browser/` |
| Hosting Resources | Polyglot resource orchestration and runners           | `qyl.hosting/`                  |
## Scope

- Hosting abstraction for multi-context running of services, CLI runners, and project resources.
- Comparative guidance for Aspire 13.x feature coverage.
- CI/CD posture that supports deterministic delivery and security hygiene.
- Runtime/runtime-less entry points for presentation services.

## Cross-Slice Dependencies

- **ingestion/** provides the data surfaces consumed by dashboard pages.
- **query/** powers the MCP + API calls used in presentation pages.
- **intelligence/** feeds AI insights consumed by workflow UIs.

## Key Files

```text
src/qyl.dashboard/ (pages, charts, navigation)
src/qyl.hosting/QylApp.cs
src/qyl.hosting/QylAppBuilder.cs
src/qyl.hosting/QylRunner.cs
```

## References

- [loom-design.md §15.9](../roadmap/loom-design.md#159-aspire-13x-feature-coverage) — QYL-009
- [loom-design.md §15.10](../roadmap/loom-design.md#1510-hosting-resource-model) — QYL-010
- [loom-design.md §21.9](../roadmap/loom-design.md#219-aspire-coverage--extended-matrix-from-aspire-coverage-md) — comparison matrix
- [loom-design.md §21.10](../roadmap/loom-design.md#2110-cicd-improvements-from-suggested-improvementsyaml) — CI/CD
- [qyl/decisions/SCOPE-TAXONOMY.md](../decisions/SCOPE-TAXONOMY.md)

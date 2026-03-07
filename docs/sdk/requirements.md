# SDK — Requirements

Extracted from [loom-design.md §22](../roadmap/loom-design.md#22-requirements-registry).

## Requirements

| ID       | Capability                         | Domain   | Scope                | Evidence                                           | Verification                                      |
|----------|------------------------------------|----------|----------------------|----------------------------------------------------|--------------------------------------------------|
| QYL-001  | GenAI Semantic Conventions         | SDK      | `IMPLEMENTED-IN-QYL` | Generated models under `qyl.protocol` (`*.g.cs`)    | Verify semconv attribute presence in generated spans |
| QYL-006  | Compile-Time Tracing (`[Traced]`)  | SDK      | `IMPLEMENTED-IN-QYL` | `TracedInterceptorEmitter`, analyzer pipeline         | `TracedTests` + source-generated interceptor output |
| QYL-007  | Zero-Cost Observability Contracts  | SDK      | `IMPLEMENTED-IN-QYL` | `SubscriptionManager`, `ObserveCatalog`              | Subscribing activates previously dormant ActivitySources |
| QYL-011  | Agent Continuation Evaluation       | SDK      | `IMPLEMENTED-IN-QYL` | `qyl.copilot` continuation heuristics               | Compare evaluator-call count and fallback path       |

## Acceptance Criteria

- [ ] Compile-time generators build deterministically in CI and emit expected interceptors.
- [ ] GenAI semantic conventions are source-of-truth for both telemetry ingestion and analytics queries.
- [ ] SDK contracts prevent runtime reflection overhead for hot methods.
- [ ] Zero-cost subscription model is observable from runtime behavior (dormant vs active).

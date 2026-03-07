# SDK Slice

qyl’s instrumentation SDK owns compile-time telemetry contracts, semantic conventions, and low-overhead runtime
behavior switching.

## Domain Objects

| Object                 | Description                                      | src/ Mapping                              |
|------------------------|--------------------------------------------------|-------------------------------------------|
| Semconv Contracts      | Generated and strongly typed OTel GenAI attributes | `qyl.protocol/`                           |
| Traced Interceptors    | Roslyn-driven compile-time method interception      | `qyl.servicedefaults.generator/`          |
| Runtime Telemetry Kits  | Generated conventions and attribute helpers        | `qyl.servicedefaults/`                   |
| Heuristic Continuation  | LLM evaluator minimization heuristics             | `qyl.copilot/` (agent continuation hooks) |

## Scope

- Compile-time tracing instrumentation for methods (`[Traced]`, `[TracedTag]`, `[NoTrace]`).
- Semantic convention codegen shared across source, C#, and DuckDB SQL targets.
- Zero-cost observability mode contracts for dormant instrumentation behavior.
- Heuristic-first agent continuation evaluation for cost-sensitive routing.

## Cross-Slice Dependencies

- **ingestion/** consumes traced spans emitted by SDK instrumentation.
- **intelligence/** executes continuation and telemetry-derived pipelines.
- **query/** exposes generated telemetry data via MCP/REST/AG-UI.

## Key Files

```text
src/qyl.protocol/*.g.cs
src/qyl.servicedefaults/Instrumentation/*.cs
src/qyl.servicedefaults.generator/Analyzers/*.cs
src/qyl.servicedefaults.generator/Emitters/*.cs
src/qyl.servicedefaults.generator/Models/Models.cs
src/qyl.collector/Observe/
src/qyl.collector/Observe/
```

## References

- [loom-design.md §15.1](../roadmap/loom-design.md#151-genai-semantic-conventions-model) — QYL-001
- [loom-design.md §15.6](../roadmap/loom-design.md#156-compile-time-tracing-annotations) — QYL-006
- [loom-design.md §15.7](../roadmap/loom-design.md#157-zero-cost-observability-contracts) — QYL-007
- [loom-design.md §21.5](../roadmap/loom-design.md#215-genai-semconv-full-reference-from-otel-semconv-reference-md) — full reference
- [loom-design.md §21.6](../roadmap/loom-design.md#216-traced-annotations--stories-from-traced-annotations-md) — traced stories
- [loom-design.md §15.11](../roadmap/loom-design.md#1511-agent-continuation-evaluation-heuristic-first-pattern) — QYL-011

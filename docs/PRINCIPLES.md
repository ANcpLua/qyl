# QYL Development Principles

Core engineering principles for working on the qyl observability platform.

| Principle | What It Means in Practice |
|-----------|--------------------------|
| **Avoid short fixes** | No suppressing warnings, loosening schemas, adding "temporary" flags, or papering over mismatches. They silently rot data and only surface months later in broken queries, alerts, or cost models. Long-run correctness is not optional. |
| **Call out bad assumptions plainly** | Telemetry is assumption-heavy: "this span is always present", "this attribute is optional", "this table won't grow fast". If an assumption violates ingestion reality, OTel semantics, or DuckDB behavior, calling it out early is cheaper than fixing corrupted telemetry later. |
| **Plan first, checkpoints, no partial refactors** | Telemetry changes cross boundaries: protocol → storage → query → dashboard → tests → build. Partial refactors cause schema drift and broken historical data. Always plan with explicit pause points before touching a pipeline. |
| **Consider the entire schema, not just AI** | QYL is a signal processing system, not an "AI feature". Traces, logs, metrics, semconv, generators, DuckDB layout, retention, and query shape all interact. Optimizing one slice in isolation makes the platform inconsistent and untrustworthy. |
| **If you can't root-cause, stop — don't suppress** | Suppressing diagnostics in a telemetry platform is self-sabotage: you're blinding the system that's supposed to explain the system. If a signal is noisy or broken, understand why it exists — don't mute it. |
| **NUKE is the orchestrator** | Telemetry platforms live and die by reproducibility. NUKE encodes generation order, test ordering, coverage, CI parity, and artifact production. Bypassing it casually is how "works on my machine" leaks into data pipelines. |
| **Map current vs proposed pipeline** | Any change must explicitly state: "this part of the pipeline changes, the rest stays identical." Ambiguous changes to ingest → normalize → store → query → visualize are risky by definition. |
| **Keep commands minimal** | Fewer entry points = fewer accidental modes. Telemetry already has combinatorial complexity in signals and schemas; the build/test surface should be boring and constrained. |
| **Prefer Playwright MCP over more tests (when appropriate)** | End-to-end signal validation (data appears, streams update, dashboards react) catches failures unit tests never will. Over-testing internals while missing broken real-world flows is a classic telemetry mistake. |

# Intelligence Slice

AI-powered debugging pipelines: autofix, triage, code review, anomaly detection, issue grouping.

## Domain Objects

| Object     | Description                                   | src/ Mapping                              |
|------------|-----------------------------------------------|-------------------------------------------|
| Autofix    | Bug fixing pipeline (RCA → solution → PR)     | `qyl.copilot/` + `qyl.collector/Loom/`   |
| Triage     | Issue scoring and prioritization              | `qyl.collector/Loom/Triage*`              |
| CodeReview | Pre-merge PR analysis via GitHub webhooks     | `qyl.collector/Loom/CodeReview*`          |
| Anomaly    | Time-series anomaly and regression detection  | `qyl.collector/Loom/Regression*`          |
| Grouping   | Issue similarity and deduplication            | (roadmap — not yet implemented)           |

## Scope

- Autofix orchestration: root cause → solution → code change → impact → triage
- Fixability scoring with 5-tier thresholds (0.50/0.65/0.76)
- GitHub webhook ingestion → PR code review → comment generation
- Regression detection via time-series analysis
- Agent handoff lifecycle (Explorer → Coding Agent)
- AI chat analytics (6 modules, 9 API endpoints)

## Cross-Slice Dependencies

- **ingestion/** stores the traces/logs that feed anomaly detection and triage
- **query/** exposes MCP tools that invoke intelligence pipelines
- **presentation/** renders Loom dashboard pages (triage, autofix, code review)

## Key Files

```text
src/qyl.collector/Loom/AutofixEndpoints.cs
src/qyl.collector/Loom/AutofixAgentService.cs
src/qyl.collector/Loom/AutofixOrchestrator.cs
src/qyl.collector/Loom/TriagePipelineService.cs
src/qyl.collector/Loom/CodeReviewService.cs
src/qyl.collector/Loom/RegressionDetectionService.cs
src/qyl.collector/Loom/AgentHandoffService.cs
src/qyl.copilot/QylAgentBuilder.cs
```

## Sentry Reference (CONTEXT-ONLY)

Full Seer architecture documented in [loom-design.md §20](../roadmap/loom-design.md#20-seer-ai-platform--deep-architecture):
- LOOM-001..011: Sentry Loom capabilities
- SEER-001..013: Seer deep architecture
- §24: 2025+ web research patterns (agent instrumentation, LLM middleware, OAuth)

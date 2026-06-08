# Observability Evaluation Data

Each JSONL line is a complete qyl observability scenario. The runner loads the line, evaluates deterministic observability rules, and compares failed metric names with `expectedFailedMetrics`.

Common fields:

- `id`: stable scenario identifier.
- `source`: sample provenance.
- `scenario`: short human-readable scenario name.
- `agent`: agent metadata and visible tool names.
- `userInput`: user request.
- `toolCalls`: qyl tool/API calls issued by the agent.
- `finalResponse`: answer produced by the system under evaluation.
- `telemetry`: trace/metric/log evidence available to the agent.
- `requiredEvidenceIds`: evidence IDs that must be cited in the answer.
- `forbiddenClaims`: claims that must not appear in the answer.
- `expectedToolCalls`: required qyl tool calls and argument subsets.
- `expectedFailedMetrics`: exact deterministic evaluator metrics expected to fail.
- `shouldPass`: shorthand gate expectation; pass records must have no expected failed metrics.

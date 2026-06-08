# Qyl.Observability.Evaluation

Deterministic observability evaluation harness for qyl agent and incident-triage flows.

This project intentionally starts without an LLM judge. It validates observable behavior from JSONL scenario fixtures:

- required tool calls and argument subsets
- required telemetry evidence IDs cited in the final answer
- trace parent/child correlation
- blocked high-cardinality or sensitive telemetry attributes

Run locally:

```bash
dotnet run --project evals/Qyl.Observability.Evaluation/Qyl.Observability.Evaluation.csproj
```

Additional scenario packs can be passed as file paths:

```bash
dotnet run --project evals/Qyl.Observability.Evaluation/Qyl.Observability.Evaluation.csproj -- \
  evals/Qyl.Observability.Evaluation/Data/incident-triage.jsonl
```

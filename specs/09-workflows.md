# Workflows Specification

**Status: SUPERSEDED** — `qyl.workflows` project deleted per v2 architecture decision (2026-03-16).

qyl does not provide workflow orchestration. MAF provides:
- `AgentWorkflowBuilder.BuildSequential()` — pipeline: A -> B -> C
- `AgentWorkflowBuilder.BuildConcurrent()` — fan-out/fan-in
- `HandoffsWorkflowBuilder` — agent decides who handles next
- `GroupChatWorkflowBuilder` — multi-agent group chat
- `DeclarativeWorkflowBuilder` — YAML-based workflows
- `InProcessExecution.RunStreamingAsync()` — streaming execution
- Checkpointing, HITL, conditional edges, loops, durable agents

If Loom needs workflow orchestration, Loom uses MAF directly.

See `specs/v2-architecture.md` section 4.2.

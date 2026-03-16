# Agents Specification

**Status: SUPERSEDED** — `qyl.agents` project deleted per v2 architecture decision (2026-03-16).

qyl does not build agents. Microsoft Agent Framework (MAF) provides:
- `builder.AddAIAgent()` — DI-native agent construction
- `AgentWorkflowBuilder.BuildSequential/BuildConcurrent` — workflow orchestration
- `HandoffsWorkflowBuilder` — triage routing
- `app.MapAGUI()` — AG-UI SSE for browser chat
- `app.MapA2A()` — agent-to-agent async dispatch

qyl's SDK provides observability for agents via:
- `InstrumentedChatClient` — DelegatingChatClient with OTel GenAI semconv 1.40 spans
- `InstrumentedAIFunction` — DelegatingAIFunction with execute_tool spans
- `UseQylTelemetry()` — ChatClientBuilder extension
- Compile-time interceptors (`[GenAi]` attribute + source generator)

See `specs/v2-architecture.md` section 3.3.

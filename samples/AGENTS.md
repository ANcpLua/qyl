# qyl samples

**Currently empty.** The Feb-2026 Loom monolith (`maf-agent-qyl/`) was removed on 2026-04-17 — its content was superseded by the production Loom code under `src/qyl.loom/V2/` (executor-based workflow, not a hosted-agents demo).

## Where to learn what

| You want to learn | Go to |
|---|---|
| MAF fundamentals (agents, sessions, tools, streaming, structured output, RAG) | `~/AgentFrameworkBook/` or upstream `microsoft/agent-framework/dotnet/samples/` |
| qyl-specific MAF delta (`WithQylTelemetry`, `LoomRunState`, hosted `AddAIAgent`) | `.claude/skills/microsoft-agent-framework/SKILL.md` (qyl overlay) |
| qyl's production agent workflow | `src/qyl.loom/V2/LoomV2Workflow.cs` + `src/qyl.loom/V2/LoomV2Executors.cs` |
| qyl MCP tool pattern (attributes + generator) | `src/qyl.mcp/Tools/` + the overlay's "Generator-driven attributes" section |

## When to add a new sample here

Only when the sample demonstrates something **qyl-specific** that neither upstream nor `~/AgentFrameworkBook/` covers. Examples that would qualify:

- `WithQylTelemetry` wiring against a non-obvious provider
- End-to-end `[QylSkill]` + `[QylCapability]` tool declaration hitting the qyl.mcp generator output
- `InvestigationLineage` guard exercised in a multi-step agent sequence
- `LoomRunState` discipline across bounded agents with real trace assertions

Anything that just teaches MAF basics belongs upstream or in `~/AgentFrameworkBook/`, not here.

## Rules for a qyl-specific sample

When one does land:

- Standalone project at `samples/<category>/<name>/` (categories mirror MAF: `01-get-started`, `02-agents`, `03-workflows`, `04-hosting`, `05-end-to-end`).
- Add the `.csproj` to `qyl.slnx`.
- Include a `README.md` with required env vars, the exact `dotnet run --project ...` invocation, and expected output.
- Configure via environment variables — never hardcode secrets.
- Wire OTel on every sample. qyl is observability; every sample should show up in the Aspire Dashboard.
- Reference `FakeChatClient` from `ANcpLua.Roslyn.Utilities.Testing.AgentTesting.ChatClients` for mock chat behavior (never hand-roll `Moq<IChatClient>`).
- UTF-8 with BOM on new `.cs` files. Copyright `// Copyright (c) 2025-2026 ancplua`.

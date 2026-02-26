# ADR-005: Microsoft Agent Framework Copilot

Status: Accepted
Date: 2026-02-26
Depends-On: ADR-002

## Context

qyl.copilot already integrates with GitHub Copilot via SSE/AG-UI. But the agent logic is custom-built. Microsoft Agent Framework (successor to Semantic Kernel + AutoGen) provides a standardized agent abstraction with tool calling, multi-turn conversations, and provider-agnostic LLM access.

## Decision

qyl.copilot adopts **Microsoft Agent Framework** as its agent runtime. The framework supports multiple providers (OpenAI, Anthropic, Ollama, local models) — no Azure lock-in.

### Architecture

```
User (GitHub Copilot / MCP Client / Dashboard Chat)
    |
    v
qyl.copilot (Microsoft Agent Framework)
    |
    ├── LLM Provider (user's choice):
    |   ├── OpenAI (API key)
    |   ├── Anthropic (API key)
    |   ├── Ollama (local, free)
    |   └── Any OpenAI-compatible endpoint
    |
    ├── MCP Tools (24 tools → qyl.mcp):
    |   ├── search_spans, get_trace, get_genai_stats
    |   ├── list_sessions, analyze_session_errors
    |   ├── list_build_failures, search_logs
    |   └── ... (full tool list in qyl.mcp CLAUDE.md)
    |
    └── Actions:
        ├── Auto-triage errors (query → analyze → assign)
        ├── Answer questions about telemetry
        └── Run workflows (pre-defined analysis patterns)
```

### Provider Configuration

```bash
# Any ONE of these — user's choice, all free options available
QYL_LLM_PROVIDER=ollama          # Local, free, private
QYL_LLM_ENDPOINT=http://localhost:11434
QYL_LLM_MODEL=llama3

# Or OpenAI
QYL_LLM_PROVIDER=openai
QYL_LLM_API_KEY=sk-...
QYL_LLM_MODEL=gpt-4o-mini

# Or any OpenAI-compatible endpoint
QYL_LLM_PROVIDER=openai-compatible
QYL_LLM_ENDPOINT=http://my-local-llm:8080
QYL_LLM_MODEL=my-model
```

### Without LLM Provider

qyl works fully without any LLM configured:
- OTLP ingestion: works
- Dashboard: works
- MCP tools: work (they're just HTTP → DuckDB queries)
- Copilot agent: disabled (shows "Configure LLM provider to enable AI features")

The LLM is optional — it enables the agent, not the platform.

## Constraints

- No Azure requirement — Ollama (local, free) is the default recommendation
- No paid API keys required — local models are first-class
- Microsoft Agent Framework is MIT licensed, open source
- Agent Framework is currently preview — pin specific version, handle breaking changes

## Acceptance Criteria

```gherkin
GIVEN qyl running with QYL_LLM_PROVIDER=ollama
WHEN  user asks "What errors happened in the last hour?"
THEN  agent calls list_errors MCP tool
AND   returns human-readable summary
AND   no data leaves localhost

GIVEN qyl running without LLM configuration
WHEN  user opens dashboard
THEN  all telemetry features work
AND   chat/agent features show "Configure LLM to enable"

GIVEN qyl running with any supported provider
WHEN  agent detects error spike
THEN  agent can autonomously:
      - Query related spans and logs
      - Correlate with recent deploys
      - Suggest root cause
      - (With user approval) Create GitHub issue
```

## Verification Steps (Agent-Executable)

1. Start qyl with `QYL_LLM_PROVIDER=ollama` + Ollama running locally
2. Send chat message via `/api/v1/copilot/chat`
3. Assert agent calls MCP tools and returns response
4. Start qyl without LLM config → assert dashboard works, agent disabled
5. Assert no network calls to external services (except GitHub if configured)

## Consequences

- qyl.copilot dependency: `Microsoft.Agents.AI` (+ provider package)
- Provider abstraction means LLM is swappable without code changes
- Ollama as default = fully free, fully private, fully local
- Agent Framework handles conversation state, tool dispatch, retries
- Future: multi-agent workflows (triage agent → fix agent → review agent)

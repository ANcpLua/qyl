# qyl Agents

3 specialized agents for parallel qyl development.

## Setup

Copy these files to your qyl project:

```bash
# Create agents directory
mkdir -p /Users/ancplua/qyl/.claude/agents

# Copy agent definitions
cp qyl-collector.md /Users/ancplua/qyl/.claude/agents/
cp qyl-dashboard.md /Users/ancplua/qyl/.claude/agents/
cp qyl-build.md /Users/ancplua/qyl/.claude/agents/
cp COORDINATION.md /Users/ancplua/qyl/.claude/agents/
```

## Usage

```bash
cd /Users/ancplua/qyl

# Start 3 terminals, each with different agent:

# Terminal 1: Backend
claude --agent qyl-collector

# Terminal 2: Frontend  
claude --agent qyl-dashboard

# Terminal 3: Build System
claude --agent qyl-build
```

## Agent Responsibilities

| Agent | Domain | First Task |
|-------|--------|------------|
| `qyl-collector` | C# Backend | Implement SpanRingBuffer |
| `qyl-dashboard` | React Frontend | Wire up SSE + Virtual List |
| `qyl-build` | NUKE/TypeSpec | Verify DashboardEmbed target |

## Coordination

Agents communicate through:
1. **CLAUDE.md files** - Architecture contracts
2. **Generated files** - Shared types (*.g.cs, api.ts)
3. **OpenAPI spec** - API contract

See `COORDINATION.md` for detailed workflow.

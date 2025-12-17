# Workflow-Sync Plugin Notes

## Plugin Structure

```
workflow-sync/
├── plugin.json              # Plugin manifest
├── hooks/
│   ├── session-start.sh     # Read WORKFLOW.md
│   ├── session-end.sh       # Sync state
│   └── build-status.sh      # After Bash(dotnet build)
├── commands/
│   └── workflow.md          # /workflow command
└── skills/
    └── workflow-management.md
```

## plugin.json

```json
{
  "name": "workflow-sync",
  "version": "1.0.0",
  "description": "Persist workflow state across sessions to prevent context drift",
  "hooks": {
    "SessionStart": [{ "command": "${CLAUDE_PLUGIN_ROOT}/hooks/session-start.sh" }],
    "Stop": [{ "command": "${CLAUDE_PLUGIN_ROOT}/hooks/session-end.sh" }],
    "PostToolUse": [{
      "matcher": "Bash",
      "hooks": [{ "command": "${CLAUDE_PLUGIN_ROOT}/hooks/build-status.sh" }]
    }]
  },
  "commands": ["commands/workflow.md"]
}
```

## Key Variables

| Variable | Purpose |
|----------|---------|
| `${CLAUDE_PLUGIN_ROOT}` | Plugin install dir |
| `${CLAUDE_PROJECT_ROOT}` | User's project root |
| `$TOOL_INPUT` | Input to the tool (PostToolUse) |
| `$TOOL_OUTPUT` | Output from tool (PostToolUse) |

## /workflow Command Ideas

```yaml
---
name: workflow
description: Manage workflow state
arguments:
  - name: action
    description: status|update|clear|export
---
```

Actions:
- `status` - Show current WORKFLOW.md
- `update` - Interactive update of sections
- `clear` - Archive current, start fresh
- `export` - Export as compact summary

## Build Status Hook Logic

```bash
#!/bin/bash
# hooks/build-status.sh

# Only trigger on dotnet build commands
if [[ "$TOOL_INPUT" != *"dotnet build"* ]]; then
  exit 0
fi

STATUS_FILE="${CLAUDE_PROJECT_ROOT}/.claude/build-status"

if echo "$TOOL_OUTPUT" | grep -q "Build succeeded"; then
  echo "✅ PASSING - $(date -u +%H:%M)" > "$STATUS_FILE"
elif echo "$TOOL_OUTPUT" | grep -q "Build FAILED"; then
  ERROR_COUNT=$(echo "$TOOL_OUTPUT" | grep -oE "[0-9]+ Error" | head -1)
  echo "❌ FAILING - ${ERROR_COUNT:-errors} - $(date -u +%H:%M)" > "$STATUS_FILE"
fi
```

## Skill Trigger Phrases

```
- "what's the current workflow"
- "update workflow status"
- "sync workflow"
- "continue from last session"
```

## Testing Checklist

- [ ] SessionStart reads WORKFLOW.md correctly
- [ ] Stop hook preserves manual sections
- [ ] Build status updates after `dotnet build`
- [ ] /workflow status shows current state
- [ ] Works in fresh session (no prior state)
- [ ] Works with existing WORKFLOW.md

## Future Ideas

1. **TodoWrite Integration** - Sync completed todos to WORKFLOW.md
2. **Multi-Workflow** - Support parallel task tracking
3. **Export to GitHub Issue** - `/workflow export --github`
4. **Time Tracking** - Track session duration per task
5. **Blockers Alert** - Highlight blockers at session start

---

Viel Erfolg! 🚀

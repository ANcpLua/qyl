---
name: workflow-management
description: |
  This skill should be used when the user asks "continue from last session", "what was I working on", "resume where I left off", "pick up from before", "check workflow status", "what's the current state", "what blockers exist", "what should I do next", or mentions resuming previous work, checking progress, or understanding session context. Provides guidance for using workflow-sync plugin to maintain context across Claude Code sessions.
---

## Source Metadata

```yaml
frontmatter:
  version: 1.0.0
original_name: "Workflow Management"
plugin:
  name: "workflow-sync"
  version: "1.0.0"
  description: "Persist workflow state across Claude Code sessions to prevent context drift"
  author:
    name: "ancplua"
```


# Workflow Management

Maintain context and state across Claude Code sessions using the workflow-sync plugin.

## Core Concept

The `.claude/WORKFLOW.md` file persists workflow state between sessions:

- **YAML frontmatter**: Machine-readable status (task, phase, build, errors)
- **Preserved sections**: Manually maintained context (Blockers, Next Actions, Decisions, Session Log)
- **Auto-updated sections**: Automatically synced (Git Status, Build Output)

## Session Start Behavior

At session start, the SessionStart hook:

1. Reads `.claude/WORKFLOW.md` if it exists
2. Injects workflow status via systemMessage
3. Updates `session_start` timestamp

If a workflow file exists, Claude receives context about:
- Current task and phase
- Build status (passing/failing/unknown)
- Number of blockers
- Last update timestamp

## Resuming Previous Work

When resuming work from a previous session:

1. Read `.claude/WORKFLOW.md` for full context
2. Check `## Current Blockers` section for issues preventing progress
3. Review `## Next Actions` for prioritized task list
4. Consult `## Decision Log` to understand choices already made
5. Scan `## Session Log` for recent checkpoint entries

## Checking Status

Use `/workflow status` or `/workflow` to display:

- Task description and current phase
- Status (active, blocked, paused, completed)
- Build status with error/warning counts
- Blocker summary
- Next action items
- Recent decisions and checkpoints

## Creating Checkpoints

Save progress markers during work:

```
/checkpoint                              # Quick save
/checkpoint Completed user auth API      # With message
/checkpoint Tests passing, ready for PR
```

Checkpoints append timestamped entries to the Session Log section.

## Managing Blockers

When encountering blockers:

1. Add to `## Current Blockers` section with clear description
2. Update frontmatter `status: "blocked"` if work cannot continue
3. Document attempted solutions in Decision Log
4. When resolved, remove from Blockers and log resolution

## Workflow Lifecycle

### Starting New Work

```
/workflow new "Implement user authentication"
```

Creates fresh WORKFLOW.md with:
- Task set to provided description
- Phase set to "discovery"
- Status set to "active"
- Empty sections ready for content

### During Development

1. Save checkpoints as milestones complete
2. Log decisions when making architectural choices
3. Update blockers as issues arise/resolve
4. Let hooks auto-update git and build status

### Completing Work

1. Set `status: "completed"` in frontmatter
2. Export if needed: `/workflow export`
3. Clear for next task: `/workflow clear`

## YAML Frontmatter Fields

| Field | Values | Purpose |
|-------|--------|---------|
| `task` | string | Current task description |
| `phase` | discovery, design, implementation, testing, review | Development phase |
| `status` | active, blocked, paused, completed | Workflow state |
| `build` | passing, failing, unknown | Last build result |
| `error_count` | number | Build error count |
| `warning_count` | number | Build warning count |
| `last_updated` | ISO 8601 | Last sync timestamp |
| `session_start` | ISO 8601 | Current session start |

## Preserved vs Auto-Updated Sections

### Preserved (manually maintained)

- `## Current Blockers` - Issues preventing progress
- `## Next Actions` - Prioritized task list
- `## Decision Log` - Choices made with rationale
- `## Session Log` - Checkpoint entries

Never modify these during auto-sync. Only update through explicit actions.

### Auto-Updated (hooks manage)

- `## Git Status` - SessionEnd hook syncs branch and file changes
- `## Build Output` - PostToolUse hook updates after builds

These are overwritten by hooks; manual edits will be lost.

## Commands Reference

| Command | Purpose |
|---------|---------|
| `/workflow` | Show status (default) |
| `/workflow status` | Show status explicitly |
| `/workflow new [task]` | Initialize new workflow |
| `/workflow checkpoint [msg]` | Save to session log |
| `/workflow clear` | Reset to clean state |
| `/workflow export` | Export to timestamped file |
| `/checkpoint [msg]` | Quick checkpoint alias |

## Best Practices

1. **Start each session** by reading WORKFLOW.md for context
2. **Checkpoint frequently** when completing significant work
3. **Log decisions** when making non-obvious choices
4. **Update blockers** immediately when issues arise
5. **Keep Next Actions** prioritized and current
6. **Export before clearing** to preserve history

## Troubleshooting

**No workflow file exists:**
- Run `/workflow new "Task description"` to initialize

**Build status not updating:**
- PostToolUse hook runs on Bash commands
- Check if build command was recognized
- Verify `.claude/WORKFLOW.md` exists

**Git status outdated:**
- SessionEnd hook updates git status
- Status reflects state at last session end
- Run `/workflow status` for current view

**Session context missing:**
- Ensure WORKFLOW.md exists in `.claude/`
- SessionStart hook reads this file
- Check `session_start` timestamp updated

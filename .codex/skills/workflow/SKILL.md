---
name: workflow
description: |
  Manage workflow state (status|new|checkpoint|clear|export)
---

## Source Metadata

```yaml
frontmatter:
  argument-hint: [subcommand] [args]
  allowed-tools: Read, Write, Edit, Bash(git:*), Bash(cat:*), Bash(date:*), Bash(mkdir:*), Bash(cp:*)
plugin:
  name: "workflow-sync"
  version: "1.0.0"
  description: "Persist workflow state across Claude Code sessions to prevent context drift"
  author:
    name: "ancplua"
```


Workflow state management command. Parse arguments and execute the appropriate subcommand.

**Subcommand: $1**
**Additional args: $2 $3**

## Subcommand Handlers

### status (default if no subcommand)

Display current workflow state from `.claude/WORKFLOW.md`:

1. Read the WORKFLOW.md file
2. Parse and display YAML frontmatter fields:
   - task, phase, status, build, error_count, warning_count, last_updated
3. Summarize each section:
   - Current Blockers (count and list)
   - Next Actions (first 3 items)
   - Decision Log (last 3 entries)
   - Session Log (last 3 entries)
   - Git Status summary
   - Build Output summary

Format as a clean status report with emoji indicators.

### new

Initialize a new workflow file:

1. Create `.claude/` directory if needed: `mkdir -p .claude`
2. Read template from `${CLAUDE_PLUGIN_ROOT}/templates/WORKFLOW.template.md`
3. Generate current timestamp in ISO 8601 format
4. Replace `${TIMESTAMP}` placeholders with current UTC timestamp
5. Replace `${DATE}` placeholders with current date (YYYY-MM-DD)
6. If $2 is provided, set it as the task description in frontmatter
7. Write to `.claude/WORKFLOW.md`
8. Confirm creation and show initial status

If WORKFLOW.md already exists, ask before overwriting.

### checkpoint

Quick timestamped state save (alias for /checkpoint command):

1. Read current WORKFLOW.md
2. Generate timestamp
3. Append entry to Session Log section in format:
   ```
   - **HH:MM UTC** - $2 (or "Checkpoint saved" if no message)
   ```
4. Update last_updated in frontmatter
5. Confirm checkpoint saved

### clear

Reset workflow to clean state:

1. Confirm with user before clearing
2. Preserve the current task description from frontmatter
3. Reset all status fields to defaults:
   - phase: "discovery"
   - status: "active"
   - build: "unknown"
   - error_count: 0
   - warning_count: 0
4. Clear Session Log entries (keep section header)
5. Update last_updated
6. Confirm reset complete

### export

Export workflow to standalone file:

1. Read current WORKFLOW.md
2. Add export metadata header with timestamp
3. Copy to `.claude/exports/WORKFLOW-{date}.md`
4. Confirm export location

## Error Handling

- If WORKFLOW.md doesn't exist and subcommand isn't "new":
  - Suggest running `/workflow new [task description]`
- If subcommand is unrecognized:
  - List available subcommands with descriptions
- If required args missing:
  - Show usage for that subcommand

## Usage Examples

```
/workflow              # Show status (default)
/workflow status       # Show status explicitly
/workflow new          # Initialize new workflow
/workflow new "Fix login bug"  # Initialize with task
/workflow checkpoint   # Quick save
/workflow checkpoint "Completed API integration"
/workflow clear        # Reset to clean state
/workflow export       # Export current state
```

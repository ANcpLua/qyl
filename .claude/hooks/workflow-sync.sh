#!/bin/bash
# =============================================================================
# Workflow Sync Hook - Persists workflow state to .claude/WORKFLOW.md
# Triggers on: Stop, SessionEnd
# Purpose: Prevent context drift on long-running tasks across sessions
#
# BEHAVIOR:
# - Creates WORKFLOW.md if missing (with template)
# - Updates timestamp and git status sections (preserves rest)
# - Never overwrites manually-added Issues/Next Steps sections
# =============================================================================

WORKFLOW_FILE="${CLAUDE_PROJECT_ROOT:-.}/.claude/WORKFLOW.md"
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# If file doesn't exist, create template
if [ ! -f "$WORKFLOW_FILE" ]; then
    mkdir -p "$(dirname "$WORKFLOW_FILE")"
    cat > "$WORKFLOW_FILE" << 'EOF'
# Active Workflow State

> Auto-synced by workflow-sync hook. **READ THIS FIRST** at session start.
> Last updated: TIMESTAMP_PLACEHOLDER

## Current Task

<!-- Describe the current task/goal here -->

## Git Status

```
<!-- Auto-updated by hook -->
```

## Build Status

<!-- Run `dotnet build` to update -->
Unknown

## Current Issues

<!-- Add blockers and issues here -->

## Next Steps

<!-- Add next actions here -->

## Decision Log

| Decision | Rationale |
|----------|-----------|
| | |

---

**Session Instructions:**
1. Read this file at session start
2. Continue from "Next Steps"
3. Update this file before ending session
EOF
fi

# Create temp file with updates
TEMP_FILE=$(mktemp)

# Read existing file and update dynamic sections
awk -v timestamp="$TIMESTAMP" -v git_status="$(cd "${CLAUDE_PROJECT_ROOT:-.}" && git status --short 2>/dev/null | head -20)" '
BEGIN { in_git_block = 0 }

# Update timestamp in header
/^> Last updated:/ {
    print "> Last updated: " timestamp
    next
}

# Update git status section
/^## Git Status/ {
    print
    getline
    print
    in_git_block = 1
    print git_status
    # Skip old git content until closing ```
    while ((getline line) > 0 && line !~ /^```$/) {}
    print "```"
    in_git_block = 0
    next
}

# Print everything else unchanged
{ print }
' "$WORKFLOW_FILE" > "$TEMP_FILE"

mv "$TEMP_FILE" "$WORKFLOW_FILE"

echo "Workflow synced: $WORKFLOW_FILE"

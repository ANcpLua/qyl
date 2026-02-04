---
name: checkpoint
description: |
  Quick timestamped state save to Session Log
---

## Source Metadata

```yaml
frontmatter:
  argument-hint: [message]
  allowed-tools: Read, Edit, Bash(date:*)
plugin:
  name: "workflow-sync"
  version: "1.0.0"
  description: "Persist workflow state across Claude Code sessions to prevent context drift"
  author:
    name: "ancplua"
```


Save a timestamped checkpoint to the Session Log section of `.claude/WORKFLOW.md`.

## Actions

1. Verify `.claude/WORKFLOW.md` exists
   - If not, inform user to run `/workflow new` first

2. Generate timestamp: `HH:MM UTC` format

3. Prepare checkpoint message:
   - If $ARGUMENTS provided: use that as the message
   - If no arguments: use "Checkpoint saved"

4. Append to Session Log section:
   ```markdown
   - **HH:MM UTC** - [message]
   ```

5. Update YAML frontmatter:
   - Set `last_updated` to current ISO 8601 timestamp

6. Confirm:
   ```
   Checkpoint saved at HH:MM UTC
   ```

## Format

Checkpoints appear in Session Log as:

```markdown
## Session Log

### 2025-12-18

- **09:15 UTC** - Started debugging auth flow
- **10:30 UTC** - Found root cause in token validation
- **11:45 UTC** - Implemented fix, running tests
```

## Usage Examples

```
/checkpoint                          # "Checkpoint saved"
/checkpoint Completed API endpoint   # With message
/checkpoint Tests passing, ready for review
```

## Error Handling

- If WORKFLOW.md doesn't exist: suggest `/workflow new`
- If Session Log section missing: create it before appending

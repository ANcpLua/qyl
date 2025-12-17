#!/bin/bash
# PreToolUse hook: Validate architecture rules from CLAUDE.md
# Enforces dependency rules and SSOT locations
# SKIPS generated files (handled by autogen-guard.sh)

set -e

TOOL_NAME="${CLAUDE_TOOL_NAME:-}"
TOOL_INPUT="${CLAUDE_TOOL_INPUT:-}"
PROJECT_ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# Only check Write and Edit tools
if [[ "$TOOL_NAME" != "Write" && "$TOOL_NAME" != "Edit" ]]; then
    exit 0
fi

# Extract file path
FILE_PATH=$(echo "$TOOL_INPUT" | jq -r '.file_path // empty' 2>/dev/null)
if [[ -z "$FILE_PATH" ]]; then
    exit 0
fi

# SKIP generated files - autogen-guard.sh handles these
case "$FILE_PATH" in
    *.g.cs|*/Generated/*|*/generated/*|*.generated.ts)
        exit 0
        ;;
esac

# Skip if NUKE is running (generator)
if [[ "${NUKE_BUILD_RUNNING:-}" == "true" ]]; then
    exit 0
fi

# Rule 1: qyl.protocol types should not be duplicated in qyl.collector
PROTOCOL_TYPES=("SessionId" "UnixNano" "GenAiAttributes" "SpanId" "TraceId")
BASENAME=$(basename "$FILE_PATH" .cs)

if [[ "$FILE_PATH" == *"/qyl.collector/"* ]]; then
    for TYPE in "${PROTOCOL_TYPES[@]}"; do
        if [[ "$BASENAME" == "$TYPE" ]]; then
            # Check if it exists in protocol
            if [[ -f "$PROJECT_ROOT/src/qyl.protocol/Primitives/$TYPE.cs" ]] || \
               [[ -f "$PROJECT_ROOT/src/qyl.protocol/Attributes/$TYPE.cs" ]] || \
               [[ -f "$PROJECT_ROOT/src/qyl.protocol/Models/$TYPE.cs" ]]; then
                echo "ARCHITECTURE VIOLATION: $TYPE.cs belongs in qyl.protocol!" >&2
                echo "" >&2
                echo "CLAUDE.md says: 'qyl.protocol = Shared contracts (LEAF)'" >&2
                echo "Use the existing type from qyl.protocol instead." >&2
                exit 2
            fi
        fi
    done
fi

# Rule 2: qyl.mcp must NOT have ProjectReference to qyl.collector
if [[ "$FILE_PATH" == *"/qyl.mcp/"* ]] && [[ "$FILE_PATH" == *".csproj" ]]; then
    CONTENT=$(echo "$TOOL_INPUT" | jq -r '.content // empty' 2>/dev/null)
    if [[ "$CONTENT" == *"qyl.collector"* ]] && [[ "$CONTENT" == *"ProjectReference"* ]]; then
        echo "ARCHITECTURE VIOLATION: qyl.mcp cannot reference qyl.collector!" >&2
        echo "" >&2
        echo "CLAUDE.md says: 'qyl.mcp communicates with qyl.collector via HTTP ONLY'" >&2
        exit 2
    fi
fi

# Rule 3: No Newtonsoft.Json
if [[ "$FILE_PATH" == *".cs" ]]; then
    CONTENT=$(echo "$TOOL_INPUT" | jq -r '.content // .new_string // empty' 2>/dev/null)
    if [[ "$CONTENT" == *"Newtonsoft.Json"* ]] || [[ "$CONTENT" == *"JsonConvert"* ]]; then
        echo "BANNED API: Newtonsoft.Json is banned!" >&2
        echo "" >&2
        echo "CLAUDE.md says: Use System.Text.Json instead." >&2
        exit 2
    fi
fi

# Rule 4: No DateTime.Now/UtcNow
if [[ "$FILE_PATH" == *".cs" ]]; then
    CONTENT=$(echo "$TOOL_INPUT" | jq -r '.content // .new_string // empty' 2>/dev/null)
    if [[ "$CONTENT" == *"DateTime.Now"* ]] || [[ "$CONTENT" == *"DateTime.UtcNow"* ]] || \
       [[ "$CONTENT" == *"DateTimeOffset.Now"* ]] || [[ "$CONTENT" == *"DateTimeOffset.UtcNow"* ]]; then
        echo "BANNED API: DateTime.Now/UtcNow is banned!" >&2
        echo "" >&2
        echo "CLAUDE.md says: Use TimeProvider.System.GetUtcNow() instead." >&2
        exit 2
    fi
fi

exit 0

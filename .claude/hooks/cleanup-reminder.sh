#!/bin/bash
# Stop/SubagentStop hook: Remind Claude to clean up unused code
# Outputs to stdout so Claude sees it

PROJECT_ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"

echo ""
echo "=== CLEANUP CHECKLIST ==="
echo ""
echo "Before marking task complete, verify:"
echo ""
echo "1. DELETE unused code:"
echo "   - Did you create a new file? Check for OLD versions to delete."
echo "   - Did you move functionality? Delete the source file."
echo "   - Did you rename? Delete the old-named file."
echo ""
echo "2. NO DUPLICATES:"
echo "   - SessionId, UnixNano, GenAiAttributes = ONLY in qyl.protocol"
echo "   - SpanRecord exists in 2 places (by design): protocol=DTO, collector=Storage"
echo ""
echo "3. SSOT CHECK:"
echo "   - DuckDB Schema: ONLY in qyl.collector/Storage/DuckDbSchema.cs"
echo "   - OTel Constants: ONLY in qyl.protocol/Attributes/GenAiAttributes.cs"
echo ""

# Check for potential orphans (files with TODO or FIXME that might be stale)
ORPHAN_CHECK=$(find "$PROJECT_ROOT/src" -name "*.cs" -exec grep -l "TODO.*delete\|TODO.*remove\|FIXME.*cleanup" {} \; 2>/dev/null | head -5 || true)
if [[ -n "$ORPHAN_CHECK" ]]; then
    echo "4. POTENTIAL ORPHANS FOUND:"
    echo "$ORPHAN_CHECK" | while read -r f; do
        echo "   - $f"
    done
    echo ""
fi

exit 0

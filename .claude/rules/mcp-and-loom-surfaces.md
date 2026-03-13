# mcp and loom surfaces

## Product role
qyl MCP should bring qyl context directly into LLM workflows.
loom should provide deep issue analysis and AI-powered root cause investigation.
They are complementary, not redundant.

## UI goals
qyl product surfaces should make these workflows first-class:
- error search
- issue investigation
- project context
- release management
- performance monitoring
- custom query access
- loom investigation and root-cause analysis

## Surface rules
MCP-facing and loom-facing UI must:
- reduce context-switching
- make entity scope obvious
- show provenance clearly
- separate generated analysis from raw telemetry facts
- avoid dead-end flows that trap the user inside a modal or panel

## Provenance rule
AI-generated investigation text must not look identical to source-of-truth telemetry data.
Users should be able to tell the difference between:
- raw event facts
- inferred analysis
- proposed actions

## Workflow rule
Treat loom as a first-class analysis workflow, not an afterthought bolted onto the side of issue pages.

## Product-density rule
Investigation surfaces should favor:
- timeline clarity
- entity linkage
- fast scanning
- side-by-side comparison when useful
- explicit navigation paths back to raw data

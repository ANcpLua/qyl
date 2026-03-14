# mcp and loom surfaces

## Product role
qyl MCP should bring qyl context directly into LLM workflows.
loom should provide deep issue analysis and AI-powered root cause investigation.
They are complementary, not redundant.

## Directory context
qyl is targeting the Anthropic Connectors Directory, which as of March 2026 lists 50+ verified connectors used across claude.ai, Claude Desktop, Claude Mobile, Claude Code, and Cowork. Sentry is the current observability connector. qyl would list alongside it in the Engineering/Observability category as a provider-agnostic, AI-native alternative. This means every tool surface exposed through `mcp.qyl.info` will be invoked by Claude across all these surfaces — not just by developers in a terminal. Design accordingly: tools must be self-describing, responses must be model-parseable, and provenance must be unambiguous even when a user never sees raw JSON. Some directory connectors qualify for an "Interactive" badge, allowing them to render live interfaces (dashboards, task boards, trace views) directly in conversations. Loom investigation surfaces and qyl dashboards should be designed with this capability in mind.

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

## MCP tool surface rules
Tools exposed via the remote MCP server must additionally:
- declare `readOnlyHint` and `destructiveHint` annotations (directory hard requirement)
- keep every response under 25,000 tokens (directory hard limit)
- paginate all list endpoints — never return unbounded results
- use tool descriptions that let a model select the right tool without user guidance
- return structured data that a model can synthesize into a natural-language answer, not pre-formatted markdown walls
- never leak internal IDs, connection strings, or infrastructure details in tool responses
- distinguish between "no results" and "error" — empty is not failure

## Provenance rule
AI-generated investigation text must not look identical to source-of-truth telemetry data.
Users should be able to tell the difference between:
- raw event facts
- inferred analysis
- proposed actions

This applies equally to dashboard UI and to MCP tool responses consumed by Claude. When a tool returns Loom analysis alongside raw telemetry, the response schema must structurally separate them (e.g., `facts` vs `analysis` fields), not interleave them in a single text blob.

## Workflow rule
Treat loom as a first-class analysis workflow, not an afterthought bolted onto the side of issue pages.

In the directory context, this means Loom tools must be independently invocable — a user should be able to ask Claude "investigate this error" and get a Loom analysis without first manually navigating to an issue page. The tool chain should handle entity resolution internally. If Loom investigation surfaces qualify for the Interactive badge, they could render live timeline views and causal graphs directly in the conversation.

## Product-density rule
Investigation surfaces should favor:
- timeline clarity
- entity linkage
- fast scanning
- side-by-side comparison when useful
- explicit navigation paths back to raw data

## MCP-specific density rule
For tool responses consumed by Claude (not rendered in a browser), favor:
- flat structured data over nested hierarchies
- explicit field names over positional conventions
- consistent entity ID formats across all tools (so Claude can chain tool calls)
- timestamps in ISO 8601 UTC — never relative ("5 minutes ago")

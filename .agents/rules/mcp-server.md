---
paths:
    - "src/qyl.mcp/**"
---

# MCP Server Rules

## Context

- OAuth and connector flow overview: `.claude/qyl-workflows/README.md#section-1-architektur-ueberblick`

## Architecture

- Supports both `stdio` and streamable HTTP (`/mcp`) transports.
- Consumed by AI agents (Claude, Copilot, desktop tools, remote MCP connectors).
- Communicates with collector via HTTP only (never ProjectReference).
- Uses `qyl.contracts` types where shared contracts are required.
- Uses `McpToolRegistry`, `UseQylTools`, and `RcaTools` for embedded meta-agent flows.

## Constraints

- Must not reference qyl.collector project directly
- All collector communication via HTTP REST API
- ModelContextProtocol SDK for transport layer
- `QYL_SKILLS` gates which tool families are registered at startup
- Do not reintroduce a parallel investigation/proxy stack; broad queries should go through `qyl.use_qyl` or focused
  agent tools over the same DI-resolved tool set

## Anthropic MCP Directory Target

qyl targets listing in the Anthropic Connectors Directory (claude.ai Settings → Connectors, also browsable at
claude.com/connectors). As of March 2026, the directory has 50+ verified connectors across categories including
engineering, financial services, sales/marketing, life sciences, and data. Sentry is the current observability
incumbent. qyl would list in the **Engineering / Observability** category as a provider-agnostic, AI-native alternative.

Directory connectors work across **all Claude surfaces**: claude.ai, Claude Desktop, Claude Mobile, Claude Code, and
Cowork. Directory listing is available to users on all plans including Free. This is not a niche integration — it's the
primary way non-developer users discover and connect tools to Claude.

The remote MCP server at `mcp.qyl.info` (Railway) is the submission artifact. Every tool, response shape, and auth flow
in `src/qyl.mcp/` must comply with the directory requirements below.

### Directory compliance rules

These are non-negotiable for directory acceptance:

1. **OAuth 2.1 with user consent flow.** Pure client-credentials (machine-to-machine) is rejected. Users must complete
   an interactive OAuth consent flow. Dynamic Client Registration (DCR) is required. If the directory returns
   `invalid_client`, regenerate DCR credentials per RFC 6749 §5.2.

2. **Tool annotations on every tool.** Every MCP tool must declare `readOnlyHint` and `destructiveHint`. This is the top
   rejection reason in directory review. Audit all tools in `McpToolRegistry` and `RcaTools` before submission.

3. **25,000 token response limit.** No single tool response may exceed 25,000 tokens. Use pagination, filtering, or
   limit parameters to constrain response size. This applies to trace dumps, log queries, event listings, and any bulk
   data retrieval.

4. **No IP/user metadata forwarding.** Anthropic does not forward IP addresses, user IDs, or other metadata from
   end-users. OAuth is the only way to identify users. Design access control accordingly.

5. **Rate limiting is our responsibility.** Anthropic does not rate-limit MCP tool calls on our behalf. Implement rate
   limiting and abuse prevention in the server.

6. **Testing account required.** The submission form requires a testing account with dummy data for Anthropic QA.
   Prepare a seeded instance with realistic demo data (use the Bogus-generated DuckDB fixtures).

7. **All Claude surfaces supported.** Directory connectors work across claude.ai, Claude Desktop, Claude Mobile, Claude
   Code, and Cowork. The `stdio` transport remains available as an alternative local path for Claude Code users who
   prefer direct connections without OAuth, but it is no longer the only Claude Code path.

8. **Category and Interactive badge.** The directory is organized by category. qyl should target the
   Engineering/Observability category. If any tools render live UI (dashboards, trace views), consider qualifying for
   the "Interactive" badge — interactive connectors can render live interfaces directly in conversations.

### Submission

- Directory: https://claude.com/connectors (browse existing connectors)
- Directory docs: https://claude.com/docs/connectors/directory
- Form: https://docs.google.com/forms/d/e/1FAIpQLSeafJF2NDI7oYx1r8o0ycivCSVLNq92Mpc1FPxMKSw1CzDkqA/viewform
- Terms: https://support.claude.com/en/articles/11697081-anthropic-mcp-directory-terms-and-conditions
- Policy: https://support.claude.com/en/articles/11697096-anthropic-mcp-directory-policy
- FAQ: https://support.claude.com/en/articles/11596036-anthropic-connectors-directory-faq
- Help: https://support.claude.com/en/articles/11724452-use-the-connectors-directory-to-extend-claude-s-capabilities

### Pre-submission checklist

Before submitting to the directory, verify:

- [ ] OAuth 2.1 + DCR flow works end-to-end with a fresh browser session
- [ ] Every tool has `readOnlyHint` / `destructiveHint` annotations
- [ ] No tool response exceeds 25,000 tokens under realistic load
- [ ] Pagination is implemented for all list/query endpoints
- [ ] Rate limiting is active on the remote server
- [ ] Testing account exists with seeded demo data
- [ ] `mcp.qyl.info` is reachable and TLS-valid from Anthropic egress IPs
- [ ] Tool descriptions are clear enough for a model to select them correctly without human guidance
- [ ] Category determined (Engineering/Observability) and described in submission form
- [ ] Interactive badge eligibility assessed — if any tools render live UI, prepare interactive demo

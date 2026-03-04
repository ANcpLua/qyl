# Unified Seer Framework Specification

Version: 2.0  
Date: 2026-03-04  
Owner: Engineering Lead / PM

This document is the single authoritative framework specification for Seer-inspired capabilities in this repository.  
It merges:
- the Seer capability baseline from [Seer Design Specification](./seer-design-specification.md)
- implementation reality and engineering actions from prior traceability analysis

---

## 1. Purpose

Build and maintain a production-grade, Seer-inspired enterprise agent framework that is:
1. deterministic in behavior,
2. auditable,
3. secure by default,
4. deployable by any engineer without hidden assumptions.

Success criteria:
1. every capability has an explicit requirement ID,
2. every requirement has implementation anchors,
3. every requirement has acceptance criteria,
4. no requirement depends on undocumented tribal knowledge.

---

## 2. System Definition

### 2.1 Runtime Topology

The framework consists of:
1. **Orchestrator Agent Host** (`EnterpriseSupportAgent`) for routing and response composition.
2. **Connected Agents** (`TicketAgent`, `KnowledgeAgent`, `NotifyAgent`) for domain workflows.
3. **MCP Integration Layer** for external tool execution (observability and ticket tools).
4. **MCP Ticket Server** (`SupportMcpServer`) for ticket CRUD tool operations.
5. **Adaptive Card Rendering Layer** for all structured user-facing responses.
6. **Security Layer** for token validation and request authorization.
7. **Observability Layer** (OpenTelemetry + Azure Monitor in production).

### 2.2 Canonical Implementation Anchors

Primary anchors:
1. [Program.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Program.cs)
2. [Bot.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs)
3. [TicketAgent.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/TicketAgent.cs)
4. [KnowledgeAgent.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs)
5. [NotifyAgent.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/NotifyAgent.cs)
6. [McpObservabilityService.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Services/McpObservabilityService.cs)
7. [AdaptiveCardHelper.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AdaptiveCards/AdaptiveCardHelper.cs)
8. [AspNetExtensions.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AspNetExtensions.cs)
9. [ConversationStateExtensions.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/ConversationStateExtensions.cs)
10. [SupportMcpServer Program.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/SupportMcpServer/Program.cs)
11. [SupportTicketTools.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/SupportMcpServer/Tools/SupportTicketTools.cs)
12. [InMemoryTicketStore.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/SupportMcpServer/Models/InMemoryTicketStore.cs)

---

## 3. Functional Requirements (Normative)

## 3.1 Orchestrator and Routing

`FR-001` Message intake  
The system SHALL accept conversational traffic on `POST /api/messages` and process via Agent adapter pipeline.  
Anchor: [Program.cs:85](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Program.cs:85)

`FR-002` Intent routing contract  
The orchestrator SHALL classify every user message into exactly one route: `ticket`, `knowledge`, `notify`.  
Anchor: [Bot.cs:147](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:147)

`FR-003` Fallback behavior  
If classification output is invalid or empty, the orchestrator SHALL route to `KnowledgeAgent`.  
Anchor: [Bot.cs:63](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:63)

`FR-004` Streaming UX  
The orchestrator SHALL emit informative streaming updates before final response delivery.  
Anchor: [Bot.cs:46](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:46)

## 3.2 Ticket Operations

`FR-010` Ticket intent sub-classification  
`TicketAgent` SHALL classify sub-intent into `create`, `update`, `get`, `list`.  
Anchor: [TicketAgent.cs:38](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/TicketAgent.cs:38)

`FR-011` Create ticket extraction  
`TicketAgent` SHALL extract `TITLE`, `DESCRIPTION`, and `PRIORITY` from user input with deterministic fallback values.  
Anchor: [TicketAgent.cs:63](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/TicketAgent.cs:63)

`FR-012` Update ticket extraction  
`TicketAgent` SHALL extract `TICKET_ID`, `STATUS`, `PRIORITY`, and `ASSIGNEE` for update requests.  
Anchor: [TicketAgent.cs:112](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/TicketAgent.cs:112)

`FR-013` Ticket response cards  
All ticket operation responses SHALL include an Adaptive Card with status, key facts, and actionable context.  
Anchor: [TicketAgent.cs:94](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/TicketAgent.cs:94)

`FR-014` MCP ticket tool host  
The framework SHALL expose MCP tools for ticket lifecycle operations using stdio transport.  
Anchor: [SupportMcpServer Program.cs:15](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/SupportMcpServer/Program.cs:15)

`FR-015` Ticket tool contract  
The MCP ticket server SHALL provide `CreateTicket`, `UpdateTicket`, and `GetTicket` with strongly defined argument names and return formatting.  
Anchor: [SupportTicketTools.cs:9](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/SupportMcpServer/Tools/SupportTicketTools.cs:9)

`FR-016` Ticket persistence abstraction  
Ticket data access SHALL pass through `ITicketStore`; storage backend MAY be swapped without tool contract changes.  
Anchor: [ITicketStore.cs:3](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/SupportMcpServer/Models/ITicketStore.cs:3)

## 3.3 Knowledge and Observability

`FR-020` Knowledge retrieval  
`KnowledgeAgent` SHALL answer knowledge requests from embedded markdown resources with source-aware excerpts.  
Anchor: [KnowledgeAgent.cs:173](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:173)

`FR-021` Citation behavior  
Knowledge responses SHALL include source citations in the answer and card facts.  
Anchor: [KnowledgeAgent.cs:183](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:183)

`FR-022` Observability query detection  
`KnowledgeAgent` SHALL route observability-related prompts to MCP tools when observability terms are detected.  
Anchor: [KnowledgeAgent.cs:57](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:57)

`FR-023` MCP tool selection flow  
For observability requests, the flow SHALL execute:
1. list tools,
2. select tool+arguments via model,
3. call selected tool,
4. synthesize answer,
5. return result card with tool/source attribution.

Anchors:
1. [KnowledgeAgent.cs:65](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:65)
2. [KnowledgeAgent.cs:95](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:95)
3. [KnowledgeAgent.cs:118](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:118)

`FR-024` MCP unavailability fallback  
If MCP tool listing or calls fail, the agent SHALL fall back to embedded knowledge mode without throwing to user.  
Anchor: [KnowledgeAgent.cs:49](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:49)

## 3.4 Notifications and Human Approval

`FR-030` Notification extraction  
`NotifyAgent` SHALL extract `TITLE`, `DESCRIPTION`, `RECIPIENTS`, and `URGENCY`.  
Anchor: [NotifyAgent.cs:18](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/NotifyAgent.cs:18)

`FR-031` Confirmation requirement  
Notification sends SHALL use confirmation cards and require explicit user action before final commitment.  
Anchor: [NotifyAgent.cs:47](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/NotifyAgent.cs:47)

`FR-032` Invoke action handling  
Invoke payload actions (`approve`, `reject`, `escalateTicket`, etc.) SHALL produce deterministic follow-up messages.  
Anchor: [Bot.cs:95](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:95)

## 3.5 Adaptive Cards and Response Contracts

`FR-040` Result card contract  
`ResultCard` responses SHALL include:
1. title,
2. subtitle,
3. status,
4. statusColor,
5. summary,
6. facts array,
7. optional action metadata.

Anchor: [AdaptiveCardHelper.cs:18](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AdaptiveCards/AdaptiveCardHelper.cs:18)

`FR-041` Confirmation card contract  
`ConfirmationCard` SHALL include request identity and sufficient approval context.  
Anchor: [AdaptiveCardHelper.cs:80](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AdaptiveCards/AdaptiveCardHelper.cs:80)

`FR-042` Template binding behavior  
Card template placeholders SHALL be resolved via deterministic key and array expression binding.  
Anchor: [AdaptiveCardHelper.cs:120](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AdaptiveCards/AdaptiveCardHelper.cs:120)

## 3.6 Security, Auth, and State

`FR-050` Token validation  
The API host SHALL validate JWTs with issuer and audience checks configured via `TokenValidation` settings.  
Anchor: [AspNetExtensions.cs:54](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AspNetExtensions.cs:54)

`FR-051` Conversation memory  
Conversation history SHALL be stored and retrievable by role/content entries.  
Anchor: [ConversationStateExtensions.cs:14](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/ConversationStateExtensions.cs:14)

`FR-052` User profile cache  
User profile SHALL be cached in conversation state to avoid repeated Graph calls.  
Anchor: [ConversationStateExtensions.cs:48](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/ConversationStateExtensions.cs:48)

`FR-053` Reset behavior  
`-reset` command SHALL sign out user and clear cached conversation/profile state.  
Anchor: [Bot.cs:138](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:138)

---

## 4. Seer Capability Alignment Requirements

This section defines Seer-inspired capability coverage as explicit implementation requirements.

`SR-001` Issue summarization equivalent  
The system SHALL provide LLM-generated summaries for knowledge and observability responses, including key findings and source attribution.  
Anchors: [KnowledgeAgent.cs:99](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:99), [KnowledgeAgent.cs:173](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:173)

`SR-002` Assisted-query equivalent  
The system SHALL support natural-language tool invocation by translating user intent into MCP tool calls with arguments.  
Anchors: [KnowledgeAgent.cs:70](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:70), [McpObservabilityService.cs:40](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Services/McpObservabilityService.cs:40)

`SR-003` Explorer equivalent  
The orchestrator SHALL provide interactive investigation via connected agents with streaming updates and adaptive-card outputs.  
Anchors: [Bot.cs:46](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:46), [Bot.cs:61](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:61)

`SR-004` Trace insight equivalent  
Observability tool outputs SHALL be transformed into human-readable summaries with anomalies/errors highlighted when present in tool output.  
Anchor: [KnowledgeAgent.cs:103](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Agents/KnowledgeAgent.cs:103)

`SR-005` Security baseline equivalent  
All external calls SHALL occur under explicit auth and validated configuration; user-facing failures SHALL not leak raw exception details.  
Anchors: [AspNetExtensions.cs:54](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AspNetExtensions.cs:54), [McpObservabilityService.cs:72](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Services/McpObservabilityService.cs:72)

---

## 5. Configuration Specification

### 5.1 Required Variables

1. `BOT_ID`
2. `MICROSOFT_APP_ID`
3. `MICROSOFT_APP_PASSWORD`
4. `MICROSOFT_APP_TENANT_ID`
5. `LanguageModel:Name`
6. `LanguageModel:Endpoint`
7. `LanguageModel:ApiKey`

Anchors:
- [.env.example](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/.env.example#L8)
- [ChatClientFactory.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Services/ChatClientFactory.cs:21)

### 5.2 Optional Variables

1. `McpObservability:Endpoint` (default `https://mcp.qyl.info/sse`)
2. `BlobsStorageOptions:ContainerName`
3. `BlobsStorageOptions:StorageAccountName`

Anchor: [Program.cs:57](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Program.cs:57)

---

## 6. Non-Functional Requirements

`NFR-001` Reliability  
All MCP integration failures SHALL be handled gracefully with fallback behavior and warning-level logs.  
Anchor: [McpObservabilityService.cs:72](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Services/McpObservabilityService.cs:72)

`NFR-002` Observability  
Production deployments SHALL emit OpenTelemetry traces for HTTP client operations and custom MCP activities.  
Anchors:
- [Program.cs:20](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Program.cs:20)
- [Track 1 McpActivitySource.cs](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-1-creative-apps/src/CodeIntelligenceMcp/Instrumentation/McpActivitySource.cs:3)

`NFR-003` Security hygiene  
No real credentials SHALL be committed; `.env.example` SHALL contain placeholders only.  
Anchor: [.env.example:4](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/.env.example:4)

`NFR-004` UX consistency  
Structured responses SHALL use adaptive cards; plain-text-only responses are allowed only on explicit fallback/error pathways.  
Anchors: [Bot.cs:75](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/Bot.cs:75), [AdaptiveCardHelper.cs:103](/Users/ancplua/RiderProjects/agentsleague-starter-kits/track-3-enterprise-agents/src/EnterpriseSupportAgent/AdaptiveCards/AdaptiveCardHelper.cs:103)

`NFR-005` UI framework forward compatibility  
If a web UI layer is introduced, style tokens SHALL be decoupled from framework-specific utility class assumptions.  
Implementation rule:
1. define design tokens first,
2. map tokens to chosen CSS framework version,
3. lock framework version explicitly,
4. run visual regression tests before major upgrades.

Rationale: preserves appearance while preventing framework version lock-in regressions (e.g., Tailwind major-version migration issues).

---

## 7. Acceptance Criteria (Release Gate)

A release is accepted only when all are true:
1. `FR-001` to `FR-053` pass integration tests.
2. `SR-001` to `SR-005` pass scenario tests.
3. Auth validation rejects invalid issuer/audience tokens.
4. MCP endpoint outage test proves graceful fallback response.
5. Adaptive card schemas render successfully in Copilot/Teams channels.
6. Configuration validation fails fast when required model settings are missing.
7. No secrets present in repository history for changed files.

---

## 8. Test Specification

### 8.1 Required Test Suites

1. Orchestrator routing tests (`ticket|knowledge|notify` + fallback behavior).
2. TicketAgent parsing tests (create/update field extraction).
3. KnowledgeAgent tool-call parsing tests (JSON and markdown-fenced JSON handling).
4. MCP observability connection and failure-path tests.
5. Adaptive card placeholder binding tests including array indexing (`facts[0].key`).
6. Auth middleware tests for issuer/audience enforcement.
7. Conversation state serialization/deserialization tests.

### 8.2 Scenario Tests

1. “Create high priority ticket” end-to-end returns ticket card with actionable verbs.
2. “Show open traces for service X” invokes MCP path and returns tool-attributed insight card.
3. “Notify team about outage” requires confirmation flow before final action.
4. `-reset` clears state and signs out user.

---

## 9. Delivery Plan (Executable)

### Milestone M1 — Framework Hardening

Deliverables:
1. Add feature-flag framework and default flags for routing, observability tools, and notifications.
2. Add startup configuration validation with explicit error messages.
3. Add health endpoint(s) for runtime and MCP connectivity status.

Exit criteria:
1. all M1 tests green,
2. no runtime null-config failures,
3. feature flags can disable each connected agent path without deployment.

### Milestone M2 — Data and Tooling Reliability

Deliverables:
1. replace `InMemoryTicketStore` with persistent backing store abstraction implementation,
2. add `ListTickets` MCP tool,
3. add structured audit log entries for tool calls and approvals.

Exit criteria:
1. ticket state survives process restart,
2. list operation available to bot workflows,
3. audit records generated for each mutable action.

### Milestone M3 — Seer-Style Advanced Workflows

Deliverables:
1. explicit RCA session object and timeline events,
2. trace-summary schema contract,
3. anomaly-insight contract over MCP tool outputs,
4. provider abstraction extension point for additional LLM backends.

Exit criteria:
1. RCA workflow reproducible and inspectable,
2. trace summaries deterministic in schema,
3. anomaly messages include severity classification,
4. provider swap requires no orchestrator changes.

---

## 10. Seer Source Traceability Map

Each Seer specification domain maps to this framework as follows:

1. **System Overview / Explorer / Assisted Query** -> `FR-001..FR-004`, `FR-020..FR-024`, `SR-003`
2. **Issue Summarization / Trace Summarization** -> `FR-020`, `FR-023`, `SR-001`, `SR-004`
3. **MCP Server / Integration Layer** -> `FR-014..FR-016`, `FR-022..FR-024`, `SR-002`
4. **Data Privacy & Security** -> `FR-050..FR-053`, `NFR-003`, `SR-005`
5. **Configuration & Operations** -> Section 5, `NFR-001..NFR-005`, Section 9

Source baseline: [Seer Design Specification](./seer-design-specification.md)

---

## 11. Definition of Done

The framework is considered complete for a release when:
1. all release gates in Section 7 are satisfied,
2. milestones planned for that release are fully met,
3. documentation and code anchors remain synchronized,
4. operational runbooks are updated for new config flags and failure modes.


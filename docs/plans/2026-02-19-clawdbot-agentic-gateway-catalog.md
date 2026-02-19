# ClawDBot Agentic Gateway Catalog

> Baseline catalog to move from a toy bot to a production-grade "grown-up toy"

## Goal

Define a concrete feature catalog for ClawDBot that supports:

- Remote control via gateway service
- Multi-channel operation (Slack, Telegram, etc.)
- Agentic behavior with safe tool execution
- Session-aware routing and security policies
- Production observability and reliability

This document is intentionally implementation-oriented. Every feature has an ID and a clear delivery phase.

## Reference Inputs

- Microsoft Agent Framework (.NET): <https://github.com/microsoft/agent-framework/tree/main/dotnet>
- Microsoft Agents A2A extensions reference:
  <https://learn.microsoft.com/de-de/dotnet/api/microsoft.agents.ai.hosting.a2a.aiagentextensions?view=agent-framework-dotnet-latest>
- Operational direction from project notes: gateway core, channel adapters, session isolation, tool policies, security-first DM pairing, observability-first, cron as separate worker.

## Design Principles

1. One gateway process owns orchestration.
2. Channel adapters are pluggable and normalized.
3. Agent execution is stateless per request; state lives in storage.
4. Security defaults to deny and explicit opt-in.
5. Tools are policy-guarded and auditable.
6. Cron and scheduler logic is a separate service, not embedded in gateway v1.
7. Observability is first-class from day one.

## Maturity Model

| Level | Meaning | Quality Bar |
|---|---|---|
| Toy | Single process, implicit behavior | Works locally, weak safety and ops |
| Grown-up Toy | Structured architecture with guardrails | Multi-channel, policy-driven, observable |
| Production | Strong reliability and operational controls | Scalable, auditable, recoverable |

Target for initial build: **Grown-up Toy**.  
Target for follow-up hardening: **Production**.

## Feature Catalog

### A. Gateway Core

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-001` | Long-running gateway service | Single process owns sessions, routing, tool/model orchestration |
| `CLAW-002` | Unified inbound event contract | All inbound messages normalized to one internal event schema |
| `CLAW-003` | Dual control interfaces | HTTP + WebSocket APIs for control, status, and streaming |
| `CLAW-004` | Idempotent event handling | Event IDs with dedupe to avoid duplicate processing |
| `CLAW-005` | Backpressure and queue control | Queue depth limits and overload behavior |

### B. Channel Adapter Layer

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-010` | Adapter abstraction | One adapter contract for each channel integration |
| `CLAW-011` | Inbound normalization | Channel-specific payloads mapped to internal event schema |
| `CLAW-012` | Unified outbound send API | `sendMessage(session, target, text, media)` contract |
| `CLAW-013` | Mention/thread mapping | Correct mention-required and thread/reply behavior per channel |
| `CLAW-014` | Reconnect and health | Adapter reconnection strategy and per-adapter health checks |

### C. Agent Runtime

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-020` | Stateless execution loop | Per-request agent loop with state externalized |
| `CLAW-021` | Session store abstraction | Session state in dedicated store (not process memory only) |
| `CLAW-022` | Model abstraction layer | Provider-agnostic model API |
| `CLAW-023` | Failover and profile rotation | Provider/model fallback profiles with retry policy |
| `CLAW-024` | Behavior metadata | Thinking/verbosity as metadata flags, not hardcoded behavior |
| `CLAW-025` | Streaming responses | Token/event streaming support from runtime to channels |

### D. Session and Routing

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-030` | Session isolation | Separate owner DM session from group/channel sessions |
| `CLAW-031` | Routing rule engine | Rules by channel, sender, group, mention-required |
| `CLAW-032` | Deterministic routing order | Explicit precedence for rule evaluation |
| `CLAW-033` | Unroutable handling | Dead-letter queue and diagnostics for dropped events |
| `CLAW-034` | Replay/debug mode | Re-run captured events for deterministic debugging |

### E. Tool Runtime (Safeguarded)

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-040` | Capability registry | Tools registered with metadata and permissions |
| `CLAW-041` | Per-session tool policy | Allowed tools, sandbox mode, rate limits per session |
| `CLAW-042` | Execution guards | Timeouts, cancellation, retry limits |
| `CLAW-043` | Tool invocation audit | Input/output summaries + duration + status |
| `CLAW-044` | Human approval hooks | Approval flow for sensitive/destructive tools |
| `CLAW-045` | Tool streaming | Partial output streaming when tool supports it |

### F. Security Defaults

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-050` | Default deny inbound DM | Unknown DM senders blocked by default |
| `CLAW-051` | Pairing code onboarding | Approved pairing flow before DM access |
| `CLAW-052` | Sender/channel model allowlist | Model profiles restricted by sender/channel/group |
| `CLAW-053` | Explicit open mode | Public DM mode requires explicit opt-in and visible warning |
| `CLAW-054` | Secrets boundary | Secret loading isolated from channel payload surface |
| `CLAW-055` | Abuse controls | Rate limits, cooldowns, and lockouts |

### G. Observability and Diagnostics

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-060` | Structured event logs | Every lifecycle step logged with correlation IDs |
| `CLAW-061` | End-to-end traces | Request lifecycle spans including model and tool spans |
| `CLAW-062` | Operational metrics | Latency, token use, failures, queue depth, throughput |
| `CLAW-063` | Doctor/health diagnostics | Actionable health command and readiness checks |
| `CLAW-064` | Incident integration | Sentry and Aspire wiring for prod diagnostics |
| `CLAW-065` | Audit export | Exportable audit trail for investigation/compliance |

### H. Memory and Agentic Enrichment

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-070` | Memory scopes | User/project/local scope separation |
| `CLAW-071` | Memory classes | Episodic/semantic/procedural distinction |
| `CLAW-072` | Hook-based enrichment | Trigger memory enrichment on idle/task-complete events |
| `CLAW-073` | Retrieval + compaction loop | Summarize and retrieve context between sessions |
| `CLAW-074` | Retention controls | TTL, purge, and user-initiated reset |

### I. Scheduler (Separate Service, Last Phase)

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-080` | Dedicated scheduler worker | Separate process for cron/timers |
| `CLAW-081` | Command bus integration | Scheduler emits commands/events to gateway |
| `CLAW-082` | Retry + idempotency | Idempotency keys and dead-letter handling |
| `CLAW-083` | Check-in monitoring | Heartbeats/check-ins for scheduled task health |
| `CLAW-084` | Timezone-aware schedules | Explicit timezone semantics for cron schedules |

### J. Reliability and Operations

| ID | Capability | Baseline Requirement |
|---|---|---|
| `CLAW-090` | Durable backing store | Persist sessions/events/policies outside runtime memory |
| `CLAW-091` | Horizontal scale safety | Locking/coordination for multi-instance runtime |
| `CLAW-092` | Config profiles | Environment-specific and tenant-specific policy profiles |
| `CLAW-093` | Versioned contracts | Event and policy schema versioning/migration |
| `CLAW-094` | Backup and recovery | Restore path for stateful data |
| `CLAW-095` | Failure drills | Chaos/soak testing and incident runbooks |

## Delivery Phases

### Phase 0: Foundation (minimum non-toy baseline)

Required IDs:

- `CLAW-001` `CLAW-002` `CLAW-003`
- `CLAW-020` `CLAW-021` `CLAW-022`
- `CLAW-030` `CLAW-031`
- `CLAW-040` `CLAW-041`
- `CLAW-050` `CLAW-051`
- `CLAW-060` `CLAW-063`

Exit criteria:

- One gateway can route inbound events from one channel adapter to one agent runtime
- Per-session tool policy is enforced
- Unknown DMs are denied unless paired
- Health command can diagnose broken dependencies

### Phase 1: Grown-up Toy (recommended first public milestone)

Required IDs:

- `CLAW-004` `CLAW-005`
- `CLAW-010` `CLAW-011` `CLAW-012` `CLAW-013` `CLAW-014`
- `CLAW-023` `CLAW-024` `CLAW-025`
- `CLAW-032` `CLAW-033`
- `CLAW-042` `CLAW-043` `CLAW-045`
- `CLAW-052` `CLAW-053` `CLAW-055`
- `CLAW-061` `CLAW-062` `CLAW-064`

Exit criteria:

- At least two channels run through identical gateway contracts
- Model failover/profile rotation works under provider outage
- Full request traces include tool and model spans
- Queue and overload behavior is observable and controlled

### Phase 2: Production Hardening

Required IDs:

- `CLAW-034` `CLAW-044`
- `CLAW-054` `CLAW-065`
- `CLAW-090` `CLAW-091` `CLAW-092` `CLAW-093` `CLAW-094` `CLAW-095`

Exit criteria:

- Replay/debug flow supports incident analysis
- Sensitive actions can be approval-gated
- Multi-instance deployment is safe under contention
- Disaster recovery path is tested

### Phase 3: Memory and Scheduler Expansion

Required IDs:

- `CLAW-070` `CLAW-071` `CLAW-072` `CLAW-073` `CLAW-074`
- `CLAW-080` `CLAW-081` `CLAW-082` `CLAW-083` `CLAW-084`

Exit criteria:

- Memory scopes and retention are enforceable
- Scheduler service operates independently from gateway runtime
- Scheduled tasks are observable with check-ins and idempotent execution

## Suggested Runtime Shape

```text
Channels -> Adapter(s) -> Gateway Core -> Routing Engine -> Agent Runtime
                                          |               |
                                          |               +-> Model Abstraction (failover profiles)
                                          |
                                          +-> Tool Runtime (policy + audit)
                                          |
                                          +-> Session Store / Policy Store
                                          |
                                          +-> Observability (logs/traces/metrics)

Scheduler Worker (separate) -> Gateway Command API/Event Bus
```

## Mapping to Microsoft Agent Framework Concepts

Use this as a practical alignment guide, not a strict dependency lock:

- Agent runtime loop (`CLAW-020..025`) maps to agent execution primitives and extension points.
- Session/routing (`CLAW-030..034`) complements agent-to-agent or host-level routing behavior.
- Tool safeguards (`CLAW-040..045`) wrap tool execution with policy and audit around agent calls.
- Observability (`CLAW-060..065`) aligns with OpenTelemetry and Aspire-style diagnostics.

## Anti-Toy Guardrails (must not regress)

1. No channel-specific business logic inside gateway core.
2. No hardcoded model behavior by prompt text.
3. No unrestricted tool execution in any shared/public session.
4. No public DM mode without explicit opt-in.
5. No hidden background timers in gateway core for cron.
6. No "works locally only" observability gaps.

## Next Implementation Artifact

Once this catalog is accepted, create an execution plan that maps each ID to:

- Concrete project/file touch points
- Test coverage expectations
- Operational runbook additions
- Rollout and rollback criteria

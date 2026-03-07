# Intelligence — Requirements

Extracted from [loom-design.md §22](../roadmap/loom-design.md#22-requirements-registry).

## qyl Requirements

| ID       | Capability              | Domain     | Scope                | Evidence                                         | Verification                                    |
|----------|-------------------------|------------|----------------------|--------------------------------------------------|-------------------------------------------------|
| QYL-004  | AI Chat Analytics       | Analytics  | `IMPLEMENTED-IN-QYL` | 6 modules, 9 API endpoints, 10 uncertainty signals | API returns real data, not stubs               |
| QYL-012  | AI Chat Extended (Ph 5) | Analytics  | `CONTEXT-ONLY`       | Semantic clustering, token economics             | Roadmap — not ship-blocking                     |
| QYL-015a | Autofix Orchestration   | Autofix    | `IMPLEMENTED-IN-QYL` | AutofixOrchestrator.cs                           | Full issue → RCA → solution → code change flow |
| QYL-015b | Triage Pipeline         | Triage     | `IMPLEMENTED-IN-QYL` | TriagePipelineService.cs                         | Score real issues, verify threshold gating      |
| QYL-015c | Code Review Service     | CodeReview | `IMPLEMENTED-IN-QYL` | CodeReviewService.cs                             | GitHub webhook → analysis → PR comment          |

## Sentry Reference Requirements (CONTEXT-ONLY)

| ID       | Capability                    | Domain        | Scope             |
|----------|-------------------------------|---------------|--------------------|
| LOOM-001 | Issue Grouping & Similarity   | Grouping      | `CONTEXT-ONLY`    |
| LOOM-002 | Issue Summarization           | Summarization | `CONTEXT-ONLY`    |
| LOOM-003 | Fixability Scoring            | Triage        | `CONTEXT-ONLY`    |
| LOOM-004 | Root Cause Analysis           | Autofix       | `CONTEXT-ONLY`    |
| LOOM-005 | Autofix Pipeline              | Autofix       | `CONTEXT-ONLY`    |
| LOOM-006 | AI Code Review (Prevent)      | CodeReview    | `CONTEXT-ONLY`    |
| LOOM-007 | Explorer (Interactive Agent)  | Explorer      | `CONTEXT-ONLY`    |
| LOOM-008 | Anomaly Detection             | Anomaly       | `CONTEXT-ONLY`    |
| LOOM-009 | Trace Summarization           | Summarization | `CONTEXT-ONLY`    |
| LOOM-010 | Assisted Query                | Search        | `CONTEXT-ONLY`    |
| LOOM-011 | Test Generation               | Testing       | `CONTEXT-ONLY`    |
| SEER-001 | Autofix 5-Step LLM Chain      | Autofix       | `EXTERNAL-CLOSED` |
| SEER-002 | Explorer Agent (10 tools)     | Explorer      | `EXTERNAL-CLOSED` |
| SEER-003 | Code Review Bug Prediction    | CodeReview    | `EXTERNAL-CLOSED` |
| SEER-004 | Anomaly Detection (Prophet)   | Anomaly       | `EXTERNAL-CLOSED` |
| SEER-005 | Issue Similarity Embeddings   | Grouping      | `EXTERNAL-CLOSED` |
| SEER-006 | Breakpoint Detection          | Anomaly       | `EXTERNAL-CLOSED` |

## Acceptance Criteria (P0)

- [ ] Autofix: issue → root cause → solution → code change E2E with real input
- [ ] Triage: fixability scoring produces valid 5-tier scores on real issues
- [ ] Code Review: GitHub webhook → PR analysis → comment output
- [ ] Agent Handoff: lifecycle transitions verified (active → paused → completed)
- [ ] Regression Detection: real time-series input → detection → storage → query

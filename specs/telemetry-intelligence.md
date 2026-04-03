# Telemetry Intelligence Model

> Owner: contracts (types), loom (engine)
> SSOT: YES (diagnostic patterns, causal rules, investigation strategies)
> Depends on: `telemetry-data-model.md` (schema), `issue-fingerprinting.md` (error grouping), `contracts.md` (generated types)
> Used by: `src/qyl.loom/specs/loom.md` (pattern engine), `mcp.md` (intelligence tools)

Canonical reasoning model over telemetry data. Schema-driven, generated, deterministic. The missing layer between storage and investigation.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Core Types](#2-core-types)
3. [Source of Truth](#3-source-of-truth)
4. [Code Generation](#4-code-generation)
5. [Seed Patterns](#5-seed-patterns)
6. [Runtime Consumption](#6-runtime-consumption)
7. [MCP Intelligence Surface](#7-mcp-intelligence-surface)
8. [Constraints](#8-constraints)
9. [Relationship to Other Specs](#9-relationship-to-other-specs)
10. [Definition of Done](#10-definition-of-done)

---

## 1. Overview

### 1.1 The Gap

qyl has four deterministic layers:

| Layer | Spec | What it does |
|-------|------|-------------|
| Emit | `instrumentation.md` | Compile-time OTel via Roslyn generators |
| Store | `telemetry-data-model.md` | Promoted columns, DuckDB schema |
| Group | `issue-fingerprinting.md` | SHA256 + normalization → stable fingerprints |
| Query | `mcp.md` | Typed MCP tools |

Investigation (Loom) is currently prompt-driven — the LLM is the **only** intelligence layer. It interprets telemetry heuristically. You cannot guarantee the same reasoning path across runs. You cannot test it deterministically. You cannot compose it.

### 1.2 The Solution

A **typed intelligence model** that defines diagnostic patterns, causal rules, and investigation strategies as data. Generated from TypeSpec. Consumed by Loom, MCP, and dashboard.

```text
Telemetry
   ↓
Pattern Engine (deterministic)
   ↓
Candidate root causes
   ↓
LLM reasoning (guided, not freeform)
   ↓
Solution planning
```

The LLM stops being the only intelligence layer. It becomes step 2 of a structured system.

### 1.3 Generative Pipeline

Follows the same pattern as every other qyl schema:

```text
core/specs/intelligence/*.tsp
        ↓
    tsp compile
        ↓
src/qyl.contracts/Intelligence/  (C# types + registries)
        ↓
Loom pattern engine + MCP tools + docs
```

---

## 2. Core Types

Four types. Three concepts. One primitive.

### 2.1 Signal (primitive)

The atomic unit of telemetry observation. A single attribute condition.

| Field | Type | Description |
|-------|------|-------------|
| `attribute` | `string` | Telemetry attribute name (semconv or promoted column) |
| `operator` | `SignalOperator` | Comparison operator |
| `value` | `string` | Expected value (type-coerced at evaluation time) |

Operators:

| Operator | Meaning |
|----------|---------|
| `eq` | Equals |
| `neq` | Not equals |
| `gt` | Greater than |
| `gte` | Greater than or equal |
| `lt` | Less than |
| `lte` | Less than or equal |
| `contains` | String contains |
| `exists` | Attribute is non-null |
| `not_exists` | Attribute is null |
| `matches` | Regex match |
| `in` | Value in set (comma-separated) |

### 2.2 DiagnosticPattern

A named combination of signals that identifies a known failure mode.

| Field | Type | Description |
|-------|------|-------------|
| `id` | `string` | Unique pattern identifier (e.g. `genai_rate_limit`) |
| `category` | `PatternCategory` | Classification (see below) |
| `signals` | `Signal[]` | All must match (conjunction) |
| `hypothesis` | `string` | What this pattern means diagnostically |
| `confidence` | `float64` | Base confidence weight (0.0–1.0) |

Categories:

| Category | Scope |
|----------|-------|
| `error` | Exception and error patterns |
| `latency` | Performance degradation |
| `cost` | Token/cost anomalies |
| `availability` | Service health patterns |
| `genai` | GenAI-specific failure modes |
| `data` | Database and storage patterns |

Multiple patterns can match the same telemetry. The pattern engine returns all matches ranked by confidence.

### 2.3 CausalRule

A directed relationship between two patterns: if cause is observed, effect is likely.

| Field | Type | Description |
|-------|------|-------------|
| `id` | `string` | Unique rule identifier |
| `causePattern` | `string` | ID of the cause `DiagnosticPattern` |
| `effectPattern` | `string` | ID of the effect `DiagnosticPattern` |
| `strength` | `float64` | Causal confidence (0.0–1.0) |
| `temporalWindow` | `string` | Time window for correlation (e.g. `5m`, `1h`) |

Causal rules build a directed graph. Given matched patterns, the engine traverses causal edges to identify root causes (patterns with no incoming causal edges).

### 2.4 InvestigationStrategy

A deterministic sequence of steps to investigate a matched pattern.

| Field | Type | Description |
|-------|------|-------------|
| `id` | `string` | Unique strategy identifier |
| `triggerPattern` | `string` | ID of the `DiagnosticPattern` that triggers this strategy |
| `steps` | `InvestigationStep[]` | Ordered investigation steps |

Each step:

| Field | Type | Description |
|-------|------|-------------|
| `action` | `string` | What to do (e.g. `query_traces`, `get_code_location`, `compare_deployments`) |
| `query` | `string` | DuckDB query template or MCP tool name |
| `description` | `string` | Human-readable explanation of this step |

The LLM does not invent investigation paths. It selects from known strategies and interprets results.

---

## 3. Source of Truth

TypeSpec definitions in `core/specs/intelligence/`:

```text
core/specs/intelligence/
├── main.tsp                      # Package definition, imports
├── signals.tsp                   # Signal, SignalOperator
├── diagnostic-patterns.tsp       # DiagnosticPattern, PatternCategory
├── causal-rules.tsp              # CausalRule
├── investigation-strategies.tsp  # InvestigationStrategy, InvestigationStep
└── seed/
    ├── patterns.tsp              # v1 seed diagnostic patterns
    ├── rules.tsp                 # v1 seed causal rules
    └── strategies.tsp            # v1 seed investigation strategies
```

### 3.1 TypeSpec Definitions

```tsp
import "@typespec/openapi3";

namespace Qyl.Intelligence;

enum SignalOperator {
  eq,
  neq,
  gt,
  gte,
  lt,
  lte,
  contains,
  exists,
  not_exists,
  matches,
  @key("in") in_set,
}

model Signal {
  attribute: string;
  operator: SignalOperator;
  value?: string;
}

enum PatternCategory {
  error,
  latency,
  cost,
  availability,
  genai,
  data,
}

model DiagnosticPattern {
  id: string;
  category: PatternCategory;
  signals: Signal[];
  hypothesis: string;
  confidence: float64;
}

model CausalRule {
  id: string;
  causePattern: string;
  effectPattern: string;
  strength: float64;
  temporalWindow?: string;
}

model InvestigationStep {
  action: string;
  query: string;
  description: string;
}

model InvestigationStrategy {
  id: string;
  triggerPattern: string;
  steps: InvestigationStep[];
}
```

---

## 4. Code Generation

Extend the existing TypeSpec → C# pipeline.

### 4.1 Generated Output

```text
src/qyl.contracts/Intelligence/
├── Signal.cs
├── SignalOperator.cs
├── DiagnosticPattern.cs
├── PatternCategory.cs
├── CausalRule.cs
├── InvestigationStep.cs
├── InvestigationStrategy.cs
└── Generated/
    ├── DiagnosticPatterns.g.cs    # static registry from seed data
    ├── CausalRules.g.cs           # static registry from seed data
    └── InvestigationStrategies.g.cs  # static registry from seed data
```

### 4.2 Registry Generation

Seed data in TypeSpec compiles to static registries:

```csharp
public static class DiagnosticPatterns
{
    public static readonly IReadOnlyList<DiagnosticPattern> All = [
        new DiagnosticPattern
        {
            Id = "genai_rate_limit",
            Category = PatternCategory.GenAi,
            Signals = [
                new Signal { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
                new Signal { Attribute = "gen_ai_provider_name", Operator = SignalOperator.Exists },
                new Signal { Attribute = "error_type", Operator = SignalOperator.Contains, Value = "rate_limit" },
            ],
            Hypothesis = "LLM provider is throttling requests. Check quota, reduce concurrency, or add backoff.",
            Confidence = 0.9,
        },
        // ...
    ];
}
```

These are compile-time collections. No file I/O, no deserialization, no reflection.

### 4.3 Documentation Generation

The same TypeSpec definitions generate `docs/intelligence-model.md` — human-readable reference with all patterns, rules, and strategies. Prose stays synchronized with code automatically.

---

## 5. Seed Patterns

Minimal first version. Three concepts, enough to bootstrap.

### 5.1 Diagnostic Patterns (v1)

| ID | Category | Signals | Hypothesis |
|----|----------|---------|------------|
| `genai_rate_limit` | genai | `status_code=2` + `gen_ai_provider_name exists` + `error_type contains rate_limit` | Provider throttling. Check quota, reduce concurrency, add backoff. |
| `genai_token_exhaustion` | genai | `gen_ai_stop_reason=length` OR `gen_ai_input_tokens > model_limit * 0.95` | Context window exceeded. Reduce prompt size or switch to larger model. |
| `genai_content_filter` | genai | `gen_ai_stop_reason contains content_filter` | Content policy violation. Review prompt content. |
| `db_timeout` | data | `exception_type=TimeoutException` + `db.system.name exists` + `duration_ns > 2000000000` | Database query timeout. Check query plan, connection pool, lock contention. |
| `db_n_plus_one` | data | `db.system.name exists` + `parent_span_id exists` + `span_count_under_parent > 10` | N+1 query pattern. Batch or prefetch related data. |
| `http_5xx_cluster` | error | `http.response.status_code gte 500` + `occurrence_rate > baseline * 3` | Server error spike. Check recent deployments and upstream dependencies. |
| `deployment_regression` | error | `error_type exists` + `first_seen_at > last_deployment_time` | New error class after deployment. Compare with previous version. |
| `cascading_timeout` | latency | `exception_type contains Timeout` + `downstream_service_error=true` | Upstream failure causing downstream timeouts. Investigate root service first. |
| `memory_pressure_latency` | latency | `process.runtime.dotnet.gc.duration gt 100` + `avg_latency > p99_baseline` | GC pressure causing latency. Check memory allocation patterns. |
| `cost_spike` | cost | `gen_ai_cost_usd > daily_average * 3` | Abnormal cost increase. Identify the model, service, and session responsible. |

### 5.2 Causal Rules (v1)

| ID | Cause | Effect | Strength | Window |
|----|-------|--------|----------|--------|
| `deploy_causes_regression` | `deployment_regression` | `http_5xx_cluster` | 0.85 | 1h |
| `rate_limit_causes_cascade` | `genai_rate_limit` | `cascading_timeout` | 0.70 | 5m |
| `db_timeout_causes_http_error` | `db_timeout` | `http_5xx_cluster` | 0.80 | 1m |
| `n_plus_one_causes_db_timeout` | `db_n_plus_one` | `db_timeout` | 0.75 | 30s |
| `memory_causes_timeout` | `memory_pressure_latency` | `cascading_timeout` | 0.65 | 5m |
| `token_exhaustion_causes_cost` | `genai_token_exhaustion` | `cost_spike` | 0.60 | 1h |

### 5.3 Investigation Strategies (v1)

**`investigate_error_issue`** — triggered by any `error` category pattern:

| Step | Action | Query/Tool | Description |
|------|--------|------------|-------------|
| 1 | `get_issue` | `SELECT * FROM error_issues WHERE id = ?` | Get issue summary, occurrence count, first/last seen |
| 2 | `get_events` | `SELECT * FROM error_issue_events WHERE issue_id = ? ORDER BY timestamp DESC LIMIT 10` | Get recent error occurrences with trace IDs |
| 3 | `get_traces` | `SELECT * FROM spans WHERE trace_id IN (?) ORDER BY start_time_unix_nano` | Reconstruct full trace graph for each occurrence |
| 4 | `get_code_location` | `SELECT code_filepath, code_function, code_lineno FROM spans WHERE span_id = ?` | Map error to source file and function |
| 5 | `correlate_deployment` | `SELECT * FROM deployments WHERE service_name = ? AND start_time <= ? ORDER BY start_time DESC LIMIT 1` | Find the deployment active when error occurred |
| 6 | `check_fix_history` | `SELECT * FROM fix_runs WHERE issue_id = ?` | Check if this error class was fixed before |

**`investigate_latency`** — triggered by `latency` category patterns:

| Step | Action | Query/Tool | Description |
|------|--------|------------|-------------|
| 1 | `identify_service` | `SELECT service_name, AVG(duration_ns), PERCENTILE_CONT(0.99) ... GROUP BY service_name` | Find the slowest service |
| 2 | `compare_distributions` | `SELECT duration_ns FROM spans WHERE service_name = ? AND start_time BETWEEN ? AND ?` | Compare current vs baseline latency distribution |
| 3 | `find_regression_window` | Time-series analysis of p99 latency | Identify when latency degraded |
| 4 | `correlate_deployment` | Same as error investigation step 5 | Find deployment in the regression window |
| 5 | `inspect_slow_spans` | `SELECT * FROM spans WHERE service_name = ? AND duration_ns > ? ORDER BY duration_ns DESC LIMIT 20` | Examine the slowest individual spans |

**`investigate_cost`** — triggered by `cost` category patterns:

| Step | Action | Query/Tool | Description |
|------|--------|------------|-------------|
| 1 | `identify_model` | `SELECT gen_ai_request_model, SUM(gen_ai_cost_usd) ... GROUP BY gen_ai_request_model` | Find the most expensive model |
| 2 | `identify_service` | `SELECT service_name, SUM(gen_ai_cost_usd) ... GROUP BY service_name` | Find the most expensive service |
| 3 | `identify_session` | `SELECT session_id, SUM(gen_ai_cost_usd) ... GROUP BY session_id ORDER BY 2 DESC` | Find the most expensive sessions |
| 4 | `trace_to_root` | Follow session → traces → spans | Understand what operations drove the cost |
| 5 | `compare_to_baseline` | Compare current period vs previous period | Quantify the cost increase |

**`investigate_genai`** — triggered by `genai` category patterns:

| Step | Action | Query/Tool | Description |
|------|--------|------------|-------------|
| 1 | `get_error_details` | `SELECT * FROM spans WHERE status_code = 2 AND gen_ai_provider_name IS NOT NULL` | Get GenAI error spans |
| 2 | `check_provider_status` | Evaluate `gen_ai_provider_name` + error frequency | Determine if provider-wide issue |
| 3 | `analyze_token_usage` | `SELECT gen_ai_input_tokens, gen_ai_output_tokens FROM spans WHERE gen_ai_request_model = ?` | Check if approaching model limits |
| 4 | `check_prompt_patterns` | Inspect spans around the error for prompt size trends | Identify if prompts are growing unbounded |
| 5 | `suggest_mitigation` | Pattern-specific recommendation | Rate limit → backoff; token limit → truncation; content filter → prompt review |

---

## 6. Runtime Consumption

### 6.1 Loom Pipeline

Loom's current 5-stage pipeline gains a deterministic pre-stage:

```text
                    ┌─────────────────────────────────┐
                    │  Stage 0: Pattern Engine         │
                    │  (deterministic, no LLM)         │
                    │                                  │
                    │  1. Extract signals from issue   │
                    │  2. Match DiagnosticPatterns      │
                    │  3. Evaluate CausalRules          │
                    │  4. Select InvestigationStrategy  │
                    │  5. Execute strategy steps        │
                    └──────────────┬──────────────────┘
                                   ↓
              Structured context: matched patterns,
              causal graph, investigation results
                                   ↓
                    ┌──────────────────────────────────┐
                    │  Stage 1-5: Existing pipeline     │
                    │  (LLM-guided, but now grounded)   │
                    │                                   │
                    │  1. Context Gathering (enriched)   │
                    │  2. Root Cause Analysis (guided)   │
                    │  3. Solution Planning              │
                    │  4. Diff Generation                │
                    │  5. Confidence Scoring             │
                    └───────────────────────────────────┘
```

The LLM receives structured evidence, not raw telemetry. This reduces hallucinations and makes reasoning reproducible.

### 6.2 Pattern Engine Interface

```csharp
public interface IPatternEngine
{
    IReadOnlyList<PatternMatch> Evaluate(IReadOnlyList<Signal> observedSignals);
    CausalGraph BuildCausalGraph(IReadOnlyList<PatternMatch> matches);
    InvestigationStrategy? SelectStrategy(PatternMatch primaryMatch);
}

public record PatternMatch(
    DiagnosticPattern Pattern,
    double Score,
    IReadOnlyList<Signal> MatchedSignals);

public record CausalGraph(
    IReadOnlyList<CausalEdge> Edges,
    IReadOnlyList<string> RootCauses);

public record CausalEdge(
    string CausePatternId,
    string EffectPatternId,
    double Strength);
```

The pattern engine is pure computation over typed data. No I/O, no LLM calls, no side effects.

### 6.3 Dashboard

The dashboard can visualize intelligence model results:

- **Pattern matches** on trace detail view — "This trace matches `db_timeout` pattern (confidence: 0.85)"
- **Causal graph** on issue detail — directed graph showing cause → effect chain
- **Investigation progress** — which strategy steps have been executed, with results

---

## 7. MCP Intelligence Surface

MCP tools expose intelligence primitives alongside telemetry queries.

### 7.1 New Tools

| Tool | Skill | Description |
|------|-------|-------------|
| `list-diagnostic-patterns` | inspect | List all available diagnostic patterns |
| `evaluate-patterns` | inspect | Given an issue/trace ID, return matched patterns with confidence scores |
| `explain-causal-chain` | analyze | Given matched patterns, return causal graph with root cause identification |
| `suggest-investigation` | analyze | Given an issue, return the recommended investigation strategy with steps |
| `execute-investigation-step` | analyze | Execute a single strategy step and return structured results |

### 7.2 Response Format

```json
{
  "patterns": [
    {
      "id": "genai_rate_limit",
      "category": "genai",
      "confidence": 0.90,
      "hypothesis": "Provider throttling. Check quota, reduce concurrency, add backoff.",
      "matched_signals": [
        { "attribute": "status_code", "operator": "eq", "value": "2", "observed": "2" },
        { "attribute": "gen_ai_provider_name", "operator": "exists", "observed": "openai" },
        { "attribute": "error_type", "operator": "contains", "value": "rate_limit", "observed": "rate_limit_exceeded" }
      ]
    }
  ],
  "causal_chain": {
    "root_cause": "genai_rate_limit",
    "edges": [
      { "cause": "genai_rate_limit", "effect": "cascading_timeout", "strength": 0.70 }
    ]
  },
  "strategy": {
    "id": "investigate_genai",
    "steps": ["..."]
  }
}
```

Stable, typed responses. No freeform LLM output in MCP tool responses for intelligence queries.

---

## 8. Constraints

<constraints>

| Constraint | Rationale |
|------------|-----------|
| Intelligence model is **data-only** | No `Func<>`, no lambdas, no code in patterns. Execution logic lives in Loom's pattern engine. Data stays portable. |
| TypeSpec is the **single source of truth** | C# types, registries, and docs are all generated. No hand-written pattern definitions. |
| Signals reference **semconv attributes and promoted columns only** | Ensures patterns are grounded in the actual telemetry schema. No invented attributes. |
| Pattern evaluation is **deterministic** | Same input signals → same matched patterns. No randomness, no LLM involvement in pattern matching. |
| All seed patterns must be **testable** | Each pattern in section 5 must have a corresponding test case: given signals X, expect pattern Y. |
| CausalRules reference **existing DiagnosticPattern IDs** | No dangling references. Validated at generation time. |
| InvestigationStrategy queries must be **valid DuckDB SQL** against the telemetry data model | No invented tables or columns. Cross-reference with `telemetry-data-model.md`. |
| New patterns are added **in TypeSpec, not in C#** | The generative pipeline is the only way to add intelligence. |

</constraints>

---

## 9. Relationship to Other Specs

The intelligence model does not replace any existing spec. It sits between them.

```text
instrumentation.md       → how telemetry is emitted
telemetry-data-model.md  → how telemetry is stored (schema)
issue-fingerprinting.md  → how errors are grouped (fingerprints)
telemetry-intelligence.md → how telemetry is reasoned about (this spec)
src/qyl.loom/specs/loom.md → how investigation is executed (agents)
mcp.md                   → how intelligence is exposed (tools)
```

The full deterministic stack:

```text
[Traced] / [GenAi] / [Db]           compile-time instrumentation
        ↓
DuckDB promoted columns              deterministic schema
        ↓
ErrorFingerprinter.Compute()          deterministic grouping
        ↓
PatternEngine.Evaluate()              deterministic diagnosis    ← NEW
        ↓
InvestigationStrategy.Steps           deterministic investigation ← NEW
        ↓
Loom LLM pipeline                     guided reasoning
        ↓
Fix generation                        code diff
```

---

## 10. Definition of Done

- [x] TypeSpec definitions in `core/specs/intelligence/` compile without errors
- [x] C# types generated to `src/qyl.contracts/Intelligence/`
- [x] Static registries generated with all v1 seed patterns, rules, and strategies
- [x] `IPatternEngine` implemented in collector with deterministic evaluation
- [x] Every seed pattern has a unit test: given signals → expected match
- [x] Every seed strategy has queries validated against `telemetry-data-model.md` schema
- [x] CausalRule references validated at generation time (no dangling pattern IDs)
- [x] MCP tools `list-diagnostic-patterns` and `evaluate-patterns` operational
- [ ] `docs/intelligence-model.md` generated from TypeSpec
- [ ] Loom pipeline invokes pattern engine before LLM reasoning
- [x] No hand-written pattern definitions in C# — all generated from TypeSpec

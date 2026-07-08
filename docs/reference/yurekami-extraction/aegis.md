# Aegis — Extraction Dossier

**One-liner:** An open-source, dependency-light Python platform that simulates Claude Code agent runs in a sandboxed virtual environment and enforces a runtime "tool firewall" + earned-autonomy control plane in production.

**Stack / language:** Python ≥ 3.12 (uses `StrEnum`, `IntEnum`, PEP 604 unions, `ClassVar`). Pydantic v2 for all models, PyYAML for config, Click + Rich for the CLI, Jinja2 for HTML reports, OpenTelemetry API/SDK declared (models only, no exporter wired yet). Hatchling build. Strict mypy + ruff. Apache-2.0. ~6,092 LOC of source. Package name `aegis-platform`, version `0.1.0-alpha`, author "AER Labs".

---

## 1. What it is / why it exists

Aegis is a **two-system** platform for teams shipping Claude Code agents:

1. **Simulation Platform** — a scenario-driven test harness. You declare a task in YAML (initial files, env vars, MCP servers, a trigger prompt, and a set of declarative assertions), the engine seeds an in-memory sandbox, intercepts every Claude Code tool call (Read/Write/Edit/Glob/Grep/Bash/WebFetch/WebSearch/Task/`mcp__*`) against simulated backends, records a full `AgentTrace`, then grades the trace across six dimensions (correctness, safety, efficiency, reliability, process quality, robustness). Adversarial variants (prompt injection, flaky tests, network failure) are first-class scenario properties.

2. **Runtime Control Plane** — a **policy engine** (priority-ordered tool firewall: allow/deny/escalate with glob tool-matching, regex param-matching, rate-limit windows, environment + autonomy-tier conditions) plus an **earned-autonomy** model (a per-agent competence score from a weighted blend of simulation performance / production completion / safety history / failure disposition, mapping to four tiers Supervised→Guided→Autonomous→Trusted with auto-promote/demote). Rollback and observability subsystems are modeled but mostly at the data-model stage.

The loop: simulate before deploy → control + measure in prod → feed prod behavior into next simulation.

**Maturity note:** The simulation engine, VFS, interceptor, evaluation/grader system, policy engine, mocks, tracing, loader, CLI, and HTML report are fully implemented. The `runtime/rollback`, `runtime/observability`, and `runtime/autonomy` packages ship **models only** (no engine). The CLI `run`/`run-suite` commands currently build an *empty* trace and exercise only the evaluation path — live Claude-Code-driven execution is an explicit "subsequent milestone" TODO.

---

## 2. Architecture overview

```
                      YAML scenario / policy files
                                │
                    scenario/loader.py  (validate → Pydantic)
                                │
   ┌────────────────────────────┴─────────────────────────────┐
   │  SIMULATION PLATFORM                                       │
   │                                                            │
   │  SimulationEngine ── setup ──► SimulationState (+GitState) │
   │       │                          │  snapshot/rollback      │
   │       │  intercept_tool_call     │                         │
   │       ▼                          ▼                         │
   │  ToolInterceptor ──► VFS handlers / Bash / Web / MCP router│
   │       │  records into                                      │
   │       ▼                                                    │
   │  AgentTrace (Turns → ToolCalls, token usage, sub-agents)   │
   │       │                                                    │
   │  mocks/ (MockState CRUD store, GitHubMock, BashMock)       │
   └────────────────────────────┬─────────────────────────────┘
                                │  trace + env_state
                                ▼
   EvaluationEngine ── AssertionEvaluator + Graders (registry) ─► ScenarioResult / SuiteResult
                                │
                    evaluation/report.py (Jinja2 → dark-theme HTML)

   ┌──────────────── RUNTIME CONTROL PLANE ─────────────────────┐
   │  PolicyEngine (from YAML) ─► PolicyDecisionResult           │
   │      priority sort · trigger.matches · conditions           │
   │      (env / autonomy_tier / rate-limit window)              │
   │  autonomy: CompetenceScore → recommended_tier (thresholds)  │
   │  rollback: Checkpoint + CompensatingAction (TOOL_COMPENSATIONS)
   │  observability: SessionMetrics, CostEntry.compute_cost, Alerts
   └────────────────────────────────────────────────────────────┘

   hooks/claude_code.py  ── glue: generate settings.json hook config,
        parse Pre/PostToolUse events, format allow/block(exit 0/2) responses
   tracing/logger.py     ── thread-safe streaming JSON trace writer
   cli/main.py           ── click group: version/validate/run/run-suite/report/init
```

Two design threads run throughout: **immutability** (Pydantic `model_copy(update=…)` and `FileMetadata.with_content/with_permissions` return new objects rather than mutating) and **snapshot/rollback** (both `SimulationState` and the VFS support named checkpoints).

---

## 3. File-by-file map

### `src/aegis/models/` — Pydantic data contracts
- **`scenario.py`** — The declarative scenario schema. Enums: `Difficulty`, `RiskLevel`, `TriggerType`, `AssertionType` (10 assertion kinds). `EnvironmentConfig` (initial_files, env_vars, mcp_servers, branch), `Trigger`, `Assertion`, `AdversarialVariant` (prompt-injection / flaky-test-rate / network-failure knobs), `Scenario`, `ScenarioSuite`.
- **`trace.py`** — Execution trace model. `ToolCall` (with immutable `.complete()`/`.fail()` returning copies), `TokenUsage` (input/output/cache), `Turn` (nestable via `sub_agent_turns`), `AgentTrace` with computed `tool_calls` flattening, `tools_called`, `has_tool_call`. Enums `ToolCallStatus`, `PolicyDecision`.
- **`evaluation.py`** — Result models. `Score` (0–1 value + dimension + `level` property), `ScoreLevel`, `EvaluationDimension` (6 dims), `AssertionResult`, `ScenarioResult` (computed `overall_score`, `dimension_scores`, assertion tallies), `SuiteResult` (pass_rate, overall_score aggregation).

### `src/aegis/simulation/` — the sandbox
- **`vfs.py`** (661 LOC, largest file) — `VirtualFileSystem`: thread-safe (`RLock`) in-memory POSIX filesystem with `.._normalise` resolving `..`/`.` without touching disk, full Read/Write/Edit/Glob/Grep/LS/mkdir/tree API mirroring Claude Code tools, and O(n) shallow-copy snapshot/rollback. `FileMetadata` dataclass is immutable-style. Custom exception hierarchy subclassing both `VFSError` and the builtin equivalents.
- **`interceptor.py`** — `ToolInterceptor`: dispatch table routing tool calls to simulated handlers (VFS ops, a git-aware `_handle_bash`, canned web responses, an `mcp__*` prefix router, a Task sub-agent stub), records each into the `AgentTrace`, and a `record_result` that walks turns in reverse to supersede a simulated result with the runtime's real one.
- **`state.py`** — `SimulationState` + `GitState` dataclass. Deep-copy named snapshots, `elapsed_seconds`, and `as_env_dict()` which is the bridge feeding graders (`vfs`, `git`, `env_vars`, `tool_call_count`).
- **`engine.py`** — `SimulationEngine`: lifecycle (setup/cleanup), `intercept_tool_call` (enforces `max_tool_calls`), `record_tool_result`, `is_complete` (tool-limit or timeout), and thin `async_*` wrappers.

### `src/aegis/evaluation/` — grading
- **`engine.py`** (472 LOC) — `Grader` Protocol, two default graders (`ToolCallCountGrader` efficiency, `AssertionPassRateGrader` correctness), the big `AssertionEvaluator` that dispatches all 10 assertion types (note: `bash_succeeds`/`bash_fails` actually shell out via `subprocess.run(shell=True, timeout=30)`), and `EvaluationEngine.evaluate` / `evaluate_suite`.
- **`graders/base.py`** — Abstract `Grader` ABC, `GraderRegistry`, and a `@grader(name, dimension, description)` class decorator that self-registers into a module-level singleton `registry`.
- **`graders/code.py`** (566 LOC) — Deterministic structural graders, all decorator-registered: file exists/contains, bash succeeds/fails, tool called/not-called, tool-call-count (with *partial credit* proportional to closeness), and two always-on safety graders — `NoDestructiveCommandsGrader` (13 compiled regexes: `rm -rf`, fork bomb, `dd of=/dev/sd*`, DROP TABLE, disk redirects) and `NoPIIExposureGrader` (SSN, credit card, email, phone, API keys, AWS keys).
- **`graders/state.py`** — `StateCheckGrader` (dot-path key resolution into env_state, e.g. `git.branch`) and `FileSystemStateGrader` (holistic VFS consistency ratio).
- **`report.py`** — Self-contained dark-theme HTML report via an inline Jinja2 template string; collapsible scenario cards using a pure-CSS checkbox toggle hack (no JS), dimension pills, grader + assertion tables, progress bars. `save_report()`.

### `src/aegis/runtime/` — control plane
- **`policy/models.py`** — `Policy`, `PolicyTrigger` (`matches_tool` glob→regex, `matches_params` regex), `PolicyCondition` (environment / autonomy_tier `{less_than,greater_than,equals}` / `TimeWindow` rate limit / time_of_day), `EscalationConfig` (type, timeout, default_on_timeout), `PolicySet.evaluate` (priority sort, first-match-wins, allow-by-default).
- **`policy/engine.py`** — `PolicyEngine`: `from_yaml`/`from_yaml_string`, mutable environment + autonomy_tier context, `_check_conditions` including a sliding-window rate counter (`_CallRecord` with `__slots__`, 1-hour history pruning), add/remove policy, call stats.
- **`autonomy/models.py`** — `AutonomyTier` IntEnum (1–4), `TIER_DESCRIPTIONS`, `DEFAULT_TIER_THRESHOLDS` (0/0.60/0.80/0.92), `CompetenceWeights` (0.40/0.25/0.25/0.10), `CompetenceScore.compute_overall` (weighted blend) + `recommended_tier`, `AutonomyConfig` (auto_promote/demote, failure_injection_rate).
- **`rollback/models.py`** — `Checkpoint` (filesystem/git/mcp), `FileCheckpoint`, `GitCheckpoint`, `CompensatingAction`, and a `TOOL_COMPENSATIONS` map (Write→restore_file, git_commit→git_reset_soft, create_pull_request→close_pr, merge→revert_merge). `RollbackRequest`/`RollbackResult`. Models only.
- **`observability/models.py`** — `SpanKind`, `CostEntry.compute_cost` (per-model $/token pricing table incl. cache reads ≈10% of input), `SessionMetrics`, `Alert`, `AnomalyDetectionConfig` (std-dev spike thresholds), `ObservabilityConfig` (export targets: Langfuse, Phoenix, Datadog, OTEL, JSON). Models only.

### `src/aegis/mocks/` — stateful backends
- **`base.py`** — `MockState`: a generic in-memory CRUD store keyed by collection→id with auto `id`/`created_at`, `query(**filters)` equality matching. `MCPServerMock` ABC, `StatefulResponse`, `MockRegistry`.
- **`github.py`** (377 LOC) — `GitHubMock`: a genuinely stateful GitHub simulation — repos/branches/PRs/issues/commits/files/checks. `merge_pull_request` actually copies head-branch files onto the base branch; deterministic SHAs via `sha1[:40]`; auto-incrementing PR/issue numbers; seed helpers.
- **`bash.py`** — `BashMock`: ~40 default regex→`CommandResponse` patterns (git, npm/pnpm/yarn, pytest, cargo, coreutils, python), custom-pattern prepend, and `inject_failure`/`inject_timeout` for adversarial testing. Special-cases `echo`.

### glue / infra
- **`hooks/claude_code.py`** — Claude Code hook integration: `generate_hook_config` (emits settings.json PreToolUse/PostToolUse matchers), `parse_pre_tool_event`/`parse_post_tool_event` (env-var/stdin JSON → ToolCall), `format_hook_response` (allow → exit 0 with optional `modified_input`; deny/escalate → `{"decision":"block"}` exit 2).
- **`tracing/logger.py`** — `TraceLogger`: thread-safe, real-time streaming JSON trace writer (flushes on every event), session/turn/tool-call lifecycle, `add_token_usage` aggregation, `filter_tool_calls`, `summary_stats`, static `load_trace`/`load_all_traces`.
- **`scenario/loader.py`** — YAML → `Scenario`/`ScenarioSuite` with `${VAR}` recursive template substitution, `scenario:`-wrapped or bare document support, and rich `ValidationError` → `ScenarioLoadError` reformatting with file context. Suite loader fails-fast aggregating all file errors.
- **`cli/main.py`** — Click CLI: `version`, `validate`, `run`, `run-suite`, `report` (HTML from saved trace JSON), `init` (scaffolds `.aegis/` settings.toml + dirs). Rich tables/panels throughout.

### examples / tests
- **`examples/policies/default.yaml`** — 7 production-shaped policies (destructive fs/SQL, credential exposure, prod DB writes, force-push escalation, npm-install tier gate, GitHub rate limit).
- **`examples/scenarios/{basic-file-ops,pr-creation}.yaml`** — Full worked scenarios incl. adversarial variants.
- **`tests/`** — Pytest suites (Microsoft-Testing-style asyncio_mode=auto) mirroring each subsystem: loader, mocks (github/bash), simulation (engine/vfs/trace_logger), runtime (policy_engine/autonomy), hooks, evaluation graders.

---

## 4. Notable code

### (a) In-memory VFS path normalization without touching disk — `simulation/vfs.py:133`
```python
@staticmethod
def _normalise(path: str) -> str:
    """Convert a path to an absolute POSIX path string."""
    path = path.replace("\\", "/")
    pure = PurePosixPath(path)
    parts: list[str] = []
    for part in pure.parts:
        if part == "..":
            if parts and parts[-1] != "/":
                parts.pop()
        elif part != ".":
            parts.append(part)
    if not parts:
        return "/"
    result = str(PurePosixPath(*parts))
    if not result.startswith("/"):
        result = "/" + result
    return result
```
Resolves `..`/`.` purely lexically via `PurePosixPath`, so a sandboxed agent can never escape to the real filesystem. The whole VFS is a flat `dict[str, FileMetadata]` keyed by normalized absolute path, with directory membership derived by prefix scans — simple, no tree pointers, and snapshot = `dict(self._tree)` (O(n) shallow copy relying on FileMetadata immutability).

### (b) Priority-ordered tool firewall with sliding-window rate limiting — `runtime/policy/engine.py:63`
```python
def evaluate(self, tool_name, params) -> PolicyDecisionResult:
    self._record_call(tool_name)
    for policy in sorted(self._policy_set.policies, key=lambda p: -p.priority):
        if not policy.enabled:
            continue
        if not policy.trigger.matches(tool_name, params):
            continue
        if not self._check_conditions(policy.condition, tool_name):
            continue
        return PolicyDecisionResult(action=policy.action, policy_name=policy.name,
                                    message=policy.message, escalation=policy.escalation)
    return PolicyDecisionResult(action=PolicyAction.ALLOW, policy_name="default",
                               message="No matching policy; allowed by default")
```
First-match-wins by descending priority; conditions layer environment, autonomy tier, and a real rate-limit counter. `_record_call` prunes history older than an hour on every call, and `_CallRecord` uses `__slots__`. Clean, auditable "allow by default, deny/escalate by rule" firewall.

### (c) Earned-autonomy competence → tier mapping — `runtime/autonomy/models.py:65`
```python
def compute_overall(self, weights=None) -> CompetenceScore:
    w = weights or CompetenceWeights()
    score = (self.simulation_performance * w.simulation_performance
             + self.production_completion * w.production_completion
             + self.safety_history * w.safety_history
             + self.failure_disposition * w.failure_disposition)
    return self.model_copy(update={"overall": min(1.0, max(0.0, score))})

def recommended_tier(self, thresholds=None) -> AutonomyTier:
    t = thresholds or DEFAULT_TIER_THRESHOLDS
    recommended = AutonomyTier.SUPERVISED
    for tier in sorted(AutonomyTier, reverse=True):
        if self.overall >= t.get(tier, 1.0):
            recommended = tier
            break
    return recommended
```
A weighted-blend competence score gated against descending thresholds — the conceptual core of "agents earn autonomy." Weights sum to 1.0; safety history defaults to 1.0 (trust-until-violation), failure disposition to 0.5.

### (d) Always-on safety grader with a destructive-command corpus — `evaluation/graders/code.py:21` + `:455`
```python
_DESTRUCTIVE_PATTERNS = [
    re.compile(r"\brm\s+-[a-z]*r[a-z]*f\b"),          # rm -rf / rm -fr
    re.compile(r"\bdrop\s+table\b", re.IGNORECASE),
    re.compile(r":\s*\(\s*\)\s*\{.*:\|:&\s*\}", re.DOTALL),  # fork bomb
    re.compile(r"\bdd\b.*\bof=/dev/[sh]d"),           # disk wipe
    re.compile(r">\s*/dev/sd[a-z]\b"),                # redirect into raw disk
    ...
]
```
`NoDestructiveCommandsGrader` and `NoPIIExposureGrader` ignore scenario assertions entirely and *always* scan every Bash call / every text output — a reusable red-team ruleset. The PII grader scans both turn responses and tool-call results.

### (e) Pure-CSS collapsible HTML report (zero JavaScript) — `evaluation/report.py`
```css
.toggle-cb { display: none; }
.toggle-cb:checked ~ .scenario-body { display: block; }
.scenario-body { display: none; }
.toggle-cb:checked ~ .scenario-header .chevron { transform: rotate(90deg); }
```
The entire interactive report (collapsible scenario cards, rotating chevrons) is driven by hidden checkbox inputs + sibling selectors — a self-contained, CSP-safe, single-file artifact with no runtime deps. Jinja2 renders it from an inline template string with registered helper globals (`pct_class`, `val_class`, `score_badge`).

### (f) Stateful mock that actually mutates cross-branch state — `mocks/github.py` (`_merge_pull_request`)
On merge it flips PR state, mints a deterministic merge SHA, then **copies every head-branch file record onto the base branch** — so a follow-up `get_file_contents` on `main` reflects the merge. This is what makes multi-step agent workflows (branch → edit → PR → merge → verify) gradeable against a mock rather than a live API.

---

## 5. Extractable value

Concrete, liftable pieces (several directly relevant to qyl-style observability/agent tooling):

1. **`VirtualFileSystem` (vfs.py)** — a self-contained, thread-safe, snapshot-capable in-memory POSIX filesystem mirroring the Claude Code tool surface (Read/Write/Edit/Glob/Grep/LS). Drop-in sandbox for any agent test harness; the lexical `_normalise` is a nice jailbreak-safe path resolver on its own.

2. **Priority-ordered policy/firewall engine (policy/engine.py + models.py)** — YAML-driven allow/deny/escalate rules with glob tool-matching, regex param-matching, environment/tier conditions, and sliding-window rate limiting. Generic enough to gate *any* tool-call stream (MCP proxy, gateway, CI). The `_CallRecord` + hourly-prune pattern is a tidy in-memory rate limiter.

3. **`@grader` decorator + `GraderRegistry` pattern (graders/base.py)** — a clean plugin-registration idiom: decorate a class, it self-registers into a singleton with metadata (name/dimension/description). Reusable for any pluggable-check system.

4. **Safety rule corpuses** — `_DESTRUCTIVE_PATTERNS` (13 compiled dangerous-command regexes) and `_PII_PATTERNS` (SSN/CC/email/phone/API-key/AWS-key) are directly reusable red-team/DLP scan lists for guarding LLM tool output.

5. **`CostEntry.compute_cost` (observability/models.py)** — a per-model Claude token-pricing table with cache-read discounting; directly comparable to qyl's GenAI token-usage/cost accounting. Note: uses placeholder model ids (`claude-opus-4-6` etc.) — treat the *shape* as reusable, refresh actual rates from the Claude API reference.

6. **Streaming JSON trace model + writer (models/trace.py + tracing/logger.py)** — an OTel-flavored `AgentTrace → Turn → ToolCall` hierarchy (with nested sub-agent turns and token usage) plus a thread-safe logger that flushes on every event. A ready-made agent-run trace schema and recorder; the immutable `ToolCall.complete()/.fail()` copy pattern is clean.

7. **Claude Code hook glue (hooks/claude_code.py)** — reusable helpers to generate `settings.json` PreToolUse/PostToolUse hook config and to translate policy decisions into the exit-0/exit-2 + `{"decision":"block"}` protocol Claude Code expects. Direct value for anyone wiring a policy layer into Claude Code.

8. **Stateful MCP mock framework (mocks/base.py + github.py)** — `MockState` generic CRUD store + `MCPServerMock` ABC + registry, with GitHubMock as a worked example that maintains cross-branch consistency. A pattern for building deterministic, replayable MCP server fakes for agent testing.

9. **Declarative scenario/assertion schema (models/scenario.py + loader.py)** — a compact YAML DSL for agent tasks with adversarial-variant knobs and `${VAR}` templating; a good starting contract for a scenario-based eval suite.

10. **Zero-JS self-contained HTML report (report.py)** — the CSS-checkbox collapsible pattern + inline dark theme is a lift-and-reuse artifact template for any evaluation/report output (CSP-safe, single file).

---

## 6. Build / run

```bash
# dev install
git clone https://github.com/aerlabs/aegis.git && cd aegis
pip install -e ".[dev]"        # hatchling; deps: pydantic, pyyaml, click, rich, jinja2, otel

# CLI (entry point: aegis = aegis.cli.main:cli)
aegis version
aegis validate examples/scenarios/pr-creation.yaml
aegis run examples/scenarios/pr-creation.yaml -o report.html
aegis run-suite examples/scenarios/ -o suite.html
aegis report trace.json --scenario examples/scenarios/pr-creation.yaml -o out.html
aegis init                     # scaffolds .aegis/ (settings.toml, scenarios/, reports/)

# tests / lint / types
pytest                         # testpaths=tests, asyncio_mode=auto
ruff check .                   # E,F,I,N,W,UP,B,SIM,RUF ; line-length 100
mypy                           # strict
```
CI: `.github/workflows/ci.yml` runs the test suite. Requires Python ≥ 3.12. **Caveat:** `aegis run`/`run-suite` currently evaluate an *empty* trace (live agent execution is a stated future milestone); the assertion `bash_succeeds`/`bash_fails` path in `evaluation/engine.py` executes real shell commands via `subprocess.run(shell=True)`, so run untrusted scenarios in a sandbox.

**Programmatic use:**
```python
from aegis.simulation import SimulationEngine
from aegis.scenario.loader import load_scenario
from aegis.evaluation import EvaluationEngine

sc = load_scenario("scenario.yaml")
eng = SimulationEngine(sc); eng.setup()
eng.intercept_tool_call("Write", {"file_path": "/workspace/x.py", "content": "..."})
trace = eng.get_trace(); state = eng.get_state(); eng.cleanup()
result = EvaluationEngine().evaluate(trace, sc, env_state=state.as_env_dict())
```

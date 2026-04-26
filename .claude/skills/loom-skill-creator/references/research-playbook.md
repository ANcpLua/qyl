# Research Playbook

How to research a Loom workflow skill systematically using parallel grep + read batches against the qyl source tree. This is the exact pattern that produces accurate, non-fabricated Loom skills.

## Principles

1. **Never write skills from memory** — always grep the current source first.
2. **Write findings to files, not inline** — research output is 200-600 lines per topic; keep it out of your context.
3. **Parallel where possible** — batch independent research tasks, run them concurrently.
4. **Verify the output** — check line counts, look for real file paths with line numbers, re-run failures.
5. **Source-verify critical registrations** — after research, grep the referenced `[McpServerTool]` / `[McpServerPrompt]` names to confirm they exist.

## Research File Location

Store all research in a persistent directory the workers can access later:

```
/Users/ancplua/qyl/.claude/skills/loom-skill-creator/research/<skill>-<topic>.md
```

Examples:
```
.../research/loom-create-alert-routing.md
.../research/loom-create-alert-mcp-tools.md
.../research/loom-create-alert-mcp-prompts.md
```

Research files are ephemeral — delete them after the skill is merged. They exist to keep large-context data off the agent's working memory during skill authoring.

## Research Sources (qyl-Specific)

No external Sentry docs URLs. All research against qyl source + shipped reference skills:

| Source | What lives there |
|---|---|
| `/Users/ancplua/qyl/services/qyl.loom/Workflows/` | `LoomWorkflowKind`, `LoomRouteDecision`, `LoomWorkflowTools`, `LoomWorkflowRouter` — the routing layer |
| `/Users/ancplua/qyl/services/qyl.loom/Workflows/Prompts/` | `FixIssuePrompts`, `ReviewBotPrompts`, `AiMonitoringPrompts`, `OnboardingPrompts` — the `[McpServerPrompt]` registrations |
| `/Users/ancplua/qyl/services/qyl.loom/Autofix/` | `LoomAutofixPrompts`, `LoomHandoffPrompts` — autofix surface |
| `/Users/ancplua/qyl/services/qyl.loom/Agents/LoomGodAnalyzerServer.cs` | `loom_get_issue_insight`, `loom_start_fix_run`, `loom_generate_pr_review`, `loom_autofix_setup_check` |
| `/Users/ancplua/qyl/services/qyl.mcp/Tools/` | `qyl.*` tools — `ErrorTools`, `SpanQueryTools`, `RcaTools`, `SummaryTools`, etc. |
| `/Users/ancplua/qyl/services/qyl.collector/` | DuckDB store, endpoints, workflows, AgentRuns — the data layer behind the MCP surface |
| `/Users/ancplua/qyl/core/specs/api/routes.tsp` | TypeSpec contract — authoritative over the C# implementation when they disagree |
| `/Users/ancplua/qyl/docs/attributes/qyl.attrs.md` | qyl OTel semconv attributes (`qyl.*` span attributes) |
| `/Users/ancplua/qyl/.claude/skills/loom-*/SKILL.md` | Existing Loom skills — style + shape reference |
| `~/.claude/skills/microsoft-agent-framework/SKILL.md` | MAF reference — agent composition patterns for loom skills that drive agents |
| `~/.claude/skills/ancplua-roslyn-utilities/SKILL.md` | ANcpLua.Roslyn.Utilities skill — test infra (`WorkflowFixture`, `FakeChatClient`, `GeneratorTestEngine`) |
| `/Users/ancplua/qyl/internal/qyl.mcp.generators/` | MCP tool generator — emits `[QylSkill]` / `[QylCapability]` registrations |

## Grep Templates

### Batch 1: Routing + Workflow Kind

Confirm whether the new workflow shape is routable today. If not, the skill generator must flag the gap.

```bash
cd /Users/ancplua/qyl

# What kinds already exist?
grep -n "public enum LoomWorkflowKind\|^    [A-Z]" services/qyl.loom/Workflows/LoomWorkflowKind.cs

# How does the router signal a match?
grep -n "class LoomRouteDecision\|public static LoomRouteDecision" services/qyl.loom/Workflows/LoomRouteDecision.cs

# What signals does the router consume?
grep -n "matchedSignals\|issueId\|pullRequestNumber\|reviewBotAuthor\|repoRoot" services/qyl.loom/Workflows/LoomWorkflowRouter.cs

# What does the router tool emit?
grep -n "\[McpServerTool" services/qyl.loom/Workflows/LoomWorkflowTools.cs
```

Document in `research/<skill>-routing.md`:
- Whether `LoomWorkflowKind` has an enum value for your shape (or not — file a follow-up).
- What structured signals the router consumes (so your skill's frontmatter `description` can list them).
- What `clarifyingQuestion` the router returns when signals conflict with yours.

### Batch 2: MCP Tool Surface

```bash
# All Loom MCP tools
grep -rn "\[McpServerTool(Name = \"loom_" services/qyl.loom/

# All qyl.* MCP tools (live on QylMcpServer, not LoomMcpServer)
grep -rn "\[McpServerTool(Name = \"qyl\." services/qyl.mcp/

# TaskSupport annotations — required vs optional
grep -rn "TaskSupport\.\(Required\|Optional\)" services/qyl.loom/ services/qyl.mcp/

# LoomToolEnvelope pattern — what your tool should return
grep -rn "LoomToolEnvelope\.Ok\|LoomToolEnvelope\.Fail" services/qyl.loom/
```

Document in `research/<skill>-mcp-tools.md`:
- Exact `Name = "..."` of every tool your skill will reference, with file:line.
- Argument shapes (C# parameter names + `[Description]` attribute contents).
- Return shape — `LoomToolEnvelope<T>` where `T` is ... ?
- `TaskSupport` choice: `Required` for side-effecting pipelines, `Optional` for meta-tools and >10k-row searches.

### Batch 3: MCP Prompt Surface

```bash
# All Loom prompts
grep -rn "\[McpServerPrompt(Name = \"qyl\.loom\." services/qyl.loom/

# Prompt arguments — what does each take?
grep -B2 -A20 "\[McpServerPrompt" services/qyl.loom/Workflows/Prompts/*.cs services/qyl.loom/Autofix/*.cs

# Prompt return shape — raw string, ChatMessage list, or structured directive?
grep -rn "public static.*Prompt\b\|public static.*Messages\b" services/qyl.loom/
```

Document in `research/<skill>-mcp-prompts.md`:
- The full `[McpServerPrompt]` attribute — name, title.
- Every argument — name, `[Description]`, type, default.
- What the prompt body contains — grep the raw-string literal, capture the structure (phases, hard rules, report shape).
- Whether the prompt is self-contained or relies on earlier context (detection JSON, parsed summary, etc.).

### Batch 4: Agent Composition + Telemetry

Only applicable if the skill drives Loom agents (autofix, fix-run, LLM-driven investigation). If the workflow is tool-only, skip.

```bash
# Apex three-builder pattern
grep -rn "AsBuilder\(\)\.UseOpenTelemetry\|AsBuilder\(\)\.UseQylTelemetry" services/qyl.loom/

# LoomRunState — session discipline
grep -rn "LoomRunState\b" services/qyl.loom/

# InvestigationLineage — bounded autonomy
grep -rn "InvestigationLineage\.\|TryEnter" services/qyl.loom/ services/qyl.mcp/

# MAF integration — IChatClient consumption
grep -rn "IChatClient\b" services/qyl.loom/ services/qyl.collector/ | head -20
```

Document in `research/<skill>-agent-composition.md`:
- Which Apex builder(s) the skill's generated examples should use.
- How telemetry middleware decorates the builder (`UseOpenTelemetry("qyl.agent")` vs `UseQylTelemetry()`).
- Whether the skill needs `LoomRunState` session plumbing.
- Whether `InvestigationLineage.TryEnter()` applies (depth 3, spawn 10 budgets).

### Batch 5: Test Coverage

```bash
# Where do Loom workflow tests live?
find tests/ -name "*.cs" | xargs grep -l "Loom\|loom_route\|qyl\.loom" 2>/dev/null | head

# FakeChatClient usage
grep -rn "FakeChatClient" tests/

# WorkflowFixture usage
grep -rn "WorkflowFixture\b" tests/

# Generator tests — how MCP tool generator is tested
grep -rn "GeneratorTestEngine\b" tests/ internal/
```

Document in `research/<skill>-test-coverage.md`:
- Test classes covering sibling Loom workflows (use as templates).
- The `FakeChatClient` construction pattern.
- The `WorkflowFixture` pattern for integration-testing a Loom workflow.
- The generator-test pattern if your skill adds a new `[QylSkill]` / `[QylCapability]` tool class.

## Research Execution Pattern

```python
# Pseudocode for the research phase

# 1. Determine batches based on workflow shape
batches = [
    ("routing", batch_1_prompt),
    ("mcp-tools", batch_2_prompt),
    ("mcp-prompts", batch_3_prompt),
    ("test-coverage", batch_5_prompt),
]
if workflow_drives_agents:
    batches.append(("agent-composition", batch_4_prompt))

# 2. Run all batches in parallel
for topic, prompt in batches:
    claude(
        prompt=prompt.format(workflow=workflow),
        outputFile=f".claude/skills/loom-skill-creator/research/{workflow}-{topic}.md"
    )

# 3. Verify outputs
for topic, _ in batches:
    file = f".claude/skills/loom-skill-creator/research/{workflow}-{topic}.md"
    lines = count_lines(file)
    grep_hits = count_grep("McpServer\|services/qyl", file)
    if lines < 100 or grep_hits < 3:
        print(f"WARNING: {file} — {lines} lines, {grep_hits} source citations — re-run")

# 4. Re-run any failures
```

## Verification Research

After workers create the skill files, run one final verification pass. This is the qyl analogue of the Sentry SDK's "verify APIs against GitHub source" — the verification target is the qyl source tree:

```
Verification prompt: "Verify these specific MCP tool names, prompt ids, and C# type names
against the qyl source tree at /Users/ancplua/qyl/services/. For each, state whether it
EXISTS or NOT, the correct name if different, and the source file + line number:

1. <tool / prompt / type from skill file>
2. ...
```

### What To Verify

| Category | Examples to check |
|---|---|
| MCP prompt ids | Exact `Name = "qyl.loom.fix_issue"` — casing, underscores, prefix |
| MCP tool names | `loom_route` vs `qyl.route` — tools on LoomMcpServer use `loom_`, tools on QylMcpServer use `qyl.` |
| Prompt argument names | C# parameter names — `repoRoot`, `issueId`, `searchQuery` — match exactly |
| LoomWorkflowKind values | `FixProductionIssue`, `ReviewBotPrComments`, etc. — enum value exists |
| Agent surfaces | `LoomGodAnalyzerServer` is a real class; `LoomFooBarServer` is not |
| qyl attribute semconv | `qyl.*` span attributes exist in `docs/attributes/qyl.attrs.md` |
| TypeSpec references | `core/specs/api/routes.tsp` line numbers match |
| Test infra types | `FakeChatClient`, `WorkflowFixture`, `GeneratorTestEngine` all come from real packages |

## Common Research Failures

| Symptom | Cause | Fix |
|---|---|---|
| Research file has 0 lines | Worker failed silently | Re-run the batch with `outputFile` explicit and a shorter prompt |
| Research file has < 100 lines | Partial failure, only process notes captured | Re-run with a narrower topic (split one batch into two) |
| Tool name in research does not grep in source | Worker hallucinated the name | Re-run with explicit `grep -rn "\[McpServerTool" services/` instruction |
| Prompt argument name does not match C# source | Worker paraphrased instead of citing | Re-run telling the worker to copy the argument name verbatim from `[Description]` |
| Research mentions `@sentry/wizard` or `npx sentry-cli` | Worker pulled Sentry-specific context | qyl has no wizard. Re-run with explicit "qyl has no CLI wizard; the MCP tool IS the wizard" framing |
| Research cites `ISourceGenerator` instead of `IIncrementalGenerator` | Stale pattern — qyl uses incremental only | Re-run with explicit "qyl uses IIncrementalGenerator exclusively" framing |
| Research suggests `DateTime.UtcNow` in a C# example | qyl forbids it | Re-run with explicit `TimeProvider.System.GetUtcNow()` requirement |

## Verification Commands (Quick Checks)

Drop-in shell snippets for the final review pass:

```bash
cd /Users/ancplua/qyl

# Every prompt id in the generated skill exists in source
diff <(grep -oE 'qyl\.loom\.[a-z_]+' .claude/skills/loom-<workflow>/SKILL.md | sort -u) \
     <(grep -rhoE 'qyl\.loom\.[a-z_]+' services/qyl.loom/ services/qyl.mcp/ | sort -u) \
  | grep '^<' && echo "PROMPT FABRICATIONS ABOVE"

# Every loom_ tool in the generated skill exists in source
diff <(grep -oE '\bloom_[a-z_]+\b' .claude/skills/loom-<workflow>/SKILL.md | sort -u) \
     <(grep -rhoE '\bloom_[a-z_]+\b' services/qyl.loom/ | sort -u) \
  | grep '^<' && echo "TOOL FABRICATIONS ABOVE"
```

Any output from those diffs → stop, fix the skill, re-run.

## See Also

- `${SKILL_ROOT}/references/philosophy.md` — bundle architecture + 4-phase wizard rationale
- `${SKILL_ROOT}/references/quality-checklist.md` — pre-merge rubric + 10-command verification battery
- `/Users/ancplua/qyl/CLAUDE.md` — qyl-wide hard rules binding every generated example
- `/Users/ancplua/qyl/.claude/skills/SKILL.md` — skill tree index (where the new skill must be registered)

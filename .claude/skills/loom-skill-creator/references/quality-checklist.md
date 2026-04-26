# Quality Checklist

Rubric for evaluating Loom workflow-skill bundles before merge. Every item must pass. Verification commands operate against qyl source — no external docs.

## Main SKILL.md

### Spec Compliance

| Check | Requirement |
|---|---|
| Focus | 4-phase wizard + MCP dispatch + hard rules + troubleshooting; deep dives belong in `references/` |
| Length | 80-150 lines. Hard cap at 200. If longer, move content into a reference. |
| `name` field | Matches directory name, kebab-case, 1-64 chars, starts `loom-` (or `qyl-` for non-Loom qyl skills) |
| `description` field | Under 1024 chars, includes trigger phrases, states the non-negotiable posture, no angle brackets inside |
| `license` field | **Omit** — qyl skills do not carry a `license` frontmatter key (unlike sentry-for-ai skills). Verify: `head -10 SKILL.md \| grep -c '^license:'` should return 0 |
| No content before `---` | YAML frontmatter must be the first thing in the file |

### Wizard Flow

| Phase | Must include |
|---|---|
| Phase 1: Detect | A real MCP tool call (e.g. `loom_detect_dotnet(repoRoot)`, `loom_parse_review_bot_comments(commentsJson)`). Not pseudo-code. |
| Phase 2: Recommend | Opinionated actions keyed to Phase 1 output. Tables, not prose. "Always / when detected / opt-in" — NOT "maybe consider". |
| Phase 3: Guide | MCP prompt fetch call with arguments, then the numbered phases from the prompt. |
| Phase 4: Cross-Link / Verify | Either adjacent-skill suggestions or a concrete verification step (test exception + `qyl.get_error_issue` call, bot-comment report shape, etc.). |

### Content Quality

| Check | Pass criteria |
|---|---|
| Non-negotiable posture | Stated in a `Non-negotiable rules` or `Security posture` block at the top. 3-6 numbered items. Imperative. No hedging. |
| Invoke-this-skill block | 3-6 trigger phrases plus the `loom-workflow routed to <kind>` cross-link. |
| MCP surface table | Every tool + prompt the skill uses, with a one-line `Purpose` column. |
| Hard rules section | 3-5 bullets of "never X, always Y". Matches the prompt's rules. |
| Troubleshooting | 3-5 common issues with concrete fixes. Not "consult the docs". |
| Cross-links | Every sibling skill named exists under `.claude/skills/`. Every MCP tool/prompt named exists in source. |

## Reference Files

### Per-File Checks

| Check | Requirement |
|---|---|
| Minimum MCP surface line | Stated at the top of every reference (`> Minimum MCP surface: <prompt> + <tool> in services/qyl.loom/<path>`) |
| Source cites | Every claim about a tool/prompt/agent cites `services/<path>.cs:LINE` |
| Working C# examples | Compiles against the current qyl codebase — not pseudo-code, no placeholder types |
| Tables for config | MCP tool arguments, prompt parameters, return-shape fields — all tabulated |
| Troubleshooting | At least 3 common issues with solutions |
| One topic per file | Philosophy in one file, quality checklist in another, research playbook in a third — no mixing |
| Length | 100-300 lines. If longer, split into a second reference. |

### C# Example Quality

| Good | Bad |
|---|---|
| `// Copyright (c) 2025-2026 ancplua` as line 1 | No copyright header |
| File-scoped namespace | Block-scoped `namespace X { ... }` |
| Primary constructor `internal sealed class Foo(IBar bar)` | Hand-rolled constructor |
| `required` init property | Nullable + `!` assertion |
| Switch expression | If-else ladder |
| `TimeProvider.System.GetUtcNow()` | `DateTime.UtcNow` or `DateTimeOffset.UtcNow` |
| `await ...Async(ct)` everywhere | `.Result`, `.Wait()`, blocking async |
| Raw string literal `"""..."""` for generated code | `SyntaxFactory.NormalizeWhitespace()` |
| `FakeChatClient` from `tests/qyl.collector.tests/Instrumentation/` | Hand-rolled `Moq<IChatClient>` |
| `LoomToolEnvelope.Ok(data)` / `LoomToolEnvelope.Fail<T>(error)` | `new LoomToolEnvelope<T>(...)` direct instantiation |
| `[QylSkill] [QylCapability]` on the tool class — generator emits DI | Manual `builder.Services.AddMcpServerTool<T>()` |

### Accuracy Indicators

Watch for these red flags that indicate fabricated content:

| Red flag | What to check |
|---|---|
| MCP prompt id that grep cannot find in `services/` | Fabrication. Fix or remove. |
| Tool name with `qyl.` prefix (e.g. `qyl.loom_route`) | Wrong — MCP tools use `loom_` / `qyl.` prefix per surface (tools on `LoomMcpServer` use `loom_`; tools on `QylMcpServer` use `qyl.`). Check which server the tool lives on. |
| Argument name that does not match the C# parameter | Read the source — the skill is lying. |
| C# example that references a type not in qyl | Fabrication — search `*.cs` to confirm. |
| `[AgentTraced]` attribute usage | Removed from qyl. Do not reintroduce. |
| `DateTime.UtcNow` / `.Result` / `dynamic` in a C# example | Violates qyl hard rules. Rewrite. |

### Honesty Checks

| Check | Requirement |
|---|---|
| Removed workflows | Documented honestly (e.g. "`loom-foo` was retired in favor of `loom-bar`") |
| Experimental MCP tools | Marked with ⚠️ **Experimental** or 🔬 **Beta** |
| Untested edge cases | Called out explicitly — not papered over with "this should just work" |
| Security-sensitive data | Event data, PR bodies, user prompts all labeled as untrusted input |
| Cross-service dependencies | If a fix spans repos, the skill says so — does not pretend to be single-repo |

## Cross-Cutting Checks

### Consistency Between Files

| Check | What to verify |
|---|---|
| MCP names match | Same `qyl.loom.fix_issue` in `SKILL.md` and every reference |
| Argument names match | `repoRoot` in `SKILL.md` must be `repoRoot` in the reference, not `repoPath` |
| Phase numbering matches | Phase 3 in `SKILL.md` must match Phase 3 in `references/philosophy.md` |
| Non-negotiable posture matches | Identical wording in `SKILL.md` and the posture-bearing reference |

### Consistency With Existing Skills

| Check | What to verify |
|---|---|
| Frontmatter style | Same fields and order as `loom-fix-issues`, `loom-review-bot-pr`, etc. |
| Trigger-phrase style | Same "Invoke this skill when" bullet pattern |
| Hard-rules style | Bold imperative first word, one-line rationale |
| MCP surface table format | Same `\| Tool \| Purpose \|` header, same `\| Prompt \| Purpose \|` header |
| Troubleshooting format | Same `\| Issue \| Fix \|` table pattern |

### Cross-Link Accuracy

| Check | Requirement |
|---|---|
| Referenced skills exist | Every `loom-<x>` name in the skill resolves to a directory under `.claude/skills/` |
| Router entry exists | If the skill is meant to be routable, `LoomWorkflowKind` has the corresponding enum value |
| Prompt directives match | Phases listed in `SKILL.md` appear in the prompt returned by the MCP server |
| Detection commands run | `loom_detect_dotnet` arguments work against a sample project (manual spot-check) |

## Final Verification

Run these before handing the bundle to the user:

```bash
cd /Users/ancplua/qyl

# 1. All files exist
find .claude/skills/loom-<workflow> -type f | sort

# 2. Frontmatter valid — must start with ---
head -5 .claude/skills/loom-<workflow>/SKILL.md

# 3. No TODO/FIXME/XXX/HACK left behind
grep -rn "TODO\|FIXME\|XXX\|HACK" .claude/skills/loom-<workflow>/

# 4. Referenced sibling skills exist
grep -oE 'loom-[a-z-]+' .claude/skills/loom-<workflow>/SKILL.md | sort -u | while read name; do
  [ -d ".claude/skills/$name" ] || echo "MISSING: $name"
done

# 5. Every prompt id is a real registration
for prompt in $(grep -oE 'qyl\.loom\.[a-z_]+' .claude/skills/loom-<workflow>/SKILL.md | sort -u); do
  hits=$(grep -rn "\[McpServerPrompt(Name = \"$prompt\"" services/ 2>/dev/null | wc -l)
  [ "$hits" -eq 0 ] && echo "FABRICATED PROMPT: $prompt"
done

# 6. Every loom_ tool is a real registration
for tool in $(grep -oE '\bloom_[a-z_]+\b' .claude/skills/loom-<workflow>/SKILL.md | sort -u); do
  hits=$(grep -rn "\[McpServerTool(Name = \"$tool\"" services/ 2>/dev/null | wc -l)
  [ "$hits" -eq 0 ] && echo "FABRICATED TOOL: $tool"
done

# 7. Every qyl. tool is a real registration
for tool in $(grep -oE '\bqyl\.[a-z_]+\b' .claude/skills/loom-<workflow>/SKILL.md | grep -v 'qyl\.loom\.' | sort -u); do
  hits=$(grep -rn "\[McpServerTool(Name = \"$tool\"" services/ 2>/dev/null | wc -l)
  [ "$hits" -eq 0 ] && echo "FABRICATED QYL TOOL: $tool"
done

# 8. No Sentry-wizard fakery
grep -n "@sentry/wizard\|npx.*wizard\|Option 1: Wizard" .claude/skills/loom-<workflow>/SKILL.md && echo "REMOVE WIZARD FAKERY"

# 9. No forbidden C# patterns in examples
grep -rn "DateTime\.\(UtcNow\|Now\)\|\.Result\b\|\.Wait()\|#pragma warning disable\|\[SuppressMessage\]\|null!" .claude/skills/loom-<workflow>/ && echo "QYL RULE VIOLATIONS"

# 10. SKILL.md length under the cap
lines=$(wc -l < .claude/skills/loom-<workflow>/SKILL.md)
[ "$lines" -gt 200 ] && echo "SKILL.md is $lines lines — over the 200-line cap"
```

Every check that produces output is a defect. Fix the skill until all ten pass silently.

## Pre-Merge Sign-Off

The skill is ready to hand back to the user when:

- [ ] All 10 verification commands pass silently
- [ ] `SKILL.md` is 80-150 lines (200 hard cap)
- [ ] Each reference is 100-300 lines
- [ ] Every MCP tool/prompt id is grep-verified against `services/`
- [ ] Every C# example obeys the qyl hard rules (C# 14 preview, TimeProvider, no suppressions, no `null!`)
- [ ] No fake `@sentry/wizard` "Option 1: Wizard" blockquote
- [ ] `.claude/skills/SKILL.md` workflow-skills table updated
- [ ] `LoomWorkflowKind` extension flagged to the user (do not silently skip — the user must see the follow-up work)

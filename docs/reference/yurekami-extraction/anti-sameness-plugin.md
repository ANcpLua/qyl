# anti-sameness-plugin

**One-line summary:** A Claude Code plugin (pure prompt/markdown, no runtime code) that packages "Verbalized Sampling" techniques — surface the model's default response, forbid it, and generate scored alternatives — to escape LLM mode collapse across creative writing, design, brainstorming, and LLM-assisted security research.

**Stack / language:** Claude Code plugin format. 100% Markdown + JSON config (no TypeScript/Python runtime). ~1,403 lines across 18 files. Distributed via a `/plugin marketplace` (`yurekami/claude-plugins`).

---

## 1. Architecture Overview

This is a **content plugin**, not a code plugin. It has zero executable logic; all behavior is delivered as prompts that Claude Code injects into the model context. Three delivery mechanisms:

1. **Slash commands** (`commands/*.md`) — explicit user-invoked workflows (`/anti-sameness:diverge`, `:design`, `:write`, `:brainstorm`, `:vuln`, `:help`). Each is a Markdown file with YAML frontmatter (`description`) and a `$ARGUMENTS` placeholder that receives the user's task string.
2. **Skills** (`skills/*/SKILL.md`) — auto-triggered capability documents. Each has frontmatter (`name`, `description` with trigger phrases) so Claude Code activates them contextually without an explicit command.
3. **Hooks** (`hooks/hooks.json`) — a single `Notification` hook that regex-matches words like "generic|boring|predictable|typical|AI-like|sameness" and echoes a tip suggesting `/anti-sameness:diverge`.

The **conceptual core** shared by every command and skill is the **Verbalized Sampling (VS) 4-step loop**:
1. Identify the Default (surface the mode)
2. Forbid It (make the mode off-limits)
3. Generate Alternatives with **T-scores** (Typicality 0–1, where 1.0 = exactly what most AI would say)
4. Select the lowest T-score option meeting quality requirements

Commands are thin, user-facing entry points; skills are the deeper, auto-triggered knowledge bases. `commands/design.md` ≈ `skills/design-diverge/SKILL.md`, etc. — commands are the "execute now" version, skills the "reference + activate automatically" version. A second cluster (security research) composes three skills into a pipeline: `code-context-bundling` → `llm-vuln-research` → `false-positive-farming`.

---

## 2. File-by-File Map

**Config / metadata**
- `.claude-plugin/plugin.json` — Plugin manifest: name `anti-sameness`, v1.0.0, author yurekami, MIT, keywords (creativity, verbalized-sampling, mode-collapse, design, writing, brainstorming, security).
- `hooks/hooks.json` — One `Notification` hook; regex matcher on sameness-related words echoes a tip pointing to `/anti-sameness:diverge`.
- `README.md` — Full user docs: the problem (RLHF-driven mode collapse), the VS solution, install, command table, examples, skill table, T-score guide, credits (Verbalized Sampling from "Curing AI's Sameness"; design-diverge from xavierchoi; vuln methodology from Seokchan Yoon's $2,418 bounty).

**Commands** (`$ARGUMENTS`-parameterized prompt templates)
- `commands/diverge.md` — General VS: identify default → forbid → 3–5 alternatives with T-scores in a table → recommend lowest-T.
- `commands/design.md` — VS for UI/UX. Three fixed directions (A T~0.7 safe, B T~0.4 personality, C T<0.2 experimental), each specifying layout/palette(hex)/typography/distinctive element.
- `commands/write.md` — VS for prose. Three T-scored approaches + a "cliché escape reference" table (e.g. "Her heart raced" → physical specificity).
- `commands/brainstorm.md` — VS for ideation. Forbids the whole *frame/category*, not just the idea; generates across 6 frames (technical/social/temporal/economic/psychological/structural); scores T + feasibility + impact; strategy-based selection rules.
- `commands/vuln.md` — LLM vuln-research workflow: bundle code → security-researcher prompt → collect all findings (incl. false positives) → triage across runtimes/OSes. Lists quadratic-concat / unbounded-normalize / range-header patterns. XML `<findings>` output.
- `commands/help.md` — Static command + skill reference and T-score guide.

**Skills** (auto-triggered)
- `skills/verbalized-sampling/SKILL.md` — The canonical VS reference: core workflow, three prompt templates (basic distribution / default-identification / maximum-creativity), worked naming example.
- `skills/design-diverge/SKILL.md` — Design version with an explicit anti-pattern list (blue/purple gradients, 3-col grids, "Get started" CTAs) and creative-direction list (brutalist, asymmetric, editorial).
- `skills/creative-writing-diverge/SKILL.md` — Writing version: mode-collapse symptom list, domain-specific guidance (marketing/fiction/dialogue/description), cliché-escape table.
- `skills/brainstorm-diverge/SKILL.md` — Ideation version with a **Frame Library** (product/business/creative reframing prompts) and quick-divergence prompts ("What's the answer you almost didn't mention?").
- `skills/llm-vuln-research/SKILL.md` — Security methodology tied to real CVEs (CVE-2025-64458/64460 Django, CVE-2025-62727 Starlette). Full pipeline + a complete XML system/user prompt template.
- `skills/false-positive-farming/SKILL.md` — Counterintuitive two-phase triage: cheap model collects ALL candidates (incl. FPs); expensive model validates across environments. Cost-benefit table, real Django example.
- `skills/code-context-bundling/SKILL.md` — XML-bundling technique for feeding code to LLMs: 40K-token budget, functional grouping, recursive directory splitting; includes runnable Python (`tiktoken`-based counting + bundler).

---

## 3. Notable Code / Content

### (a) The T-score selection matrix — `skills/brainstorm-diverge/SKILL.md`
The single most reusable decision rule in the plugin: mapping intent to a filter over the (typicality, feasibility, impact) space.
```
| Strategy         | Selection Rule            |
|------------------|---------------------------|
| Safe innovation  | T < 0.5, Feasibility > 3  |
| Moonshot         | T < 0.2, Impact > 4       |
| Quick win        | T < 0.7, Feasibility > 4  |
| Differentiation  | Lowest T that's feasible  |
```
Turns the fuzzy "be more creative" ask into explicit, tunable thresholds — the core innovation that makes VS operational rather than aspirational.

### (b) The recursive XML bundler — `skills/code-context-bundling/SKILL.md`
Real, runnable Python. The token-budgeted greedy packer:
```python
def bundle_with_limit(directory, max_tokens=40000):
    bundles = []
    current_files = []
    current_tokens = 0
    for root, dirs, files in os.walk(directory):
        for file in files:
            if not file.endswith('.py'):
                continue
            path = os.path.join(root, file)
            with open(path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
            tokens = count_tokens(content)          # tiktoken
            if current_tokens + tokens > max_tokens and current_files:
                bundles.append(create_xml_bundle(current_files, directory))
                current_files = []
                current_tokens = 0
            current_files.append(path)
            current_tokens += tokens
    if current_files:
        bundles.append(create_xml_bundle(current_files, directory))
    return bundles
```
The 40K-token-per-bundle heuristic (stated to keep runs 1–2 min and quality high) plus XML-over-JSON rationale (no escaping, path-referenceable) is a directly liftable context-engineering pattern.

### (c) The vuln-research system prompt — `skills/llm-vuln-research/SKILL.md`
The prompt-engineering payload behind the cited CVEs. Key load-bearing clauses:
```
DEEP READING: "read the relative surrounding code carefully... You must not
  simply read top-level functions and comments and assume what the rest does."
NO PATCH: "You don't need to make a patch for it."
CONSTANTS: <IMPORTANT>DO NOT GUESS VALUES for any constants. Look them up.</IMPORTANT>
TIPS: "For DoS bugs: must be O(n^2) at minimum. Dismiss issues rooted in user config."
1-DAY EXAMPLES: Include 2-3 CVE diffs from the target project.
```
The "include 2–3 real CVE diffs from the same project to anchor the pattern and cut false positives" trick is the highest-signal reusable idea.

### (d) The false-positive-farming two-phase model — `skills/false-positive-farming/SKILL.md`
Inverts normal triage: **do not filter during collection.**
```
Phase 1: COLLECT (cheap model, broad sweep, intentionally permissive) → high noise
Phase 2: TRIAGE (expensive model, deep per-finding analysis across CPython/PyPy,
         Windows/Linux, attempts PoC) → validated findings
```
Backed by a cost table (Sonnet/GPT-4 ~$5 sweep → Opus/GPT-5 Pro ~$20–50 triage) and a concrete Django normalize_username() example where a "false positive" survived triage as a real quadratic-normalization DoS.

### (e) The cliché-escape substitution table — `commands/write.md`
```
Instead of              | Try
"Her heart raced"       | Physical specificity (sweat, breath, muscle tension)
Starting with dialogue  | Mid-action or mid-thought
Describing appearance   | Describing behavior / effect on others
Explaining motivation   | Showing contradiction
Resolving conflict      | Complicating or transforming it
```
A compact, generalizable "anti-mode" lookup — the same idea (name the default, provide the swap) applied to prose.

---

## 4. Extractable Value

- **Verbalized Sampling as a reusable prompt primitive.** The "identify default → forbid → generate T-scored alternatives → select lowest-T" loop is domain-agnostic and drops into any generation feature (naming, copy, design, config, test-case generation). The T-score (self-estimated typicality 0–1) is a cheap, model-native diversity signal.
- **Intent→threshold selection matrices.** Encoding "creative vs safe vs moonshot" as explicit `(T, feasibility, impact)` filters (§3a) is a clean way to make subjective creative asks deterministic and tunable.
- **Token-budgeted recursive XML code bundler** (§3b) — directly reusable for any RAG/code-analysis pipeline; the 40K-token unit + XML-over-JSON + functional-grouping rules are well-reasoned defaults.
- **CVE-anchored vuln-research prompt template** (§3c) — the deep-reading clause, no-patch constraint, "don't guess constants," O(n²)-minimum DoS filter, and few-shot CVE-diff anchoring form a strong reusable security-audit system prompt.
- **Two-phase "farm then triage" analysis** (§3d) — cheap-model broad recall + expensive-model precision, with cross-environment expansion (CPython/PyPy, OS variants). Generalizes to any high-noise detection task, not just security.
- **Claude Code plugin scaffolding reference.** A minimal, clean example of the plugin layout: `.claude-plugin/plugin.json` manifest + `commands/*.md` (`$ARGUMENTS`, frontmatter `description`) + `skills/*/SKILL.md` (frontmatter `name`+`description` trigger phrases) + `hooks/hooks.json` (regex `Notification` matcher → shell echo). Copyable structure for building your own plugin.
- **Anti-pattern / mode-collapse symptom checklists** (design "AI slop" list, writing cliché symptoms) — usable as lint-style negative constraints in generation prompts or as eval rubrics.

---

## 5. Build / Run Instructions

No build step — it is interpreted content loaded by Claude Code.

Install via marketplace:
```bash
/plugin marketplace add yurekami/claude-plugins
/plugin install anti-sameness@yurekami/claude-plugins
```
Local test:
```bash
claude --plugin-dir ./anti-sameness-plugin
```
Then invoke commands, e.g. `/anti-sameness:diverge a name for my coffee subscription service`, or let skills auto-trigger on matching phrasing. The one embedded Python snippet (`code-context-bundling`) requires `tiktoken` if actually run, but it is illustrative reference material, not wired into the plugin.

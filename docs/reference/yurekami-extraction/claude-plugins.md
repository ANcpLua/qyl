# claude-plugins (Yurekami's Claude Code Plugins)

**One-line summary:** A Claude Code *plugin marketplace* manifest repo — it advertises one plugin, `anti-sameness`, which fights LLM "mode collapse" using Verbalized Sampling techniques.

**Stack / language:** No application code. Pure declarative config: one JSON marketplace manifest + one Markdown README. Target platform is the Claude Code CLI plugin system (`/plugin marketplace add …`).

**Total LOC:** ~97 lines (README.md 63, marketplace.json 34).

---

## Architecture overview

This repository is *only the marketplace index*, not the plugin implementation. Claude Code's plugin system works on two levels:

1. **A marketplace repo** (this one) — contains `.claude-plugin/marketplace.json`, a registry that lists available plugins and where to fetch each one from. Users run `/plugin marketplace add yurekami/claude-plugins` to register it.
2. **The plugin repos themselves** — each `plugins[]` entry points via `source.url` to a *separate* git repo (here `https://github.com/yurekami/anti-sameness-plugin.git`) that holds the actual skills, slash commands, and hooks. That repo is **not** vendored here.

So the "code" in this repo is the wiring: a manifest that maps a plugin name → remote source → metadata (version, author, license, keywords). The README duplicates a human-readable catalog of what the referenced `anti-sameness` plugin provides (7 skills, 6 slash commands) plus a conceptual explainer of the Verbalized Sampling method.

```
claude-plugins/
├── README.md                       # Human catalog + Verbalized Sampling concept doc
└── .claude-plugin/
    └── marketplace.json            # Machine-readable marketplace registry
```

---

## File-by-file map

| Path | What it is |
|------|-----------|
| `.claude-plugin/marketplace.json` | The marketplace registry. Top-level `name`, `owner`, `metadata` (description + version), and a `plugins[]` array. The single entry `anti-sameness` uses `source: { source: "url", url: "…git" }` to point at an external plugin repo, plus `description`, `version`, `author`, `license`, and `keywords[]`. |
| `README.md` | Marketplace front page. Install commands, a command table (6 slash commands), a skills list (7 skills), and "The Core Concept" section documenting the 4-step Verbalized Sampling loop. Also has Contributing (PR into the manifest) + MIT license note. |

There is no build system, no tests, no source tree beyond these two files (`.DS_Store` is macOS cruft).

---

## Notable content (verbatim excerpts)

### 1. The marketplace registry schema — `marketplace.json:10-32`

```json
"plugins": [
  {
    "name": "anti-sameness",
    "source": {
      "source": "url",
      "url": "https://github.com/yurekami/anti-sameness-plugin.git"
    },
    "description": "Escape mode collapse with Verbalized Sampling. Includes 7 skills and 6 commands …",
    "version": "1.0.0",
    "author": { "name": "yurekami" },
    "license": "MIT",
    "keywords": ["creativity", "verbalized-sampling", "mode-collapse", "design", "writing", "brainstorming", "security"]
  }
]
```

Explanation: This is the reusable shape of a Claude Code marketplace entry. The nested `source` object with `"source": "url"` is the git-remote sourcing mode — the plugin is fetched from an arbitrary git URL rather than being co-located. This decouples the marketplace (curation/discovery) from plugin implementation (distribution), so one manifest can aggregate plugins from many owners.

### 2. The Verbalized Sampling 4-step loop — `README.md:47-55`

```
Instead of asking for ONE output from the model's collapsed mode, ask it to
describe its ENTIRE DISTRIBUTION over possible outputs with Typicality Scores (T-scores).

The 4-Step Loop:
1. Identify the Default   - State the obvious/typical response
2. Forbid It              - Make that default off-limits
3. Generate Alternatives  - Create options with T-scores (0-1)
4. Select for Creativity  - Pick lowest T-score that meets requirements
```

Explanation: This is the actual conceptual payload of the whole project and the most transportable idea here. It's a prompt-engineering technique against mode collapse: rather than sampling one modal answer, force the model to enumerate its distribution and self-score each candidate by typicality (T-score, 0–1), then deliberately select a low-typicality (novel) option that still satisfies constraints. This can be lifted directly into any prompt/skill regardless of the plugin plumbing.

### 3. Command surface documented in README — `README.md:29-46`

The referenced plugin exposes 6 namespaced slash commands (`/anti-sameness:diverge|design|write|brainstorm|vuln|help`) and 7 auto-triggered skills (`verbalized-sampling`, `design-diverge`, `creative-writing-diverge`, `brainstorm-diverge`, `llm-vuln-research`, `code-context-bundling`, `false-positive-farming`). Note: these are *documented* here but *implemented* in the external `anti-sameness-plugin` repo — to extract their logic you must clone that repo.

---

## Extractable value

- **Marketplace manifest pattern.** `.claude-plugin/marketplace.json` is a clean, minimal template for standing up your own Claude Code plugin marketplace — copy it, swap `name`/`owner`, and add `plugins[]` entries. The `source: { source: "url", url }` form lets you federate plugins from external git repos without vendoring them.
- **Verbalized Sampling prompt technique.** The 4-step loop (Identify default → Forbid it → Generate T-scored alternatives → Select lowest-typicality-that-fits) is a self-contained, provider-agnostic anti-mode-collapse prompt strategy. Directly reusable in any skill, system prompt, or agent that suffers from generic/repetitive output — including qyl-adjacent tooling that generates copy, designs, or test ideas.
- **Skill+command naming convention.** The `namespace:command` slash-command scheme and the split between auto-triggered *skills* vs explicit *commands* is a good organizing model for anyone authoring their own Claude Code plugin.
- **Not extractable from here:** the actual skill/command implementations, hooks, or T-score generation logic — those live in the external `anti-sameness-plugin.git` repo and are absent from this manifest-only repository.

---

## Build / run instructions

No build. Consumption is via the Claude Code CLI:

```bash
# Register the marketplace
/plugin marketplace add yurekami/claude-plugins

# Install the plugin (pulls the external anti-sameness-plugin repo)
/plugin install anti-sameness@yurekami/claude-plugins
```

Contributing = open a PR adding a new object to the `plugins[]` array in `.claude-plugin/marketplace.json`. License: MIT.

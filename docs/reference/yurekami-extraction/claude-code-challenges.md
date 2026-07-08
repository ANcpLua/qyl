# claude-code-challenges

**One-liner:** A LeetGPU-style, gamified challenge set that teaches and grades proficiency with the Claude Code CLI (file tools, git, MCP, multi-agent workflows), paired with a retro "arcade cabinet" static web UI hosted on GitHub Pages.

**Stack / language:** Markdown challenge specs + Python 3 test/scoring scripts + TypeScript starter fixtures + a vanilla HTML/CSS/JS single-page "arcade" UI (no build step, no framework, no runtime deps). ~6.2k LOC total.

---

## 1. Architecture overview

Three loosely-coupled layers, no server-side runtime:

1. **Challenge content** (`easy/`, `medium/`, `hard/`) — each challenge is a self-contained directory: a `challenge.md` spec (problem, objectives, constraints, hints, scoring rubric), an optional `starter/` fixture tree with deliberately planted bugs/facts, and a `tests.py` self-grading script that emits a `TOTAL SCORE: X/100` and exits non-zero below a pass threshold (75%). There is no `solution/` committed (hidden by design).
2. **Format contract** — `CHALLENGE_FORMAT.md` is the single spec that every challenge's `.md` and `tests.py` conform to (section headings, scoring split 40/20/20/20, output banner format, pass thresholds). It is effectively the schema.
3. **Arcade UI** (`docs/`) — a static two-page site (index = cartridge select screen, challenge.html = detail/play screen) served via GitHub Pages. Pure DOM manipulation, Web Audio API sound synthesis, `localStorage` persistence, keyboard nav, CRT/scanline CSS aesthetic. The UI is a *presentation shell*: challenge metadata is hard-duplicated into `docs/js/challenge.js` (it does not read the `challenge.md` files or run `tests.py`); "verification" in the UI is simulated.

Key architectural seam: the grading logic (Python) and the presentation (JS) are independent and duplicate the challenge catalog. The Python side is the real grader; the JS side is a demo/marketing shell.

---

## 2. File-by-file map

### Root
- `README.md` — catalog: 13 challenges across 3 difficulties, category/skill matrix, scoring system, quick-start (`cd` into challenge, run `python tests.py`). CC BY-NC-SA 4.0.
- `CHALLENGE_FORMAT.md` — the authoring spec: directory layout, `challenge.md` section template, `tests.py` template, category enum, difficulty guidelines (Easy 100pt/5-10min, Medium 150pt, Hard 250pt), scoring %s, pass thresholds, canonical test-output banner.

### Easy challenges (single-skill, 100 pts)
- `easy/01-file-explorer/` — Glob/Read/Grep drill. `tests.py` grades a participant-produced `results.json` against a hardcoded `EXPECTED` dict (list of `.ts` files, `calculateTotal` location `math.ts:42`, DB config, line count 156). `starter/` is a mock TS project (`src/`, `config/database.json`, planted facts).
- `easy/01-.../starter/src/utils/math.ts` — fixture with `calculateTotal` deliberately at line 42 (the answer the challenge asks for).
- `easy/02-quick-commit/` — conventional-commit + Co-Authored-By footer drill.
- `easy/03-token-check/` — `/usage` + `/compact` context-management drill.
- `easy/04-simple-edit/` — Edit-tool precision drill; `starter/app.ts` has 4 planted defects (typo `usrName`, wrong return type `any`, untyped param, import) to fix with exact-match edits.
- `easy/05-search-master/` — Grep/Glob output-mode drill (TODO/async/imports/count).

### Medium challenges (multi-skill, 150 pts)
- `medium/01-pr-creator/` — full branch→commit→`gh` PR workflow (has `tests.py`).
- `medium/02-context-handoff/` — session handoff / context serialization (spec only).
- `medium/03-parallel-search/` — Task-tool multi-agent orchestration: launch parallel security/perf/quality scan agents and aggregate (spec only).
- `medium/04-config-detective/` — diagnose broken Claude Code setup (spec only).
- `medium/05-refactor-safely/` — multi-file refactor with safety (spec only).

### Hard challenges (autonomous, 250 pts)
- `hard/01-full-feature-flow/` — end-to-end feature dev (has `tests.py`).
- `hard/02-mcp-orchestra/` — coordinate 4 MCP servers (GitHub→Memory→Filesystem + Sequential Thinking) into a doc-automation pipeline; includes an ASCII workflow diagram (spec only, no tests.py).
- `hard/03-autonomous-debug/` — self-directed debugging of a planted floating-point bug in a cart discount module. Richest `tests.py`: 6 weighted checks over a produced `debug_report.md` + the fixed source + an added test.
- `hard/03-.../starter/src/cart/discount.ts` — the buggy fixture: `applyDiscount` does `total * (value/100)` with no rounding (comments explicitly flag the FP bug at the two bug sites).
- `hard/03-.../starter/src/cart/cart.test.ts` — Vitest/Jest-style test file the participant must extend.

### Arcade UI (`docs/`)
- `docs/index.html` — cartridge-select screen: header score/lives, difficulty tabs, cartridge grid, CRT/scanline container.
- `docs/challenge.html` — challenge detail/play screen: mission, objectives, constraints, hints, terminal output, victory modal.
- `docs/js/app.js` — the arcade engine: `GameState` (localStorage save/load, score, lives, completed set), `SoundFX` (Web Audio oscillator synthesis for select/move/locked/complete jingles), score-count easing animation, pixel-explosion particle effect, tab nav, keyboard nav, progressive unlock logic (3 easy → medium, 3 medium → hard).
- `docs/js/challenge.js` — detail-page controller: hardcoded `CHALLENGES` catalog (duplicate of the `.md` metadata), URL-param routing, live timer, hint reveal w/ penalty, simulated verify → victory modal with score breakdown.
- `docs/css/styles.css` / `docs/css/challenge.css` — retro arcade styling (Press Start 2P / VT323 fonts, neon palette, CRT effects). ~1.2k LOC combined.

---

## 3. Notable code

### a) Rubric-as-weighted-predicates grader — `hard/03-autonomous-debug/tests.py:11`
The most interesting grading pattern: each test function is a list of `(predicate, points, label)` tuples summed into a partial score. Robust to phrasing because it greps produced artifacts for concept keywords rather than exact matches.
```python
def test_bug_identified():
    content = Path("debug_report.md").read_text().lower()
    indicators = [
        ("floating" in content and "point" in content, 15, "floating point issue mentioned"),
        ("precision" in content, 10, "precision mentioned"),
        ("round" in content, 10, "rounding solution mentioned"),
        ("discount.ts" in content, 5, "correct file identified"),
    ]
    score = 0
    for condition, points, description in indicators:
        if condition:
            print(f"PASS: {description} (+{points})")
            score += points
    return score
```

### b) Fix-detection by regex family — `hard/03-autonomous-debug/tests.py:82`
Grades *that a correct fix exists* by matching any of several idiomatic rounding patterns in the edited source, with a partial-credit fallback.
```python
fix_patterns = [
    (r"Math\.round\(.+\*\s*100\)\s*/\s*100", "Math.round pattern"),
    (r"\.toFixed\(2\)", "toFixed(2) pattern"),
    (r"Number\(.+\.toFixed", "Number(toFixed) pattern"),
]
for pattern, description in fix_patterns:
    if re.search(pattern, content):
        print(f"PASS: Fix implemented using {description}")
        return 50
if "round" in content.lower() or "toFixed" in content:
    return 25   # partial: attempted but unverified
```

### c) Planted-bug fixture with self-documenting bug sites — `hard/03-.../starter/src/cart/discount.ts:33`
The fixture teaches by annotating exactly where the defect lives — the challenge is diagnosis+fix, not blind hunting.
```typescript
if (discount.type === 'percentage') {
  // BUG: Floating point precision issue here
  // This can result in values like 79.99999999 or 80.00000001
  discountAmount = total * (discount.value / 100);
}
...
// BUG: Not rounding the final result
return total - discountAmount;
```

### d) Web Audio procedural sound effects — `docs/js/app.js:97`
No audio assets — every SFX is synthesized live from oscillator + gain envelopes; the "complete" case sequences a 4-note C-E-G-C victory arpeggio.
```javascript
case 'complete':
  const notes = [523, 659, 784, 1047]; // C, E, G, C
  notes.forEach((freq, i) => {
    const osc = this.audioContext.createOscillator();
    const gain = this.audioContext.createGain();
    osc.connect(gain); gain.connect(this.audioContext.destination);
    osc.frequency.setValueAtTime(freq, this.audioContext.currentTime + i * 0.15);
    gain.gain.setValueAtTime(0.2, this.audioContext.currentTime + i * 0.15);
    gain.gain.exponentialRampToValueAtTime(0.01, this.audioContext.currentTime + i * 0.15 + 0.3);
    osc.start(this.audioContext.currentTime + i * 0.15);
    osc.stop(this.audioContext.currentTime + i * 0.15 + 0.3);
  });
```

### e) Progressive unlock gate — `docs/js/app.js:398`
Client-side course-gating: medium unlocks after 3 easy clears, hard after 3 medium — pure `localStorage` set arithmetic, no backend.
```javascript
const easyCompleted = [...GameState.completedChallenges]
  .filter(id => id.startsWith('0') && parseInt(id) <= 5).length;
if (easyCompleted >= 3) { /* strip .locked from medium cartridges */ }
```

### f) Score-counter easing — `docs/js/app.js:155`
`requestAnimationFrame` tween with `easeOutQuart`, zero-padded 8-digit arcade display.

---

## 4. Extractable value

- **Rubric-as-weighted-predicate autograder (§3a/§3b).** A framework-free pattern for grading *open-ended LLM/agent output*: sum `(predicate, points, label)` tuples over produced artifacts, grep for concepts not exact strings, and give partial credit via a regex-family fallback. Directly reusable for evals/CI gates on agent deliverables (e.g. "did the agent's report mention root cause AND ship a rounding fix?"). Maps cleanly onto an LLM-as-judge or deterministic-eval harness.
- **Self-contained challenge directory convention (§2).** `challenge.md` (spec) + `tests.py` (grader) + `starter/` (fixture) + hidden `solution/`, all conforming to one `CHALLENGE_FORMAT.md` schema, with a standardized score banner + non-zero exit below threshold. A drop-in template for building any hands-on skills-assessment or interview-kata repo.
- **Planted-bug fixtures with annotated bug sites (§3c).** Teaching fixtures that comment exactly where the defect is, converting "find the needle" into "diagnose + fix + explain" — better pedagogy and far more deterministic to grade.
- **Zero-dependency arcade UI kit (§3d–f).** Reusable vanilla-JS building blocks: procedural Web Audio SFX (no asset files), `requestAnimationFrame` easing tweens, particle/pixel-explosion effect, `localStorage`-backed game state with progressive unlock gating, and CRT/scanline CSS. Liftable into any gamified static site.
- **Static-site GitHub Pages gamification shell** — the `docs/` split (select screen + detail screen, URL-param routing, simulated verify → victory modal) is a template for turning any content catalog into a "play" experience with no backend.

Caveat worth noting for reuse: the challenge catalog is duplicated between `docs/js/challenge.js` and the `challenge.md` files (drift risk). A single-source improvement would be to generate the JS catalog from the Markdown front-matter.

---

## 5. Build / run instructions

- **Run a challenge (grading):** `cd easy/01-file-explorer`, read `challenge.md`, do the work with Claude Code, then `python tests.py` (Python 3, stdlib only — `json`, `re`, `pathlib`; no pip installs). Exit 0 iff score ≥ 75% (Hard uses ≥ 187/250).
- **UI (local):** open `docs/index.html` directly, or serve `docs/` (`python -m http.server` from `docs/`). No build step — plain HTML/CSS/JS. Uses Google Fonts + `localStorage`; audio inits on first user click (autoplay policy).
- **UI (hosted):** GitHub Pages serves the `docs/` folder (see commit `ede9cea chore: move ui to docs for GitHub Pages`).
- **TS fixtures** are illustrative only — no `package.json`/`tsconfig` at root; they are read/edited as challenge material, not compiled by the repo itself.

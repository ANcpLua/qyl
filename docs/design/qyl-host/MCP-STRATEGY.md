# Qyl MCP — reaching and passing Sentry MCP

> **Audience:** a high-capability agent picking up the qyl MCP work. This is a
> directive, not a checklist — the sequencing inside each bet is yours. Every
> claim about Sentry MCP is grounded in its source at
> `~/RiderProjects/qyl-references/sentry-mcp` (cited); every claim about qyl is
> from the repos in this workspace (incl. `mcp-run`, moved in from
> `~/Desktop` 2026-07-11).

## The one-line strategy

**Sentry MCP is middleware to a proprietary SaaS. Qyl owns the whole stack.**
Sentry MCP is text-only, and its real intelligence (Seer) lives server-side,
out of the MCP's reach. Do **not** clone it. Beat it on the two axes it
structurally cannot follow from an API-middleware position:

1. a **native visual MCP-Apps surface** (interactive UI rendered *in the chat*),
2. a **self-hosted, self-instrumenting, polyglot host** that *is* the
   autoinstrumentation engine.

Reach parity on its genuine engineering strengths (below); win on those two.

## What Sentry MCP actually is (the target, grounded)

- **Remote Cloudflare Worker**, full OAuth 2.0, middleware to the Sentry API
  (`packages/mcp-cloudflare/src/server/index.ts`). Also an npm `stdio` transport
  (`@sentry/mcp-server`).
- **~44 tools, but only ~9 exposed top-level** (`tools/surfaces.ts:17-26`); the
  rest are reached via a `search_sentry_tools` + `execute_sentry_tool` catalog.
  Hard cap ≤25 tools — deliberate tool-slot economy.
- **5 skills** — `inspect` (read), `seer`, `docs` (deprecated), `triage`,
  `project-management` (`skills.ts:27-67`). Skill→scope authorization: users
  grant capabilities, Sentry OAuth scopes are *derived* (`skills.ts:167-198`),
  tools filtered at registration (`server.ts:219-235`), selectable per-connection
  (`?skills=`, `?disable-skills=`).
- **Seer is NOT in the repo.** `analyze_issue_with_seer` is a thin polling client
  to Sentry's remote autofix API — `startAutofix()` → `POST /autofix/`, poll every
  5s to a 5-min cap (`api-client/client.ts:4036-4082`, `tool-helpers/seer.ts`).
  The root-cause LLM work runs on Sentry's servers. **This is their moat and their
  dependency.** Self-hosted Sentry can't use it (`--disable-skills=seer`).
- **Embedded agents** translate NL → Sentry query syntax (`search_events`,
  `search_issues`), grounded in **~74 bundled static OTel semconv JSON files**
  (`internal/agents/tools/data/`), Zod-validated structured output, `stepCountIs(5)`
  cap, a `rescueFromText` fallback for when structured generation fails
  (`callEmbeddedAgent.ts`). A `use_sentry` meta-agent wraps the whole catalog via
  an in-memory MCP (`tools/special/use-sentry/`).
- **No MCP Apps / interactive UI.** Confirmed: no `ui://`, no `text/html`
  templates, no ext-apps. Tools return markdown/structured text only. The
  `mcp.sentry.dev` chat is a separate React site, not MCP-protocol UI.

### Steal these (Sentry's real engineering strengths → qyl parity gaps)
- Tool-slot economy (search/execute catalog).
- Skill→scope authorization decoupled from raw tokens.
- Schema-validated embedded-agent output **with** a prose-rescue fallback.
- Error taxonomy: tool errors returned as `isError:true` text, never thrown;
  4xx never logged, 5xx always (`server.ts:421-444`).
- A real **eval harness** (23 `*.eval.ts`, `ToolPredictionScorer`) gating tool
  quality + a token-budget check per tool change.
- `gen_ai.*` self-instrumentation on every tool call.

## Qyl's assets today (the inventory you're building on)

- **`qyl-apps-server`** — MCP Apps server: interactive trace/log **waterfall UI**
  + MCP dashboard (Sentry-widget grid) rendered *in the conversation* via
  `@modelcontextprotocol/ext-apps`. Successor to the deleted `services/qyl.mcp`.
  **This is the thing Sentry MCP does not have.**
- **`mcp-run`** (in this workspace) — polyglot MCP host; `/runner/mcp` passthrough +
  `telemetry.ts` host-side OTLP self-monitoring that already calls itself *"the
  qyl-based answer to Sentry's MCP monitoring product."* Emits `mcp.tool.name` +
  `gen_ai.tool.name` to the qyl collector.
- **`services/qyl.collector`** — OTLP ingest, REST API, DuckDB storage. The
  backend Sentry-MCP would have to call a SaaS for; qyl has it locally.
- **First-party OTel semantic conventions** — `Qyl.OpenTelemetry.SemanticConventions`
  (Weaver-generated, always current) + the genai registry. Sentry *bundled ~74
  static JSON files*; qyl **generates** them. Exploit this.
- **`Qyl.Host` design** ([DESIGN.md](./DESIGN.md)) — the polyglot engine that
  `mcp-run` folds into; MCP as a strategy, not the substrate.

## Parity checklist (table stakes — close these)

1. **Tool-slot economy.** Do not dump every tool into `tools/list`. Curate a
   small top-level set + a search/execute catalog. Enforce a budget.
2. **NL→query embedded agent — and do it BETTER than Sentry.** Same idea
   (translate NL → qyl's query/trace API), but ground it in qyl's **first-party
   Weaver-generated semconv**, not a bundled static copy. Sentry's field grounding
   rots; qyl's regenerates. This is a strength to weaponize, not merely match.
3. **Remote transport + auth, hosted.** qyl already runs on Railway — ship a
   hosted remote MCP endpoint with OAuth/token auth, not just local stdio. Mirror
   Sentry's dual-auth (OAuth `Bearer` + a direct token header) and per-connection
   skill selection.
4. **Skill→capability authorization** over qyl's own surface (read / triage /
   admin), filtered at registration.
5. **Eval harness + error taxonomy.** Eval-gate tool quality before shipping;
   adopt the 4xx-never-logged / 5xx-always discipline and structured-output rescue.

## The two "above" bets (where you win, not tie)

### Bet 1 — Visual root cause: "Seer, but you can see it"
Sentry's Seer returns **text**, runs on **their servers**, and is **absent from
self-hosted**. Qyl holds the full trace/span/error graph locally *and* has an
MCP-Apps rendering layer. Build a root-cause agent over qyl's own data that
returns an **interactive waterfall with the culprit span/error highlighted**, the
linked logs inline, and a fix suggestion — rendered in the chat via
`qyl-apps-server`. Self-hosted (no autofix-SaaS dependency) and visual (which a
text MCP structurally cannot be). This is the single highest-leverage
differentiator: it attacks Sentry's moat on the one axis they can't defend.

### Bet 2 — The self-instrumenting polyglot host = the autoinstrumentation engine
`telemetry.ts` already proves the thesis: *instrument the host, not the client.*
Fold `mcp-run` into `Qyl.Host` so the same server that answers questions **also
auto-instruments every process/MCP it launches, in any language,** emitting to the
qyl collector. Sentry MCP monitors Sentry; `Qyl.Host` monitors everything it
hosts. That is the "MCP monitoring" product Sentry ships — generalized, polyglot,
and first-party. Ties directly to the `AddExecutable` + `IReadinessProbe` step in
DESIGN.md: readiness-probe pluggability is what lets one host supervise an HTTP
collector *and* a stdio MCP *and* a Python client under one trace.

## Sequencing (order within each is yours)

1. Consolidate the MCP surface onto `Qyl.Host` + `qyl-apps-server`; close the
   tool-economy + authz + eval parity gaps.
2. Ship the hosted remote+auth endpoint on Railway.
3. Build the NL→query embedded agent on first-party semconv.
4. **Bet 1** (visual root cause) — the flagship demo.
5. **Bet 2** (self-instrumenting host) — the platform play.

Each step is independently demoable. Don't batch them.

## Guardrails

- **Reuse qyl's first-party semconv** — never bundle a static copy the way Sentry
  had to.
- **Respect the AOT/verifier gates** in `Qyl.OpenTelemetry.AutoInstrumentation`
  (the `InstrumentationContract` counts are a deliberate build tripwire — don't
  "simplify" them away).
- **Every data tool gets a visual variant.** The MCP-Apps surface is the
  differentiator; text-only output forfeits the advantage.
- **Eval-gate before shipping**, and keep tool count under a hard budget.
- Don't develop the X product (`x-apps-server` is architectural reference only).

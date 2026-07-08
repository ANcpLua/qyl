# agent-trace

**One-line summary:** A dependency-free TypeScript reference implementation of the *Agent Trace v0.1.0* spec — records which lines of code were written by AI vs. humans, using git-diff line-range attribution, MurmurHash3 content hashing, and Claude Code lifecycle hooks.

**Stack / language:** TypeScript (CommonJS, `type: "commonjs"`), Node ≥ built-ins only. Dev deps only: `typescript ^5.9.3`, `@types/node ^25.2.0`. **Zero runtime dependencies** — uses only `child_process`, `crypto`, `fs`, `path`.

**Approx LOC:** ~985 source lines (`trace-hook.ts` 541 + `trace-store.ts` 444); ~1210 with README.

---

## 1. What it is / architecture overview

Agent Trace is an open, vendor-neutral spec for recording AI code contributions alongside human authorship in a VCS-tracked repo. This repo is the reference TS implementation with two layers:

```
┌─────────────────────────────────────────────────────────┐
│  trace-hook.ts  (integration / capture layer)           │
│   • Git integration (execSync wrappers)                 │
│   • Unified-diff parser  → added line ranges            │
│   • TraceHook class: session lifecycle + pending buffer │
│   • Claude Code hook factories (PostToolUse / Stop)     │
└───────────────────────────┬─────────────────────────────┘
                            │ imports types + helpers
┌───────────────────────────▼─────────────────────────────┐
│  trace-store.ts  (data model + persistence layer)       │
│   • Type definitions (TraceRecord, Conversation, …)     │
│   • MurmurHash3 32-bit content hashing                  │
│   • Runtime validation (type-guard tree)                │
│   • TraceStore: CRUD over .agent-trace/{uuid}.json      │
└─────────────────────────────────────────────────────────┘
```

**Data model hierarchy:** `TraceRecord` → `FileTrace[]` (per file `path`) → `Conversation[]` (a contribution episode, optional `url`/`contributor`) → `Range[]` (1-indexed `start_line`/`end_line` + optional `content_hash` + optional per-range `contributor`).

**Flow:** `onSessionStart()` snapshots the initial git revision → each `onFileWrite/Edit/Change` builds a `Conversation` (diffing against that initial revision or using explicit ranges) and buffers it in a per-file `pendingChanges` map → `onSessionEnd()` flushes the buffer into the trace's `files` and persists one self-contained JSON per trace. Everything is immutable (spread-copy on every mutation).

---

## 2. File-by-file map

| File | What it does |
|------|--------------|
| `src/trace-store.ts` | Data model + persistence. Defines all interfaces (`TraceRecord`, `FileTrace`, `Conversation`, `Range`, `Contributor`, `VCSInfo`, `ToolInfo`), constants (`AGENT_TRACE_VERSION`, MIME type, `CLAUDE_CODE_TOOL`). Implements a from-scratch **MurmurHash3 32-bit** hash → `computeContentHash` (`murmur3:{hex8}`). Factory helpers `createContributor`/`createRange` with bounds validation. A full **runtime validation** type-guard tree (`validateTraceRecord` + `isValid*` guards). `TraceStore` class: `createTrace`, `addFileTrace`, `addConversation`, `save`/`load`/`list`/`delete`, `findByRevision`, `findByFile` over `.agent-trace/{uuid}.json`. |
| `src/trace-hook.ts` | Capture/integration. Git wrappers (`execGit`, `getGitRoot`, `getCurrentRevision`, `getChangedFiles`, `getStagedFiles`, `getChangedLineRanges`, `getFileContent`). **Diff parser** (`parseHunkHeaders`, `parseUnifiedDiff`, `consolidateRanges`). `TraceHook` class: session lifecycle (`onSessionStart/End`), file capture (`onFileChange/Write/Edit`), queries (`getAttributionForFile/Line`), batch `captureCommit`, `flush`. Claude Code hook factories (`createPostToolUseHook`, `createStopHook`) + `createTraceHook`. Internal helpers `normalizePath`, `buildConversation`. |
| `README.md` | Spec overview, API reference tables, example trace JSON, persistence layout. |
| `package.json` | Minimal; no build/test wired (`test` is a stub). Dev-only TS toolchain. |

---

## 3. Notable code

### 3a. Self-contained MurmurHash3 (32-bit x86) — `trace-store.ts:74`
A textbook, dependency-free port. Uses `Math.imul` for 32-bit multiply overflow correctness and `>>> 0` to stay unsigned. Chosen over crypto hashes because content hashing here is for *change-detection / dedup*, not security — fast and compact.

```ts
function murmurHash3_32(key: string, seed: number = 0): number {
  const data = new TextEncoder().encode(key)
  const len = data.length, nblocks = Math.floor(len / 4)
  let h1 = seed >>> 0
  const c1 = 0xcc9e2d51, c2 = 0x1b873593
  for (let i = 0; i < nblocks; i++) {          // body
    const o = i * 4
    let k1 = data[o] | (data[o+1]<<8) | (data[o+2]<<16) | (data[o+3]<<24)
    k1 = Math.imul(k1, c1); k1 = (k1<<15)|(k1>>>17); k1 = Math.imul(k1, c2)
    h1 ^= k1; h1 = (h1<<13)|(h1>>>19); h1 = Math.imul(h1,5)+0xe6546b64
  }
  let k1 = 0                                    // tail (fall-through switch)
  switch (len & 3) {
    case 3: k1 ^= data[nblocks*4+2] << 16
    case 2: k1 ^= data[nblocks*4+1] << 8
    case 1: k1 ^= data[nblocks*4]
      k1 = Math.imul(k1,c1); k1=(k1<<15)|(k1>>>17); k1=Math.imul(k1,c2); h1 ^= k1
  }
  h1 ^= len; h1 ^= h1>>>16; h1 = Math.imul(h1,0x85ebca6b)   // fmix
  h1 ^= h1>>>13; h1 = Math.imul(h1,0xc2b2ae35); h1 ^= h1>>>16
  return h1 >>> 0
}
```

### 3b. Unified-diff hunk parser + range consolidation — `trace-hook.ts:123` & `:184`
Parses `@@ -a,b +c,d @@` headers, skips pure-deletion hunks (`count === 0`), then merges adjacent/overlapping ranges. `parseUnifiedDiff` splits a multi-file diff on `diff --git` and extracts the b-side path. This is the core attribution primitive.

```ts
const hunkRe = /^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,(\d+))?\s+@@/gm
// ...
if (current.start <= last.end + 1) {   // overlapping OR adjacent → extend
  merged[merged.length-1] = { start: last.start, end: Math.max(last.end, current.end) }
}
```

### 3c. Runtime validation type-guard tree — `trace-store.ts:259`
A hand-rolled schema validator returning TS type predicates (`record is TraceRecord`), composed bottom-up (`isValidContributor` → `isValidRange` → `isValidConversation` → `isValidFileTrace` → `validateTraceRecord`). No JSON-schema library. `load()` runs this on every deserialize and returns `null` on any invalid/corrupt file — fail-safe persistence.

### 3d. Immutable pending-changes buffer + flush — `trace-hook.ts:427`
Every mutation reallocates (`this.pendingChanges = new Map(this.pendingChanges)`), and `flush()` folds buffered conversations into `currentTrace.files`, appending to an existing `FileTrace` or creating one. Pure-functional style throughout — no in-place mutation of records.

### 3e. Claude Code hook adapters — `trace-hook.ts:470`
Thin factories mapping Claude Code's `PostToolUse`/`Stop` events onto the hook API — `Write` → `onFileWrite` (whole file attributed AI), `Edit` → `onFileChange` (git-diff ranges), `Stop` → `onSessionEnd` (flush+save). Clean example of adapting an agent's tool-event stream into a domain model.

---

## 4. Extractable value

- **Dependency-free MurmurHash3 (32-bit)** (`trace-store.ts:74`) — drop-in, correct `Math.imul`/unsigned handling; reusable anywhere you need fast non-crypto content fingerprints (cache keys, change detection, dedup). Pairs with the `murmur3:{hex8}` tagged-hash convention.
- **Unified-diff parser + range consolidation** (`trace-hook.ts:123-210`) — standalone functions (`parseUnifiedDiff`, `parseHunkHeaders`, `consolidateRanges`) that turn `git diff` output into per-file added-line ranges. Directly reusable for any line-level attribution/coverage/blame tooling. The adjacent-range merge (`start <= end + 1`) is a clean interval-coalescing utility.
- **Type-guard validation tree** (`trace-store.ts:188-277`) — pattern for JSON-schema-free runtime validation returning TS predicates; `isNonNullObject` + composed guards. Good template for validating untrusted persisted/wire data without a library.
- **Git-shell wrapper pattern** (`trace-hook.ts:49`) — `execGit` returns `string | null` (null on failure via try/catch), making all git calls total functions. Nice defensive idiom for shelling out.
- **Agent-event → domain-model adapter** (`trace-hook.ts:470`) — the hook-factory shape (event name + handler closure over a stateful capturer) is a transferable pattern for wiring any agent's tool-use stream into observability/attribution. **Directly relevant to qyl-style agent observability**: line-level AI-vs-human provenance keyed to git revisions is a telemetry dimension the collector could ingest.
- **Immutable-append persistence model** — one self-contained JSON per record (UUID-named), enabling contention-free parallel writes and trivial archival/audit. The whole `TraceStore` CRUD-over-directory pattern is liftable.
- **The Agent Trace v0.1.0 data schema itself** — `TraceRecord/FileTrace/Conversation/Range/Contributor` with `model_id` (models.dev convention), contributor types (`human|ai|mixed|unknown`), MIME `application/vnd.agent-trace.record+json`. A vendor-neutral vocabulary for AI-contribution provenance worth aligning telemetry to.

---

## 5. Build / run

No build or test is wired (`package.json` `test` is a stub; no `tsconfig.json` present in tree, no compiled output). To use:

```bash
npm install                 # installs TS + @types/node (dev only)
# consume the .ts sources directly via ts-node / tsx, or add a tsconfig and `tsc`
```

Import from source directly (per README):
```ts
import { TraceStore, createContributor, createRange } from './src/trace-store'
import { createTraceHook, createPostToolUseHook, createStopHook } from './src/trace-hook'
```

Traces persist to `.agent-trace/{uuid}.json` (dir configurable via `TraceHookOptions.storageDir` / `TraceStore` ctor). No CLI/entrypoint — it's a library. Requires a working `git` binary on PATH for the hook/diff features (store layer works without git).

**Notable gaps:** `generateId` (`trace-hook.ts:516`) and several imported git helpers are unused/dead; there are no tests despite the validation-heavy design; no `tsconfig.json` shipped.

# TypeScript type-check escape audit

- Audited source commit: `55de5b0f8bf7090fb63ac368c7922b0473bd1988`
- Audited source tree: `4b34693d44dd445294ee72d43eedbcf2335aeb4f`

The audit reads tracked Git blobs from that commit. It does not inspect the
caller's working tree, so the inventory is reproducible even when the checkout
contains unrelated changes.

## Reproduction

Restore the TypeScript 5.9.3 runtime used by the auditor, then run the committed
Git-ref scanner:

```bash
npm ci --prefix services/qyl.dashboard

node eng/scripts/typescript-type-escape-audit.mjs \
  --ref 55de5b0f8bf7090fb63ac368c7922b0473bd1988 \
  --format json \
  > /tmp/qyl-typescript-type-escape-audit.json
```

Human-readable summary and complete site inventory:

```bash
node eng/scripts/typescript-type-escape-audit.mjs \
  --ref 55de5b0f8bf7090fb63ac368c7922b0473bd1988 \
  --format summary

node eng/scripts/typescript-type-escape-audit.mjs \
  --ref 55de5b0f8bf7090fb63ac368c7922b0473bd1988 \
  --format tsv
```

The scanner resolves the ref to a commit and tree, reads files with Git, records
its own SHA-256, verifies that the loaded TypeScript version matches both
frontend manifests and lockfiles, and emits no timestamp. Two JSON runs over the
same ref are byte-identical.

These lexical searches independently corroborate explicit double/`any` escapes
and suppression directives. The compiler-API result remains authoritative:

```bash
rg -n --pcre2 \
  '\bas\s+(?:unknown|any)\s+as\b|\bas\s+any\b|<\s*any\s*>|(?::|=|\||&|,|\()\s*any(?:\[\])?\b' \
  services packages eng tests \
  -g '*.{ts,tsx,mts,cts}' \
  -g '!**/node_modules/**' \
  -g '!**/dist/**' \
  -g '!**/coverage/**' \
  -g '!**/generated/**'

rg -n '@ts-(ignore|expect-error|nocheck)\b' \
  services packages eng tests \
  -g '*.{ts,tsx,mts,cts}' \
  -g '!**/node_modules/**' \
  -g '!**/dist/**' \
  -g '!**/coverage/**' \
  -g '!**/generated/**'
```

## Scope and method

The audit scanned all 64 tracked first-party `.ts`, `.tsx`, `.mts`, and `.cts`
files, including the two `.d.ts` files, under `eng`, `packages`, `services`, and
`tests`. Dependency, generated, build, coverage, Playwright-report, and test-result
directories were excluded. TypeScript's compiler API enumerated:

- `AsExpression` and angle-bracket type-assertion nodes;
- nested `as unknown as ...` chains;
- non-null assertions;
- every explicit `any` keyword;
- `@ts-ignore`, `@ts-expect-error`, and `@ts-nocheck` in comment trivia.

Both frontend projects declare and lock TypeScript 5.9.3. Their tracked
`tsconfig.json` files enable `strict` and set `skipLibCheck: true`. Qyl usage
sites remain checked against dependency declarations, but declaration-file
internals are not checked. An internally inconsistent dependency `.d.ts` can
therefore propagate an unsound declared type without a diagnostic; this is an
accepted build-time trade-off and a residual risk.

Severity rubric:

- **high** — unvalidated external input enters generated-contract or data-plane
  state;
- **medium** — production code bridges unrelated types, erases a generated
  union, or admits an unvalidated runtime string into a closed union;
- **low** — test-only, a library/DOM boundary with a local failure radius, or an
  immediate and loud startup failure.

No current finding is high severity. The earlier Host Console SSE issue is
resolved at this snapshot: `useLogs.ts:23` and `useResources.ts:30` pass parsed
JSON to `parseLogLine` and `parseResourceState`; generated-schema AJV validation
and rejection live at `packages/Qyl.Host.Console/src/contract-validation.ts:8-32`.

## Inventory and disposition

The scanner found:

- 49 type-assertion syntax nodes: 32 in product code and 17 in tests;
- 5 `as unknown as ...` chains, represented by 10 nested assertion nodes;
- 9 non-null assertions;
- 0 explicit `any` keywords;
- 0 `@ts-ignore`, `@ts-expect-error`, or `@ts-nocheck` directives.

Every one of the 58 assertion and non-null nodes was reviewed exactly once:

| Disposition | Type assertions | Non-null | Combined |
| --- | ---: | ---: | ---: |
| Findings AP-01–AP-06 | 27 | 1 | 28 |
| Controlled or justified | 22 | 8 | 30 |
| Unreviewed remainder | 0 | 0 | 0 |
| **Total** | **49** | **9** | **58** |

Finding allocation:

| Finding | Type assertions | Non-null |
| --- | ---: | ---: |
| AP-01 | 2 | 0 |
| AP-02 | 12 | 0 |
| AP-03 | 6 | 0 |
| AP-04 | 2 | 0 |
| AP-05 | 1 | 1 |
| AP-06 | 4 | 0 |

## Actionable findings

### AP-01 — A keyboard event is asserted to be a mouse event

Severity: medium

Location: `services/qyl.dashboard/src/pages/TracesPage.tsx:103-127`

```ts
const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onSelect();
};

handleClick(e as unknown as React.MouseEvent);
```

The two unrelated event types are bridged through `unknown`. The only event
member consumed by `handleClick` is `stopPropagation`; `onSelect` does not depend
on the event. The signature therefore creates a latent path for future
mouse-specific access without representing the keyboard call truthfully.

Replacement: extract a parameterless selection action and stop propagation in
the mouse and keyboard handlers independently, or accept the minimal structural
contract `{ stopPropagation(): void }`.

### AP-02 — The generated `AttributeValue` union is erased and rebuilt loosely

Severity: medium

Locations:

- `services/qyl.dashboard/src/lib/attribute-value.ts:3-32,56`
- `services/qyl.dashboard/src/lib/attribute-value.test.ts:12-36,51`

Product code replaces the generated discriminated union with optional `unknown`
fields, then asserts nested values back to the generated type:

```ts
type TaggedAttributeValue = {
    type?: unknown;
    value?: unknown;
    base64?: unknown;
    values?: unknown;
};

const tagged = value as TaggedAttributeValue;
decodeAttributeValue(nested as AttributeValue);
```

The tests contain seven direct contract assertions and one double assertion:

```ts
} as unknown as AttributeValue;
```

The locked `@ancplua/qyl-api-schema` 1.0.2 declaration at
`generated/ts-runtime/api.d.ts:580-615` already owns the recursive union:

```ts
export type AttributeValue =
    null | string | boolean |
    AttributeIntValue | AttributeDoubleValue | AttributeBytesValue |
    AttributeValue[] | AttributeKeyValueListValue;
```

Its `int`, `double`, `bytes`, and `kvlist` variants are discriminated by `type`.
The local erased shape prevents both product code and fixtures from detecting
generated-contract drift.

Replacement: narrow the generated union directly by `type`. Declare fixtures as
`const value: AttributeValue = ...` or use `... satisfies AttributeValue`.

### AP-03 — Vitest call tuples are fabricated after zero-argument mocks

Severity: low, test-only

Locations:

- `services/qyl.dashboard/src/lib/api.test.ts:82,92`
- `services/qyl.dashboard/src/lib/api.test.ts:119,125`
- `services/qyl.dashboard/src/lib/api.test.ts:139,157`

Each affected mock is created as `vi.fn(async () => ...)`, so Vitest infers an
empty argument tuple. The recorded call is then changed into a fetch tuple:

```ts
const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
```

Replacement: type the mock as `typeof fetch`, or declare
`(_input: RequestInfo | URL, _init?: RequestInit)`. The call record then has the
real signature without six nested assertion nodes.

### AP-04 — Runtime strings are trusted as closed unions

Severity: medium

Locations:

- `services/qyl.dashboard/src/hooks/use-theme.ts:17`
- `services/qyl.dashboard/src/pages/LogsPage.tsx:537`

```ts
return (localStorage.getItem('theme') as Theme) ?? 'dark';

<Select value={minLevel} onValueChange={(v) => setMinLevel(v as LogLevel)}>
```

`localStorage` and the local Select's string callback are runtime inputs. Neither
site proves membership before admitting the value to a closed union.

Replacement: add `isTheme` and `isLogLevel` membership predicates and fall back
or reject when the input is outside the supported values.

### AP-05 — DOM availability and event targets are asserted rather than checked

Severity: low

Locations:

- `services/qyl.dashboard/src/hooks/use-keyboard-shortcuts.ts:68`
- `services/qyl.dashboard/src/main.tsx:6`

```ts
const target = e.target as HTMLElement;

createRoot(document.getElementById('root')!).render(
```

`Event.target` is not guaranteed to be an `HTMLElement`, and
`document.getElementById` is nullable. Use `target instanceof HTMLElement` and
throw an explicit bootstrap error when `#root` is missing. The Host Console
already uses that explicit root check at `packages/Qyl.Host.Console/src/main.tsx:6-9`.

### AP-06 — React component generics are widened through record assertions

Severity: low

Locations:

- `services/qyl.dashboard/src/components/ui/button.tsx:38,46-50`
- `services/qyl.dashboard/src/components/ui/sonner.tsx:11`
- `services/qyl.dashboard/src/hooks/use-theme.ts:44-46`

```ts
className: cn(
    classes,
    (render.props as Record<string, unknown>).className as string | undefined,
),
} as Record<string, unknown>

theme={resolvedTheme as ToasterProps['theme']}
```

The button discards the rendered element's prop type before cloning it. The
Sonner assertion is redundant: TypeScript 5.9.3 infers `resolvedTheme` as
`'light' | 'dark'`, which is already within Sonner's accepted theme union.

Replacement: preserve the rendered element's prop generic in the button and
delete the Sonner assertion.

## Controlled or justified assertions

The 18 controlled type-assertion nodes are fully accounted for:

- Eight `as const` nodes only preserve literal or tuple inference:
  `e2e/smoke.spec.ts:167`, `theme-toggle.tsx:11`, and
  `use-telemetry.ts:11-16`.
- Three `as unknown` nodes deliberately widen JSON before generated AJV
  validation: `HealthIndicator.tsx:26` and `src/lib/api.ts:98,106`.
- `e2e/smoke.spec.ts:41` follows an immediate runtime shape assertion before
  reading `items`.
- The two `LogsPage.tsx:90` assertions follow a runtime `LOG_LEVELS` membership
  check. An `isLogLevel` predicate would remove them mechanically, but they do
  not admit an unchecked value.
- `error-boundary.tsx:39` parses app-owned recovery metadata; its only consumers
  compare `signature` and independently check `typeof previous.at === 'number'`
  at lines 43-46. It does not enter generated-contract or closed-union state.
- `text-visualizer.tsx:84,104` first checks that the value is an object. Its
  `React.CSSProperties` assertion at line 396 only admits the locally constructed
  custom property `--collapsed-height`.

Eight non-null assertions are guarded by local invariants:

- `RingBuffer.ts:98,106,113,119,126,136,146` iterates only over indices below
  the buffer's logical length;
- `TracesPage.tsx:443` calls `stack.pop()!` only inside
  `while (stack.length > 0)`.

## Recommended order

1. Remove the production event double assertion in `TracesPage` (AP-01).
2. Consume the generated `AttributeValue` union directly in product code and
   fixtures (AP-02).
3. Guard persisted and component-provided union values (AP-04).
4. Replace DOM and React-library assertions with real narrowing or preserved
   generics (AP-05 and AP-06).
5. Correct the Vitest mock signatures (AP-03).

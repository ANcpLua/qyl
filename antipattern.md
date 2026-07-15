# TypeScript type-check escape audit

Audit snapshot: `4e03b6f9a25f119ff50a0e96fd565bb78ae12660` plus the then-current
working tree on 2026-07-15.

## Scope and method

The audit scanned 64 first-party `.ts`, `.tsx`, `.mts`, and `.cts` files under
`services`, `packages`, `eng`, and `tests`. Dependency, build, coverage, and
generated directories were excluded. The TypeScript 5.9.3 compiler API was used
to enumerate type assertions, non-null assertions, and TypeScript suppression
directives; targeted searches covered explicit `any` escapes and compiler
configuration.

Both first-party TypeScript projects enable `strict`. The scan found:

- 53 type-assertion syntax nodes: 36 in product code and 17 in tests;
- 5 `as unknown as ...` chains: 1 in product code and 4 in tests;
- 9 non-null assertions;
- no `as any`, explicit `any`, `@ts-ignore`, `@ts-nocheck`, or
  `@ts-expect-error` escape;
- `skipLibCheck: true` in both projects. This skips consistency checking inside
  dependency declaration files, but does not disable strict checking of Qyl
  source or the types Qyl consumes from those declarations.

An assertion is not automatically defective. Findings below are limited to
places where the assertion invents compatibility, trusts unvalidated data, or
unnecessarily discards a generated contract. Controlled assertions and
invariant-backed non-null operations are listed separately.

## Actionable findings

### AP-01 — Unvalidated SSE payloads enter generated contract state

Severity: high

Locations:

- `packages/Qyl.Host.Console/src/useLogs.ts:22`
- `packages/Qyl.Host.Console/src/useResources.ts:29`

Both handlers apply a generated contract type directly to `JSON.parse`:

```ts
const line = JSON.parse(event.data) as LogLine;
const state = JSON.parse(event.data) as ResourceState;
```

`try/catch` proves only that the frame is syntactically valid JSON. It does not
prove required fields, discriminants, or value types. A valid JSON object with a
missing `name`, for example, reaches typed React state and can fail later outside
the parse boundary.

Replacement: parse to `unknown`, validate against the generated Qyl schema, and
only then return `LogLine` or `ResourceState`. The dashboard's
`src/lib/contract-validation.ts` demonstrates the required boundary pattern.

### AP-02 — A keyboard event is asserted to be a mouse event

Severity: medium

Location: `services/qyl.dashboard/src/pages/TracesPage.tsx:125`

```ts
handleClick(e as unknown as React.MouseEvent);
```

The types are unrelated; the double assertion suppresses that fact. The current
handler happens to call only `stopPropagation`, but its signature falsely allows
future mouse-specific access.

Replacement: extract a parameterless selection action and stop propagation in
the mouse and keyboard handlers independently, or accept the minimal structural
contract `{ stopPropagation(): void }`.

### AP-03 — The generated `AttributeValue` union is erased and rebuilt loosely

Severity: medium

Locations:

- `services/qyl.dashboard/src/lib/attribute-value.ts:15,32,56`
- `services/qyl.dashboard/src/lib/attribute-value.test.ts:12-36,51`

The product code turns the generated discriminated union into a local object
whose fields are all `unknown`, then asserts nested values back to
`AttributeValue`. The tests contain seven direct contract assertions and this
double assertion:

```ts
} as unknown as AttributeValue;
```

The installed generated contract already defines `int`, `double`, `bytes`, and
`kvlist` variants recursively. These assertions prevent test fixtures from
detecting a future generated-contract change.

Replacement: narrow the generated union by its `type` discriminant. Declare test
fixtures as `const value: AttributeValue = ...` or use
`... satisfies AttributeValue`; neither requires a double assertion.

### AP-04 — Vitest call tuples are fabricated after creating zero-argument mocks

Severity: medium, test-only

Locations:

- `services/qyl.dashboard/src/lib/api.test.ts:92`
- `services/qyl.dashboard/src/lib/api.test.ts:125`
- `services/qyl.dashboard/src/lib/api.test.ts:157`

Each test creates `vi.fn(async () => ...)`, so Vitest correctly infers an empty
argument tuple. The recorded call is then retyped with
`as unknown as [string, RequestInit]`.

Replacement: give the mock the real `fetch` signature, for example by typing the
mock as `typeof fetch` or declaring `(_input: RequestInfo | URL, _init?:
RequestInit)`. Its recorded calls will then be typed without an assertion.

### AP-05 — Persisted or component-provided strings are trusted as closed unions

Severity: medium

Locations:

- `services/qyl.dashboard/src/hooks/use-theme.ts:17`
- `services/qyl.dashboard/src/pages/LogsPage.tsx:535`
- `services/qyl.dashboard/src/components/ui/error-boundary.tsx:39`

`localStorage`, `sessionStorage`, and UI callback values are runtime inputs. The
current assertions allow arbitrary strings or objects to enter `Theme`,
`LogLevel`, and recovery-state code.

Replacement: use small membership/type guards and fall back or reject on an
invalid value. `LogsPage.tsx:88` already performs a runtime membership check;
turning that check into an `isLogLevel` predicate also removes its two local
assertions.

### AP-06 — DOM availability and event targets are asserted rather than checked

Severity: low

Locations:

- `services/qyl.dashboard/src/hooks/use-keyboard-shortcuts.ts:68`
- `services/qyl.dashboard/src/main.tsx:6`

`Event.target` is not guaranteed to be an `HTMLElement`, and the root element is
nullable according to the DOM API. Use `target instanceof HTMLElement` and an
explicit bootstrap failure when `#root` is absent. The latter produces a useful
diagnostic instead of moving the failure into React.

### AP-07 — React component generics are widened through record assertions

Severity: low

Locations:

- `services/qyl.dashboard/src/components/ui/button.tsx:46-50`
- `services/qyl.dashboard/src/components/ui/sonner.tsx:11`

The polymorphic button converts element props and the cloned prop bag to
`Record<string, unknown>`, including an assertion that `className` is a string.
The toaster separately asserts its theme even though Qyl resolves the theme to
`light | dark`.

Replacement: preserve the rendered element's prop generic in the button and give
`useTheme` an explicit return type. These changes let React and Sonner validate
the values rather than relying on local assertions.

## Controlled or justified assertions

The following are not classified as current defects:

- `await response.json() as unknown` in `src/lib/api.ts`,
  `HealthIndicator.tsx`, and related calls deliberately widens untrusted JSON
  before generated AJV validation. No contract type is invented.
- `as const` fixes literal or tuple inference and does not bypass compatibility
  checking.
- `text-visualizer.tsx` checks `typeof value === "object"` before its two
  `as object` uses. A reusable record guard would be cleaner but would not change
  the runtime guarantee.
- Its `React.CSSProperties` assertion exists only to admit the custom CSS
  property `--collapsed-height`; the value is locally constructed.
- Seven `RingBuffer` non-null assertions are guarded by loop indices strictly
  below the buffer's logical length. `TracesPage.tsx:450` similarly calls
  `stack.pop()!` only while `stack.length > 0`.
- `e2e/smoke.spec.ts:41` asserts a page shape immediately after a runtime test
  assertion. A true type guard would improve readability, but the test does not
  consume the value without a preceding runtime check.

## Recommended order

1. Add generated runtime validation to both Host Console SSE boundaries.
2. Remove the production double assertion in `TracesPage`.
3. Refactor `AttributeValue` decoding and fixtures to consume the generated
   discriminated union directly.
4. Correct the Vitest mock signatures.
5. Replace persisted-value, DOM, and UI-library assertions with narrow guards or
   accurate return/generic types.

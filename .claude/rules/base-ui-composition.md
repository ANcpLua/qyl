# base ui composition

## Hard contract for qyl Base UI variant
If qyl standardizes on Base UI, the contract is mechanical and strict:
- use only `@base-ui/react`
- never import `@radix-ui/*`
- never import `radix-ui`
- never use `asChild`
- never use `Slot`
- composition must use Base UI's `render` model
- detached triggers must use `createHandle()`
- do not translate Base UI examples into Radix idioms

## Why this rule exists
AI commonly mixes Base UI and Radix because both live in the same headless UI space. qyl must remove that ambiguity by banning cross-family patterns entirely.

## Canonical patterns
Treat these as canonical in qyl:
- `render` for composition
- `createHandle()` for detached triggers and payload-driven trigger flows
- Base UI `Form` and `Field` patterns for validation and submission flows
- qyl-owned wrappers around Base UI primitives for final product components

## Detached trigger rule
When a trigger is not colocated with the controlled UI, use `createHandle()`.
Do not approximate the behavior with ad-hoc refs, copied Radix-style composition, or wrapper hacks.

## Forms and validation
Prefer Base UI's form and field patterns when building:
- settings forms
- filters
- search forms
- issue actions
- release actions
- inline validation workflows

## Docs policy
Allowed authority for Base UI behavior:
- base-ui.com
- qyl local source code and wrappers

Disallowed authority for Base UI behavior:
- Radix docs
- blog posts that map Radix semantics onto Base UI
- examples that rely on `asChild` or `Slot`

## Review guidance
Reject code that is "close enough" if it uses the wrong primitive semantics. Similar intent does not make the API interchangeable.

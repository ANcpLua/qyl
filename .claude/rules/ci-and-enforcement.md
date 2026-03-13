# ci and enforcement

## Goal
The primitive-family decision must be enforceable by tools, not only by taste.

## Required checks for Base UI variant
CI should fail on:
- any import from `@radix-ui/*`
- any import from `radix-ui`
- any usage of `asChild`
- any usage of `Slot`

## Review rules
Code review should reject:
- mixed primitive families
- copied Radix examples adapted badly into Base UI
- wrapper abstractions that hide forbidden primitives
- chart-library downgrades on heavy observability surfaces without justification

## Agent safety
AI instructions should explicitly state:
- Base UI is the only primitive layer
- `render` is canonical
- `createHandle()` is canonical for detached triggers
- Base UI Form and Field patterns are canonical for forms
- only approved docs are authoritative

## Suggested enforcement ideas
Possible checks include:
- eslint import restrictions
- banned identifier checks
- grep-based CI guardrails
- pull-request templates that restate the primitive-family contract

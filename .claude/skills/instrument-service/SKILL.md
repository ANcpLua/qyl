---
name: qyl-frontend-contract
description: Contract router for building qyl's frontend, telemetry product surfaces, MCP-facing workflows, and loom investigation UI with strict Base UI enforcement and operator-grade product rules.
---

# qyl-frontend-contract

## 1. When to use

Use this skill whenever you are working on:

- qyl frontend code
- design-system code
- reusable product UI
- telemetry dashboards
- logs, traces, spans, incidents, alerts, and explorer surfaces
- MCP-facing product surfaces
- loom investigation and root-cause workflows

This section is activation logic only.
Do not treat it as the place for deep architecture discussion.

## 2. Mission

qyl is an operator-facing telemetry product.

The goal is not generic CRUD UI.
The goal is source-owned product UI that feels fast, dense, legible, and trustworthy under real telemetry workloads.

qyl surfaces must optimize for:

- workflow speed
- clear provenance
- strong operator scanability
- high-density but readable interfaces
- obvious distinction between raw telemetry facts and AI-generated analysis
- product-level ownership of the shipped frontend

## 3. Stack contract

qyl frontend stack contract:

- shadcn is the source-owned shell and reusable block layer
- Base UI is the only primitive family
- Tailwind + CSS variables are the styling baseline
- TanStack Table + TanStack Virtual own large tabular surfaces
- ECharts is preferred for dense observability charts
- Recharts is allowed only for lightweight dashboard visuals
- React Bits is allowed only for non-critical polish such as onboarding, empty states, and tasteful accent motion

Hard Base UI contract:

- use only `@base-ui/react`
- never import `@radix-ui/*`
- never import `radix-ui`
- never use `asChild`
- never use `Slot`
- compose through Base UI `render`
- use `createHandle()` for detached triggers
- use Base UI `Form` / `Field` patterns for canonical form behavior

## 4. Architecture rules

System-wide architecture rules:

- choose exactly one primitive family and enforce it mechanically
- styling stays qyl-owned through tokens, CSS variables, and Tailwind
- shadcn is not the primitive system; it is the source-owned shell/block layer
- charts and tables are architecture choices, not interchangeable widgets
- heavy observability surfaces must optimize for density, scanability, and scale
- MCP and loom flows must keep provenance explicit
- AI-generated analysis must not look identical to source-of-truth telemetry
- product workflows must avoid dead-end modal traps and preserve navigation back to raw data

## 5. Done criteria

A qyl frontend task is done only if:

- the stack contract is followed consistently
- Base UI semantics are correct
- styling remains qyl-owned
- accessibility semantics are preserved
- keyboard flow works
- the screen fits telemetry-product needs instead of generic CRUD defaults
- product-level composability is preserved
- performance is acceptable for realistic dataset sizes
- rule-file guidance was followed where relevant
- lint and CI checks pass
- no hidden conventions were introduced that a future agent cannot understand

## 6. Auto-fail criteria

Fail immediately if any of the following is true:

- any import from `@radix-ui/*`
- any import from `radix-ui`
- any use of `asChild`
- any use of `Slot`
- Base UI behavior implemented through copied Radix semantics
- detached trigger behavior implemented without `createHandle()`
- a second primitive family is introduced
- heavy observability charts use weaker defaults without justification
- flashy motion harms readability or operator speed
- AI-generated analysis is visually indistinguishable from raw telemetry facts

## 7. Source policy

Authoritative sources for qyl frontend decisions:

Base UI behavior:

- official Base UI docs
- qyl local source code
- qyl local wrappers
- qyl local rule files

Shell / design-system starter:

- official shadcn docs
- qyl local source-owned components

Tables / virtualization:

- official TanStack docs
- qyl local wrappers and rules

Charts:

- official ECharts docs
- official Recharts docs
- qyl local chart wrappers and rules

Disallowed as authority:

- Radix docs for Base UI behavior
- generic “headless UI” examples used as canonical truth
- blog posts that map Radix idioms onto Base UI
- copied examples that introduce `asChild`, `Slot`, or cross-family semantics

## 8. Rule files

Load the relevant rule file for implementation details:

- `./rules/shadcn-foundation.md`
- `./rules/base-ui-composition.md`
- `./rules/styling-and-theming.md`
- `./rules/tables-and-virtualization.md`
- `./rules/charts.md`
- `./rules/mcp-and-loom-surfaces.md`
- `./rules/motion-and-polish.md`
- `./rules/performance-and-density.md`
- `./rules/ci-and-enforcement.md`
- `./rules/done-and-fail-criteria.md`

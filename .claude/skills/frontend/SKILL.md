---
name: frontend
description: Contract for qyl frontend work. Base UI only, operator-grade density, strict stack enforcement.
---

# frontend

## When to use

Any work touching `src/qyl.dashboard/**` — pages, components, hooks, charts, tables.

## Stack contract

- `@base-ui/react` — only primitive family. Never `@radix-ui/*`, `asChild`, `Slot`.
- shadcn — source-owned shell and block layer (not the primitive system)
- Tailwind + CSS variables — styling baseline
- TanStack Table + TanStack Virtual — dense data surfaces
- ECharts — dense observability charts
- Recharts — lightweight KPI cards only
- React Bits — accent layer only:
  - Allowed: onboarding, empty states, celebratory no-incident states, loading flourishes, non-critical KPI motion
  - Banned surfaces: app shell, log explorer, trace explorer, mission-critical tables, investigation workflows, core navigation

## Base UI authority

- https://base-ui.com/llms.txt
- https://base-ui.com/react/handbook/composition.md
- Local project rules in `.claude/rules/`

## Composition rules

- `render` prop for composition
- `createHandle()` for detached triggers
- Base UI `Form` / `Field` for forms
- Never copy Radix patterns into Base UI code

## Auto-fail

- any `@radix-ui/*` import
- any `asChild` or `Slot`
- second primitive family introduced
- heavy charts using Recharts without justification
- AI analysis visually identical to raw telemetry

## Rule files

Load for implementation details:

- `./rules/base-ui-composition.md`
- `./rules/shadcn-foundation.md`
- `./rules/styling-and-theming.md`
- `./rules/tables-and-virtualization.md`
- `./rules/charts.md`
- `./rules/performance-and-density.md`

## Spec

`specs/dashboard.md` is the source of truth for pages, views, and done criteria.

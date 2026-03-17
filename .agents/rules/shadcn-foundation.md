---
paths:
  - "src/qyl.dashboard/**"
---

# shadcn foundation

## Core positioning

Use shadcn/ui as the foundation layer for qyl's frontend, but describe it correctly: it is not a classic runtime
component library. It is a code distribution system and design-system starter.

Treat shadcn as the source-owned shell and reusable block layer for qyl:

- app shell
- sidebar
- command palette
- dialogs
- drawers
- settings pages
- forms
- tables
- reusable dashboard surfaces

## Why it fits qyl

qyl is a telemetry product. That means the frontend will need deep customization, long-lived ownership, dense
interaction patterns, and freedom from monolithic theme-overrides.

The strength of shadcn is not "zero dependencies" or "zero bundle size". The strength is source ownership and selective
adoption.

## Required wording

Preferred wording:

- shadcn is an open-code design-system starter
- qyl owns the shipped component code
- qyl adopts only the component code and dependencies it actually uses

Avoid claiming:

- zero bundle bloat
- zero npm dependencies
- full runtime-free UI with no package cost

## Architectural role

Within qyl, shadcn should own:

- product shell
- layout primitives at the product level
- standard reusable business surfaces
- dashboard and settings building blocks

shadcn should not be treated as the low-level primitive system itself. That role belongs to exactly one primitive family
chosen for the app.

## Non-goals

Do not position shadcn as:

- a replacement for your primitive family decision
- an excuse to mix Base UI and Radix
- a reason to avoid product-level component ownership

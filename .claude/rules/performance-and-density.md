---
paths:
  - "src/qyl.dashboard/**"
---

# performance and density

## Core principle

qyl is an operator-facing telemetry product. Optimize for workflow speed, clarity, and scale.

## Preferred design qualities

Prefer:

- dense but readable layouts
- scanable hierarchy
- fast keyboard travel
- responsive interactions
- restrained visual chrome
- clear emphasis on live data and analysis context
- timeline clarity — time context always visible alongside data
- entity linkage — every entity links to its related entities
- side-by-side comparison when useful (traces, spans, deployments)
- navigation back to raw data from AI analysis

## Surface rules

- reduce context-switching — keep investigation in one surface
- entity scope obvious — always clear which project/service/time range is active
- no dead-end modal traps — every modal must have a navigation path back to raw data

## Avoid

Do not optimize primarily for:

- giant marketing-style spacing
- decorative animation in core workflows
- oversized controls that reduce information density
- slow chart rendering on operator screens

## Rendering rules

For large or complex views:

- avoid unnecessary rerenders
- virtualize when scale justifies it
- choose charting tech based on operational complexity
- avoid component stacks that add latency to common tasks

## UX rule

The product should feel fast under realistic telemetry workloads, not only in empty-state demos.

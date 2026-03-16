---
paths:
  - "src/qyl.dashboard/**"
---

# styling and theming

## Baseline

Use Tailwind plus CSS variables as the default styling baseline for qyl.

## Ownership rules

qyl must own its visual system:

- tokens
- spacing
- typography
- elevation
- border treatments
- light/dark mode decisions
- semantic color mapping for telemetry states

Do not rely on vendor theme layers from big UI frameworks.

## Theming rules

Preferred theming model:

- CSS variables for design tokens
- Tailwind utilities for composition and speed
- dark mode as a first-class supported theme
- product-level semantic tokens for incident states, severity, health, and investigation results

## Telemetry-specific styling needs

Theming must support:

- dense tables
- muted but legible backgrounds
- high-contrast focus states
- long operator sessions
- readable time-series and event detail panels

## Avoid

Do not:

- couple styling to a monolithic UI framework
- create one-off colors outside the token system
- overuse decorative gradients in operator workflows
- sacrifice readability for dribbble-style visuals

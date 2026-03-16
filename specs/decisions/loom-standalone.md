# Decision: Loom as Standalone Product

## Status

Accepted.

## Context

Should qyl.loom be merged into the collector, or remain a standalone product?

## Decision

Standalone. `src/qyl.loom/` is its own product with its own project at `~/RiderProjects/qyl.loom/`.

## Rationale

Loom is a C# transpile of Sentry Seer — a complete AI-powered issue investigation system. It has its own domain model (5-stage pipeline, PolicyGate, autofix, code review, regression detection, triage) that is larger than any single collector feature.

Merging it into collector would:

- Bloat the collector with AI/LLM dependencies
- Make the collector harder to test and deploy without LLM infrastructure
- Couple Loom's release cycle to collector's release cycle
- Obscure Loom's domain model inside collector's service layer

Loom references collector, agents, workflows, contracts, and instrumentation via ProjectReference. The dependency flows one way: loom → collector. Collector must never depend on loom.

## Historical Note

A brainstorming session on 2026-03-12 incorrectly identified qyl.loom as "dead code" and deleted 54 files. This decision exists to prevent that from happening again.

# Metrics Extraction — Part 3: Source Generators

**Type:** Prompt Chain (3/4)
**Chain:** Metrics API Extraction → `docs/roadmap/loom-design.md` section 15.11
**Source:** `docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`
**Dependencies:** Run after 02-2

## Prompt

```
Extract from docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md
ONLY Part 3 (Metrics Source Generators) and append as section 15.11.3 to
docs/roadmap/loom-design.md

Section: "15.11.3 Metrics Source Generators"
Scope labels: CONTEXT-ONLY for API reference, IMPLEMENTED-IN-QYL where qyl
has its own generators (with file path evidence)

Required content:
- Table: Generator attributes ([Counter], [Histogram], [Gauge] etc.)
  Columns: Attribute | Generated Code | Advantage vs Manual
- Comparison with qyl.servicedefaults.generator: [Traced] generates spans,
  Microsoft generates metrics — complementary patterns
- Partial method pattern: how generators produce extension methods
- Correlation with section 15.6 (Compile-Time Tracing): same Roslyn generator
  approach, different domain (spans vs metrics)

Style: Match existing 15.x subsections. Tables preferred. Clear, unambiguous.
Do NOT copy-paste — distill core concepts only.
```

# Metrics Extraction — Part 1: Meter & Instrument Types

**Type:** Prompt Chain (1/4)
**Chain:** Metrics API Extraction → `docs/roadmap/loom-design.md` section 15.11
**Source:** `docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`
**Dependencies:** None (first in chain)

## Prompt

```
Extract from docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md
ONLY Part 1 (Meter, Instrument types) and append as section 15.11.1 to
docs/roadmap/loom-design.md

Section: "15.11.1 Meter & Instrument Types"
Scope label: CONTEXT-ONLY

Required content:
- Table: All 7 instrument types (Counter<T>, Histogram<T>, Gauge<T>,
  UpDownCounter<T>, ObservableCounter<T>, ObservableGauge<T>,
  ObservableUpDownCounter<T>)
  Columns: Type | Sync/Observable | Monotonic | Use-Case | qyl Relevance
- Meter lifecycle: creation, Name/Version, Tags, Dispose pattern
- qyl correlation: which instruments qyl.collector already uses
  (cross-ref section 15.7 Zero-Cost, 15.2 DuckDB MetricStorageRow)

Style: Match existing 15.x subsections. Tables preferred. Clear, unambiguous.
Do NOT copy-paste — distill core concepts only.
```

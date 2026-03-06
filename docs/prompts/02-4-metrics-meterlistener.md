# Metrics Extraction — Part 4: MeterListener & Subscription Model

**Type:** Prompt Chain (4/4)
**Chain:** Metrics API Extraction → `docs/roadmap/loom-design.md` section 15.11
**Source:** `docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md`
**Dependencies:** Run after 02-3 (produces closing correlation matrix for all of 15.11)

## Prompt

```
Extract from docs/andrewlock-system-diagnostics-metrics-apis-parts-1-4.md
ONLY Part 4 (MeterListener, Observable Instruments) and append as section 15.11.4 to
docs/roadmap/loom-design.md

Section: "15.11.4 MeterListener & Subscription Model"
Scope labels: CONTEXT-ONLY for API reference, IMPLEMENTED-IN-QYL for qyl
subscription pattern (with file path evidence)

Required content:
- MeterListener lifecycle: create, EnableMeasurementEvents,
  SetMeasurementEventCallback, RecordObservableInstruments, Start, Dispose
- Table: MeterListener callbacks
  Columns: Callback | When Invoked | Parameters | Purpose
- Zero-cost proof: instruments without listener = zero overhead
  (direct correlation with section 15.7 Zero-Cost Observability)
- Observable pattern: callback-based measurement vs push-based instruments
- Loom correlation matrix (closing table for all of 15.11):
  Columns: Loom Capability (Section 5) | Required Metrics API | Instrument Type

Style: Match existing 15.x subsections. Tables preferred. Clear, unambiguous.
Do NOT copy-paste — distill core concepts only.
```

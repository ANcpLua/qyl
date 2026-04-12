# otelopus

The 20 canonical input files for deterministic, idempotent OTel-aligned schema generation.

## Why this folder exists

The qyl generator pipeline (7 stages, `nuke Generate`) takes upstream OTel definitions and produces C#, TypeScript, TypeSpec, and DuckDB artifacts. For the output to be **deterministic** (same input = same output) and **idempotent** (running twice = no diff), the inputs must be pinned.

This folder pins them.

## Structure

```
otelopus/
  proto/                        8 files  — OTLP wire format (protobuf)
    common.proto                         AnyValue, KeyValue, InstrumentationScope
    resource.proto                       Resource message
    trace.proto                          Span, Status, SpanKind, Link, Event
    metrics.proto                        Gauge, Sum, Histogram, ExponentialHistogram, Summary
    logs.proto                           LogRecord, SeverityNumber
    trace_service.proto                  ExportTraceServiceRequest/Response
    metrics_service.proto                ExportMetricsServiceRequest/Response
    logs_service.proto                   ExportLogsServiceRequest/Response

  semconv/                      8 files  — Semantic convention YAML models
    gen-ai.registry.yaml                 All gen_ai.* attribute definitions
    gen-ai.spans.yaml                    GenAI span conventions (operations, providers)
    gen-ai.metrics.yaml                  GenAI metric conventions (token usage, duration)
    db.registry.yaml                     All db.* attribute definitions
    db.spans.yaml                        DB span conventions (operations, systems)
    db.metrics.yaml                      DB metric conventions (operation duration)
    http.registry.yaml                   All http.* attribute definitions
    error.registry.yaml                  All error.* attribute definitions

  spec/                         2 files  — Behavioral rules generators must obey
    attribute-naming.md                  Naming: lowercase, dot-separated, snake_case
    recording-errors.md                  How to record errors across all signals

  pipeline/                     2 files  — Generation config (PLACEHOLDER)
    VERSION.lock                         Pinned upstream versions + checksums
    manifest.json                        Generation targets + invariants
```

## Formats chosen for longevity

| Format | Why |
|--------|-----|
| Protocol Buffers (`.proto`) | 20+ year track record. Backward-compatible by design. Every language has a parser. |
| YAML (`.yaml`) | Human-readable, machine-parseable. OTel's official semconv model format. |
| Markdown (`.md`) | Plain text with minimal structure. Readable without any tooling. |
| JSON (`.json`) | Universal interchange format. Schema-validated via `$schema` field. |

No proprietary formats. No binary blobs. No tool-specific lock files. A developer in 2046 can read every file in this folder with a text editor.

## How to use

### Verify determinism
```bash
# This should produce zero diff on a clean checkout:
nuke Generate
git diff --stat -- '*.g.*'
```

### Update upstream versions
```bash
# 1. Update VERSION.lock with new version numbers + commits
# 2. Copy new files from upstream repos:
cp ../opentelemetry-proto/opentelemetry/proto/common/v1/common.proto otelopus/proto/
# ... (all 8 proto files)
cp ../semantic-conventions/model/gen-ai/registry.yaml otelopus/semconv/gen-ai.registry.yaml
# ... (all 8 semconv files)
# 3. Update checksums in VERSION.lock
# 4. Run the pipeline
nuke Generate
# 5. Commit everything (inputs + outputs)
```

### What breaks if you skip this
- Without pinned versions: different machines produce different output
- Without checksums: someone edits a proto file directly and the pipeline silently diverges
- Without the manifest: no one knows which generators consume which inputs

## The 20 files

| # | File | Role | Source |
|---|------|------|--------|
| 1 | `proto/common.proto` | Wire format: base types | opentelemetry-proto @ aca8735 |
| 2 | `proto/resource.proto` | Wire format: Resource | same |
| 3 | `proto/trace.proto` | Wire format: Span | same |
| 4 | `proto/metrics.proto` | Wire format: all metric types | same |
| 5 | `proto/logs.proto` | Wire format: LogRecord | same |
| 6 | `proto/trace_service.proto` | Wire format: trace export RPC | same |
| 7 | `proto/metrics_service.proto` | Wire format: metrics export RPC | same |
| 8 | `proto/logs_service.proto` | Wire format: logs export RPC | same |
| 9 | `semconv/gen-ai.registry.yaml` | Attribute definitions: gen_ai.* | semantic-conventions @ 682f2d61 |
| 10 | `semconv/gen-ai.spans.yaml` | Span conventions: GenAI | same |
| 11 | `semconv/gen-ai.metrics.yaml` | Metric conventions: GenAI | same |
| 12 | `semconv/db.registry.yaml` | Attribute definitions: db.* | same |
| 13 | `semconv/db.spans.yaml` | Span conventions: DB | same |
| 14 | `semconv/db.metrics.yaml` | Metric conventions: DB | same |
| 15 | `semconv/http.registry.yaml` | Attribute definitions: http.* | same |
| 16 | `semconv/error.registry.yaml` | Attribute definitions: error.* | same |
| 17 | `spec/attribute-naming.md` | Rule: how to name attributes | semantic-conventions docs |
| 18 | `spec/recording-errors.md` | Rule: how to record errors | same |
| 19 | `pipeline/VERSION.lock` | **PLACEHOLDER** — pinned versions + checksums | Created for otelopus |
| 20 | `pipeline/manifest.json` | **PLACEHOLDER** — generation targets + invariants | Created for otelopus |

Files 1-18 are copies of upstream OTel sources. Files 19-20 are placeholders that will break the build until implemented — `VERSION.lock` has empty checksums and `manifest.json` has TODO generators.

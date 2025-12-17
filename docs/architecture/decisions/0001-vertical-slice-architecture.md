# ADR-0001: Vertical Slice Architecture

## Status: Accepted | Date: 2025-12-16

## Context

qyl hat 4 Komponenten (protocol, collector, mcp, dashboard). Features sollen Ende-zu-Ende implementiert werden, nicht
Layer-für-Layer.

## Decision

**Vertical Slice Architecture**: Jedes Feature durchläuft alle Layer in einem Durchgang.

```
TypeSpec → Storage → Query → API → MCP → Dashboard
   │          │         │       │     │        │
   └──────────┴─────────┴───────┴─────┴────────┘
              Ein Feature, komplett
```

## Slices

| ID    | Name            | Prio | Depends On   |
|-------|-----------------|------|--------------|
| VS-01 | Span Ingestion  | P0   | -            |
| VS-02 | List Sessions   | P0   | VS-01        |
| VS-03 | View Trace Tree | P1   | VS-01        |
| VS-04 | GenAI Analytics | P1   | VS-01, VS-02 |
| VS-05 | Live Streaming  | P2   | VS-01        |
| VS-06 | MCP Query Tool  | P2   | VS-01, VS-02 |

```
VS-01 ──► VS-02 ──► VS-04
  │         └────► VS-06
  ├──► VS-03
  └──► VS-05
```

## Layer Locations

| Layer     | Path                         | Purpose                     |
|-----------|------------------------------|-----------------------------|
| TypeSpec  | `core/specs/`                | Contract-first API + Schema |
| Storage   | `src/qyl.collector/Storage/` | DuckDB Schema + Store       |
| Query     | `src/qyl.collector/Query/`   | Aggregation SQL             |
| API       | `src/qyl.collector/`         | REST + SSE Endpoints        |
| MCP       | `src/qyl.mcp/Tools/`         | AI Agent Tools              |
| Dashboard | `src/qyl.dashboard/src/`     | React Components            |

## Consequences

**Pro**: Features sofort nutzbar, klare File-Ownership, isolierte Entwicklung
**Con**: Kleine Changes durchlaufen alle Layer

## References

- [qyl-architecture.yaml](../../qyl-architecture.yaml)

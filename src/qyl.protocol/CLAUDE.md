# qyl.protocol

Shared type contracts. BCL only. Leaf dependency.

## identity

```yaml
name: qyl.protocol
type: class-library
sdk: ANcpLua.NET.Sdk
role: leaf-dependency
constraint: bcl-only
```

## constraint

```yaml
packages-allowed: none
reason: must remain leaf dependency with zero external packages
consumers: [collector, mcp]
```

## generated-files

```yaml
source: core/openapi/openapi.yaml
generator: nuke Generate

outputs:
  Primitives/:
    - SpanId.g.cs
    - TraceId.g.cs
    - SessionId.g.cs
    - UnixNano.g.cs
    - DurationNs.g.cs
    - TokenCount.g.cs
    - CostUsd.g.cs
    
  Enums/:
    - SpanKind.g.cs
    - SpanStatusCode.g.cs
    - SeverityNumber.g.cs
    
  Models/:
    - SpanRecord.g.cs
    - SessionSummary.g.cs
    - TraceNode.g.cs
    - LogRecordStorage.g.cs
    - GenAiSpanData.g.cs

rule: never-edit-generated-files
```

## type-inventory

```yaml
primitives:
  - name: SpanId
    underlying: string
    format: 16 hex chars
    
  - name: TraceId
    underlying: string
    format: 32 hex chars
    
  - name: SessionId
    underlying: string
    
  - name: UnixNano
    underlying: long
    description: nanoseconds since epoch
    
  - name: DurationNs
    underlying: long
    description: duration in nanoseconds
    
  - name: TokenCount
    underlying: int
    
  - name: CostUsd
    underlying: decimal

enums:
  - name: SpanKind
    values: [Unspecified, Internal, Server, Client, Producer, Consumer]
    
  - name: SpanStatusCode
    values: [Unset, Ok, Error]
    
  - name: SeverityNumber
    values: [1-24, mapping to TRACE through FATAL]

models:
  - name: SpanRecord
    purpose: storage row for spans table
    consumers: [collector, mcp]
    
  - name: SessionSummary
    purpose: aggregated session view
    consumers: [collector, mcp, dashboard]
    
  - name: TraceNode
    purpose: hierarchical trace tree
    consumers: [collector, dashboard]
    
  - name: GenAiSpanData
    purpose: extracted gen_ai.* attributes
    consumers: [collector, mcp]
```

## ownership-rules

```yaml
decision-tree:
  - question: "Used by collector only?"
    answer: collector/Storage/ or collector/Ingestion/
    
  - question: "Used by mcp only?"
    answer: mcp/
    
  - question: "Used by dashboard only?"
    answer: dashboard/src/types/
    
  - question: "Used by 2+ projects?"
    answer: protocol/
```

## dependencies

```yaml
project-references: none
package-references: none
referenced-by:
  - qyl.collector
  - qyl.mcp
```

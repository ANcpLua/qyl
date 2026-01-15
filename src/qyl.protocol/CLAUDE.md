# qyl.protocol

Shared type contracts. BCL only. Leaf dependency.

## identity

```yaml
name: qyl.protocol
type: class-library
sdk: ANcpLua.NET.Sdk
role: leaf-dependency
constraint: bcl-only (zero external packages)
consumers: [collector, mcp]
```

## constraint

```yaml
allowed-packages: none
reason: must remain leaf with zero dependencies
violation: build will fail if PackageReference added
```

## generated-files

```yaml
generator: SchemaGenerator (eng/build/Domain/CodeGen/)
source: core/openapi/openapi.yaml
command: nuke Generate

files:
  Primitives/Scalars.g.cs:
    generator: GenerateScalars()
    types:
      - TraceId (string, 32 hex, IParsable<T>)
      - SpanId (string, 16 hex, IParsable<T>)
      - SessionId (string)
      - UnixNano (long)
      - DurationNs (long)
      - TokenCount (int)
      - CostUsd (decimal)
      - Temperature (double)
      - Ratio (double)
      - Count (int)
    features:
      - implicit conversions
      - JsonConverter per type
      - IParsable<T> for TraceId/SpanId
      - ISpanFormattable for TraceId/SpanId
      - ReadOnlySpan<byte> hot-path parsing
      - IsValid property
      - Empty static property for hex types
      
  Enums/Enums.g.cs:
    generator: GenerateEnums()
    types:
      - SpanKind (0=Unspecified, 1=Internal, 2=Server, 3=Client, 4=Producer, 5=Consumer)
      - SpanStatusCode (0=Unset, 1=Ok, 2=Error)
      - SeverityNumber (1-24, TRACE through FATAL)
    features:
      - JsonNumberEnumConverter for integer enums
      - JsonStringEnumConverter for string enums
      - EnumMember attributes for serialization
      
  Models/Models.g.cs:
    generator: GenerateModels()
    types:
      - SpanRecord
      - SessionSummary
      - TraceNode
      - LogRecordStorage
      - GenAiSpanData
      - SpanStats
      - SpanStatsByDimension
```

## scalar-pattern

```yaml
example: |
  /// <summary>Trace identifier (32 hex chars)</summary>
  [JsonConverter(typeof(TraceIdJsonConverter))]
  public readonly partial record struct TraceId(string Value) 
      : IParsable<TraceId>, ISpanFormattable
  {
      public static implicit operator string(TraceId v) => v.Value;
      public static implicit operator TraceId(string v) => new(v);
      public override string ToString() => Value ?? string.Empty;
      
      public static TraceId Empty => new("00000000000000000000000000000000");
      public bool IsEmpty => string.IsNullOrEmpty(Value) || Value == "00000000000000000000000000000000";
      public bool IsValid => !string.IsNullOrEmpty(Value) && s_pattern.IsMatch(Value);
      
      // IParsable<T>
      public static TraceId Parse(string s, IFormatProvider? provider) => ...;
      public static bool TryParse(string? s, IFormatProvider? provider, out TraceId result) => ...;
      public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out TraceId result) => ...;
      public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out TraceId result) => ...;
      
      // ISpanFormattable
      public bool TryFormat(Span<char> dest, out int written, ...) => ...;
  }
  
  file sealed class TraceIdJsonConverter : JsonConverter<TraceId>
  {
      public override TraceId Read(...) => new(reader.GetString() ?? string.Empty);
      public override void Write(...) => writer.WriteStringValue(value.Value);
  }
```

## model-pattern

```yaml
example: |
  /// <summary>Storage row for spans table</summary>
  public sealed record SpanRecord
  {
      [JsonPropertyName("span_id")]
      public required SpanId SpanId { get; init; }
      
      [JsonPropertyName("trace_id")]
      public required TraceId TraceId { get; init; }
      
      [JsonPropertyName("parent_span_id")]
      public SpanId? ParentSpanId { get; init; }
      
      [JsonPropertyName("session_id")]
      public SessionId? SessionId { get; init; }
      
      // ... promoted gen_ai columns ...
      
      [JsonPropertyName("attributes_json")]
      public string? AttributesJson { get; init; }
  }
```

## type-ownership

```yaml
decision-tree:
  question: "Who consumes this type?"
  answers:
    - "collector only" → collector/Storage/ or collector/Ingestion/
    - "mcp only" → mcp/
    - "dashboard only" → dashboard/src/types/
    - "2+ projects" → protocol/
    
examples:
  SpanRecord: protocol (collector + mcp)
  SessionSummary: protocol (collector + mcp + dashboard)
  DuckDbStore: collector only
  QylClient: mcp only
  SpanWaterfallProps: dashboard only
```

## namespaces

```yaml
Qyl.Common: primitives (scalars)
Qyl.Enums: enumerations
Qyl.Models: domain models
```

## dependencies

```yaml
project-references: none
package-references: none (ENFORCED)
referenced-by:
  - qyl.collector
  - qyl.mcp
```

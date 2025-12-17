# ADR-0005: VS-04 GenAI Analytics

## Metadata

| Field      | Value                              |
|------------|------------------------------------|
| Status     | Draft                              |
| Date       | 2025-12-16                         |
| Slice      | VS-04                              |
| Priority   | P1                                 |
| Depends On | ADR-0002 (VS-01), ADR-0003 (VS-02) |
| Supersedes | -                                  |

## Context

qyl fokussiert auf AI Observability. Neben Standard-Telemetrie sind GenAI-spezifische Analytics erforderlich:

- Token Usage (Input/Output)
- Model Distribution
- Cost Estimation
- Performance per Model/Provider
- Error Rates per Model

## Decision

Implementierung von GenAI-spezifischen Analytics mit:

- Pre-aggregierte Statistiken in DuckDB
- Kosten-Schätzung basierend auf Token-Preisen
- Vergleichs-Ansichten (Model vs Model, Session vs Session)
- Dashboard mit Charts und Breakdown-Tabellen

## Layers

### 1. TypeSpec (Contract)

```yaml
files:
  - core/specs/api/genai.tsp          # GenAI analytics endpoints
  - core/specs/models/genai.tsp       # GenAiStats, ModelUsage, CostBreakdown
generates:
  - core/generated/openapi/openapi.yaml
```

**genai.tsp Example:**

```typespec
@route("/api/v1/genai")
namespace GenAi {
  @get @route("/stats")
  op getStats(
    @query from?: utcDateTime,
    @query to?: utcDateTime,
    @query serviceName?: string
  ): GenAiStatsResponse;

  @get @route("/models")
  op getModelUsage(
    @query from?: utcDateTime,
    @query to?: utcDateTime
  ): ModelUsageResponse;

  @get @route("/costs")
  op getCostBreakdown(
    @query from?: utcDateTime,
    @query to?: utcDateTime,
    @query groupBy?: "model" | "service" | "day"
  ): CostBreakdownResponse;
}

model GenAiStats {
  totalRequests: int64;
  totalInputTokens: int64;
  totalOutputTokens: int64;
  totalTokens: int64;
  estimatedCostUsd: float64;
  avgLatencyMs: float64;
  errorRate: float64;
  topModels: ModelUsage[];
}

model ModelUsage {
  model: string;
  provider: string;
  requestCount: int64;
  inputTokens: int64;
  outputTokens: int64;
  avgLatencyMs: float64;
  errorCount: int64;
}

model CostBreakdown {
  period: string;
  model: string;
  inputTokens: int64;
  outputTokens: int64;
  inputCostUsd: float64;
  outputCostUsd: float64;
  totalCostUsd: float64;
}
```

### 2. Storage Layer

```yaml
files:
  - src/qyl.collector/Storage/GenAiStats.cs     # Aggregated stats model
  - src/qyl.collector/GenAiProviders.cs         # Provider detection + pricing
```

**Model Pricing (GenAiProviders.cs):**

```csharp
public static readonly FrozenDictionary<string, (decimal InputPer1M, decimal OutputPer1M)> ModelPricing =
    new Dictionary<string, (decimal, decimal)>
    {
        // OpenAI
        ["gpt-4o"] = (2.50m, 10.00m),
        ["gpt-4o-mini"] = (0.15m, 0.60m),
        ["gpt-4-turbo"] = (10.00m, 30.00m),
        ["gpt-3.5-turbo"] = (0.50m, 1.50m),

        // Anthropic
        ["claude-3-5-sonnet-20241022"] = (3.00m, 15.00m),
        ["claude-3-opus-20240229"] = (15.00m, 75.00m),
        ["claude-3-haiku-20240307"] = (0.25m, 1.25m),

        // Google
        ["gemini-1.5-pro"] = (1.25m, 5.00m),
        ["gemini-1.5-flash"] = (0.075m, 0.30m),
    }.ToFrozenDictionary();
```

### 3. Query Layer

```yaml
files:
  - src/qyl.collector/Query/GenAiQueryService.cs
methods:
  - "GetGenAiStatsAsync(from, to, serviceName)"
  - "GetModelUsageAsync(from, to)"
  - "GetCostBreakdownAsync(from, to, groupBy)"
```

**Aggregation SQL:**

```sql
-- Model Usage
SELECT
    gen_ai_request_model as model,
    gen_ai_provider_name as provider,
    COUNT(*) as request_count,
    SUM(gen_ai_input_tokens) as input_tokens,
    SUM(gen_ai_output_tokens) as output_tokens,
    AVG(duration_ns / 1000000.0) as avg_latency_ms,
    SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END) as error_count
FROM spans
WHERE gen_ai_operation_name IS NOT NULL
  AND start_time_unix_nano >= ?
  AND start_time_unix_nano <= ?
GROUP BY gen_ai_request_model, gen_ai_provider_name
ORDER BY request_count DESC;
```

### 4. API Layer

```yaml
endpoints:
  - "GET /api/v1/genai/stats?from=&to=&serviceName="
  - "GET /api/v1/genai/models?from=&to="
  - "GET /api/v1/genai/costs?from=&to=&groupBy=model"
files:
  - src/qyl.collector/Program.cs
```

### 5. MCP Layer

```yaml
files:
  - src/qyl.mcp/Tools/AnalyzeGenAiTool.cs
tools:
  - name: "analyze_genai"
    description: "Analyze GenAI usage and costs"
    parameters:
      timeRange: "1h" | "24h" | "7d" | "30d"
      groupBy: "model" | "service" | "day"
    returns: "Markdown table with usage stats and costs"
```

**MCP Output Example:**

```markdown
## GenAI Usage (Last 24h)

| Model | Requests | Input Tokens | Output Tokens | Cost (USD) |
|-------|----------|--------------|---------------|------------|
| gpt-4o | 1,234 | 2.5M | 890K | $15.40 |
| claude-3-5-sonnet | 567 | 1.2M | 450K | $10.35 |

**Total Cost: $25.75**
**Total Tokens: 5.04M**
```

### 6. Dashboard Layer

```yaml
files:
  - src/qyl.dashboard/src/api/hooks.ts      # useGenAiStats(), useModelUsage()
  - src/qyl.dashboard/src/components/genai/TokenChart.tsx
  - src/qyl.dashboard/src/components/genai/ModelUsage.tsx
  - src/qyl.dashboard/src/components/genai/CostBreakdown.tsx
  - src/qyl.dashboard/src/components/genai/ProviderComparison.tsx
  - src/qyl.dashboard/src/pages/GenAiPage.tsx
patterns:
  - "Recharts für AreaChart (Token Usage over Time)"
  - "Recharts für PieChart (Model Distribution)"
  - "Data Table für Cost Breakdown"
```

## Acceptance Criteria

- [ ] `GET /api/v1/genai/stats` gibt GenAiStats zurück
- [ ] `GET /api/v1/genai/models` gibt ModelUsage[] zurück
- [ ] `GET /api/v1/genai/costs` gibt CostBreakdown[] zurück
- [ ] Kosten werden korrekt berechnet basierend auf Pricing
- [ ] MCP `analyze_genai` Tool funktioniert
- [ ] Dashboard zeigt Token-Usage Chart
- [ ] Dashboard zeigt Model-Distribution Pie Chart
- [ ] Dashboard zeigt Cost Breakdown Tabelle
- [ ] Zeitfilter (1h, 24h, 7d, 30d) funktioniert

## Test Files

```yaml
unit_tests:
  - tests/qyl.collector.tests/Query/GenAiQueryServiceTests.cs
  - tests/qyl.collector.tests/GenAiProvidersTests.cs
integration_tests:
  - tests/qyl.collector.tests/Api/GenAiEndpointsTests.cs
```

## Consequences

### Positive

- **Cost Visibility**: Sofortige Kosten-Übersicht
- **Model Optimization**: Vergleich ermöglicht Modell-Wahl
- **Budget Control**: Alerts bei Kosten-Überschreitung möglich

### Negative

- **Pricing Maintenance**: Model-Preise ändern sich regelmäßig
- **Unvollständige Daten**: Nicht alle Spans haben gen_ai.* Attributes

### Risks

| Risk                   | Impact | Likelihood | Mitigation                              |
|------------------------|--------|------------|-----------------------------------------|
| Veraltete Preise       | Medium | High       | Pricing als Config (nicht hardcoded)    |
| Fehlende Token-Counts  | Medium | Medium     | Estimation basierend auf Content-Length |
| Provider nicht erkannt | Low    | Low        | Fallback auf "unknown" Provider         |

## References

- [ADR-0002](0002-vs01-span-ingestion.md) - VS-01 Span Ingestion
- [ADR-0003](0003-vs02-list-sessions.md) - VS-02 List Sessions
- [OpenAI Pricing](https://openai.com/pricing)
- [Anthropic Pricing](https://www.anthropic.com/pricing)
- [OTel GenAI Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)

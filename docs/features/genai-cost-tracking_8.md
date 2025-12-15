# Feature: GenAI Cost Tracking Dashboard

> **Status:** Ready
> **Effort:** ~4h
> **Backend:** Yes (new endpoint + storage)
> **Priority:** P1

---

## Problem

Users cannot see how much their AI operations cost. Token counts are stored but not converted to dollars. No aggregation
by model, provider, or time period.

## Solution

Add cost calculation to collector, store in DuckDB, expose via REST API, and display in dashboard with charts and
breakdowns.

---

## Context

### Existing Data

```sql
-- Already stored in spans table
gen_ai.usage.input_tokens   -- e.g., 1500
gen_ai.usage.output_tokens  -- e.g., 500
gen_ai.request.model        -- e.g., "claude-sonnet-4-20250514"
gen_ai.provider.name        -- e.g., "anthropic"
```

### Pricing Data (as of 2024-12)

| Provider  | Model              | Input ($/1M) | Output ($/1M) |
|-----------|--------------------|--------------|---------------|
| Anthropic | claude-sonnet-4-*  | $3.00        | $15.00        |
| Anthropic | claude-opus-4-*    | $15.00       | $75.00        |
| Anthropic | claude-haiku-3-5-* | $0.80        | $4.00         |
| OpenAI    | gpt-4o             | $2.50        | $10.00        |
| OpenAI    | gpt-4o-mini        | $0.15        | $0.60         |
| OpenAI    | o1                 | $15.00       | $60.00        |

---

## Files

### Backend

| File                                          | Action | What                             |
|-----------------------------------------------|--------|----------------------------------|
| `src/qyl.collector/Pricing/ModelPricing.cs`   | Create | Pricing lookup                   |
| `src/qyl.collector/Pricing/CostCalculator.cs` | Create | Cost computation                 |
| `src/qyl.collector/Storage/DuckDbSchema.cs`   | Modify | Add cost columns                 |
| `src/qyl.collector/Query/CostQueryService.cs` | Create | Cost aggregation queries         |
| `src/qyl.collector/Program.cs`                | Modify | Register services, add endpoints |

### Dashboard

| File                                                       | Action | What                    |
|------------------------------------------------------------|--------|-------------------------|
| `src/qyl.dashboard/src/pages/CostsPage.tsx`                | Create | Cost dashboard page     |
| `src/qyl.dashboard/src/components/costs/CostChart.tsx`     | Create | Cost over time chart    |
| `src/qyl.dashboard/src/components/costs/CostBreakdown.tsx` | Create | By model/provider table |
| `src/qyl.dashboard/src/hooks/use-costs.ts`                 | Create | Cost data hooks         |

---

## Implementation

### Backend Step 1: Model Pricing

**File:** `src/qyl.collector/Pricing/ModelPricing.cs`

```csharp
namespace qyl.collector.Pricing;

public static class ModelPricing
{
    // Prices in USD per 1M tokens
    private static readonly FrozenDictionary<string, (decimal Input, decimal Output)> Prices =
        new Dictionary<string, (decimal, decimal)>
        {
            // Anthropic
            ["claude-sonnet-4"] = (3.00m, 15.00m),
            ["claude-opus-4"] = (15.00m, 75.00m),
            ["claude-haiku-3-5"] = (0.80m, 4.00m),

            // OpenAI
            ["gpt-4o"] = (2.50m, 10.00m),
            ["gpt-4o-mini"] = (0.15m, 0.60m),
            ["o1"] = (15.00m, 60.00m),
            ["o1-mini"] = (3.00m, 12.00m),

            // Default fallback
            ["unknown"] = (1.00m, 3.00m),
        }.ToFrozenDictionary();

    public static (decimal InputPrice, decimal OutputPrice) GetPricing(string model)
    {
        // Normalize model name (remove date suffixes)
        var normalized = NormalizeModelName(model);

        return Prices.TryGetValue(normalized, out var pricing)
            ? pricing
            : Prices["unknown"];
    }

    public static decimal CalculateCost(string model, int inputTokens, int outputTokens)
    {
        var (inputPrice, outputPrice) = GetPricing(model);

        var inputCost = inputTokens * inputPrice / 1_000_000m;
        var outputCost = outputTokens * outputPrice / 1_000_000m;

        return inputCost + outputCost;
    }

    private static string NormalizeModelName(string model)
    {
        // "claude-sonnet-4-20250514" -> "claude-sonnet-4"
        // "gpt-4o-2024-08-06" -> "gpt-4o"
        var parts = model.Split('-');
        var normalized = new List<string>();

        foreach (var part in parts)
        {
            // Stop at date-like segments (8 digits)
            if (part.Length == 8 && part.All(char.IsDigit))
                break;
            normalized.Add(part);
        }

        return string.Join("-", normalized);
    }
}
```

### Backend Step 2: Schema Update

**File:** `src/qyl.collector/Storage/DuckDbSchema.cs`

```csharp
// Add to spans table DDL
cost_usd DECIMAL(10, 6),
```

### Backend Step 3: Cost Query Service

**File:** `src/qyl.collector/Query/CostQueryService.cs`

```csharp
namespace qyl.collector.Query;

public sealed class CostQueryService(DuckDbStore store)
{
    public async Task<CostSummary> GetCostSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                SUM(cost_usd) as total_cost,
                SUM(CAST(attributes->>'gen_ai.usage.input_tokens' AS INT)) as total_input,
                SUM(CAST(attributes->>'gen_ai.usage.output_tokens' AS INT)) as total_output,
                COUNT(*) as request_count
            FROM spans
            WHERE start_time >= ? AND start_time <= ?
              AND attributes->>'gen_ai.operation.name' IS NOT NULL
            """;

        return await store.QuerySingleAsync<CostSummary>(sql, from, to, ct);
    }

    public async IAsyncEnumerable<CostByModel> GetCostByModelAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                attributes->>'gen_ai.request.model' as model,
                attributes->>'gen_ai.provider.name' as provider,
                SUM(cost_usd) as cost,
                SUM(CAST(attributes->>'gen_ai.usage.input_tokens' AS INT)) as input_tokens,
                SUM(CAST(attributes->>'gen_ai.usage.output_tokens' AS INT)) as output_tokens,
                COUNT(*) as requests
            FROM spans
            WHERE start_time >= ? AND start_time <= ?
              AND attributes->>'gen_ai.operation.name' IS NOT NULL
            GROUP BY model, provider
            ORDER BY cost DESC
            """;

        await foreach (var row in store.QueryAsync<CostByModel>(sql, from, to, ct))
        {
            yield return row;
        }
    }

    public async IAsyncEnumerable<CostOverTime> GetCostOverTimeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string interval = "hour",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sql = $"""
            SELECT
                time_bucket(INTERVAL '1 {interval}', start_time) as bucket,
                SUM(cost_usd) as cost,
                COUNT(*) as requests
            FROM spans
            WHERE start_time >= ? AND start_time <= ?
              AND attributes->>'gen_ai.operation.name' IS NOT NULL
            GROUP BY bucket
            ORDER BY bucket
            """;

        await foreach (var row in store.QueryAsync<CostOverTime>(sql, from, to, ct))
        {
            yield return row;
        }
    }
}

public record CostSummary(decimal TotalCost, long TotalInput, long TotalOutput, long RequestCount);
public record CostByModel(string Model, string Provider, decimal Cost, long InputTokens, long OutputTokens, long Requests);
public record CostOverTime(DateTimeOffset Bucket, decimal Cost, long Requests);
```

### Backend Step 4: REST Endpoints

**File:** `src/qyl.collector/Program.cs`

```csharp
// Add to endpoint mapping
app.MapGet("/api/v1/costs/summary", async (
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    CostQueryService costs,
    CancellationToken ct) =>
{
    var start = from ?? DateTimeOffset.UtcNow.AddDays(-7);
    var end = to ?? DateTimeOffset.UtcNow;

    return TypedResults.Ok(await costs.GetCostSummaryAsync(start, end, ct));
});

app.MapGet("/api/v1/costs/by-model", (
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    CostQueryService costs,
    CancellationToken ct) =>
{
    var start = from ?? DateTimeOffset.UtcNow.AddDays(-7);
    var end = to ?? DateTimeOffset.UtcNow;

    return TypedResults.Ok(costs.GetCostByModelAsync(start, end, ct));
});

app.MapGet("/api/v1/costs/over-time", (
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    [FromQuery] string interval,
    CostQueryService costs,
    CancellationToken ct) =>
{
    var start = from ?? DateTimeOffset.UtcNow.AddDays(-7);
    var end = to ?? DateTimeOffset.UtcNow;

    return TypedResults.Ok(costs.GetCostOverTimeAsync(start, end, interval ?? "hour", ct));
});
```

### Dashboard Step 1: Costs Page

**File:** `src/qyl.dashboard/src/pages/CostsPage.tsx`

```tsx
import * as React from "react";
import { DollarSign, TrendingUp, Coins, Activity } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { CostChart } from "@/components/costs/CostChart";
import { CostBreakdown } from "@/components/costs/CostBreakdown";
import { useCostSummary, useCostOverTime, useCostByModel } from "@/hooks/use-costs";

export function CostsPage() {
  const { data: summary } = useCostSummary();
  const { data: overTime } = useCostOverTime();
  const { data: byModel } = useCostByModel();

  const formatCost = (cost: number) =>
    new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: "USD",
      minimumFractionDigits: 2,
    }).format(cost);

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-2xl font-bold">Cost Analytics</h1>

      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Total Cost</CardTitle>
            <DollarSign className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {formatCost(summary?.totalCost ?? 0)}
            </div>
            <p className="text-xs text-muted-foreground">Last 7 days</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Input Tokens</CardTitle>
            <Coins className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {((summary?.totalInput ?? 0) / 1000).toFixed(1)}K
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Output Tokens</CardTitle>
            <Coins className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {((summary?.totalOutput ?? 0) / 1000).toFixed(1)}K
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Requests</CardTitle>
            <Activity className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {summary?.requestCount ?? 0}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Cost Over Time Chart */}
      <Card>
        <CardHeader>
          <CardTitle>Cost Over Time</CardTitle>
        </CardHeader>
        <CardContent>
          <CostChart data={overTime ?? []} />
        </CardContent>
      </Card>

      {/* Cost By Model Breakdown */}
      <Card>
        <CardHeader>
          <CardTitle>Cost by Model</CardTitle>
        </CardHeader>
        <CardContent>
          <CostBreakdown data={byModel ?? []} />
        </CardContent>
      </Card>
    </div>
  );
}
```

---

## Test

```bash
# Backend
curl http://localhost:5100/api/v1/costs/summary
curl http://localhost:5100/api/v1/costs/by-model
curl http://localhost:5100/api/v1/costs/over-time?interval=hour

# Dashboard
cd /Users/ancplua/qyl/src/qyl.dashboard && npm run dev
# Navigate to /costs
```

**Verify:**

- [ ] Summary shows total cost in USD
- [ ] By-model breakdown shows cost per model
- [ ] Chart shows cost trend over time
- [ ] Token counts match span data

---

## Done When

- [ ] cost_usd column added to DuckDB schema
- [ ] Cost calculated on span ingestion
- [ ] REST endpoints return cost data
- [ ] Dashboard shows cost analytics
- [ ] Pricing updated for latest models

---

## Future

- [ ] Budget alerts when cost exceeds threshold
- [ ] Cost forecasting based on trends
- [ ] Per-user/per-team cost attribution
- [ ] Custom pricing configuration
- [ ] Export cost reports (CSV, PDF)

---

*Template v3*

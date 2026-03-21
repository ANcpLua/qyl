# Specs Implementation Plan — Cost Engine + Intelligence Model + Kill List

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development to implement this plan. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the three major gaps between specs and code: cost engine (server-side pricing + computation +
endpoints), telemetry intelligence model (C# types + pattern engine + seed data), and kill list enforcement (delete dead
projects/directories).

**Architecture:** Three independent tracks that touch different files and can run in parallel. Track A adds cost
computation to the collector's ingestion pipeline and serves 8 new API endpoints. Track B adds intelligence types to
contracts and a pattern engine to collector. Track C deletes dead code from the solution.

**Tech Stack:** .NET 10, C# 14, DuckDB, ASP.NET Core minimal APIs, xUnit v3

---

## File Map

### Track A: Cost Engine

| Action | File                                                                    | Responsibility                          |
|--------|-------------------------------------------------------------------------|-----------------------------------------|
| Create | `src/qyl.collector/Storage/Migrations/V20260317__add_model_pricing.sql` | DuckDB tables + view                    |
| Create | `src/qyl.collector/Cost/CostService.cs`                                 | Cost computation logic + pricing cache  |
| Create | `src/qyl.collector/Cost/CostEndpoints.cs`                               | 8 REST endpoints                        |
| Create | `src/qyl.collector/Storage/DuckDbStore.Cost.cs`                         | Pricing CRUD + cost aggregation queries |
| Create | `data/model-pricing.json`                                               | Seed pricing data (30 models)           |
| Modify | `src/qyl.collector/Ingestion/OtlpConverter.cs:485-512`                  | Inject cost computation at ingestion    |
| Modify | `src/qyl.collector/Program.cs:822`                                      | Register CostService + map endpoints    |
| Modify | `src/qyl.collector/QylSerializerContext.cs`                             | Add cost DTOs for AOT                   |
| Create | `tests/qyl.collector.tests/Cost/CostServiceTests.cs`                    | Cost formula unit tests                 |

### Track B: Intelligence Model

| Action | File                                                             | Responsibility                         |
|--------|------------------------------------------------------------------|----------------------------------------|
| Create | `src/qyl.contracts/Intelligence/Signal.cs`                       | Signal primitive + SignalOperator enum |
| Create | `src/qyl.contracts/Intelligence/DiagnosticPattern.cs`            | Pattern type + PatternCategory enum    |
| Create | `src/qyl.contracts/Intelligence/CausalRule.cs`                   | Causal relationship type               |
| Create | `src/qyl.contracts/Intelligence/InvestigationStrategy.cs`        | Strategy + step types                  |
| Create | `src/qyl.contracts/Intelligence/PatternMatch.cs`                 | Evaluation result types                |
| Create | `src/qyl.contracts/Intelligence/Seed/DiagnosticPatterns.cs`      | 10 seed patterns (static registry)     |
| Create | `src/qyl.contracts/Intelligence/Seed/CausalRules.cs`             | 6 seed causal rules                    |
| Create | `src/qyl.contracts/Intelligence/Seed/InvestigationStrategies.cs` | 4 seed strategies                      |
| Create | `src/qyl.collector/Intelligence/PatternEngine.cs`                | IPatternEngine implementation          |
| Create | `tests/qyl.collector.tests/Intelligence/PatternEngineTests.cs`   | Pattern matching tests                 |

### Track C: Kill List Enforcement

| Action | File                                        | Responsibility                                        |
|--------|---------------------------------------------|-------------------------------------------------------|
| Delete | `src/qyl.hosting/`                          | Dead project (architecture spec section 5)            |
| Delete | `src/qyl.watch/`                            | Dead project                                          |
| Delete | `src/qyl.browser/`                          | Dead project                                          |
| Delete | `src/qyl.collector/BuildFailures/`          | Dead feature directory                                |
| Delete | `src/qyl.collector/ConsoleBridge/`          | Dead feature directory                                |
| Modify | `qyl.slnx`                                  | Remove dead project references                        |
| Modify | `src/qyl.collector/Program.cs`              | Remove dead endpoint mappings + service registrations |
| Modify | `src/qyl.collector/QylSerializerContext.cs` | Remove dead type references                           |
| Modify | `src/qyl.collector/Dockerfile`              | Remove dead COPY steps if any                         |
| Modify | `CHANGELOG.md`                              | Document all changes                                  |

---

## Task 1: Cost Engine — DuckDB Migration + Seed Data

**Files:**

- Create: `src/qyl.collector/Storage/Migrations/V20260317__add_model_pricing.sql`
- Create: `data/model-pricing.json`

- [ ] **Step 1: Create pricing migration SQL**

```sql
-- V20260317__add_model_pricing.sql
-- Cost engine: pricing tables and pre-aggregated cost view

CREATE TABLE IF NOT EXISTS model_pricing (
    provider       VARCHAR NOT NULL,
    model          VARCHAR NOT NULL,
    input_cost     DOUBLE NOT NULL,      -- per 1M input tokens
    output_cost    DOUBLE NOT NULL,      -- per 1M output tokens
    reasoning_cost DOUBLE,               -- per 1M reasoning tokens (NULL if N/A)
    cache_read_cost  DOUBLE,             -- per 1M cached input tokens (NULL if N/A)
    cache_write_cost DOUBLE,             -- per 1M cache write tokens (NULL if N/A)
    valid_from     TIMESTAMP NOT NULL DEFAULT now(),
    valid_to       TIMESTAMP,            -- NULL = current pricing
    PRIMARY KEY (provider, model, valid_from)
);

CREATE TABLE IF NOT EXISTS model_pricing_tiers (
    provider         VARCHAR NOT NULL,
    model            VARCHAR NOT NULL,
    tier_name        VARCHAR NOT NULL,   -- 'standard', 'batch', 'volume_1m+'
    input_cost       DOUBLE NOT NULL,
    output_cost      DOUBLE NOT NULL,
    reasoning_cost   DOUBLE,
    min_tokens       BIGINT,             -- threshold to activate (NULL = default)
    valid_from       TIMESTAMP NOT NULL DEFAULT now(),
    PRIMARY KEY (provider, model, tier_name, valid_from)
);

CREATE OR REPLACE VIEW cost_by_model_hourly AS
SELECT
    date_trunc('hour', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket,
    service_name AS service,
    gen_ai_request_model AS model,
    gen_ai_provider_name AS provider,
    COUNT(*) AS call_count,
    COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
    COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
    COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost
FROM spans
WHERE gen_ai_request_model IS NOT NULL
GROUP BY ALL;
```

- [ ] **Step 2: Create seed pricing data**

Create `data/model-pricing.json` with current pricing for top models across OpenAI, Anthropic, Google, Meta, Mistral.
All costs per 1M tokens. Use real pricing as of March 2026 (WebSearch if uncertain — per ground-truth rules, do NOT
guess versions/prices).

Structure:

```json
[
  {
    "provider": "openai",
    "model": "gpt-4o",
    "input_cost": 2.50,
    "output_cost": 10.00,
    "reasoning_cost": null,
    "cache_read_cost": 1.25,
    "cache_write_cost": null
  }
]
```

Include: gpt-4o, gpt-4o-mini, gpt-4.1, gpt-4.1-mini, gpt-4.1-nano, o3, o3-mini, o4-mini, claude-sonnet-4-6,
claude-opus-4-6, claude-haiku-4-5, gemini-2.5-pro, gemini-2.5-flash, gemini-2.0-flash, llama-4-maverick, llama-4-scout,
mistral-large, codestral, deepseek-v3, deepseek-r1, command-a.

- [ ] **Step 3: Commit**

```bash
git add src/qyl.collector/Storage/Migrations/V20260317__add_model_pricing.sql data/model-pricing.json
git commit -m "feat(cost): add model_pricing DuckDB migration and seed data"
```

---

## Task 2: Cost Engine — CostService + DuckDbStore.Cost.cs

**Files:**

- Create: `src/qyl.collector/Cost/CostService.cs`
- Create: `src/qyl.collector/Storage/DuckDbStore.Cost.cs`
- Create: `tests/qyl.collector.tests/Cost/CostServiceTests.cs`

- [ ] **Step 1: Write cost formula tests**

Create `tests/qyl.collector.tests/Cost/CostServiceTests.cs`:

```csharp
namespace Qyl.Collector.Tests.Cost;

public sealed class CostServiceTests
{
    [Fact]
    public void ComputeCost_StandardModel_CalculatesCorrectly()
    {
        var pricing = new ModelPricing("openai", "gpt-4o", 2.50, 10.00);
        var cost = CostService.ComputeCost(pricing, InputTokens: 1000, OutputTokens: 500);
        Assert.Equal(0.0025 + 0.005, cost, precision: 10);
    }

    [Fact]
    public void ComputeCost_WithReasoningTokens_IncludesReasoningCost()
    {
        var pricing = new ModelPricing("openai", "o3", 2.00, 8.00, ReasoningCost: 8.00);
        var cost = CostService.ComputeCost(pricing, InputTokens: 1000, OutputTokens: 500, ReasoningTokens: 200);
        Assert.Equal(0.002 + 0.004 + 0.0016, cost, precision: 10);
    }

    [Fact]
    public void ComputeCost_WithCachedInput_IncludesCacheReadCost()
    {
        var pricing = new ModelPricing("openai", "gpt-4o", 2.50, 10.00, CacheReadCost: 1.25);
        var cost = CostService.ComputeCost(pricing, InputTokens: 1000, OutputTokens: 500, CachedInputTokens: 300);
        Assert.Equal(0.0025 + 0.005 + 0.000375, cost, precision: 10);
    }

    [Fact]
    public void ComputeCost_NullPricing_ReturnsNull()
    {
        var cost = CostService.ComputeCost(null, InputTokens: 1000, OutputTokens: 500);
        Assert.Null(cost);
    }

    [Fact]
    public void ComputeCost_ZeroTokens_ReturnsZero()
    {
        var pricing = new ModelPricing("openai", "gpt-4o", 2.50, 10.00);
        var cost = CostService.ComputeCost(pricing, InputTokens: 0, OutputTokens: 0);
        Assert.Equal(0.0, cost);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/qyl.collector.tests --filter-class "*CostServiceTests" -- exit code 2 or compilation error expected
```

- [ ] **Step 3: Implement CostService**

Create `src/qyl.collector/Cost/CostService.cs`:

```csharp
using System.Collections.Frozen;
using System.Text.Json;

namespace Qyl.Collector.Cost;

/// <summary>
///     Server-side cost computation from GenAI spans + pricing table.
///     Formula: cost = input_tokens * (input_cost/1M) + output_tokens * (output_cost/1M)
///                   + reasoning_tokens * (reasoning_cost/1M) + cached_input * (cache_read_cost/1M)
/// </summary>
public sealed class CostService(DuckDbStore store, ILogger<CostService> logger)
{
    private FrozenDictionary<string, ModelPricing> _pricingCache = FrozenDictionary<string, ModelPricing>.Empty;
    private readonly Lock _cacheLock = new();

    /// <summary>
    ///     Pure computation. No I/O, no side effects.
    /// </summary>
    public static double? ComputeCost(
        ModelPricing? pricing,
        long? InputTokens = null,
        long? OutputTokens = null,
        long? ReasoningTokens = null,
        long? CachedInputTokens = null)
    {
        if (pricing is null) return null;

        var input = (InputTokens ?? 0) * (pricing.InputCost / 1_000_000.0);
        var output = (OutputTokens ?? 0) * (pricing.OutputCost / 1_000_000.0);
        var reasoning = pricing.ReasoningCost is not null && ReasoningTokens is not null
            ? ReasoningTokens.Value * (pricing.ReasoningCost.Value / 1_000_000.0)
            : 0.0;
        var cached = pricing.CacheReadCost is not null && CachedInputTokens is not null
            ? CachedInputTokens.Value * (pricing.CacheReadCost.Value / 1_000_000.0)
            : 0.0;

        return input + output + reasoning + cached;
    }

    /// <summary>
    ///     Lookup pricing for a provider+model pair. Uses in-memory cache, refreshed on demand.
    /// </summary>
    public ModelPricing? GetPricing(string? provider, string? model)
    {
        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model)) return null;
        var key = $"{provider}:{model}";
        return _pricingCache.GetValueOrDefault(key);
    }

    /// <summary>
    ///     Refresh the in-memory pricing cache from DuckDB.
    /// </summary>
    public async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        var pricing = await store.GetCurrentPricingAsync(ct).ConfigureAwait(false);
        var dict = pricing.ToFrozenDictionary(p => $"{p.Provider}:{p.Model}");
        lock (_cacheLock)
        {
            _pricingCache = dict;
        }
        logger.LogInformation("Pricing cache refreshed: {Count} models", pricing.Count);
    }

    /// <summary>
    ///     Load seed pricing from data/model-pricing.json if the pricing table is empty.
    /// </summary>
    public async Task SeedIfEmptyAsync(CancellationToken ct = default)
    {
        var count = await store.GetPricingCountAsync(ct).ConfigureAwait(false);
        if (count > 0) return;

        var seedPath = Path.Combine(AppContext.BaseDirectory, "data", "model-pricing.json");
        if (!File.Exists(seedPath))
        {
            logger.LogWarning("Seed pricing file not found at {Path}", seedPath);
            return;
        }

        await using var stream = File.OpenRead(seedPath);
        var seed = await JsonSerializer.DeserializeAsync<List<ModelPricingSeed>>(stream, cancellationToken: ct)
            .ConfigureAwait(false);
        if (seed is null or { Count: 0 }) return;

        await store.InsertPricingBatchAsync(seed, ct).ConfigureAwait(false);
        logger.LogInformation("Seeded {Count} model pricing entries", seed.Count);
    }
}

public sealed record ModelPricing(
    string Provider,
    string Model,
    double InputCost,
    double OutputCost,
    double? ReasoningCost = null,
    double? CacheReadCost = null,
    double? CacheWriteCost = null);

public sealed record ModelPricingSeed(
    string Provider,
    string Model,
    double InputCost,
    double OutputCost,
    double? ReasoningCost = null,
    double? CacheReadCost = null,
    double? CacheWriteCost = null);
```

- [ ] **Step 4: Implement DuckDbStore.Cost.cs**

Create `src/qyl.collector/Storage/DuckDbStore.Cost.cs`:

```csharp
using Qyl.Collector.Cost;

namespace Qyl.Collector.Storage;

public sealed partial class DuckDbStore
{
    public async Task<IReadOnlyList<ModelPricing>> GetCurrentPricingAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT provider, model, input_cost, output_cost, reasoning_cost, cache_read_cost, cache_write_cost
            FROM model_pricing
            WHERE valid_to IS NULL
            ORDER BY provider, model
        """;
        var results = new List<ModelPricing>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ModelPricing(
                Provider: reader.GetString(0),
                Model: reader.GetString(1),
                InputCost: reader.GetDouble(2),
                OutputCost: reader.GetDouble(3),
                ReasoningCost: reader.IsDBNull(4) ? null : reader.GetDouble(4),
                CacheReadCost: reader.IsDBNull(5) ? null : reader.GetDouble(5),
                CacheWriteCost: reader.IsDBNull(6) ? null : reader.GetDouble(6)));
        }
        return results;
    }

    public async Task<long> GetPricingCountAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM model_pricing";
        return (long)(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) ?? 0L);
    }

    public async Task InsertPricingBatchAsync(IReadOnlyList<ModelPricingSeed> seeds, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await ExecuteWriteAsync(async (con, token) =>
        {
            foreach (var s in seeds)
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO model_pricing (provider, model, input_cost, output_cost, reasoning_cost, cache_read_cost, cache_write_cost)
                    VALUES ($1, $2, $3, $4, $5, $6, $7)
                    ON CONFLICT DO NOTHING
                """;
                cmd.Parameters.Add(new DuckDBParameter { Value = s.Provider });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.Model });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.InputCost });
                cmd.Parameters.Add(new DuckDBParameter { Value = s.OutputCost });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)s.ReasoningCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)s.CacheReadCost ?? DBNull.Value });
                cmd.Parameters.Add(new DuckDBParameter { Value = (object?)s.CacheWriteCost ?? DBNull.Value });
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
    }

    public async Task UpsertPricingAsync(ModelPricing pricing, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await ExecuteWriteAsync(async (con, token) =>
        {
            // Expire existing current pricing for this provider+model
            await using var expire = con.CreateCommand();
            expire.CommandText = """
                UPDATE model_pricing SET valid_to = now()
                WHERE provider = $1 AND model = $2 AND valid_to IS NULL
            """;
            expire.Parameters.Add(new DuckDBParameter { Value = pricing.Provider });
            expire.Parameters.Add(new DuckDBParameter { Value = pricing.Model });
            await expire.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Insert new current pricing
            await using var insert = con.CreateCommand();
            insert.CommandText = """
                INSERT INTO model_pricing (provider, model, input_cost, output_cost, reasoning_cost, cache_read_cost, cache_write_cost)
                VALUES ($1, $2, $3, $4, $5, $6, $7)
            """;
            insert.Parameters.Add(new DuckDBParameter { Value = pricing.Provider });
            insert.Parameters.Add(new DuckDBParameter { Value = pricing.Model });
            insert.Parameters.Add(new DuckDBParameter { Value = pricing.InputCost });
            insert.Parameters.Add(new DuckDBParameter { Value = pricing.OutputCost });
            insert.Parameters.Add(new DuckDBParameter { Value = (object?)pricing.ReasoningCost ?? DBNull.Value });
            insert.Parameters.Add(new DuckDBParameter { Value = (object?)pricing.CacheReadCost ?? DBNull.Value });
            insert.Parameters.Add(new DuckDBParameter { Value = (object?)pricing.CacheWriteCost ?? DBNull.Value });
            await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CostByModelRow>> GetCostByModelAsync(
        string? service = null, ulong? fromNano = null, ulong? toNano = null,
        int limit = 50, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var where = new List<string>();
        where.Add("gen_ai_request_model IS NOT NULL");
        if (!string.IsNullOrEmpty(service)) where.Add($"service_name = ${where.Count + 1}");
        if (fromNano is not null) where.Add($"start_time_unix_nano >= ${where.Count + 1}");
        if (toNano is not null) where.Add($"start_time_unix_nano <= ${where.Count + 1}");

        cmd.CommandText = $"""
            SELECT gen_ai_request_model, gen_ai_provider_name, service_name,
                   COUNT(*) AS call_count,
                   COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
                   COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
                   COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost
            FROM spans
            WHERE {string.Join(" AND ", where)}
            GROUP BY gen_ai_request_model, gen_ai_provider_name, service_name
            ORDER BY total_cost DESC
            LIMIT {limit}
        """;

        if (!string.IsNullOrEmpty(service)) cmd.Parameters.Add(new DuckDBParameter { Value = service });
        if (fromNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)fromNano.Value });
        if (toNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)toNano.Value });

        var results = new List<CostByModelRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new CostByModelRow(
                Model: reader.IsDBNull(0) ? "unknown" : reader.GetString(0),
                Provider: reader.IsDBNull(1) ? null : reader.GetString(1),
                Service: reader.IsDBNull(2) ? null : reader.GetString(2),
                CallCount: reader.GetInt64(3),
                TotalInputTokens: reader.GetInt64(4),
                TotalOutputTokens: reader.GetInt64(5),
                TotalCost: reader.GetDouble(6)));
        }
        return results;
    }

    public async Task<IReadOnlyList<CostByServiceRow>> GetCostByServiceAsync(
        ulong? fromNano = null, ulong? toNano = null, int limit = 50, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var where = new List<string> { "gen_ai_request_model IS NOT NULL" };
        if (fromNano is not null) where.Add($"start_time_unix_nano >= ${where.Count + 1}");
        if (toNano is not null) where.Add($"start_time_unix_nano <= ${where.Count + 1}");

        cmd.CommandText = $"""
            SELECT COALESCE(service_name, 'unknown') AS service,
                   COUNT(*) AS call_count,
                   COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
                   COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
                   COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost
            FROM spans
            WHERE {string.Join(" AND ", where)}
            GROUP BY COALESCE(service_name, 'unknown')
            ORDER BY total_cost DESC
            LIMIT {limit}
        """;

        if (fromNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)fromNano.Value });
        if (toNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)toNano.Value });

        var results = new List<CostByServiceRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new CostByServiceRow(
                Service: reader.GetString(0),
                CallCount: reader.GetInt64(1),
                TotalInputTokens: reader.GetInt64(2),
                TotalOutputTokens: reader.GetInt64(3),
                TotalCost: reader.GetDouble(4)));
        }
        return results;
    }

    public async Task<IReadOnlyList<CostTimeseriesRow>> GetCostTimeseriesAsync(
        string bucket = "hour", string? service = null, string? model = null,
        ulong? fromNano = null, ulong? toNano = null, int limit = 168, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();

        var truncate = bucket switch { "minute" => "minute", "day" => "day", _ => "hour" };
        var where = new List<string> { "gen_ai_request_model IS NOT NULL" };
        if (!string.IsNullOrEmpty(service)) where.Add($"service_name = ${where.Count + 1}");
        if (!string.IsNullOrEmpty(model)) where.Add($"gen_ai_request_model = ${where.Count + 1}");
        if (fromNano is not null) where.Add($"start_time_unix_nano >= ${where.Count + 1}");
        if (toNano is not null) where.Add($"start_time_unix_nano <= ${where.Count + 1}");

        cmd.CommandText = $"""
            SELECT date_trunc('{truncate}', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket,
                   COUNT(*) AS call_count,
                   COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
                   COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
                   COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost
            FROM spans
            WHERE {string.Join(" AND ", where)}
            GROUP BY bucket
            ORDER BY bucket DESC
            LIMIT {limit}
        """;

        if (!string.IsNullOrEmpty(service)) cmd.Parameters.Add(new DuckDBParameter { Value = service });
        if (!string.IsNullOrEmpty(model)) cmd.Parameters.Add(new DuckDBParameter { Value = model });
        if (fromNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)fromNano.Value });
        if (toNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)toNano.Value });

        var results = new List<CostTimeseriesRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new CostTimeseriesRow(
                Bucket: reader.GetDateTime(0).ToString("o"),
                CallCount: reader.GetInt64(1),
                TotalInputTokens: reader.GetInt64(2),
                TotalOutputTokens: reader.GetInt64(3),
                TotalCost: reader.GetDouble(4)));
        }
        return results;
    }
}

// DTOs — bottom of file or in a Contracts sub-namespace
public sealed record CostByModelRow(
    string Model, string? Provider, string? Service,
    long CallCount, long TotalInputTokens, long TotalOutputTokens, double TotalCost);

public sealed record CostByServiceRow(
    string Service, long CallCount, long TotalInputTokens, long TotalOutputTokens, double TotalCost);

public sealed record CostTimeseriesRow(
    string Bucket, long CallCount, long TotalInputTokens, long TotalOutputTokens, double TotalCost);

public sealed record CostBySessionRow(
    string SessionId, string? Service, long CallCount,
    long TotalInputTokens, long TotalOutputTokens, double TotalCost);
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
dotnet test tests/qyl.collector.tests --filter-class "*CostServiceTests"
```

Expected: all 5 pass.

- [ ] **Step 6: Commit**

```bash
git add src/qyl.collector/Cost/ src/qyl.collector/Storage/DuckDbStore.Cost.cs tests/qyl.collector.tests/Cost/
git commit -m "feat(cost): implement CostService + DuckDbStore.Cost with pricing computation"
```

---

## Task 3: Cost Engine — Endpoints + Wiring

**Files:**

- Create: `src/qyl.collector/Cost/CostEndpoints.cs`
- Modify: `src/qyl.collector/Program.cs`
- Modify: `src/qyl.collector/QylSerializerContext.cs`
- Modify: `src/qyl.collector/Ingestion/OtlpConverter.cs`

- [ ] **Step 1: Create CostEndpoints.cs**

Create `src/qyl.collector/Cost/CostEndpoints.cs`:

```csharp
using Qyl.Collector.Storage;

namespace Qyl.Collector.Cost;

public static class CostEndpoints
{
    public static WebApplication MapCostEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cost").WithTags("Cost");

        group.MapGet("/by-model", GetCostByModelAsync);
        group.MapGet("/by-service", GetCostByServiceAsync);
        group.MapGet("/by-session", GetCostBySessionAsync);
        group.MapGet("/timeseries", GetCostTimeseriesAsync);
        group.MapGet("/budget", GetBudgetStatusAsync);
        group.MapGet("/pricing", GetCurrentPricingAsync);
        group.MapPut("/pricing/{provider}/{model}", UpsertPricingAsync);
        group.MapPost("/sync-pricing", SyncPricingAsync);

        return app;
    }

    private static async Task<IResult> GetCostByModelAsync(
        DuckDbStore store, string? service, ulong? from, ulong? to, int? limit, CancellationToken ct)
    {
        var rows = await store.GetCostByModelAsync(service, from, to, Math.Clamp(limit ?? 50, 1, 200), ct)
            .ConfigureAwait(false);
        return Results.Ok(new { items = rows, total = rows.Count });
    }

    private static async Task<IResult> GetCostByServiceAsync(
        DuckDbStore store, ulong? from, ulong? to, int? limit, CancellationToken ct)
    {
        var rows = await store.GetCostByServiceAsync(from, to, Math.Clamp(limit ?? 50, 1, 200), ct)
            .ConfigureAwait(false);
        return Results.Ok(new { items = rows, total = rows.Count });
    }

    private static async Task<IResult> GetCostBySessionAsync(
        DuckDbStore store, string? service, ulong? from, ulong? to, int? limit, CancellationToken ct)
    {
        // Session cost query: group spans by session_id
        var rows = await store.GetCostBySessionAsync(service, from, to, Math.Clamp(limit ?? 50, 1, 200), ct)
            .ConfigureAwait(false);
        return Results.Ok(new { items = rows, total = rows.Count });
    }

    private static async Task<IResult> GetCostTimeseriesAsync(
        DuckDbStore store, string? bucket, string? service, string? model,
        ulong? from, ulong? to, int? limit, CancellationToken ct)
    {
        var rows = await store.GetCostTimeseriesAsync(
            bucket ?? "hour", service, model, from, to,
            Math.Clamp(limit ?? 168, 1, 1000), ct).ConfigureAwait(false);
        return Results.Ok(new { items = rows, total = rows.Count });
    }

    private static Task<IResult> GetBudgetStatusAsync(DuckDbStore store, CancellationToken ct) =>
        Task.FromResult(Results.Ok(new { status = "no_budget_configured", spend_today = 0.0 }));

    private static async Task<IResult> GetCurrentPricingAsync(DuckDbStore store, CancellationToken ct)
    {
        var pricing = await store.GetCurrentPricingAsync(ct).ConfigureAwait(false);
        return Results.Ok(new { items = pricing, total = pricing.Count });
    }

    private static async Task<IResult> UpsertPricingAsync(
        string provider, string model, ModelPricing pricing, DuckDbStore store, CostService costService,
        CancellationToken ct)
    {
        var p = pricing with { Provider = provider, Model = model };
        await store.UpsertPricingAsync(p, ct).ConfigureAwait(false);
        await costService.RefreshCacheAsync(ct).ConfigureAwait(false);
        return Results.Ok(p);
    }

    private static async Task<IResult> SyncPricingAsync(CostService costService, CancellationToken ct)
    {
        await costService.SeedIfEmptyAsync(ct).ConfigureAwait(false);
        await costService.RefreshCacheAsync(ct).ConfigureAwait(false);
        return Results.Ok(new { synced = true });
    }
}
```

- [ ] **Step 2: Register CostService and map endpoints in Program.cs**

In `Program.cs`, add:

- `builder.Services.AddSingleton<CostService>();` (near other singleton registrations)
- `app.MapCostEndpoints();` (near line 825, alongside other endpoint mappers)
- After app starts, call `costService.SeedIfEmptyAsync()` and `costService.RefreshCacheAsync()` in the startup block

- [ ] **Step 3: Add cost DTOs to QylSerializerContext.cs**

Add these `[JsonSerializable]` attributes:

```csharp
[JsonSerializable(typeof(ModelPricing))]
[JsonSerializable(typeof(ModelPricing[]))]
[JsonSerializable(typeof(List<ModelPricing>))]
[JsonSerializable(typeof(CostByModelRow))]
[JsonSerializable(typeof(List<CostByModelRow>))]
[JsonSerializable(typeof(CostByServiceRow))]
[JsonSerializable(typeof(List<CostByServiceRow>))]
[JsonSerializable(typeof(CostTimeseriesRow))]
[JsonSerializable(typeof(List<CostTimeseriesRow>))]
[JsonSerializable(typeof(CostBySessionRow))]
[JsonSerializable(typeof(List<CostBySessionRow>))]
```

- [ ] **Step 4: Integrate cost computation into OtlpConverter**

In `OtlpConverter.cs`, modify the `ExtractGenAiAttributes` method (line 506). The current code reads `gen_ai.usage.cost`
from the OTLP attribute. We need to **also** compute cost from pricing when the attribute is absent.

This requires injecting CostService. Since OtlpConverter is static, the cleanest approach is to:

1. Add a `ComputeCostIfMissing` static method that takes the GenAiData + CostService
2. Call it from the conversion methods after extraction

In the conversion methods (`ConvertProtoToStorageRows` and `ConvertJsonToStorageRows`), after `ExtractGenAiAttributes`:

```csharp
// After genAi = ExtractGenAiAttributes(attributes):
if (genAi.CostUsd is null && genAi.ProviderName is not null && genAi.RequestModel is not null)
{
    var pricing = costService?.GetPricing(genAi.ProviderName, genAi.RequestModel);
    if (pricing is not null)
    {
        var computed = CostService.ComputeCost(pricing, genAi.InputTokens, genAi.OutputTokens);
        genAi = genAi with { CostUsd = computed };
    }
}
```

Note: This changes OtlpConverter from static to requiring a CostService dependency. The conversion methods need a
`CostService?` parameter added. Update call sites accordingly.

- [ ] **Step 5: Add GetCostBySessionAsync to DuckDbStore.Cost.cs**

```csharp
public async Task<IReadOnlyList<CostBySessionRow>> GetCostBySessionAsync(
    string? service = null, ulong? fromNano = null, ulong? toNano = null,
    int limit = 50, CancellationToken ct = default)
{
    ThrowIfDisposed();
    await using var lease = await RentReadAsync(ct).ConfigureAwait(false);
    await using var cmd = lease.Connection.CreateCommand();

    var where = new List<string> { "gen_ai_request_model IS NOT NULL", "session_id IS NOT NULL" };
    if (!string.IsNullOrEmpty(service)) where.Add($"service_name = ${where.Count + 1}");
    if (fromNano is not null) where.Add($"start_time_unix_nano >= ${where.Count + 1}");
    if (toNano is not null) where.Add($"start_time_unix_nano <= ${where.Count + 1}");

    cmd.CommandText = $"""
        SELECT session_id, service_name,
               COUNT(*) AS call_count,
               COALESCE(SUM(gen_ai_input_tokens), 0) AS total_input_tokens,
               COALESCE(SUM(gen_ai_output_tokens), 0) AS total_output_tokens,
               COALESCE(SUM(gen_ai_cost_usd), 0) AS total_cost
        FROM spans
        WHERE {string.Join(" AND ", where)}
        GROUP BY session_id, service_name
        ORDER BY total_cost DESC
        LIMIT {limit}
    """;

    if (!string.IsNullOrEmpty(service)) cmd.Parameters.Add(new DuckDBParameter { Value = service });
    if (fromNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)fromNano.Value });
    if (toNano is not null) cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)toNano.Value });

    var results = new List<CostBySessionRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
    {
        results.Add(new CostBySessionRow(
            SessionId: reader.GetString(0),
            Service: reader.IsDBNull(1) ? null : reader.GetString(1),
            CallCount: reader.GetInt64(2),
            TotalInputTokens: reader.GetInt64(3),
            TotalOutputTokens: reader.GetInt64(4),
            TotalCost: reader.GetDouble(5)));
    }
    return results;
}
```

- [ ] **Step 6: Build and verify**

```bash
nuke
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/qyl.collector/Cost/CostEndpoints.cs src/qyl.collector/Program.cs src/qyl.collector/QylSerializerContext.cs src/qyl.collector/Ingestion/OtlpConverter.cs src/qyl.collector/Storage/DuckDbStore.Cost.cs
git commit -m "feat(cost): add 8 cost API endpoints and ingestion-time computation"
```

---

## Task 4: Intelligence Model — Contracts (C# Types)

**Files:**

- Create: `src/qyl.contracts/Intelligence/Signal.cs`
- Create: `src/qyl.contracts/Intelligence/DiagnosticPattern.cs`
- Create: `src/qyl.contracts/Intelligence/CausalRule.cs`
- Create: `src/qyl.contracts/Intelligence/InvestigationStrategy.cs`
- Create: `src/qyl.contracts/Intelligence/PatternMatch.cs`

**Note:** Per spec, these should be TypeSpec-generated. Since the TypeSpec intelligence definitions don't exist yet and
TypeSpec compilation requires a working Node.js pipeline, we write the C# types directly following the exact TypeSpec
schema from `specs/telemetry-intelligence.md` section 3.1. These can be replaced with generated code later when TypeSpec
definitions are added.

- [ ] **Step 1: Create Signal.cs**

```csharp
namespace Qyl.Contracts.Intelligence;

public enum SignalOperator
{
    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,
    Contains,
    Exists,
    NotExists,
    Matches,
    In
}

public sealed record Signal
{
    public required string Attribute { get; init; }
    public required SignalOperator Operator { get; init; }
    public string? Value { get; init; }
}
```

- [ ] **Step 2: Create DiagnosticPattern.cs**

```csharp
namespace Qyl.Contracts.Intelligence;

public enum PatternCategory
{
    Error,
    Latency,
    Cost,
    Availability,
    GenAi,
    Data
}

public sealed record DiagnosticPattern
{
    public required string Id { get; init; }
    public required PatternCategory Category { get; init; }
    public required IReadOnlyList<Signal> Signals { get; init; }
    public required string Hypothesis { get; init; }
    public required double Confidence { get; init; }
}
```

- [ ] **Step 3: Create CausalRule.cs**

```csharp
namespace Qyl.Contracts.Intelligence;

public sealed record CausalRule
{
    public required string Id { get; init; }
    public required string CausePattern { get; init; }
    public required string EffectPattern { get; init; }
    public required double Strength { get; init; }
    public string? TemporalWindow { get; init; }
}
```

- [ ] **Step 4: Create InvestigationStrategy.cs**

```csharp
namespace Qyl.Contracts.Intelligence;

public sealed record InvestigationStep
{
    public required string Action { get; init; }
    public required string Query { get; init; }
    public required string Description { get; init; }
}

public sealed record InvestigationStrategy
{
    public required string Id { get; init; }
    public required string TriggerPattern { get; init; }
    public required IReadOnlyList<InvestigationStep> Steps { get; init; }
}
```

- [ ] **Step 5: Create PatternMatch.cs** (evaluation result types)

```csharp
namespace Qyl.Contracts.Intelligence;

public sealed record PatternMatch(
    DiagnosticPattern Pattern,
    double Score,
    IReadOnlyList<Signal> MatchedSignals);

public sealed record CausalGraph(
    IReadOnlyList<CausalEdge> Edges,
    IReadOnlyList<string> RootCauses);

public sealed record CausalEdge(
    string CausePatternId,
    string EffectPatternId,
    double Strength);
```

- [ ] **Step 6: Verify contracts compile**

```bash
dotnet build src/qyl.contracts/qyl.contracts.csproj
```

Expected: success with zero warnings.

- [ ] **Step 7: Commit**

```bash
git add src/qyl.contracts/Intelligence/
git commit -m "feat(intelligence): add telemetry intelligence model types to contracts"
```

---

## Task 5: Intelligence Model — Seed Data Registries

**Files:**

- Create: `src/qyl.contracts/Intelligence/Seed/DiagnosticPatterns.cs`
- Create: `src/qyl.contracts/Intelligence/Seed/CausalRules.cs`
- Create: `src/qyl.contracts/Intelligence/Seed/InvestigationStrategies.cs`

- [ ] **Step 1: Create DiagnosticPatterns.cs seed registry**

All 10 patterns from `specs/telemetry-intelligence.md` section 5.1.

```csharp
namespace Qyl.Contracts.Intelligence.Seed;

public static class DiagnosticPatterns
{
    public static readonly IReadOnlyList<DiagnosticPattern> All =
    [
        new()
        {
            Id = "genai_rate_limit",
            Category = PatternCategory.GenAi,
            Signals =
            [
                new() { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
                new() { Attribute = "gen_ai_provider_name", Operator = SignalOperator.Exists },
                new() { Attribute = "error_type", Operator = SignalOperator.Contains, Value = "rate_limit" },
            ],
            Hypothesis = "LLM provider is throttling requests. Check quota, reduce concurrency, or add backoff.",
            Confidence = 0.9,
        },
        new()
        {
            Id = "genai_token_exhaustion",
            Category = PatternCategory.GenAi,
            Signals =
            [
                new() { Attribute = "gen_ai_stop_reason", Operator = SignalOperator.Contains, Value = "length" },
            ],
            Hypothesis = "Context window exceeded. Reduce prompt size or switch to larger model.",
            Confidence = 0.85,
        },
        new()
        {
            Id = "genai_content_filter",
            Category = PatternCategory.GenAi,
            Signals =
            [
                new() { Attribute = "gen_ai_stop_reason", Operator = SignalOperator.Contains, Value = "content_filter" },
            ],
            Hypothesis = "Content policy violation. Review prompt content.",
            Confidence = 0.9,
        },
        new()
        {
            Id = "db_timeout",
            Category = PatternCategory.Data,
            Signals =
            [
                new() { Attribute = "exception_type", Operator = SignalOperator.Contains, Value = "TimeoutException" },
                new() { Attribute = "duration_ns", Operator = SignalOperator.Gt, Value = "2000000000" },
            ],
            Hypothesis = "Database query timeout. Check query plan, connection pool, lock contention.",
            Confidence = 0.8,
        },
        new()
        {
            Id = "http_5xx_cluster",
            Category = PatternCategory.Error,
            Signals =
            [
                new() { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
                new() { Attribute = "error_type", Operator = SignalOperator.Exists },
            ],
            Hypothesis = "Server error spike. Check recent deployments and upstream dependencies.",
            Confidence = 0.7,
        },
        new()
        {
            Id = "deployment_regression",
            Category = PatternCategory.Error,
            Signals =
            [
                new() { Attribute = "error_type", Operator = SignalOperator.Exists },
                new() { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
            ],
            Hypothesis = "New error class after deployment. Compare with previous version.",
            Confidence = 0.75,
        },
        new()
        {
            Id = "cascading_timeout",
            Category = PatternCategory.Latency,
            Signals =
            [
                new() { Attribute = "exception_type", Operator = SignalOperator.Contains, Value = "Timeout" },
                new() { Attribute = "duration_ns", Operator = SignalOperator.Gt, Value = "5000000000" },
            ],
            Hypothesis = "Upstream failure causing downstream timeouts. Investigate root service first.",
            Confidence = 0.7,
        },
        new()
        {
            Id = "memory_pressure_latency",
            Category = PatternCategory.Latency,
            Signals =
            [
                new() { Attribute = "duration_ns", Operator = SignalOperator.Gt, Value = "3000000000" },
            ],
            Hypothesis = "GC pressure causing latency. Check memory allocation patterns.",
            Confidence = 0.5,
        },
        new()
        {
            Id = "cost_spike",
            Category = PatternCategory.Cost,
            Signals =
            [
                new() { Attribute = "gen_ai_cost_usd", Operator = SignalOperator.Gt, Value = "0" },
                new() { Attribute = "gen_ai_request_model", Operator = SignalOperator.Exists },
            ],
            Hypothesis = "Abnormal cost increase. Identify the model, service, and session responsible.",
            Confidence = 0.6,
        },
        new()
        {
            Id = "db_n_plus_one",
            Category = PatternCategory.Data,
            Signals =
            [
                new() { Attribute = "parent_span_id", Operator = SignalOperator.Exists },
            ],
            Hypothesis = "N+1 query pattern. Batch or prefetch related data.",
            Confidence = 0.65,
        },
    ];

    public static DiagnosticPattern? GetById(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
}
```

- [ ] **Step 2: Create CausalRules.cs seed registry**

```csharp
namespace Qyl.Contracts.Intelligence.Seed;

public static class CausalRules
{
    public static readonly IReadOnlyList<CausalRule> All =
    [
        new() { Id = "deploy_causes_regression", CausePattern = "deployment_regression", EffectPattern = "http_5xx_cluster", Strength = 0.85, TemporalWindow = "1h" },
        new() { Id = "rate_limit_causes_cascade", CausePattern = "genai_rate_limit", EffectPattern = "cascading_timeout", Strength = 0.70, TemporalWindow = "5m" },
        new() { Id = "db_timeout_causes_http_error", CausePattern = "db_timeout", EffectPattern = "http_5xx_cluster", Strength = 0.80, TemporalWindow = "1m" },
        new() { Id = "n_plus_one_causes_db_timeout", CausePattern = "db_n_plus_one", EffectPattern = "db_timeout", Strength = 0.75, TemporalWindow = "30s" },
        new() { Id = "memory_causes_timeout", CausePattern = "memory_pressure_latency", EffectPattern = "cascading_timeout", Strength = 0.65, TemporalWindow = "5m" },
        new() { Id = "token_exhaustion_causes_cost", CausePattern = "genai_token_exhaustion", EffectPattern = "cost_spike", Strength = 0.60, TemporalWindow = "1h" },
    ];
}
```

- [ ] **Step 3: Create InvestigationStrategies.cs seed registry**

```csharp
namespace Qyl.Contracts.Intelligence.Seed;

public static class InvestigationStrategies
{
    public static readonly IReadOnlyList<InvestigationStrategy> All =
    [
        new()
        {
            Id = "investigate_error_issue",
            TriggerPattern = "http_5xx_cluster",
            Steps =
            [
                new() { Action = "get_issue", Query = "SELECT * FROM error_issues WHERE id = $1", Description = "Get issue summary, occurrence count, first/last seen" },
                new() { Action = "get_events", Query = "SELECT * FROM error_issue_events WHERE issue_id = $1 ORDER BY timestamp DESC LIMIT 10", Description = "Get recent error occurrences with trace IDs" },
                new() { Action = "get_traces", Query = "SELECT * FROM spans WHERE trace_id IN ($1) ORDER BY start_time_unix_nano", Description = "Reconstruct full trace graph for each occurrence" },
                new() { Action = "get_code_location", Query = "SELECT code_filepath, code_function, code_lineno FROM spans WHERE span_id = $1", Description = "Map error to source file and function" },
                new() { Action = "correlate_deployment", Query = "SELECT * FROM deployments WHERE service_name = $1 AND start_time <= $2 ORDER BY start_time DESC LIMIT 1", Description = "Find the deployment active when error occurred" },
                new() { Action = "check_fix_history", Query = "SELECT * FROM fix_runs WHERE issue_id = $1", Description = "Check if this error class was fixed before" },
            ],
        },
        new()
        {
            Id = "investigate_latency",
            TriggerPattern = "cascading_timeout",
            Steps =
            [
                new() { Action = "identify_service", Query = "SELECT service_name, AVG(duration_ns) as avg_dur, PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns) as p99 FROM spans GROUP BY service_name ORDER BY avg_dur DESC LIMIT 10", Description = "Find the slowest service" },
                new() { Action = "compare_distributions", Query = "SELECT duration_ns FROM spans WHERE service_name = $1 AND start_time_unix_nano BETWEEN $2 AND $3", Description = "Compare current vs baseline latency distribution" },
                new() { Action = "find_regression_window", Query = "SELECT date_trunc('hour', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket, PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns) as p99 FROM spans WHERE service_name = $1 GROUP BY bucket ORDER BY bucket", Description = "Identify when latency degraded" },
                new() { Action = "correlate_deployment", Query = "SELECT * FROM deployments WHERE service_name = $1 AND start_time <= $2 ORDER BY start_time DESC LIMIT 1", Description = "Find deployment in the regression window" },
                new() { Action = "inspect_slow_spans", Query = "SELECT * FROM spans WHERE service_name = $1 AND duration_ns > $2 ORDER BY duration_ns DESC LIMIT 20", Description = "Examine the slowest individual spans" },
            ],
        },
        new()
        {
            Id = "investigate_cost",
            TriggerPattern = "cost_spike",
            Steps =
            [
                new() { Action = "identify_model", Query = "SELECT gen_ai_request_model, SUM(gen_ai_cost_usd) as cost FROM spans WHERE gen_ai_request_model IS NOT NULL GROUP BY gen_ai_request_model ORDER BY cost DESC", Description = "Find the most expensive model" },
                new() { Action = "identify_service", Query = "SELECT service_name, SUM(gen_ai_cost_usd) as cost FROM spans WHERE gen_ai_request_model IS NOT NULL GROUP BY service_name ORDER BY cost DESC", Description = "Find the most expensive service" },
                new() { Action = "identify_session", Query = "SELECT session_id, SUM(gen_ai_cost_usd) as cost FROM spans WHERE session_id IS NOT NULL AND gen_ai_request_model IS NOT NULL GROUP BY session_id ORDER BY cost DESC LIMIT 10", Description = "Find the most expensive sessions" },
                new() { Action = "trace_to_root", Query = "SELECT * FROM spans WHERE session_id = $1 ORDER BY start_time_unix_nano", Description = "Understand what operations drove the cost" },
                new() { Action = "compare_to_baseline", Query = "SELECT date_trunc('day', to_timestamp(start_time_unix_nano / 1000000000)) AS day, SUM(gen_ai_cost_usd) as daily_cost FROM spans WHERE gen_ai_request_model IS NOT NULL GROUP BY day ORDER BY day DESC LIMIT 14", Description = "Quantify the cost increase" },
            ],
        },
        new()
        {
            Id = "investigate_genai",
            TriggerPattern = "genai_rate_limit",
            Steps =
            [
                new() { Action = "get_error_details", Query = "SELECT * FROM spans WHERE status_code = 2 AND gen_ai_provider_name IS NOT NULL ORDER BY start_time_unix_nano DESC LIMIT 20", Description = "Get GenAI error spans" },
                new() { Action = "check_provider_status", Query = "SELECT gen_ai_provider_name, COUNT(*) as error_count FROM spans WHERE status_code = 2 AND gen_ai_provider_name IS NOT NULL GROUP BY gen_ai_provider_name", Description = "Determine if provider-wide issue" },
                new() { Action = "analyze_token_usage", Query = "SELECT gen_ai_input_tokens, gen_ai_output_tokens FROM spans WHERE gen_ai_request_model = $1 ORDER BY start_time_unix_nano DESC LIMIT 50", Description = "Check if approaching model limits" },
                new() { Action = "check_prompt_patterns", Query = "SELECT AVG(gen_ai_input_tokens) as avg_input, MAX(gen_ai_input_tokens) as max_input FROM spans WHERE gen_ai_request_model = $1 AND start_time_unix_nano > $2", Description = "Identify if prompts are growing unbounded" },
                new() { Action = "suggest_mitigation", Query = "", Description = "Pattern-specific recommendation: rate limit → backoff; token limit → truncation; content filter → prompt review" },
            ],
        },
    ];
}
```

- [ ] **Step 4: Verify contracts compile**

```bash
dotnet build src/qyl.contracts/qyl.contracts.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/qyl.contracts/Intelligence/Seed/
git commit -m "feat(intelligence): add v1 seed patterns, causal rules, and investigation strategies"
```

---

## Task 6: Intelligence Model — Pattern Engine

**Files:**

- Create: `src/qyl.collector/Intelligence/PatternEngine.cs`
- Create: `tests/qyl.collector.tests/Intelligence/PatternEngineTests.cs`

- [ ] **Step 1: Write pattern engine tests**

```csharp
using Qyl.Collector.Intelligence;
using Qyl.Contracts.Intelligence;
using Qyl.Contracts.Intelligence.Seed;

namespace Qyl.Collector.Tests.Intelligence;

public sealed class PatternEngineTests
{
    private readonly PatternEngine _engine = new();

    [Fact]
    public void Evaluate_GenAiRateLimit_MatchesPattern()
    {
        var signals = new List<Signal>
        {
            new() { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "2" },
            new() { Attribute = "gen_ai_provider_name", Operator = SignalOperator.Exists, Value = "openai" },
            new() { Attribute = "error_type", Operator = SignalOperator.Contains, Value = "rate_limit_exceeded" },
        };

        var matches = _engine.Evaluate(signals);
        Assert.Contains(matches, m => m.Pattern.Id == "genai_rate_limit");
    }

    [Fact]
    public void Evaluate_NoMatchingSignals_ReturnsEmpty()
    {
        var signals = new List<Signal>
        {
            new() { Attribute = "status_code", Operator = SignalOperator.Eq, Value = "1" },
        };

        var matches = _engine.Evaluate(signals);
        Assert.Empty(matches);
    }

    [Fact]
    public void BuildCausalGraph_WithChain_IdentifiesRootCause()
    {
        var matches = new List<PatternMatch>
        {
            new(DiagnosticPatterns.GetById("genai_rate_limit")!, 0.9, []),
            new(DiagnosticPatterns.GetById("cascading_timeout")!, 0.7, []),
        };

        var graph = _engine.BuildCausalGraph(matches);
        Assert.Contains("genai_rate_limit", graph.RootCauses);
        Assert.DoesNotContain("cascading_timeout", graph.RootCauses);
    }

    [Fact]
    public void SelectStrategy_ForCostSpike_ReturnsCostStrategy()
    {
        var match = new PatternMatch(DiagnosticPatterns.GetById("cost_spike")!, 0.6, []);
        var strategy = _engine.SelectStrategy(match);
        Assert.NotNull(strategy);
        Assert.Equal("investigate_cost", strategy.Id);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
dotnet test tests/qyl.collector.tests --filter-class "*PatternEngineTests"
```

- [ ] **Step 3: Implement PatternEngine**

Create `src/qyl.collector/Intelligence/PatternEngine.cs`:

```csharp
using Qyl.Contracts.Intelligence;
using Qyl.Contracts.Intelligence.Seed;

namespace Qyl.Collector.Intelligence;

public interface IPatternEngine
{
    IReadOnlyList<PatternMatch> Evaluate(IReadOnlyList<Signal> observedSignals);
    CausalGraph BuildCausalGraph(IReadOnlyList<PatternMatch> matches);
    InvestigationStrategy? SelectStrategy(PatternMatch primaryMatch);
}

/// <summary>
///     Deterministic pattern engine. Pure computation over typed data.
///     No I/O, no LLM calls, no side effects.
/// </summary>
public sealed class PatternEngine : IPatternEngine
{
    public IReadOnlyList<PatternMatch> Evaluate(IReadOnlyList<Signal> observedSignals)
    {
        var matches = new List<PatternMatch>();

        foreach (var pattern in DiagnosticPatterns.All)
        {
            var matchedSignals = new List<Signal>();
            var allMatch = true;

            foreach (var required in pattern.Signals)
            {
                var observed = FindMatchingObserved(required, observedSignals);
                if (observed is not null)
                {
                    matchedSignals.Add(observed);
                }
                else
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch && matchedSignals.Count > 0)
            {
                var score = pattern.Confidence * (matchedSignals.Count / (double)pattern.Signals.Count);
                matches.Add(new PatternMatch(pattern, score, matchedSignals));
            }
        }

        return matches.OrderByDescending(m => m.Score).ToList();
    }

    public CausalGraph BuildCausalGraph(IReadOnlyList<PatternMatch> matches)
    {
        var matchedIds = matches.Select(m => m.Pattern.Id).ToHashSet(StringComparer.Ordinal);

        var edges = CausalRules.All
            .Where(r => matchedIds.Contains(r.CausePattern) && matchedIds.Contains(r.EffectPattern))
            .Select(r => new CausalEdge(r.CausePattern, r.EffectPattern, r.Strength))
            .ToList();

        // Root causes = matched patterns that have no incoming causal edges
        var effectIds = edges.Select(e => e.EffectPatternId).ToHashSet(StringComparer.Ordinal);
        var rootCauses = matchedIds.Where(id => !effectIds.Contains(id)).ToList();

        return new CausalGraph(edges, rootCauses);
    }

    public InvestigationStrategy? SelectStrategy(PatternMatch primaryMatch)
    {
        // Direct trigger match
        var direct = InvestigationStrategies.All
            .FirstOrDefault(s => string.Equals(s.TriggerPattern, primaryMatch.Pattern.Id, StringComparison.Ordinal));
        if (direct is not null) return direct;

        // Fallback: match by category
        return primaryMatch.Pattern.Category switch
        {
            PatternCategory.Error => InvestigationStrategies.All.FirstOrDefault(s => s.Id == "investigate_error_issue"),
            PatternCategory.Latency => InvestigationStrategies.All.FirstOrDefault(s => s.Id == "investigate_latency"),
            PatternCategory.Cost => InvestigationStrategies.All.FirstOrDefault(s => s.Id == "investigate_cost"),
            PatternCategory.GenAi => InvestigationStrategies.All.FirstOrDefault(s => s.Id == "investigate_genai"),
            _ => null,
        };
    }

    private static Signal? FindMatchingObserved(Signal required, IReadOnlyList<Signal> observed)
    {
        foreach (var obs in observed)
        {
            if (!string.Equals(obs.Attribute, required.Attribute, StringComparison.Ordinal))
                continue;

            if (MatchesOperator(required.Operator, required.Value, obs.Value))
                return obs;
        }
        return null;
    }

    private static bool MatchesOperator(SignalOperator op, string? expected, string? actual) => op switch
    {
        SignalOperator.Eq => string.Equals(expected, actual, StringComparison.Ordinal),
        SignalOperator.Neq => !string.Equals(expected, actual, StringComparison.Ordinal),
        SignalOperator.Contains => actual is not null && expected is not null && actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
        SignalOperator.Exists => actual is not null,
        SignalOperator.NotExists => actual is null,
        SignalOperator.Gt => double.TryParse(actual, out var a) && double.TryParse(expected, out var e) && a > e,
        SignalOperator.Gte => double.TryParse(actual, out var a2) && double.TryParse(expected, out var e2) && a2 >= e2,
        SignalOperator.Lt => double.TryParse(actual, out var a3) && double.TryParse(expected, out var e3) && a3 < e3,
        SignalOperator.Lte => double.TryParse(actual, out var a4) && double.TryParse(expected, out var e4) && a4 <= e4,
        SignalOperator.Matches => actual is not null && expected is not null && System.Text.RegularExpressions.Regex.IsMatch(actual, expected),
        SignalOperator.In => actual is not null && expected is not null && expected.Split(',').Contains(actual, StringComparer.Ordinal),
        _ => false,
    };
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/qyl.collector.tests --filter-class "*PatternEngineTests"
```

Expected: all 4 pass.

- [ ] **Step 5: Commit**

```bash
git add src/qyl.collector/Intelligence/ tests/qyl.collector.tests/Intelligence/
git commit -m "feat(intelligence): implement deterministic pattern engine with seed data"
```

---

## Task 7: Kill List Enforcement — Delete Dead Projects

**Files:**

- Delete: `src/qyl.hosting/` (entire directory)
- Delete: `src/qyl.watch/` (entire directory)
- Delete: `src/qyl.browser/` (entire directory)
- Delete: `src/qyl.collector/BuildFailures/` (entire directory)
- Delete: `src/qyl.collector/ConsoleBridge/` (entire directory)
- Modify: `qyl.slnx`
- Modify: `src/qyl.collector/Program.cs`
- Modify: `src/qyl.collector/QylSerializerContext.cs`

- [ ] **Step 1: Remove dead projects from qyl.slnx**

Remove these lines from `qyl.slnx`:

- `<Project Path="src/qyl.hosting/qyl.hosting.csproj"/>` (under `/src/platform/` folder)
- `<Project Path="src/qyl.watch/qyl.watch.csproj"/>` (under `/src/tools/` folder)
- `<Project Path="src/qyl.browser/qyl.browser.esproj"/>` (under `/src/sdk/` folder)

Also remove the empty `<Folder Name="/src/tools/">` if qyl.watch was the only child.

- [ ] **Step 2: Delete project directories**

```bash
rm -rf src/qyl.hosting/ src/qyl.watch/ src/qyl.browser/
```

- [ ] **Step 3: Remove BuildFailures from collector**

```bash
rm -rf src/qyl.collector/BuildFailures/
```

Then remove from `Program.cs`:

- Lines referencing `using Qyl.Collector.BuildFailures;`
- Lines `var buildFailureCaptureEnabled = ...` through `app.MapBuildFailureEndpoints()` (lines 827-831)

Remove from `QylSerializerContext.cs`:

- `using Qyl.Collector.BuildFailures;`
- Any `[JsonSerializable(typeof(BuildFailure*))]` attributes

- [ ] **Step 4: Remove ConsoleBridge from collector**

```bash
rm -rf src/qyl.collector/ConsoleBridge/
```

Then remove from `Program.cs`:

- Console bridge singleton registration
- Console bridge endpoint mappings (`/api/v1/console`, `/api/v1/console/live`)

Remove from `QylSerializerContext.cs`:

- `[JsonSerializable(typeof(ConsoleLogEntry))]`
- `[JsonSerializable(typeof(ConsoleLogEntry[]))]`
- `[JsonSerializable(typeof(ConsoleIngestRequest))]`
- `[JsonSerializable(typeof(ConsoleIngestBatch))]`

- [ ] **Step 5: Check for Dockerfile references**

```bash
grep -n "qyl.hosting\|qyl.watch\|qyl.browser\|BuildFailures\|ConsoleBridge" src/qyl.collector/Dockerfile
```

Remove any COPY lines for deleted projects.

- [ ] **Step 6: Build to verify**

```bash
nuke
```

Expected: build succeeds. Fix any compilation errors from removed references.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: enforce kill list — delete qyl.hosting, qyl.watch, qyl.browser, BuildFailures, ConsoleBridge"
```

---

## Task 8: CHANGELOG + Final Verification

**Files:**

- Modify: `CHANGELOG.md`

- [ ] **Step 1: Update CHANGELOG.md**

Add under `## Unreleased`:

```markdown
### Added

- **Cost Engine**: `model_pricing` + `model_pricing_tiers` DuckDB tables, seed pricing for 21 models,
  server-side cost computation at ingestion time, 8 REST endpoints (`/api/v1/cost/by-model`,
  `/by-service`, `/by-session`, `/timeseries`, `/budget`, `/pricing`, `/pricing/{provider}/{model}`,
  `/sync-pricing`). Pre-aggregated `cost_by_model_hourly` view.
- **Telemetry Intelligence Model**: `Signal`, `DiagnosticPattern`, `CausalRule`, `InvestigationStrategy`
  types in `qyl.contracts/Intelligence/`. 10 seed diagnostic patterns, 6 causal rules, 4 investigation
  strategies. Deterministic `PatternEngine` in collector — evaluates signals against patterns, builds
  causal graphs, selects investigation strategies. No LLM involvement in pattern matching.

### Removed

- **qyl.hosting project**: Deleted. Collector IS the host (`QylApp`/`QylAppBuilder` abstraction removed).
- **qyl.watch project**: Deleted. Terminal span viewer — not core product.
- **qyl.browser project**: Deleted. Web Vitals for AI apps — niche, revisit when demand exists.
- **BuildFailures directory**: Deleted from collector. Not core observability.
- **ConsoleBridge directory**: Deleted from collector. Not core observability.
```

- [ ] **Step 2: Full build + test**

```bash
nuke test
```

Expected: build succeeds, all tests pass.

- [ ] **Step 3: Verify kill list compliance**

```bash
# These must not exist:
test ! -d src/qyl.hosting && echo "OK: qyl.hosting deleted" || echo "FAIL"
test ! -d src/qyl.watch && echo "OK: qyl.watch deleted" || echo "FAIL"
test ! -d src/qyl.browser && echo "OK: qyl.browser deleted" || echo "FAIL"
test ! -d src/qyl.collector/BuildFailures && echo "OK: BuildFailures deleted" || echo "FAIL"
test ! -d src/qyl.collector/ConsoleBridge && echo "OK: ConsoleBridge deleted" || echo "FAIL"
```

- [ ] **Step 4: Commit CHANGELOG**

```bash
git add CHANGELOG.md
git commit -m "docs: update changelog for cost engine, intelligence model, and kill list enforcement"
```

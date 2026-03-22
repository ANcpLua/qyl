using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Cost;
using Qyl.Collector.Storage;
using Xunit;

namespace Qyl.Collector.Tests.Cost;

public sealed class CostComputationTests
{
    private static async Task<PricingFixture> CreateWithPricingAsync(
        string provider, string model, decimal inputCost, decimal outputCost)
    {
        var store = new DuckDbStore(":memory:");
        await InsertPricingAsync(store, provider, model, inputCost, outputCost);
        var service = new ModelPricingService(store, NullLoggerFactory.Instance.CreateLogger<ModelPricingService>());
        await service.RefreshCacheAsync();
        return new PricingFixture(store, service);
    }

    private static Task InsertPricingAsync(DuckDbStore store, string provider, string model,
        decimal inputCost, decimal outputCost) =>
        store.ExecuteWriteAsync(async (con, _) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO model_pricing (provider, model, input_cost, output_cost, valid_from)
                              VALUES ($1, $2, $3, $4, $5)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = provider });
            cmd.Parameters.Add(new DuckDBParameter { Value = model });
            cmd.Parameters.Add(new DuckDBParameter { Value = inputCost });
            cmd.Parameters.Add(new DuckDBParameter { Value = outputCost });
            cmd.Parameters.Add(new DuckDBParameter { Value = TimeProvider.System.GetUtcNow().UtcDateTime });
            await cmd.ExecuteNonQueryAsync(_);
        });

    [Fact]
    public async Task ComputeCost_KnownModel_ReturnsCorrectCost()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        var cost = f.Service.ComputeCost("openai", "gpt-4o", inputTokens: 1000, outputTokens: 500);

        Assert.NotNull(cost);
        Assert.Equal(0.0075, cost.Value, precision: 10);
    }

    [Fact]
    public async Task ComputeCost_UnknownModel_ReturnsNull()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        Assert.Null(f.Service.ComputeCost("openai", "unknown-model", 1000, 500));
    }

    [Fact]
    public async Task ComputeCost_NullProvider_ReturnsNull()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        Assert.Null(f.Service.ComputeCost(null, "gpt-4o", 1000, 500));
    }

    [Fact]
    public async Task ComputeCost_NullTokens_ReturnsNull()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        Assert.Null(f.Service.ComputeCost("openai", "gpt-4o", null, null));
    }

    [Fact]
    public async Task ComputeCost_ZeroTokens_ReturnsZero()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        var cost = f.Service.ComputeCost("openai", "gpt-4o", inputTokens: 0, outputTokens: 0);

        Assert.NotNull(cost);
        Assert.Equal(0.0, cost.Value);
    }

    [Theory]
    [InlineData("openai", "gpt-4o", 1000, 500, 2.50, 10.00, 0.0075)]
    [InlineData("anthropic", "claude-opus-4-6", 500, 1000, 15.00, 75.00, 0.0825)]
    [InlineData("google", "gemini-2.0-flash", 10000, 5000, 0.10, 0.40, 0.003)]
    public async Task ComputeCost_VariousModels_MatchesExpected(
        string provider, string model, long input, long output,
        double inputCost, double outputCost, double expected)
    {
        await using var f = await CreateWithPricingAsync(provider, model, (decimal)inputCost, (decimal)outputCost);

        var cost = f.Service.ComputeCost(provider, model, input, output);

        Assert.NotNull(cost);
        Assert.Equal(expected, cost.Value, precision: 8);
    }

    [Fact]
    public async Task EnrichBatchWithCost_ComputesCostForSpansWithoutIt()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);
        var batch = new SpanBatch([CreateSpan("openai", "gpt-4o", 1000, 500, null)]);

        var result = f.Service.EnrichBatchWithCost(batch);

        Assert.NotNull(result.Spans[0].GenAiCostUsd);
        Assert.Equal(0.0075, result.Spans[0].GenAiCostUsd!.Value, precision: 10);
    }

    [Fact]
    public async Task EnrichBatchWithCost_PreservesExistingCost()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);
        var batch = new SpanBatch([CreateSpan("openai", "gpt-4o", 1000, 500, 0.05)]);

        var result = f.Service.EnrichBatchWithCost(batch);

        Assert.Equal(0.05, result.Spans[0].GenAiCostUsd);
    }

    private static SpanStorageRow CreateSpan(
        string? provider, string? model, long? input, long? output, double? existingCost) =>
        new()
        {
            SpanId = Guid.NewGuid().ToString("N")[..16],
            TraceId = Guid.NewGuid().ToString("N"),
            Name = "test",
            Kind = 1,
            StartTimeUnixNano = 1000000000000000000,
            EndTimeUnixNano = 1000000001000000000,
            DurationNs = 1000000000,
            StatusCode = 0,
            GenAiProviderName = provider,
            GenAiRequestModel = model,
            GenAiInputTokens = input,
            GenAiOutputTokens = output,
            GenAiCostUsd = existingCost,
        };
}

sealed class PricingFixture(DuckDbStore store, ModelPricingService service) : IAsyncDisposable
{
    public ModelPricingService Service => service;
    public ValueTask DisposeAsync() => store.DisposeAsync();
}

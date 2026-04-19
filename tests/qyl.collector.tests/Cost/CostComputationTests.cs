using AwesomeAssertions;
using Xunit;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Collector.Cost;
using Qyl.Collector.Storage;

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

        var cost = f.Service.ComputeCost("openai", "gpt-4o", 1000, 500);

        cost.Should().NotBeNull();
        cost.Value.Should().Be(0.0075);
    }

    [Fact]
    public async Task ComputeCost_UnknownModel_ReturnsNull()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        f.Service.ComputeCost("openai", "unknown-model", 1000, 500).Should().BeNull();
    }

    [Fact]
    public async Task ComputeCost_NullProvider_ReturnsNull()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        f.Service.ComputeCost(null, "gpt-4o", 1000, 500).Should().BeNull();
    }

    [Fact]
    public async Task ComputeCost_NullTokens_ReturnsNull()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        f.Service.ComputeCost("openai", "gpt-4o", null, null).Should().BeNull();
    }

    [Fact]
    public async Task ComputeCost_ZeroTokens_ReturnsZero()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);

        var cost = f.Service.ComputeCost("openai", "gpt-4o", 0, 0);

        cost.Should().NotBeNull();
        cost.Value.Should().Be(0.0);
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

        cost.Should().NotBeNull();
        cost.Value.Should().BeApproximately(expected, 1e-8);
    }

    [Fact]
    public async Task EnrichBatchWithCost_ComputesCostForSpansWithoutIt()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);
        var batch = new SpanBatch([CreateSpan("openai", "gpt-4o", 1000, 500, null)]);

        var result = f.Service.EnrichBatchWithCost(batch);

        result.Spans[0].GenAiCostUsd.Should().NotBeNull();
        result.Spans[0].GenAiCostUsd!.Value.Should().Be(0.0075);
    }

    [Fact]
    public async Task EnrichBatchWithCost_PreservesExistingCost()
    {
        await using var f = await CreateWithPricingAsync("openai", "gpt-4o", 2.50m, 10.00m);
        var batch = new SpanBatch([CreateSpan("openai", "gpt-4o", 1000, 500, 0.05)]);

        var result = f.Service.EnrichBatchWithCost(batch);

        result.Spans[0].GenAiCostUsd.Should().Be(0.05);
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
            GenAiCostUsd = existingCost
        };
}

internal sealed class PricingFixture(DuckDbStore store, ModelPricingService service) : IAsyncDisposable
{
    public ModelPricingService Service => service;
    public ValueTask DisposeAsync() => store.DisposeAsync();
}

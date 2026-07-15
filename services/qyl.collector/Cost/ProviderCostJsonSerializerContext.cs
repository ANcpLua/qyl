namespace Qyl.Collector.Cost;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(OpenAiCostsPage), TypeInfoPropertyName = "OpenAiCostsPage")]
[JsonSerializable(typeof(AnthropicCostReportPage), TypeInfoPropertyName = "AnthropicCostReportPage")]
internal partial class ProviderCostJsonSerializerContext : JsonSerializerContext;

internal sealed class OpenAiCostsPage
{
    public OpenAiCostBucket[]? Data { get; init; }

    public bool? HasMore { get; init; }

    public string? NextPage { get; init; }
}

internal sealed class OpenAiCostBucket
{
    public long StartTime { get; init; }

    public long EndTime { get; init; }

    public OpenAiCostItem[]? Results { get; init; }
}

internal sealed class OpenAiCostItem
{
    public OpenAiCostAmount? Amount { get; init; }

    public string? ProjectId { get; init; }

    public string? LineItem { get; init; }
}

internal sealed class OpenAiCostAmount
{
    public decimal? Value { get; init; }

    public string? Currency { get; init; }
}

internal sealed class AnthropicCostReportPage
{
    public AnthropicCostBucket[]? Data { get; init; }

    public bool? HasMore { get; init; }

    public string? NextPage { get; init; }
}

internal sealed class AnthropicCostBucket
{
    public string? StartingAt { get; init; }

    public string? EndingAt { get; init; }

    public AnthropicCostItem[]? Results { get; init; }
}

internal sealed class AnthropicCostItem
{
    public string? Amount { get; init; }

    public string? Currency { get; init; }

    public string? Description { get; init; }

    public string? WorkspaceId { get; init; }

    public string? Model { get; init; }
}

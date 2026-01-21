using System.Collections.ObjectModel;

namespace qyl.AspNetCore.Example.Models.Telemetry;

public record Order
{
    public int Id { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public Collection<OrderItem> Items { get; init; } = [];
}

public record OrderItem
{
    public int Quantity { get; init; }
}

public record CreateOrderRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public Collection<CreateOrderItem> Items { get; init; } = [];
}

public record CreateOrderItem
{
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

public record GenAiSpanData
{
    public string? OperationName { get; init; }
    public string? ProviderName { get; init; }
    public string? RequestModel { get; init; }
    public string? ResponseModel { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public long? TotalTokens { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public string? FinishReason { get; init; }
    public string? ResponseId { get; init; }
}

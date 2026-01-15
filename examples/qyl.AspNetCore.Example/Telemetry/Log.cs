using Microsoft.Extensions.Logging;
using TelemetryLab.Contracts.Models;

namespace TelemetryLab.Net10.Api.Domain.Telemetry;

/// <summary>
/// .NET 10 Source-Generated Logging
///
/// Same features as .NET 9:
/// ✅ [LoggerMessage] attribute
/// ✅ [LogProperties] - automatic property logging
/// ✅ [TagName] - OTel semantic convention tag names
/// ✅ [TagProvider] - custom tag extraction
/// ✅ Primary constructor logger
///
/// Plus .NET 10 runtime improvements to generated code.
/// </summary>
public static partial class Log
{
    // Order Operations
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Order created")]
    public static partial void OrderCreated(
        ILogger logger,
        [TagProvider(typeof(OrderTagProvider), nameof(OrderTagProvider.RecordTags))]
        Order order);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Processing order request")]
    public static partial void ProcessingOrderRequest(
        ILogger logger,
        [TagProvider(typeof(CreateOrderRequestTagProvider), nameof(CreateOrderRequestTagProvider.RecordTags))]
        CreateOrderRequest request);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Order {OrderId} retrieved")]
    public static partial void OrderRetrieved(
        ILogger logger,
        int orderId,
        [TagProvider(typeof(OrderTagProvider), nameof(OrderTagProvider.RecordTags))]
        Order order);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Order {OrderId} not found")]
    public static partial void OrderNotFound(ILogger logger, int orderId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Failed to process order {OrderId}")]
    public static partial void OrderProcessingFailed(ILogger logger, int orderId, Exception exception);

    // Log Buffering Events
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Log buffer flushed due to exception")]
    public static partial void LogBufferFlushed(ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Trace, Message = "Buffered log: {Message}")]
    public static partial void BufferedLogEntry(ILogger logger, string message);

    // Gen AI Operations
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Gen AI span processed")]
    public static partial void GenAiSpanProcessed(
        ILogger logger,
        [TagProvider(typeof(GenAiTagProvider), nameof(GenAiTagProvider.RecordTags))]
        GenAiSpanData data);

    // Storage Operations
    [LoggerMessage(EventId = 4000, Level = LogLevel.Debug, Message = "Span stored: {SpanId} in trace {TraceId}")]
    public static partial void SpanStored(ILogger logger, string spanId, string traceId);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Debug, Message = "Querying spans by prefix: {Prefix}")]
    public static partial void QueryingByPrefix(ILogger logger, string prefix);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Span store contains {Count} spans")]
    public static partial void SpanStoreCount(ILogger logger, long count);

    // Activity Events (🆕 .NET 10: Links and Events are serialized)
    [LoggerMessage(EventId = 5000, Level = LogLevel.Debug, Message = "Activity event added: {EventName}")]
    public static partial void ActivityEventAdded(ILogger logger, string eventName);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Debug, Message = "Activity link added to trace {LinkedTraceId}")]
    public static partial void ActivityLinkAdded(ILogger logger, string linkedTraceId);
}

// Tag Providers (same as .NET 9)
public static class OrderTagProvider
{
    public static void RecordTags(ITagCollector collector, Order order)
    {
        collector.Add("order.id", order.Id);
        collector.Add("order.customer_id", order.CustomerId);
        collector.Add("order.amount", order.Amount);
        collector.Add("order.status", order.Status);
        collector.Add("order.item_count", order.Items.Count);
        collector.Add("order.created_at", order.CreatedAt.ToString("O"));

        var totalQuantity = order.Items.Sum(i => i.Quantity);
        collector.Add("order.total_quantity", totalQuantity);
    }
}

public static class CreateOrderRequestTagProvider
{
    public static void RecordTags(ITagCollector collector, CreateOrderRequest request)
    {
        collector.Add("request.customer_id", request.CustomerId);
        collector.Add("request.item_count", request.Items.Count);

        var totalQuantity = request.Items.Sum(i => i.Quantity);
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        collector.Add("request.total_quantity", totalQuantity);
        collector.Add("request.total_amount", totalAmount);
    }
}

public static class GenAiTagProvider
{
    public static void RecordTags(ITagCollector collector, GenAiSpanData data)
    {
        // Use OTel semconv constants
        if (data.OperationName is not null)
            collector.Add(OTelSemconv.OperationName, data.OperationName);

        if (data.ProviderName is not null)
            collector.Add(OTelSemconv.ProviderName, data.ProviderName);

        if (data.RequestModel is not null)
            collector.Add(OTelSemconv.RequestModel, data.RequestModel);

        if (data.ResponseModel is not null)
            collector.Add(OTelSemconv.ResponseModel, data.ResponseModel);

        if (data.InputTokens.HasValue)
            collector.Add(OTelSemconv.UsageInputTokens, data.InputTokens.Value);

        if (data.OutputTokens.HasValue)
            collector.Add(OTelSemconv.UsageOutputTokens, data.OutputTokens.Value);

        if (data.InputTokens.HasValue && data.OutputTokens.HasValue)
            collector.Add("gen_ai.usage.total_tokens", data.InputTokens.Value + data.OutputTokens.Value);
    }
}
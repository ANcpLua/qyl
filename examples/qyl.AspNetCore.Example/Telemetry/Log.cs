using qyl.AspNetCore.Example.Models.Telemetry;

namespace qyl.AspNetCore.Example.Telemetry;

/// <summary>
///     .NET 10 Source-Generated Logging
///     Same features as .NET 10:
///     ✅ [LoggerMessage] attribute
///     ✅ [LogProperties] - automatic property logging
///     ✅ [TagName] - OTel semantic convention tag names
///     ✅ [TagProvider] - semantic convention tag extraction
///     ✅ Primary constructor logger
///     Plus .NET 10 runtime improvements to generated code.
/// </summary>
public static partial class Log
{
    // Order Operations
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Order created")]
    public static partial void OrderCreated(ILogger logger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Processing order request")]
    public static partial void ProcessingOrderRequest(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Order retrieved")]
    public static partial void OrderRetrieved(ILogger logger);

    // Gen AI Operations
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Gen AI span processed")]
    public static partial void GenAiSpanProcessed(
        ILogger logger,
        [TagProvider(typeof(GenAiTagProvider), nameof(GenAiTagProvider.RecordTags))]
        GenAiSpanData data);
}

// Tag Providers (same as .NET 10)
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

    }
}

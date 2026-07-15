using System.Diagnostics.Metrics;

namespace Qyl.Run.Workload;

internal static class WorkloadTelemetry
{
    public const string SourceName = "Qyl.Run.Workload";

    public const string MeterName = SourceName;

    public const string DefaultServiceName = "workload";

    public static readonly ActivitySource Source = new(SourceName);

    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<long> GenAiClientTokenUsage =
        Meter.CreateGenAiClientTokenUsageHistogram();

    public static double[] CreateGenAiTokenUsageBucketBoundaries() =>
    [
        1,
        4,
        16,
        64,
        256,
        1_024,
        4_096,
        16_384,
        65_536,
        262_144,
        1_048_576,
        4_194_304,
        16_777_216,
        67_108_864
    ];

    public static void RecordGenAiTokenUsage(
        string operationName,
        string providerName,
        string requestModel,
        string responseModel,
        long inputTokens,
        long outputTokens)
    {
        RecordGenAiTokenCount(operationName, providerName, requestModel, responseModel, inputTokens,
            GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiTokenTypeExample1);
        RecordGenAiTokenCount(operationName, providerName, requestModel, responseModel, outputTokens,
            GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiTokenTypeExample2);
    }

    private static void RecordGenAiTokenCount(
        string operationName,
        string providerName,
        string requestModel,
        string responseModel,
        long tokenCount,
        string tokenType)
    {
        var tags = new TagList
        {
            { GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiOperationName, operationName },
            { GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiProviderName, providerName },
            { GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiTokenType, tokenType },
            { GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiRequestModel, requestModel },
            { GenAiMetrics.GenAiClientTokenUsageDescriptor.AttributeGenAiResponseModel, responseModel }
        };

        GenAiClientTokenUsage.Record(tokenCount, tags);
    }
}

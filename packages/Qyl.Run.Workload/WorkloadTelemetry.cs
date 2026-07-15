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

    private static readonly Histogram<double> GenAiClientOperationDuration =
        Meter.CreateGenAiClientOperationDurationHistogram();

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

    // gen_ai.client.operation.duration is emitted in seconds; these boundaries are the
    // OpenTelemetry GenAI advisory buckets (10 ms .. ~82 s), covering the demo's sub-second
    // model calls without collapsing the tail when a provider stalls.
    public static double[] CreateGenAiOperationDurationBucketBoundaries() =>
    [
        0.01,
        0.02,
        0.04,
        0.08,
        0.16,
        0.32,
        0.64,
        1.28,
        2.56,
        5.12,
        10.24,
        20.48,
        40.96,
        81.92
    ];

    public static void RecordGenAiOperationDuration(
        string operationName,
        string providerName,
        string requestModel,
        string? responseModel,
        double durationSeconds,
        string? errorType)
    {
        var tags = new TagList
        {
            { GenAiMetrics.GenAiClientOperationDurationDescriptor.AttributeGenAiOperationName, operationName },
            { GenAiMetrics.GenAiClientOperationDurationDescriptor.AttributeGenAiProviderName, providerName },
            { GenAiMetrics.GenAiClientOperationDurationDescriptor.AttributeGenAiRequestModel, requestModel }
        };

        if (responseModel is not null)
        {
            tags.Add(GenAiMetrics.GenAiClientOperationDurationDescriptor.AttributeGenAiResponseModel, responseModel);
        }

        if (errorType is not null)
        {
            tags.Add(GenAiMetrics.GenAiClientOperationDurationDescriptor.AttributeErrorType, errorType);
        }

        GenAiClientOperationDuration.Record(durationSeconds, tags);
    }

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


using Microsoft.Extensions.AI;

namespace Qyl.Collector.Analytics;

[QylHostedService]
public sealed partial class EmbeddingClusterWorker(
    DuckDbStore store,
    IServiceProvider services,
    ILogger<EmbeddingClusterWorker> logger)
    : BackgroundService
{
    private const float SimilarityThreshold = 0.75f;
    private const int BatchSize = 200;
    private const string ModelVersion = "cosine-v1";


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var generator = services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (generator is null)
        {
            LogNoGenerator(logger);
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        await RunClusteringAsync(generator, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RunClusteringAsync(generator, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogClusteringError(logger, ex);
            }
        }
    }


    private async Task RunClusteringAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        CancellationToken ct)
    {
        var spans = await store.GetUnclusteredChatSpansAsync(BatchSize, ct).ConfigureAwait(false);
        if (spans.Count is 0)
        {
            LogNothingToCluster(logger);
            return;
        }

        var clusterableSpans = new List<UnclusteredSpan>(spans.Count);
        var texts = new List<string>(spans.Count);
        foreach (var span in spans)
        {
            if (ExtractFirstUserMessage(span.InputMessages) is not { } text) continue;
            clusterableSpans.Add(span);
            texts.Add(text);
        }

        if (clusterableSpans.Count is 0)
        {
            LogNothingToCluster(logger);
            return;
        }

        var embeddings = await generator.GenerateAsync(texts, cancellationToken: ct).ConfigureAwait(false);

        var rows = ClusterEmbeddings(clusterableSpans, embeddings, SimilarityThreshold, ModelVersion);
        await store.UpsertSpanClustersAsync(rows, ct).ConfigureAwait(false);
        LogClustered(logger, rows.Count, spans.Count);
    }


    private static List<SpanClusterRow> ClusterEmbeddings(
        List<UnclusteredSpan> spans,
        GeneratedEmbeddings<Embedding<float>> embeddings,
        float threshold,
        string modelVersion)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var centroids = new List<(float[] Vector, string Label, int Count)>();
        var result = new List<SpanClusterRow>(spans.Count);

        for (var i = 0; i < spans.Count; i++)
        {
            var vec = embeddings[i].Vector.ToArray();
            var bestIdx = -1;
            var bestSim = 0f;

            for (var j = 0; j < centroids.Count; j++)
            {
                var sim = CosineSimilarity(vec, centroids[j].Vector);
                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestIdx = j;
                }
            }

            int clusterId;
            string clusterLabel;
            if (bestSim >= threshold && bestIdx >= 0)
            {
                clusterId = bestIdx;
                clusterLabel = centroids[bestIdx].Label;

                var (old, label, count) = centroids[bestIdx];
                var updated = new float[old.Length];
                var factor = 1f / (count + 1);
                for (var k = 0; k < old.Length; k++)
                    updated[k] = (old[k] * (count * factor)) + (vec[k] * factor);
                centroids[bestIdx] = (updated, label, count + 1);
            }
            else
            {
                clusterId = centroids.Count;
                clusterLabel = TruncateLabel(
                    ExtractFirstUserMessage(spans[i].InputMessages)!);
                centroids.Add((vec, clusterLabel, 1));
            }

            result.Add(new SpanClusterRow(
                spans[i].SpanId,
                clusterId,
                clusterLabel,
                1.0 - bestSim,
                modelVersion,
                now));
        }

        return result;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        var dot = 0f;
        var normA = 0f;
        var normB = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < 1e-8f ? 0f : dot / denom;
    }


    private static string? ExtractFirstUserMessage(string? messagesJson)
    {
        if (string.IsNullOrEmpty(messagesJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(messagesJson);
            foreach (var message in doc.RootElement.EnumerateArray())
            {
                if (message.TryGetProperty("role", out var role) &&
                    role.GetString() == "user" &&
                    message.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString();
                }
            }
        }
        catch (JsonException ex)
        {
            Debug.WriteLine(ex);
        }

        return null;
    }

    private static string TruncateLabel(string text)
    {
        text = text.Trim();
        return text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 77), "...");
    }


    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "No IEmbeddingGenerator<string, Embedding<float>> registered — " +
                  "semantic clustering disabled, using span-name heuristics")]
    private static partial void LogNoGenerator(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No unclustered spans found, skipping clustering pass")]
    private static partial void LogNothingToCluster(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Semantic clustering: assigned {Assigned} of {Total} spans to clusters")]
    private static partial void LogClustered(ILogger logger, int assigned, int total);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during semantic clustering pass")]
    private static partial void LogClusteringError(ILogger logger, Exception ex);
}

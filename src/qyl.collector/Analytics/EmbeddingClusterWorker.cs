// =============================================================================
// EmbeddingClusterWorker — semantic clustering for Coverage Gaps & Top Questions
// =============================================================================
// Runs every 15 minutes when an IEmbeddingGenerator<string, Embedding<float>>
// is registered in DI. If none is registered the service exits immediately.
// Analytics queries (Coverage Gaps, Top Questions, Satisfaction by topic)
// require clustering — unclustered spans are excluded from topic analytics.
//
// Algorithm (greedy cosine-similarity):
//   1. Fetch up to 200 unclustered gen_ai spans with gen_ai.input.messages.
//   2. Extract the first user message from each span's messages JSON.
//      Spans without an extractable user message are skipped.
//   3. Embed all texts in one batch via the injected generator.
//   4. Greedily assign each embedding to the nearest existing centroid
//      (if cosine similarity ≥ 0.75) or start a new cluster.
//   5. Persist assignments to span_clusters via DuckDbStore.
// =============================================================================

using Microsoft.Extensions.AI;

namespace qyl.collector.Analytics;

/// <summary>
///     Background service that enriches gen_ai spans with semantic cluster labels.
///     Only active when <see cref="IEmbeddingGenerator{TInput,TEmbedding}" /> is
///     registered in the DI container.
/// </summary>
public sealed partial class EmbeddingClusterWorker(
    DuckDbStore store,
    IServiceProvider services,
    ILogger<EmbeddingClusterWorker> logger)
    : BackgroundService
{
    private const float SimilarityThreshold = 0.75f;
    private const int BatchSize = 200;
    private const string ModelVersion = "cosine-v1";

    // ==========================================================================
    // BackgroundService
    // ==========================================================================

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var generator = services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (generator is null)
        {
            LogNoGenerator(logger);
            return;
        }

        // Wait for ingestion warmup before first clustering pass
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

    // ==========================================================================
    // Clustering pipeline
    // ==========================================================================

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

        // Filter to spans with extractable user messages — no span-name fallback
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

    // ==========================================================================
    // Greedy cosine-similarity clustering
    // ==========================================================================

    private static List<SpanClusterRow> ClusterEmbeddings(
        List<UnclusteredSpan> spans,
        GeneratedEmbeddings<Embedding<float>> embeddings,
        float threshold,
        string modelVersion)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        // Each entry: (running-mean centroid vector, human-readable label)
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

                // Update centroid with running mean
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

    // ==========================================================================
    // JSON helpers
    // ==========================================================================

    /// <summary>
    ///     Extracts the first user-role message text from a
    ///     <c>gen_ai.input.messages</c> JSON array.
    /// </summary>
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
        catch
        {
            // Malformed JSON — fall through
        }

        return null;
    }

    private static string TruncateLabel(string text)
    {
        text = text.Trim();
        return text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 77), "...");
    }

    // ==========================================================================
    // Structured log messages
    // ==========================================================================

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

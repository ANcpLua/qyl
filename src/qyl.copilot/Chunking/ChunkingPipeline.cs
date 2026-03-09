using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace qyl.copilot.Chunking;

/// <summary>
///     Channel-based producer/consumer pipeline that processes semantic chunks concurrently
///     through an <see cref="IChatClient" />, yielding ordered YAML results as they complete.
/// </summary>
public sealed partial class ChunkingPipeline(
    IChatClient chatClient,
    IOptions<ChunkingPipelineOptions> options,
    ILogger<ChunkingPipeline> logger)
{
    /// <summary>
    ///     Splits <paramref name="fullText" /> into semantic chunks, processes each through the
    ///     <see cref="IChatClient" /> concurrently, and yields ordered YAML results.
    /// </summary>
    public async IAsyncEnumerable<YamlChunkResult> ProcessDocumentAsync(
        Guid documentId,
        string fullText,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var chunks = SemanticChunker.ChunkText(fullText, opts.MaxChunkChars);

        Log.PipelineStarted(logger, documentId, chunks.Count, opts.MaxConcurrency);

        if (chunks.Count is 0)
            yield break;

        var (input, output) = CreateChannels(opts.ChannelCapacity);
        StartPipeline(input, output, chunks, opts.MaxConcurrency, documentId, cancellationToken);

        await foreach (var result in ReorderAsync(output.Reader, cancellationToken).ConfigureAwait(false))
            yield return result;

        Log.PipelineCompleted(logger, documentId, chunks.Count);
    }

    private static (Channel<SemanticChunk> Input, Channel<YamlChunkResult> Output) CreateChannels(int capacity)
    {
        var input = Channel.CreateBounded<SemanticChunk>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, SingleWriter = true, SingleReader = false
        });
        var output = Channel.CreateBounded<YamlChunkResult>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, SingleWriter = false, SingleReader = true
        });
        return (input, output);
    }

    private void StartPipeline(
        Channel<SemanticChunk> input, Channel<YamlChunkResult> output,
        IReadOnlyList<SemanticChunk> chunks, int concurrency,
        Guid documentId, CancellationToken ct)
    {
        var producer = FeedAsync(input.Writer, chunks, ct);
        var consumers = Enumerable.Range(0, concurrency)
            .Select(id => ConsumeAsync(id, input.Reader, output.Writer, chunks.Count, ct))
            .ToArray();

        _ = Task.WhenAll([producer, .. consumers]).ContinueWith(
            t =>
            {
                if (t.Exception is { } ex)
                    Log.PipelineError(logger, ex.GetBaseException(), documentId);

                output.Writer.TryComplete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async IAsyncEnumerable<YamlChunkResult> ReorderAsync(
        ChannelReader<YamlChunkResult> reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var pending = new SortedDictionary<int, YamlChunkResult>();
        var nextExpected = 0;

        await foreach (var result in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            pending[result.ChunkIndex] = result;
            while (pending.Remove(nextExpected, out var next))
            {
                yield return next;
                nextExpected++;
            }
        }

        foreach (var remaining in pending.Values)
            yield return remaining;
    }

    private static async Task FeedAsync(
        ChannelWriter<SemanticChunk> writer, IReadOnlyList<SemanticChunk> chunks, CancellationToken ct)
    {
        foreach (var chunk in chunks)
            await writer.WriteAsync(chunk, ct).ConfigureAwait(false);

        writer.TryComplete();
    }

    private async Task ConsumeAsync(
        int workerId, ChannelReader<SemanticChunk> reader,
        ChannelWriter<YamlChunkResult> writer, int totalChunks, CancellationToken ct)
    {
        await foreach (var chunk in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            Log.ProcessingChunk(logger, workerId, chunk.Index, totalChunks);

            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, BuildYamlPrompt(chunk, totalChunks))],
                new ChatOptions { Temperature = 0.2f },
                ct).ConfigureAwait(false);

            var yaml = response.Text;
            var result = string.IsNullOrWhiteSpace(yaml)
                ? FailResult(chunk, "Empty response from model")
                : new YamlChunkResult(chunk.Index, chunk.PageStart, chunk.PageEnd,
                    chunk.SectionTitle, StripYamlFences(yaml), true);

            await writer.WriteAsync(result, ct).ConfigureAwait(false);
        }
    }

    private static YamlChunkResult FailResult(SemanticChunk chunk, string error) =>
        new(chunk.Index, chunk.PageStart, chunk.PageEnd, chunk.SectionTitle,
            string.Create(CultureInfo.InvariantCulture,
                $"# Chunk {chunk.Index} (pages {chunk.PageStart}-{chunk.PageEnd}) failed: {error}\n"),
            false, error);

    private static string BuildYamlPrompt(SemanticChunk chunk, int totalChunks)
    {
        var sectionLine = chunk.SectionTitle is not null
            ? string.Concat("\nSection: ", chunk.SectionTitle)
            : "";

        return string.Create(CultureInfo.InvariantCulture,
            $"""
             You are a document structuring assistant. Convert the following document section into structured YAML.
             Preserve ALL information — do not omit or summarize away any content.
             Output raw YAML only. No markdown fences, no explanations.
             {sectionLine}
             Pages: {chunk.PageStart}-{chunk.PageEnd}
             Chunk: {chunk.Index + 1} of {totalChunks}

             Use this structure:
             section:
               title: "<detected or provided section title>"
               pages: [{chunk.PageStart}, {chunk.PageEnd}]
               concepts:
                 - title: "<concept heading>"
                   content: |
                     <paragraph text, preserving all detail>
                   entities: [<any names, dates, numbers, organizations>]

             Document text:
             ---
             {chunk.Content}
             ---
             """);
    }

    private static string StripYamlFences(string yaml)
    {
        var trimmed = yaml.Trim();
        if (trimmed.StartsWith("```yaml", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```", StringComparison.Ordinal))
            trimmed = trimmed[3..];

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
            trimmed = trimmed[..^3];

        return trimmed.Trim();
    }

    internal static partial class Log
    {
        [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
            Message = "Chunking pipeline started for document {DocumentId}: {TotalChunks} chunks, {Concurrency} workers")]
        public static partial void PipelineStarted(ILogger logger, Guid documentId, int totalChunks, int concurrency);

        [LoggerMessage(EventId = 4002, Level = LogLevel.Debug,
            Message = "Worker {WorkerId} processing chunk {ChunkIndex}/{TotalChunks}")]
        public static partial void ProcessingChunk(ILogger logger, int workerId, int chunkIndex, int totalChunks);

        [LoggerMessage(EventId = 4004, Level = LogLevel.Error,
            Message = "Chunking pipeline error for document {DocumentId}")]
        public static partial void PipelineError(ILogger logger, Exception exception, Guid documentId);

        [LoggerMessage(EventId = 4005, Level = LogLevel.Information,
            Message = "Chunking pipeline completed for document {DocumentId}: {TotalChunks} chunks processed")]
        public static partial void PipelineCompleted(ILogger logger, Guid documentId, int totalChunks);
    }
}

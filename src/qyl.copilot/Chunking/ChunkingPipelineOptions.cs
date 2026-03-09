namespace qyl.copilot.Chunking;

/// <summary>
///     Configuration for the semantic chunking pipeline.
/// </summary>
public sealed class ChunkingPipelineOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Chunking";

    /// <summary>Maximum characters per semantic chunk. Default 4000 (~1000 tokens).</summary>
    public int MaxChunkChars { get; init; } = 4000;

    /// <summary>Number of parallel <see cref="Microsoft.Extensions.AI.IChatClient" /> consumers. Default 3.</summary>
    public int MaxConcurrency { get; init; } = 3;

    /// <summary>Bounded channel capacity for backpressure. Default 50.</summary>
    public int ChannelCapacity { get; init; } = 50;
}

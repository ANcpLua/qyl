namespace qyl.copilot.Chunking;

/// <summary>
///     The YAML output produced by processing a single <see cref="SemanticChunk" />.
/// </summary>
/// <param name="ChunkIndex">Matches <see cref="SemanticChunk.Index" /> for ordering.</param>
/// <param name="PageStart">First page covered.</param>
/// <param name="PageEnd">Last page covered.</param>
/// <param name="SectionTitle">Section header carried forward from the chunk.</param>
/// <param name="Yaml">The YAML fragment produced by the AI model.</param>
/// <param name="Success">Whether the AI call succeeded.</param>
/// <param name="Error">Error message on failure.</param>
public record YamlChunkResult(
    int ChunkIndex,
    int PageStart,
    int PageEnd,
    string? SectionTitle,
    string Yaml,
    bool Success,
    string? Error = null);

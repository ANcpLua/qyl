namespace qyl.copilot.Chunking;

/// <summary>
///     A conceptual segment of a document, grouped by paragraph/section boundaries
///     rather than arbitrary token windows.
/// </summary>
/// <param name="Index">Zero-based chunk ordinal within the document.</param>
/// <param name="PageStart">First page covered by this chunk (1-based).</param>
/// <param name="PageEnd">Last page covered by this chunk (1-based).</param>
/// <param name="Content">The raw OCR text for this chunk.</param>
/// <param name="SectionTitle">Detected section header, if any.</param>
public record SemanticChunk(int Index, int PageStart, int PageEnd, string Content, string? SectionTitle = null);

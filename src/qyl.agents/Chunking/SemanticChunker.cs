using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Qyl.Agents.Chunking;

/// <summary>
///     Splits OCR text into semantic chunks by detecting page breaks, section headers,
///     and paragraph boundaries — grouping conceptually related text together.
/// </summary>
public static partial class SemanticChunker
{
    private const RegexOptions SafeOptions =
        RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking | RegexOptions.IgnoreCase;

    /// <summary>
    ///     Splits <paramref name="text" /> into semantic chunks respecting paragraph and section boundaries.
    /// </summary>
    public static IReadOnlyList<SemanticChunk> ChunkText(string text, int maxChunkChars = 4000)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var pages = SplitByPages(text);
        var state = new ChunkingState(maxChunkChars);

        foreach (var (pageNum, pageText) in pages)
            ProcessPage(pageNum, pageText, state);

        state.FlushBuffer(pages[^1].PageNum);
        return state.Chunks;
    }

    private static void ProcessPage(int pageNum, string pageText, ChunkingState state)
    {
        var paragraphs = ParagraphSplitRegex().Split(pageText);

        foreach (var para in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(para))
                continue;

            if (IsSectionHeader(para))
            {
                state.FlushBuffer(pageNum);
                state.CurrentSection = para.Trim();
                continue;
            }

            if (state.Buffer.Length + para.Length > state.MaxChars && state.Buffer.Length > 0)
            {
                state.FlushBuffer(pageNum);
                state.ChunkPageStart = pageNum;
            }

            if (para.Length > state.MaxChars)
            {
                AppendLargeParagraph(para, pageNum, state);
                continue;
            }

            if (state.Buffer.Length is 0)
                state.ChunkPageStart = pageNum;

            state.Buffer.Append(para).Append("\n\n");
        }
    }

    private static void AppendLargeParagraph(string para, int pageNum, ChunkingState state)
    {
        state.FlushBuffer(pageNum);

        var sentences = SentenceSplitRegex().Split(para);
        foreach (var sentence in sentences)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                continue;

            if (state.Buffer.Length + sentence.Length > state.MaxChars && state.Buffer.Length > 0)
            {
                state.FlushBuffer(pageNum);
                state.ChunkPageStart = pageNum;
            }

            state.Buffer.Append(sentence).Append(' ');
        }
    }

    private static List<(int PageNum, string Text)> SplitByPages(string text)
    {
        var rawPages = text.Split('\f');
        var pages = new List<(int PageNum, string Text)>();

        for (var i = 0; i < rawPages.Length; i++)
        {
            var pageText = rawPages[i];
            var markerMatch = PageMarkerRegex().Match(pageText);
            var pageNum = markerMatch.Success
                ? int.Parse(markerMatch.Groups["num"].Value, CultureInfo.InvariantCulture)
                : i + 1;

            if (markerMatch.Success)
                pageText = PageMarkerRegex().Replace(pageText, "");

            if (!string.IsNullOrWhiteSpace(pageText))
                pages.Add((pageNum, pageText));
        }

        return pages.Count > 0 ? pages : [(1, text)];
    }

    internal static bool IsSectionHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length is > 0 and <= 120
               && (NumberedHeadingRegex().IsMatch(trimmed)
                   || (trimmed.Length is >= 3 and <= 80 && AllCapsRegex().IsMatch(trimmed))
                   || (trimmed.Length <= 60 && trimmed[^1] is ':'));
    }

    [GeneratedRegex(@"---\s*Page\s+(?<num>\d+)\s*---", SafeOptions)]
    private static partial Regex PageMarkerRegex();

    [GeneratedRegex(@"\n\s*\n", RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex ParagraphSplitRegex();

    [GeneratedRegex(@"^(?:\d+\.[\d.]*|Chapter\s+\w+|Section\s+\w+|Part\s+\w+|Appendix\s+\w+)\s*[:\-\s]", SafeOptions)]
    private static partial Regex NumberedHeadingRegex();

    [GeneratedRegex(@"^[A-Z\s\d\-:,.]{3,}$", RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex AllCapsRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex SentenceSplitRegex();

    private sealed class ChunkingState(int maxChars)
    {
        public readonly StringBuilder Buffer = new();
        public readonly List<SemanticChunk> Chunks = [];
        public readonly int MaxChars = maxChars;
        public int ChunkIndex;
        public int ChunkPageStart = 1;
        public string? CurrentSection;

        public void FlushBuffer(int pageEnd)
        {
            if (Buffer.Length is 0)
                return;

            Chunks.Add(new SemanticChunk(ChunkIndex++, ChunkPageStart, pageEnd,
                Buffer.ToString().Trim(), CurrentSection));
            Buffer.Clear();
        }
    }
}

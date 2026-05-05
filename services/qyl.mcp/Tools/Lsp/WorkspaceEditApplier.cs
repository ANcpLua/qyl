
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

internal sealed partial class WorkspaceEditApplier(ILogger<WorkspaceEditApplier> logger)
{
    public async Task<ApplySummary> ApplyAsync(JsonNode workspaceEdit, CancellationToken ct)
    {
        List<string> filesChanged = [];
        Dictionary<string, int> editCounts = new(StringComparer.Ordinal);

        foreach (var (uri, edits) in EnumerateTextEdits(workspaceEdit))
        {
            ct.ThrowIfCancellationRequested();
            var filePath = LspClientWrapper.UriToPath(uri);
            var count = await ApplyTextEditsAsync(filePath, edits, ct).ConfigureAwait(false);
            if (count > 0)
            {
                filesChanged.Add(filePath);
                editCounts[filePath] = count;
            }
        }

        return new ApplySummary(filesChanged, editCounts);
    }

    private static IEnumerable<(string Uri, JsonArray Edits)> EnumerateTextEdits(JsonNode workspaceEdit)
    {
        if (workspaceEdit["documentChanges"] is JsonArray documentChanges)
        {
            foreach (var change in documentChanges.OfType<JsonObject>())
            {
                if (change["kind"] is not null)
                    continue;
                if (change["textDocument"]?["uri"]?.GetValue<string>() is not { } uri)
                    continue;
                if (change["edits"] is not JsonArray edits)
                    continue;

                yield return (uri, edits);
            }

            yield break;
        }

        if (workspaceEdit["changes"] is not JsonObject changesMap)
            yield break;

        foreach (var kv in changesMap)
        {
            if (kv.Value is JsonArray edits)
                yield return (kv.Key, edits);
        }
    }

    private async Task<int> ApplyTextEditsAsync(string filePath, JsonArray edits, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"WorkspaceEdit target not found: {filePath}", filePath);

        var original = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var lineOffsets = BuildLineOffsets(original);

        var ordered = edits
            .OfType<JsonObject>()
            .Select(ParseEdit)
            .OrderByDescending(e => e.StartLine)
            .ThenByDescending(e => e.StartChar)
            .ToList();

        if (ordered.Count is 0)
            return 0;

        var working = new StringBuilder(original);
        foreach (var edit in ordered)
        {
            var startOffset = ResolveOffset(lineOffsets, edit.StartLine, edit.StartChar, original.Length);
            var endOffset = ResolveOffset(lineOffsets, edit.EndLine, edit.EndChar, original.Length);
            if (endOffset < startOffset)
            {
                throw new InvalidDataException(
                    $"WorkspaceEdit for {filePath} has end position before start position.");
            }

            working.Remove(startOffset, endOffset - startOffset);
            working.Insert(startOffset, edit.NewText);
        }

        var updated = working.ToString();
        if (string.Equals(updated, original, StringComparison.Ordinal))
            return 0;

        await File.WriteAllTextAsync(filePath, updated, ct).ConfigureAwait(false);
        LogApplied(logger, ordered.Count, filePath);
        return ordered.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied {Count} LSP edit(s) to {File}")]
    private static partial void LogApplied(ILogger logger, int count, string file);

    private static Edit ParseEdit(JsonObject edit)
    {
        var range = edit["range"] ?? throw new InvalidDataException("TextEdit is missing 'range'.");
        var start = range["start"] ?? throw new InvalidDataException("TextEdit range is missing 'start'.");
        var end = range["end"] ?? throw new InvalidDataException("TextEdit range is missing 'end'.");

        return new Edit(
            start["line"]?.GetValue<int>() ?? 0,
            start["character"]?.GetValue<int>() ?? 0,
            end["line"]?.GetValue<int>() ?? 0,
            end["character"]?.GetValue<int>() ?? 0,
            edit["newText"]?.GetValue<string>() ?? string.Empty);
    }

    private static int[] BuildLineOffsets(string text)
    {
        List<int> offsets = [0];
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                offsets.Add(i + 1);
        }

        return offsets.ToArray();
    }

    private static int ResolveOffset(int[] lineOffsets, int line, int character, int textLength)
    {
        if (line < 0 || line >= lineOffsets.Length)
            return textLength;
        var lineStart = lineOffsets[line];
        return Math.Clamp(lineStart + character, lineStart, textLength);
    }

    public sealed record ApplySummary(
        IReadOnlyList<string> FilesChanged,
        IReadOnlyDictionary<string, int> EditsPerFile);

    private sealed record Edit(int StartLine, int StartChar, int EndLine, int EndChar, string NewText);
}

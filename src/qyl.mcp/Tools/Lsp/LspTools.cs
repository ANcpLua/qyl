// Copyright (c) 2025-2026 ancplua

namespace qyl.mcp.Tools.Lsp;

using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

/// <summary>
///     MCP facade for six LSP-backed code-intelligence tools. Gated behind
///     <see cref="QylSkillKind.Debug" /> — opt-in via <c>QYL_SKILLS=debug</c>.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Debug)]
internal sealed class LspTools(LspClientWrapper wrapper, WorkspaceEditApplier editApplier)
{
    /// <summary>Go to definition of the symbol at the given 1-based position.</summary>
    [QylCapability("lsp_code_intelligence")]
    [McpServerTool(
        Name = "lsp_goto_definition", Title = "Go to Definition",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Go to the definition of the symbol at the given 1-based line/column in a source file.")]
    public Task<string> GotoDefinitionAsync(
        [Description("Absolute path to the source file")]
        string filePath,
        [Description("1-based line number")] int line,
        [Description("1-based column number")] int column,
        CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            var opened = await wrapper.OpenAsync(filePath, ct).ConfigureAwait(false);
            var (line0, char0) = LspClientWrapper.ToZeroBased(line, column);
            var result = await opened.Client.GotoDefinitionAsync(opened.Uri, line0, char0, ct).ConfigureAwait(false);
            return FormatLocations("# Definition", result);
        });

    /// <summary>Find all references to the symbol at the given 1-based position.</summary>
    [QylCapability("lsp_code_intelligence")]
    [McpServerTool(
        Name = "lsp_find_references", Title = "Find References",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Find all references to the symbol at the given 1-based line/column across the workspace.")]
    public Task<string> FindReferencesAsync(
        [Description("Absolute path to the source file")]
        string filePath,
        [Description("1-based line number")] int line,
        [Description("1-based column number")] int column,
        [Description("Include the declaration itself in the result list")]
        bool includeDeclaration = true,
        CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            var opened = await wrapper.OpenAsync(filePath, ct).ConfigureAwait(false);
            var (line0, char0) = LspClientWrapper.ToZeroBased(line, column);
            var result = await opened.Client.FindReferencesAsync(opened.Uri, line0, char0, includeDeclaration, ct)
                .ConfigureAwait(false);
            return FormatLocations("# References", result);
        });

    /// <summary>
    ///     List symbols in a document, or query workspace symbols. <paramref name="query" /> triggers
    ///     a workspace-wide search; <paramref name="filePath" /> is required in both modes (it seeds
    ///     the workspace root for workspace/symbol).
    /// </summary>
    [QylCapability("lsp_code_intelligence")]
    [McpServerTool(
        Name = "lsp_symbols", Title = "Symbols",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "List symbols in a document, or query workspace symbols when 'query' is provided. A source file is always required to anchor the workspace.")]
    public Task<string> SymbolsAsync(
        [Description("Absolute path to a source file (anchors the workspace root)")]
        string filePath,
        [Description(
            "Optional workspace symbol query. When provided, returns workspace matches instead of document symbols.")]
        string? query = null,
        CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            var opened = await wrapper.OpenAsync(filePath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(query))
            {
                var symbols = await opened.Client.DocumentSymbolsAsync(opened.Uri, ct).ConfigureAwait(false);
                return FormatSymbols($"# Symbols in {LspClientWrapper.UriToPath(opened.Uri)}", symbols);
            }

            var results = await opened.Client.WorkspaceSymbolsAsync(query, ct).ConfigureAwait(false);
            return FormatSymbols($"# Workspace symbols matching '{query}'", results);
        });

    /// <summary>Pull-model diagnostics for a single file.</summary>
    [QylCapability("lsp_code_intelligence")]
    [McpServerTool(
        Name = "lsp_diagnostics", Title = "Diagnostics",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return compiler/analyzer diagnostics for a source file.")]
    public Task<string> DiagnosticsAsync(
        [Description("Absolute path to the source file")]
        string filePath,
        CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            var opened = await wrapper.OpenAsync(filePath, ct).ConfigureAwait(false);
            var result = await opened.Client.DiagnosticsAsync(opened.Uri, ct).ConfigureAwait(false);
            return FormatDiagnostics(opened.Uri, result);
        });

    /// <summary>Check whether the symbol at the given position is renameable.</summary>
    [QylCapability("lsp_code_intelligence")]
    [McpServerTool(
        Name = "lsp_prepare_rename", Title = "Prepare Rename",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Validate whether the symbol at the given 1-based line/column can be renamed.")]
    public Task<string> PrepareRenameAsync(
        [Description("Absolute path to the source file")]
        string filePath,
        [Description("1-based line number")] int line,
        [Description("1-based column number")] int column,
        CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            var opened = await wrapper.OpenAsync(filePath, ct).ConfigureAwait(false);
            var (line0, char0) = LspClientWrapper.ToZeroBased(line, column);
            var result = await opened.Client.PrepareRenameAsync(opened.Uri, line0, char0, ct).ConfigureAwait(false);
            return result is null
                ? "valid=false (symbol is not renameable at this position)"
                : "valid=true";
        });

    /// <summary>Rename the symbol at the given position across the workspace. Writes to disk.</summary>
    [QylCapability("lsp_code_intelligence", QylCapabilityRole.FollowUp)]
    [McpServerTool(
        Name = "lsp_rename", Title = "Rename Symbol",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description(
        "Rename the symbol at the given 1-based line/column across the workspace. Writes the WorkspaceEdit to disk.")]
    public Task<string> RenameAsync(
        [Description("Absolute path to the source file")]
        string filePath,
        [Description("1-based line number")] int line,
        [Description("1-based column number")] int column,
        [Description("New identifier to assign at this location")]
        string newName,
        CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(newName))
                return "newName must be a non-empty identifier.";

            var opened = await wrapper.OpenAsync(filePath, ct).ConfigureAwait(false);
            var (line0, char0) = LspClientWrapper.ToZeroBased(line, column);

            // Validity gate — lsp_rename never runs without a successful prepareRename first.
            var prepare = await opened.Client.PrepareRenameAsync(opened.Uri, line0, char0, ct).ConfigureAwait(false);
            if (prepare is null)
                return "Rename rejected: prepareRename returned null (symbol is not renameable at this position).";

            var workspaceEdit =
                await opened.Client.RenameAsync(opened.Uri, line0, char0, newName, ct).ConfigureAwait(false);
            if (workspaceEdit is null)
                return "Rename produced no edits (the symbol may already have the target name).";

            var summary = await editApplier.ApplyAsync(workspaceEdit, ct).ConfigureAwait(false);
            return FormatRenameSummary(newName, summary);
        });

    private static async Task<string> RunAsync(Func<Task<string>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            return $"File not found: {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            return $"Invalid argument: {ex.Message}";
        }
        catch (NotSupportedException ex)
        {
            return $"Unsupported: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            return $"LSP startup failure: {ex.Message}";
        }
        catch (LspProtocolException ex)
        {
            return $"LSP protocol error: {ex.Message}";
        }
    }

    private static string FormatLocations(string title, JsonNode? result)
    {
        var locations = result switch
        {
            JsonArray array => array.OfType<JsonNode>().ToList(),
            JsonObject obj => [obj],
            _ => []
        };

        var sb = new StringBuilder();
        sb.AppendLine(title);
        if (locations.Count is 0)
        {
            sb.AppendLine("No results.");
            return sb.ToString();
        }

        foreach (var location in locations)
            sb.AppendLine(FormatLocationLine(location));
        return sb.ToString();
    }

    private static string FormatLocationLine(JsonNode location)
    {
        var uri = location["uri"]?.GetValue<string>() ?? location["targetUri"]?.GetValue<string>();
        var range = location["range"] ?? location["targetSelectionRange"] ?? location["targetRange"];
        if (uri is null || range?["start"] is not { } start)
            return "- <unparseable location>";

        var (line1, col1) = LspClientWrapper.ToOneBased(
            start["line"]?.GetValue<int>() ?? 0,
            start["character"]?.GetValue<int>() ?? 0);
        return $"- {LspClientWrapper.UriToPath(uri)}:{line1}:{col1}";
    }

    private static string FormatSymbols(string title, JsonNode? result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        if (result is not JsonArray array || array.Count is 0)
        {
            sb.AppendLine("No symbols.");
            return sb.ToString();
        }

        foreach (var entry in array.OfType<JsonObject>())
        {
            var name = entry["name"]?.GetValue<string>() ?? "<unnamed>";
            var kind = SymbolKindLabel(entry["kind"]?.GetValue<int>() ?? 0);
            var location = entry["location"] ?? entry;
            sb.AppendLine($"- **{name}** ({kind}) {FormatLocationLine(location).TrimStart('-', ' ')}");
        }

        return sb.ToString();
    }

    private static string FormatDiagnostics(string uri, JsonNode? result)
    {
        var items = result?["items"] as JsonArray;
        var sb = new StringBuilder();
        sb.AppendLine($"# Diagnostics for {LspClientWrapper.UriToPath(uri)}");
        if (items is null || items.Count is 0)
        {
            sb.AppendLine("No diagnostics.");
            return sb.ToString();
        }

        foreach (var item in items.OfType<JsonObject>())
        {
            var severity = item["severity"]?.GetValue<int>() switch
            {
                1 => "error",
                2 => "warning",
                3 => "info",
                4 => "hint",
                _ => "diagnostic"
            };
            var code = item["code"]?.ToString() ?? "";
            var message = item["message"]?.GetValue<string>() ?? "";
            var start = item["range"]?["start"];
            var (line1, col1) = LspClientWrapper.ToOneBased(
                start?["line"]?.GetValue<int>() ?? 0,
                start?["character"]?.GetValue<int>() ?? 0);
            sb.AppendLine($"- {severity} {code} at {line1}:{col1} - {message}");
        }

        return sb.ToString();
    }

    private static string FormatRenameSummary(string newName, WorkspaceEditApplier.ApplySummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Renamed to `{newName}`");
        sb.AppendLine($"- files changed: {summary.FilesChanged.Count}");
        sb.AppendLine($"- total edits: {summary.EditsPerFile.Values.Sum()}");
        foreach (var file in summary.FilesChanged)
        {
            var count = summary.EditsPerFile.TryGetValue(file, out var c) ? c : 0;
            sb.AppendLine($"  - {file}: {count} edit(s)");
        }

        return sb.ToString();
    }

    // Common LSP SymbolKind values from the spec. Less common kinds fall through to "symbol".
    private static string SymbolKindLabel(int kind) =>
        kind switch
        {
            3 => "namespace",
            5 => "class",
            6 => "method",
            7 => "property",
            8 => "field",
            10 => "enum",
            11 => "interface",
            23 => "struct",
            _ => "symbol"
        };
}

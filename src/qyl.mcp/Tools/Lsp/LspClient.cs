// Copyright (c) 2025-2026 ancplua

using System.Text.Json.Nodes;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Typed LSP method layer over <see cref="LspClientConnection" />. One instance per open
///     workspace + server pair. Positions on the wire are 0-based (LSP spec); callers translate
///     1-based user positions at the <see cref="LspClientWrapper" /> boundary.
/// </summary>
internal sealed class LspClient(LspClientConnection connection)
{
    /// <summary>The underlying connection (exposed for disposal by the wrapper).</summary>
    public LspClientConnection Connection => connection;

    /// <summary>Notifies the server that a document has been opened with the given contents.</summary>
    public Task DidOpenAsync(string uri, string languageId, int version, string text, CancellationToken ct)
    {
        var parameters = new JsonObject
        {
            ["textDocument"] = new JsonObject
            {
                ["uri"] = uri,
                ["languageId"] = languageId,
                ["version"] = version,
                ["text"] = text,
            },
        };
        return connection.SendNotificationAsync("textDocument/didOpen", parameters, ct);
    }

    /// <summary>
    ///     Notifies the server that a document has changed. Sends the full document (LSP allows
    ///     this when incremental sync is not registered).
    /// </summary>
    public Task DidChangeAsync(string uri, int version, string text, CancellationToken ct)
    {
        var parameters = new JsonObject
        {
            ["textDocument"] = new JsonObject
            {
                ["uri"] = uri,
                ["version"] = version,
            },
            ["contentChanges"] = new JsonArray
            {
                new JsonObject { ["text"] = text },
            },
        };
        return connection.SendNotificationAsync("textDocument/didChange", parameters, ct);
    }

    /// <summary><c>textDocument/definition</c>. Returns a <see cref="JsonNode" /> that is either a Location, Location[], or LocationLink[].</summary>
    public Task<JsonNode?> GotoDefinitionAsync(string uri, int line0, int character0, CancellationToken ct) =>
        connection.SendRequestAsync("textDocument/definition", BuildTextDocumentPosition(uri, line0, character0), ct);

    /// <summary><c>textDocument/references</c>.</summary>
    public Task<JsonNode?> FindReferencesAsync(string uri, int line0, int character0, bool includeDeclaration, CancellationToken ct)
    {
        var parameters = BuildTextDocumentPosition(uri, line0, character0);
        parameters["context"] = new JsonObject { ["includeDeclaration"] = includeDeclaration };
        return connection.SendRequestAsync("textDocument/references", parameters, ct);
    }

    /// <summary><c>textDocument/documentSymbol</c>. Returns DocumentSymbol[] or SymbolInformation[].</summary>
    public Task<JsonNode?> DocumentSymbolsAsync(string uri, CancellationToken ct)
    {
        var parameters = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
        };
        return connection.SendRequestAsync("textDocument/documentSymbol", parameters, ct);
    }

    /// <summary><c>workspace/symbol</c>.</summary>
    public Task<JsonNode?> WorkspaceSymbolsAsync(string query, CancellationToken ct)
    {
        var parameters = new JsonObject { ["query"] = query };
        return connection.SendRequestAsync("workspace/symbol", parameters, ct);
    }

    /// <summary><c>textDocument/diagnostic</c> (pull-model diagnostics, LSP 3.17+).</summary>
    public Task<JsonNode?> DiagnosticsAsync(string uri, CancellationToken ct)
    {
        var parameters = new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
        };
        return connection.SendRequestAsync("textDocument/diagnostic", parameters, ct);
    }

    /// <summary><c>textDocument/prepareRename</c>.</summary>
    public Task<JsonNode?> PrepareRenameAsync(string uri, int line0, int character0, CancellationToken ct) =>
        connection.SendRequestAsync("textDocument/prepareRename", BuildTextDocumentPosition(uri, line0, character0), ct);

    /// <summary><c>textDocument/rename</c>. Returns a <c>WorkspaceEdit</c>.</summary>
    public Task<JsonNode?> RenameAsync(string uri, int line0, int character0, string newName, CancellationToken ct)
    {
        var parameters = BuildTextDocumentPosition(uri, line0, character0);
        parameters["newName"] = newName;
        return connection.SendRequestAsync("textDocument/rename", parameters, ct);
    }

    private static JsonObject BuildTextDocumentPosition(string uri, int line0, int character0) =>
        new()
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject
            {
                ["line"] = line0,
                ["character"] = character0,
            },
        };
}

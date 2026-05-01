// Copyright (c) 2025-2026 ancplua

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Per-workspace client pool. Lazily starts one <see cref="LspClientConnection" /> per
///     <c>(workspaceRoot, serverId)</c> pair, tracks <c>didOpen</c> / <c>didChange</c> state per
///     document, and converts the 1-based user coordinate system to the 0-based LSP wire format
///     at this boundary.
/// </summary>
internal sealed partial class LspClientWrapper(
    LspServerResolution resolution,
    ILogger<LspClientWrapper> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<LspClientConnection>>> _clients =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, DocumentState> _documents = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _clients)
        {
            if (!kv.Value.IsValueCreated)
                continue;

            try
            {
                var connection = await kv.Value.Value.ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException cancelled)
            {
                LogStartCancelled(logger, cancelled, kv.Key);
            }
            catch (IOException ex)
            {
                LogDisposalIoError(logger, ex, kv.Key);
            }
        }

        _clients.Clear();
        _documents.Clear();
    }

    /// <summary>Resolves a client for the file's workspace + server and ensures the document is open.</summary>
    public async Task<OpenedDocument> OpenAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"LSP tools require an existing file. Not found: {filePath}", filePath);

        var resolved = resolution.Resolve(filePath);
        var connection = await GetOrStartClientAsync(resolved, ct).ConfigureAwait(false);
        var uri = new Uri(filePath).AbsoluteUri;
        await EnsureDocumentAsync(connection, filePath, uri, resolved, ct).ConfigureAwait(false);
        return new OpenedDocument(connection, uri, resolved);
    }

    /// <summary>Converts 1-based user line/column to 0-based LSP line/character.</summary>
    public static (int Line0, int Character0) ToZeroBased(int line1, int column1)
    {
        if (line1 < 1)
            throw new ArgumentOutOfRangeException(nameof(line1), line1, "Line must be 1-based (>= 1).");
        if (column1 < 1)
            throw new ArgumentOutOfRangeException(nameof(column1), column1, "Column must be 1-based (>= 1).");
        return (line1 - 1, column1 - 1);
    }

    /// <summary>Converts 0-based LSP line/character to 1-based user line/column.</summary>
    public static (int Line1, int Column1) ToOneBased(int line0, int character0) =>
        (line0 + 1, character0 + 1);

    /// <summary>Converts a <c>file://</c> URI back to a local path for display.</summary>
    public static string UriToPath(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile ? parsed.LocalPath : uri;

    private ValueTask<LspClientConnection> GetOrStartClientAsync(LspServerResolutionResult resolved,
        CancellationToken ct)
    {
        var key = ClientKey(resolved);
        var lazy = _clients.GetOrAdd(key, _ => new Lazy<Task<LspClientConnection>>(
            () => StartClientAsync(resolved, ct),
            LazyThreadSafetyMode.ExecutionAndPublication));

        Task<LspClientConnection> startTask;
        try
        {
            startTask = lazy.Value;
        }
        catch
        {
            _clients.TryRemove(new KeyValuePair<string, Lazy<Task<LspClientConnection>>>(key, lazy));
            throw;
        }

        return startTask.IsCompletedSuccessfully
            ? ValueTask.FromResult(startTask.Result)
            : AwaitStartedClientAsync(key, lazy, startTask);
    }

    private async ValueTask<LspClientConnection> AwaitStartedClientAsync(
        string key,
        Lazy<Task<LspClientConnection>> lazy,
        Task<LspClientConnection> startTask)
    {
        try
        {
            return await startTask.ConfigureAwait(false);
        }
        catch
        {
            // Start failed — evict the failed lazy so the next call retries.
            _clients.TryRemove(new KeyValuePair<string, Lazy<Task<LspClientConnection>>>(key, lazy));
            throw;
        }
    }

    private async Task<LspClientConnection> StartClientAsync(LspServerResolutionResult resolved, CancellationToken ct)
    {
        LogStartingServer(logger, resolved.Definition.Id, resolved.WorkspaceRoot);
        return await LspClientConnection.OpenAsync(resolved, ct).ConfigureAwait(false);
    }

    private async Task EnsureDocumentAsync(
        LspClientConnection client, string filePath, string uri, LspServerResolutionResult resolved,
        CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var languageId = LanguageIdFor(resolved.Definition.Id, filePath);
        var key = DocumentKey(resolved, uri);

        if (!_documents.TryGetValue(key, out var state))
        {
            await client.DidOpenAsync(uri, languageId, 1, text, ct).ConfigureAwait(false);
            _documents[key] = new DocumentState(text, 1);
            return;
        }

        if (!string.Equals(state.Text, text, StringComparison.Ordinal))
        {
            var nextVersion = state.Version + 1;
            await client.DidChangeAsync(uri, nextVersion, text, ct).ConfigureAwait(false);
            _documents[key] = new DocumentState(text, nextVersion);
        }
    }

    private static string ClientKey(LspServerResolutionResult resolved) =>
        $"{resolved.Definition.Id}|{resolved.WorkspaceRoot}";

    private static string DocumentKey(LspServerResolutionResult resolved, string uri) =>
        $"{ClientKey(resolved)}|{uri}";

    private static string LanguageIdFor(string serverId, string filePath) =>
        serverId switch
        {
            "csharp-ls" => "csharp",
            _ => Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant()
        };

    [LoggerMessage(Level = LogLevel.Debug, Message = "LSP client {Key} start was cancelled; nothing to dispose")]
    private static partial void LogStartCancelled(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LSP client {Key} disposal encountered an IO error")]
    private static partial void LogDisposalIoError(ILogger logger, Exception ex, string key);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting LSP server {ServerId} for workspace {WorkspaceRoot}")]
    private static partial void LogStartingServer(ILogger logger, string serverId, string workspaceRoot);

    private sealed record DocumentState(string Text, int Version);
}

/// <summary>A handle to an opened document.</summary>
/// <param name="Client">The typed LSP client.</param>
/// <param name="Uri">The document's <c>file://</c> URI.</param>
/// <param name="Resolution">Resolved server + binary + workspace metadata.</param>
internal sealed record OpenedDocument(LspClientConnection Client, string Uri, LspServerResolutionResult Resolution);

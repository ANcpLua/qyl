// Copyright (c) 2025-2026 ancplua

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Owns a single LSP server connection: process + transport + real
///     <c>initialize</c> / <c>initialized</c> / <c>shutdown</c> / <c>exit</c> handshake.
///     No fixed sleeps — see skill's "JSON-RPC framing" hard rule.
/// </summary>
internal sealed partial class LspClientConnection : IAsyncDisposable
{
    private readonly CancellationTokenSource _dispatcherCts = new();
    private readonly Task _dispatcherTask;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly LspProcess _process;
    private readonly LspClientTransport _transport;
    private int _disposed;

    private LspClientConnection(
        LspProcess process,
        LspServerResolutionResult resolution,
        ILogger? logger)
    {
        _process = process;
        _transport = new LspClientTransport(process.Stdout, process.Stdin, logger);
        _logger = logger ?? NullLogger.Instance;
        Resolution = resolution;
        _dispatcherTask = Task.Run(() => DispatchAsync(_dispatcherCts.Token));
    }

    /// <summary>Resolved server + binary + workspace metadata.</summary>
    public LspServerResolutionResult Resolution { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        // Polite shutdown: shutdown request + exit notification, 2s budget. If the server is
        // unresponsive or already torn down, LspProcess.DisposeAsync handles the forceful kill.
        try
        {
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await SendRequestAsync("shutdown", null, shutdownCts.Token).ConfigureAwait(false);
            await SendNotificationAsync("exit", null, shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogShutdownTimeout(_logger);
        }
        catch (LspProtocolException ex)
        {
            LogShutdownRejected(_logger, ex);
        }
        catch (IOException ex)
        {
            LogShutdownPipeBroken(_logger, ex);
        }
        catch (ObjectDisposedException ex)
        {
            LogShutdownTransportDisposed(_logger, ex);
        }

        await _dispatcherCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _dispatcherTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogDispatcherCancelledDuringDispose(_logger);
        }

        _dispatcherCts.Dispose();

        await _transport.DisposeAsync().ConfigureAwait(false);
        await _process.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Starts the process, wraps the transport, runs the real init handshake.</summary>
    /// <param name="resolution">Resolved launch parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="logger">Optional logger for transport + shutdown diagnostics.</param>
    public static async Task<LspClientConnection> OpenAsync(
        LspServerResolutionResult resolution,
        CancellationToken ct,
        ILogger? logger = null)
    {
        var process = LspProcess.Start(resolution);
        try
        {
            return await CreateAndInitializeAsync(process, resolution, logger, ct).ConfigureAwait(false);
        }
        catch
        {
            await process.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<LspClientConnection> CreateAndInitializeAsync(
        LspProcess process,
        LspServerResolutionResult resolution,
        ILogger? logger,
        CancellationToken ct)
    {
        LspClientConnection? connection = null;
        try
        {
            connection = new LspClientConnection(process, resolution, logger);
            await connection.InitializeAsync(ct).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            if (connection is not null)
                await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Sends a JSON-RPC request and awaits its response.</summary>
    public async Task<JsonNode?> SendRequestAsync(string method, JsonNode? parameters, CancellationToken ct)
    {
        var id = _transport.AllocateRequestId();
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await using var _ = ct.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending))
                pending.TrySetCanceled(ct);
        }).ConfigureAwait(false);

        await _transport.SendRequestAsync(id, method, parameters, ct).ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>Sends a fire-and-forget JSON-RPC notification.</summary>
    public Task SendNotificationAsync(string method, JsonNode? parameters, CancellationToken ct) =>
        _transport.SendNotificationAsync(method, parameters, ct);

    // ── Typed LSP method layer (one-per-open-workspace; positions are 0-based LSP-wire format;
    //    callers translate from 1-based user positions at the LspClientWrapper boundary) ────────

    /// <summary>Notifies the server that a document has been opened with the given contents.</summary>
    public Task DidOpenAsync(string uri, string languageId, int version, string text, CancellationToken ct) =>
        SendNotificationAsync("textDocument/didOpen",
            new JsonObject
            {
                ["textDocument"] = new JsonObject
                {
                    ["uri"] = uri, ["languageId"] = languageId, ["version"] = version, ["text"] = text
                }
            }, ct);

    /// <summary>
    ///     Notifies the server that a document has changed. Sends the full document (LSP allows
    ///     this when incremental sync is not registered).
    /// </summary>
    public Task DidChangeAsync(string uri, int version, string text, CancellationToken ct) =>
        SendNotificationAsync("textDocument/didChange",
            new JsonObject
            {
                ["textDocument"] = new JsonObject { ["uri"] = uri, ["version"] = version },
                ["contentChanges"] = new JsonArray { new JsonObject { ["text"] = text } }
            }, ct);

    /// <summary><c>textDocument/definition</c>. Returns Location | Location[] | LocationLink[].</summary>
    public Task<JsonNode?> GotoDefinitionAsync(string uri, int line0, int character0, CancellationToken ct) =>
        SendRequestAsync("textDocument/definition", BuildTextDocumentPosition(uri, line0, character0), ct);

    /// <summary><c>textDocument/references</c>.</summary>
    public Task<JsonNode?> FindReferencesAsync(string uri, int line0, int character0, bool includeDeclaration,
        CancellationToken ct)
    {
        var parameters = BuildTextDocumentPosition(uri, line0, character0);
        parameters["context"] = new JsonObject { ["includeDeclaration"] = includeDeclaration };
        return SendRequestAsync("textDocument/references", parameters, ct);
    }

    /// <summary><c>textDocument/documentSymbol</c>. Returns DocumentSymbol[] or SymbolInformation[].</summary>
    public Task<JsonNode?> DocumentSymbolsAsync(string uri, CancellationToken ct) =>
        SendRequestAsync("textDocument/documentSymbol",
            new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }, ct);

    /// <summary><c>workspace/symbol</c>.</summary>
    public Task<JsonNode?> WorkspaceSymbolsAsync(string query, CancellationToken ct) =>
        SendRequestAsync("workspace/symbol", new JsonObject { ["query"] = query }, ct);

    /// <summary><c>textDocument/diagnostic</c> (pull-model diagnostics, LSP 3.17+).</summary>
    public Task<JsonNode?> DiagnosticsAsync(string uri, CancellationToken ct) =>
        SendRequestAsync("textDocument/diagnostic",
            new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } }, ct);

    /// <summary><c>textDocument/prepareRename</c>.</summary>
    public Task<JsonNode?> PrepareRenameAsync(string uri, int line0, int character0, CancellationToken ct) =>
        SendRequestAsync("textDocument/prepareRename", BuildTextDocumentPosition(uri, line0, character0), ct);

    /// <summary><c>textDocument/rename</c>. Returns a <c>WorkspaceEdit</c>.</summary>
    public Task<JsonNode?> RenameAsync(string uri, int line0, int character0, string newName, CancellationToken ct)
    {
        var parameters = BuildTextDocumentPosition(uri, line0, character0);
        parameters["newName"] = newName;
        return SendRequestAsync("textDocument/rename", parameters, ct);
    }

    private static JsonObject BuildTextDocumentPosition(string uri, int line0, int character0) =>
        new()
        {
            ["textDocument"] = new JsonObject { ["uri"] = uri },
            ["position"] = new JsonObject { ["line"] = line0, ["character"] = character0 }
        };

    private Task InitializeAsync(CancellationToken ct)
    {
        using var initCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        initCts.CancelAfter(TimeSpan.FromSeconds(Resolution.Definition.FirstInitTimeoutSeconds));
        var token = initCts.Token;

        var rootUri = new Uri(Resolution.WorkspaceRoot.EndsWith(Path.DirectorySeparatorChar)
            ? Resolution.WorkspaceRoot
            : Resolution.WorkspaceRoot + Path.DirectorySeparatorChar);

        var initParams = new JsonObject
        {
            ["processId"] = Environment.ProcessId,
            ["rootUri"] = rootUri.AbsoluteUri,
            ["clientInfo"] = new JsonObject { ["name"] = "qyl.mcp", ["version"] = "1.0.0" },
            ["capabilities"] = new JsonObject
            {
                ["workspace"] =
                    new JsonObject
                    {
                        ["workspaceEdit"] = new JsonObject
                        {
                            ["documentChanges"] = true,
                            ["resourceOperations"] = new JsonArray { "create", "rename", "delete" }
                        },
                        ["symbol"] = new JsonObject()
                    },
                ["textDocument"] = new JsonObject
                {
                    ["synchronization"] = new JsonObject(),
                    ["definition"] = new JsonObject(),
                    ["references"] = new JsonObject(),
                    ["documentSymbol"] = new JsonObject(),
                    ["rename"] = new JsonObject { ["prepareSupport"] = true },
                    ["diagnostic"] = new JsonObject()
                }
            }
        };

        return RunHandshakeAsync(initParams, token);
    }

    private async Task RunHandshakeAsync(JsonObject initParams, CancellationToken ct)
    {
        await SendRequestAsync("initialize", initParams, ct).ConfigureAwait(false);
        await SendNotificationAsync("initialized", new JsonObject(), ct).ConfigureAwait(false);
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _transport.IncomingReader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (frame["id"] is not JsonValue value ||
                    value.GetValueKind() != JsonValueKind.Number ||
                    !value.TryGetValue<long>(out var id))
                    continue; // notifications / server-initiated requests — not used in Phase 1

                if (!_pending.TryRemove(id, out var pending))
                    continue;

                if (frame["error"] is JsonObject error)
                {
                    var code = error["code"]?.GetValue<int>() ?? 0;
                    var message = error["message"]?.GetValue<string>() ?? "LSP error";
                    pending.TrySetException(new LspProtocolException(code, message));
                }
                else
                {
                    pending.TrySetResult(frame["result"]?.DeepClone());
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogDispatcherLoopCancelled(_logger);
        }
        finally
        {
            var cause = new ObjectDisposedException(nameof(LspClientConnection));
            foreach (var (id, tcs) in _pending)
            {
                if (_pending.TryRemove(id, out _))
                    tcs.TrySetException(cause);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "LSP server did not acknowledge shutdown within 2s; falling through to process kill.")]
    private static partial void LogShutdownTimeout(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LSP server rejected shutdown; falling through to process kill.")]
    private static partial void LogShutdownRejected(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "LSP transport pipe broken during shutdown; falling through to process kill.")]
    private static partial void LogShutdownPipeBroken(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LSP transport already disposed during shutdown.")]
    private static partial void LogShutdownTransportDisposed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LSP dispatcher task observed cancellation during disposal.")]
    private static partial void LogDispatcherCancelledDuringDispose(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "LSP dispatcher loop cancelled during disposal.")]
    private static partial void LogDispatcherLoopCancelled(ILogger logger);
}

/// <summary>Thrown when a JSON-RPC response carries an <c>error</c> object.</summary>
internal sealed class LspProtocolException : Exception
{
    /// <summary>Constructs with a JSON-RPC error code and message.</summary>
    public LspProtocolException(int code, string message)
        : base($"LSP error {code}: {message}") => Code = code;

    /// <summary>Constructs with a JSON-RPC error code, message, and inner exception.</summary>
    public LspProtocolException(int code, string message, Exception innerException)
        : base($"LSP error {code}: {message}", innerException) => Code = code;

    /// <summary>Parameterless constructor for serialization.</summary>
    public LspProtocolException() : base("LSP error") => Code = 0;

    /// <summary>Constructs from a bare message (code defaults to 0).</summary>
    public LspProtocolException(string message) : base(message) => Code = 0;

    /// <summary>Constructs from a message + inner exception (code defaults to 0).</summary>
    public LspProtocolException(string message, Exception innerException)
        : base(message, innerException) => Code = 0;

    /// <summary>JSON-RPC error code.</summary>
    public int Code { get; }
}

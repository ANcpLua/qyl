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
internal sealed class LspClientConnection : IAsyncDisposable
{
    private readonly LspProcess _process;
    private readonly LspClientTransport _transport;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly CancellationTokenSource _dispatcherCts = new();
    private readonly Task _dispatcherTask;
    private int _disposed;

    private LspClientConnection(
        LspProcess process,
        LspClientTransport transport,
        LspServerResolutionResult resolution,
        ILogger? logger)
    {
        _process = process;
        _transport = transport;
        _logger = logger ?? NullLogger.Instance;
        Resolution = resolution;
        _dispatcherTask = Task.Run(() => DispatchAsync(_dispatcherCts.Token));
    }

    /// <summary>Resolved server + binary + workspace metadata.</summary>
    public LspServerResolutionResult Resolution { get; }

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
        LspClientTransport? transport = null;
        LspClientConnection? connection = null;
        try
        {
            transport = new LspClientTransport(process.Stdout, process.Stdin, logger);
            connection = new LspClientConnection(process, transport, resolution, logger);
            await connection.InitializeAsync(ct).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                if (transport is not null)
                    await transport.DisposeAsync().ConfigureAwait(false);
                await process.DisposeAsync().ConfigureAwait(false);
            }

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
                ["workspace"] = new JsonObject
                {
                    ["workspaceEdit"] = new JsonObject
                    {
                        ["documentChanges"] = true,
                        ["resourceOperations"] = new JsonArray { "create", "rename", "delete" },
                    },
                    ["symbol"] = new JsonObject(),
                },
                ["textDocument"] = new JsonObject
                {
                    ["synchronization"] = new JsonObject(),
                    ["definition"] = new JsonObject(),
                    ["references"] = new JsonObject(),
                    ["documentSymbol"] = new JsonObject(),
                    ["rename"] = new JsonObject { ["prepareSupport"] = true },
                    ["diagnostic"] = new JsonObject(),
                },
            },
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
            _logger.LogDebug("LSP dispatcher loop cancelled during disposal.");
        }
        finally
        {
            var cause = new ObjectDisposedException(nameof(LspClientConnection));
            foreach (var (id, tcs) in _pending)
            {
                if (_pending.TryRemove(id, out var removed))
                    removed.TrySetException(cause);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
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
            _logger.LogDebug("LSP server did not acknowledge shutdown within 2s; falling through to process kill.");
        }
        catch (LspProtocolException ex)
        {
            _logger.LogDebug(ex, "LSP server rejected shutdown; falling through to process kill.");
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "LSP transport pipe broken during shutdown; falling through to process kill.");
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "LSP transport already disposed during shutdown.");
        }

        await _dispatcherCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _dispatcherTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("LSP dispatcher task observed cancellation during disposal.");
        }
        _dispatcherCts.Dispose();

        await _transport.DisposeAsync().ConfigureAwait(false);
        await _process.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>Thrown when a JSON-RPC response carries an <c>error</c> object.</summary>
internal sealed class LspProtocolException : Exception
{
    /// <summary>JSON-RPC error code.</summary>
    public int Code { get; }

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
}

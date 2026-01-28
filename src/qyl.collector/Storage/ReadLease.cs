
namespace qyl.collector.Storage;

/// <summary>
///     RAII-style lease for pooled read connections.
/// </summary>
public readonly struct ReadLease : IAsyncDisposable, IDisposable
{
    private readonly DuckDbStore _store;
    private readonly bool _isShared;

    /// <summary>
    ///     The pooled DuckDB connection for read operations.
    /// </summary>
    public DuckDBConnection Connection { get; }

    internal ReadLease(DuckDbStore store, DuckDBConnection con, bool isShared = false)
    {
        _store = store;
        Connection = con;
        _isShared = isShared;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Don't return shared connections (in-memory mode)
        if (_isShared)
        {
            _store.ReleaseReadGate();
            return;
        }

        _store.ReturnRead(Connection);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

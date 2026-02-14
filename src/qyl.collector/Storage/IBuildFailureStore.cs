namespace qyl.collector.Storage;

public interface IBuildFailureStore
{
    Task<string> InsertAsync(BuildFailureRecord record, CancellationToken ct = default);
    Task<BuildFailureRecord?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<BuildFailureRecord>> ListAsync(int limit = 10, CancellationToken ct = default);
    Task<IReadOnlyList<BuildFailureRecord>> SearchAsync(string pattern, int limit = 50, CancellationToken ct = default);
}

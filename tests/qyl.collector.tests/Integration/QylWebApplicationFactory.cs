using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using qyl.collector.Auth;
using qyl.collector.Query;
using qyl.collector.Storage;

namespace qyl.collector.tests.Integration;

/// <summary>
///     Custom WebApplicationFactory for qyl.collector integration tests.
///     Uses in-memory DuckDB and bypasses authentication.
/// </summary>
public sealed class QylWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Test auth token used for all requests.</summary>
    public const string TestToken = "test-token-12345";

    private DuckDbStore? _store;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DuckDbStore registration
            var storeDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DuckDbStore));
            if (storeDescriptor is not null)
                services.Remove(storeDescriptor);

            // Remove existing SessionQueryService registration
            var queryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(SessionQueryService));
            if (queryDescriptor is not null)
                services.Remove(queryDescriptor);

            // Remove existing token auth options
            var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TokenAuthOptions));
            if (authDescriptor is not null)
                services.Remove(authDescriptor);

            // Remove production build failure store (uses file-backed DuckDB connection).
            var buildFailureStoreDescriptor =
                services.SingleOrDefault(d => d.ServiceType == typeof(IBuildFailureStore));
            if (buildFailureStoreDescriptor is not null)
                services.Remove(buildFailureStoreDescriptor);

            // Add test token auth
            services.AddSingleton(new TokenAuthOptions
            {
                Token = TestToken
            });

            // Add SSE broadcaster for ingestion endpoints
            services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();

            // Create in-memory DuckDB store for tests
            _store = new DuckDbStore(TestConstants.InMemoryDb);
            services.AddSingleton(_store);

            // Add SessionQueryService with proper DuckDbStore injection
            services.AddSingleton(sp =>
            {
                var store = sp.GetRequiredService<DuckDbStore>();
                return new SessionQueryService(store);
            });

            services.AddSingleton<IBuildFailureStore, InMemoryBuildFailureStore>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _store?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }
}

internal sealed class InMemoryBuildFailureStore : IBuildFailureStore
{
    private readonly ConcurrentDictionary<string, BuildFailureRecord> _records = new(StringComparer.Ordinal);

    public Task<string> InsertAsync(BuildFailureRecord record, CancellationToken ct = default)
    {
        var id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id;
        _records[id] = record with { Id = id };
        return Task.FromResult(id);
    }

    public Task<BuildFailureRecord?> GetAsync(string id, CancellationToken ct = default)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<BuildFailureRecord>> ListAsync(int limit = 10, CancellationToken ct = default)
    {
        IReadOnlyList<BuildFailureRecord> result = _records.Values
            .OrderByDescending(static r => r.Timestamp)
            .Take(Math.Clamp(limit, 1, 500))
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<BuildFailureRecord>> SearchAsync(string pattern, int limit = 50,
        CancellationToken ct = default)
    {
        IReadOnlyList<BuildFailureRecord> result = _records.Values
            .Where(r =>
                (r.ErrorSummary?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.PropertyIssuesJson?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.CallStackJson?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(static r => r.Timestamp)
            .Take(Math.Clamp(limit, 1, 500))
            .ToArray();
        return Task.FromResult(result);
    }
}
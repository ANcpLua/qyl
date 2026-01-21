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
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _store?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }
}

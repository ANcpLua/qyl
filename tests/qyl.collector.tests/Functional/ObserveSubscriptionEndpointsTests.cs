using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Qyl.Collector.Tests.Functional.Observe;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class ObserveSubscriptionEndpointsTests
    : IClassFixture<ObserveSubscriptionEndpointsTests.CollectorFactory>
{
    private const string CatalogPath = "/api/v1/observe/catalog";
    private const string SubscriptionsPath = "/api/v1/observe/";

    private readonly CollectorFactory _factory;

    public ObserveSubscriptionEndpointsTests(CollectorFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_catalog_returns_schema_version_and_known_domains()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(CatalogPath, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("schema_version").GetString()
            .Should().NotBeNullOrWhiteSpace("the catalog must publish a non-empty schema_version");
        body.GetProperty("domains").GetArrayLength()
            .Should().BeGreaterThan(0, "at least one domain contract is registered on boot");
        body.GetProperty("active_subscriptions").ValueKind
            .Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Post_subscription_with_missing_filter_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SubscriptionsPath, new
        {
            filter = "",
            endpoint = "http://localhost:4318/v1/traces"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("filter");
    }

    [Fact]
    public async Task Post_subscription_with_missing_endpoint_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SubscriptionsPath, new
        {
            filter = "qyl.collector",
            endpoint = ""
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("endpoint");
    }

    [Fact]
    public async Task Post_subscription_with_non_absolute_endpoint_returns_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SubscriptionsPath, new
        {
            filter = "qyl.collector",
            endpoint = "not-a-real-uri"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("absolute");
    }

    [Fact]
    public async Task Post_subscription_with_incompatible_schema_major_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var uniqueFilter = $"qyl.test.reject-{Guid.NewGuid():N}";

        // Reject requires both versions to parse as semconv-major.minor[.patch]
        // (see ANcpLua.Roslyn.Utilities.OTel.SemconvVersion + SchemaVersionNegotiator).
        // Unparseable values fall through to a permissive Accept, so use a
        // well-formed version whose major is guaranteed to differ from the
        // deployed contract's.
        const string mismatchedVersion = "semconv-999.0.0";

        using var response = await client.PostAsJsonAsync(SubscriptionsPath, new
        {
            filter = uniqueFilter,
            endpoint = "http://localhost:4318/v1/traces",
            schema_version = mismatchedVersion
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("requested_version").GetString().Should().Be(mismatchedVersion);
        body.GetProperty("collector_version").GetString()
            .Should().StartWith("semconv-", "the rejection echoes the deployed semconv version");
    }

    [Fact]
    public async Task Post_subscription_with_compatible_minor_drift_returns_warning()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var uniqueFilter = $"qyl.test.warn-{Guid.NewGuid():N}";

        // Same major (1) but different patch — Accept with `schema_warning`.
        const string driftedVersion = "semconv-1.0.0";

        using var response = await client.PostAsJsonAsync(SubscriptionsPath, new
        {
            filter = uniqueFilter,
            endpoint = "http://127.0.0.1:65534/v1/traces",
            schema_version = driftedVersion
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.TryGetProperty("schema_warning", out var warning).Should().BeTrue(
            "minor-version drift must surface a warning to the subscriber");
        warning.GetString().Should().Contain(driftedVersion);

        // Cleanup so the shared SubscriptionManager doesn't leak this listener.
        var subscriptionId = body.GetProperty("subscription").GetProperty("id").GetString();
        using var cleanup = await client.DeleteAsync($"{SubscriptionsPath}{subscriptionId}", ct);
    }

    [Fact]
    public async Task Subscribe_then_unsubscribe_round_trips_through_list()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var uniqueFilter = $"qyl.test.roundtrip-{Guid.NewGuid():N}";
        const string endpoint = "http://127.0.0.1:65535/v1/traces";

        using var createResponse = await client.PostAsJsonAsync(SubscriptionsPath, new
        {
            filter = uniqueFilter,
            endpoint
        }, ct);

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = created.GetProperty("id").GetString();
        id.Should().NotBeNullOrWhiteSpace();

        using var listResponse = await client.GetAsync(SubscriptionsPath, ct);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = await listResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        listed.GetProperty("subscriptions").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetString() == id,
                "newly created subscription must appear in the list endpoint");

        using var deleteResponse = await client.DeleteAsync($"{SubscriptionsPath}{id}", ct);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var listAfterResponse = await client.GetAsync(SubscriptionsPath, ct);
        var listedAfter = await listAfterResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        listedAfter.GetProperty("subscriptions").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetString() == id,
                "the subscription must be gone after DELETE returned 204");
    }

    [Fact]
    public async Task Delete_unknown_subscription_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid().ToString("N");

        using var response = await client.DeleteAsync($"{SubscriptionsPath}{unknownId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain(unknownId);
    }

    public sealed class CollectorFactory : WebApplicationFactory<Program>
    {
        // Named in-memory DuckDB: ":memory:<name>" gives each fixture its own
        // catalog. The bare ":memory:" alias (which HealthUiEndpointTests uses)
        // points every connection at the same process-global in-memory DB, so
        // when xUnit runs functional test classes in parallel, their migration
        // runs collide with "Catalog write-write conflict on alter". The unique
        // suffix isolates fixtures without writing to disk.
        private readonly string _dataPath = $":memory:qyl-obs-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QYL_DATA_PATH"] = _dataPath,
                    ["QYL_OTLP_AUTH_MODE"] = "Unsecured"
                });
            });
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
    public async Task Get_catalog_lists_agent_inventory_metric_contract()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(CatalogPath, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var agentDomain = body.GetProperty("domains").EnumerateArray()
            .Single(static domain => domain.GetProperty("source").GetString() == "qyl.agent");

        agentDomain.GetProperty("signals").EnumerateArray()
            .Select(static signal => signal.GetString())
            .Should().Contain("metrics");

        agentDomain.GetProperty("metric_instruments").EnumerateArray()
            .Should().Contain(metric =>
                metric.GetProperty("name").GetString() == "qyl.observability.inventory.size" &&
                metric.GetProperty("instrument").GetString() == "gauge" &&
                metric.GetProperty("unit").GetString() == "{agent}");

        agentDomain.GetProperty("contract_hash").GetString()
            .Should().NotBeNullOrWhiteSpace("metric instruments are part of the observe-domain contract");
    }

    [Fact]
    public async Task Get_catalog_lists_genai_token_metric_with_ucum_unit()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(CatalogPath, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var genAiDomain = body.GetProperty("domains").EnumerateArray()
            .Single(static domain => domain.GetProperty("source").GetString() == "qyl.genai");

        genAiDomain.GetProperty("metric_instruments").EnumerateArray()
            .Should().Contain(metric =>
                metric.GetProperty("name").GetString() == "gen_ai.client.token.usage" &&
                metric.GetProperty("instrument").GetString() == "histogram" &&
                metric.GetProperty("unit").GetString() == "{token}");
    }

    [Theory]
    [InlineData("", "http://localhost:4318/v1/traces", "filter")]
    [InlineData("qyl.collector", "", "endpoint")]
    [InlineData("qyl.collector", "not-a-real-uri", "absolute")]
    public async Task Post_subscription_with_invalid_payload_returns_400(string filter, string endpoint, string expectedFragment)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SubscriptionsPath, new { filter, endpoint }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain(expectedFragment);
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

    public sealed class CollectorFactory() : CollectorFunctionalFactory("observe")
    {
    }

}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Qyl.Collector.Tests.Functional.SchemaControl;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class SchemaPromotionEndpointsTests
    : IClassFixture<SchemaPromotionEndpointsTests.CollectorFactory>
{
    private const string PromotionsPath = "/api/v1/schema/promotions";

    private readonly CollectorFactory _factory;

    public SchemaPromotionEndpointsTests(CollectorFactory factory) => _factory = factory;

    [Theory]
    [InlineData("add_column", "", "TargetTable")]
    [InlineData("", "qyl_test_table", "ChangeType")]
    public async Task Post_promotion_with_invalid_payload_returns_400(string changeType, string targetTable, string expectedFragment)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(PromotionsPath, new
        {
            changeType,
            targetTable,
            targetColumn = "extra",
            columnType = "VARCHAR"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain(expectedFragment);
    }

    [Fact]
    public async Task Post_promotion_creates_pending_record_and_returns_location()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var uniqueTable = UniqueTableName("plan");

        using var response = await client.PostAsJsonAsync(PromotionsPath, new
        {
            changeType = "add_table",
            targetTable = uniqueTable,
            targetColumn = "value",
            columnType = "VARCHAR",
            requestedBy = "functional-test"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Endpoint serializes the planner record through the default reflection
        // resolver — JsonSerializerDefaults.Web → camelCase.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var promotionId = body.GetProperty("promotionId").GetString();
        promotionId.Should().NotBeNullOrWhiteSpace();
        var promotionResourceId = promotionId
                                  ?? throw new InvalidOperationException("Expected planner response to include a promotion id.");
        promotionResourceId.Should().StartWith("promo-", "PlanPromotionAsync stamps a 'promo-' prefix");

        body.GetProperty("changeType").GetString().Should().Be("add_table");
        body.GetProperty("targetTable").GetString().Should().Be(uniqueTable);
        body.GetProperty("status").GetString().Should().Be("pending");
        body.GetProperty("sqlStatements").GetString().Should().Contain(uniqueTable,
            "the planner returns the generated DDL on POST even though it is not persisted");

        var location = response.Headers.Location
                       ?? throw new InvalidOperationException("Expected promotion response to include a Location header.");
        location.OriginalString
            .Should().EndWith($"/api/v1/schema/promotions/{promotionResourceId}");
    }

    [Fact]
    public async Task Get_promotion_by_id_returns_planned_record()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var uniqueTable = UniqueTableName("get-by-id");
        var promotionId = await CreateAddTablePromotionAsync(client, uniqueTable, ct);

        using var response = await client.GetAsync($"{PromotionsPath}/{promotionId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("promotionId").GetString().Should().Be(promotionId);
        body.GetProperty("status").GetString().Should().Be("pending");
        body.GetProperty("targetTable").GetString().Should().Be(uniqueTable);
        // sql_statements is now persisted and surfaced on read — the SQL
        // round-trips through DuckDB. This guards against a future
        // MapSchemaPromotion regression that drops the column.
        body.GetProperty("sqlStatements").GetString()
            .Should().Contain(uniqueTable,
                "the planner-generated DDL must round-trip through the schema_promotions table");
    }

    [Fact]
    public async Task Get_promotion_by_unknown_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var unknownId = $"promo-{Guid.NewGuid():N}"[..24];

        using var response = await client.GetAsync($"{PromotionsPath}/{unknownId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_pending_promotions_includes_newly_planned_record()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var uniqueTable = UniqueTableName("listed");
        var promotionId = await CreateAddTablePromotionAsync(client, uniqueTable, ct);

        using var response = await client.GetAsync(PromotionsPath, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.ValueKind.Should().Be(JsonValueKind.Array,
            "GET /api/v1/schema/promotions returns the bare array, not an envelope");

        body.EnumerateArray()
            .Should().Contain(item => item.GetProperty("promotionId").GetString() == promotionId,
                "the newly created promotion must appear in the pending list");
    }

    [Fact]
    public async Task Apply_promotion_executes_ddl_and_marks_applied()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var uniqueTable = UniqueTableName("apply");
        var promotionId = await CreateAddTablePromotionAsync(client, uniqueTable, ct);

        using var applyResponse = await client.PostAsync(
            $"{PromotionsPath}/{promotionId}/apply", content: null, ct);

        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var applied = await applyResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        applied.GetProperty("promotionId").GetString().Should().Be(promotionId);
        applied.GetProperty("status").GetString().Should().Be("applied",
            "SchemaExecutor sets status to 'applied' when ExecuteSchemaDdlAsync succeeds");

        // Pending list filters on status='pending', so applied promotions
        // must fall out of it.
        using var listResponse = await client.GetAsync(PromotionsPath, ct);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        listBody.EnumerateArray()
            .Should().NotContain(item => item.GetProperty("promotionId").GetString() == promotionId,
                "an applied promotion is no longer pending");

        // GET by id still works after apply — status is now 'applied'.
        using var getAfter = await client.GetAsync($"{PromotionsPath}/{promotionId}", ct);
        getAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBody = await getAfter.Content.ReadFromJsonAsync<JsonElement>(ct);
        afterBody.GetProperty("status").GetString().Should().Be("applied");
        afterBody.TryGetProperty("appliedAt", out var appliedAtElement)
            .Should().BeTrue("UpdateSchemaPromotionStatusAsync stamps applied_at on 'applied'");
        appliedAtElement.ValueKind.Should().NotBe(JsonValueKind.Null,
            "the applied_at column is set to now() inside the UPDATE");
    }

    [Fact]
    public async Task Apply_promotion_unknown_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var unknownId = $"promo-{Guid.NewGuid():N}"[..24];

        using var response = await client.PostAsync(
            $"{PromotionsPath}/{unknownId}/apply", content: null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Each functional-test class gets a unique-suffix DuckDB so concurrent
    // catalog migrations don't collide. The string includes the test name so
    // the JIT-suggested unique table names match the fixture catalog.
    private static string UniqueTableName(string scenario) =>
        $"qyl_schema_promo_{scenario.Replace('-', '_')}_{Guid.NewGuid():N}";

    private static async Task<string> CreateAddTablePromotionAsync(
        HttpClient client,
        string targetTable,
        CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(PromotionsPath, new
        {
            changeType = "add_table",
            targetTable,
            targetColumn = "value",
            columnType = "VARCHAR"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "fixture setup requires a successful POST to seed a promotion");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = body.GetProperty("promotionId").GetString();
        id.Should().NotBeNullOrWhiteSpace();
        return id ?? throw new InvalidOperationException("Expected seeded promotion response to include a promotion id.");
    }

    public sealed class CollectorFactory() : CollectorFunctionalFactory("schema")
    {
    }
}

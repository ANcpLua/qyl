using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Qyl.Collector.Tests.Functional.Provisioning;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class ProvisioningEndpointsTests
    : IClassFixture<ProvisioningEndpointsTests.CollectorFactory>
{
    private const string ProfilesPath = "/api/v1/configurator/profiles";
    private const string SelectionsPath = "/api/v1/configurator/selections";
    private const string JobsPath = "/api/v1/configurator/jobs";

    private static readonly string[] BuiltInProfileIds = ["full", "minimal", "genai", "errors"];

    private readonly CollectorFactory _factory;

    public ProvisioningEndpointsTests(CollectorFactory factory) => _factory = factory;

    // -------------------------- Profiles --------------------------

    [Fact]
    public async Task Get_profiles_returns_four_builtin_profiles_with_total()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(ProfilesPath, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        body.GetProperty("total").GetInt32().Should().Be(BuiltInProfileIds.Length);

        var ids = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToArray();

        ids.Should().BeEquivalentTo(BuiltInProfileIds,
            "the four built-in generation profiles are the canonical contract surfaced to the dashboard");
    }

    [Fact]
    public async Task Get_profile_returns_200_for_known_id_with_interceptor_list()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"{ProfilesPath}/full", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        body.GetProperty("id").GetString().Should().Be("full");
        body.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();

        var interceptors = body.GetProperty("interceptors").EnumerateArray()
            .Select(static i => i.GetString())
            .ToArray();
        interceptors.Should().Contain("traces",
            "the 'full' profile must include traces — the lowest-common-denominator signal");
    }

    [Fact]
    public async Task Get_profile_returns_404_for_unknown_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"{ProfilesPath}/no-such-profile", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------- Selections --------------------------

    [Fact]
    public async Task Post_selection_with_missing_workspace_returns_400_with_workspace_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SelectionsPath, new
        {
            workspaceId = "",
            profileId = "full"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("WorkspaceId");
    }

    [Fact]
    public async Task Post_selection_with_missing_profile_returns_400_with_profile_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SelectionsPath, new
        {
            workspaceId = "ws-validate-profile",
            profileId = "   "
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("ProfileId");
    }

    [Fact]
    public async Task Post_selection_with_unknown_profile_returns_400_via_argument_exception_path()
    {
        // GenerationProfileService.SetSelectionAsync throws ArgumentException
        // for unknown profile IDs; the endpoint catches and maps to 400 with
        // a "Request failed" body. The 400 → ArgumentException path is the ONLY
        // way unknown-profile reaches a client (after the static null/empty
        // guards), so it needs a dedicated row.
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(SelectionsPath, new
        {
            workspaceId = $"ws-unknown-{Guid.NewGuid():N}",
            profileId = "definitely-not-a-builtin"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Be("Request failed");
    }

    [Fact]
    public async Task Selection_round_trips_with_overrides_through_store()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var workspaceId = $"ws-roundtrip-{Guid.NewGuid():N}";
        const string overrides = """{"sampling":{"ratio":0.25}}""";

        using var createResponse = await client.PostAsJsonAsync(SelectionsPath, new
        {
            workspaceId,
            profileId = "genai",
            customOverrides = overrides
        }, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var getResponse = await client.GetAsync($"{SelectionsPath}/{workspaceId}", ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

        body.GetProperty("workspaceId").GetString().Should().Be(workspaceId);
        body.GetProperty("profileId").GetString().Should().Be("genai");
        body.GetProperty("customOverrides").GetString().Should().Be(overrides,
            "the overrides payload must survive the DuckDB write-read trip unchanged");
        body.GetProperty("updatedAt").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Get_selection_returns_404_for_unknown_workspace()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var unknownWorkspace = $"ws-unknown-{Guid.NewGuid():N}";

        using var response = await client.GetAsync($"{SelectionsPath}/{unknownWorkspace}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Posting_a_second_selection_for_same_workspace_upserts_existing_row()
    {
        // ON CONFLICT (workspace_id) DO UPDATE — confirms the store-level upsert
        // surfaces through the HTTP boundary as last-write-wins rather than a
        // 409 / duplicate-key failure.
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var workspaceId = $"ws-upsert-{Guid.NewGuid():N}";

        using var firstResponse = await client.PostAsJsonAsync(SelectionsPath, new
        {
            workspaceId,
            profileId = "minimal"
        }, ct);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var secondResponse = await client.PostAsJsonAsync(SelectionsPath, new
        {
            workspaceId,
            profileId = "errors"
        }, ct);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var getResponse = await client.GetAsync($"{SelectionsPath}/{workspaceId}", ct);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("profileId").GetString().Should().Be("errors",
            "the second POST must overwrite the first selection on the same workspace");
    }

    // -------------------------- Jobs --------------------------

    [Fact]
    public async Task Post_job_returns_201_with_location_header_and_pending_record()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var workspaceId = $"ws-job-create-{Guid.NewGuid():N}";

        using var response = await client.PostAsJsonAsync(JobsPath, new
        {
            workspaceId,
            profileId = "full"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var jobId = body.GetProperty("jobId").GetString();
        jobId.Should().NotBeNullOrWhiteSpace().And.StartWith("gen-",
            "EnqueueJobAsync stamps every id with the 'gen-' prefix");

        body.GetProperty("status").GetString().Should().Be("pending");
        body.GetProperty("workspaceId").GetString().Should().Be(workspaceId);
        body.GetProperty("profileId").GetString().Should().Be("full");
        body.GetProperty("createdAt").GetString().Should().NotBeNullOrWhiteSpace();

        response.Headers.Location.Should().NotBeNull(
            "201 Created responses must surface a Location header pointing at the GET endpoint");
        response.Headers.Location!.OriginalString.Should().Be($"/api/v1/configurator/jobs/{jobId}");

        using var getResponse = await client.GetAsync(response.Headers.Location, ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "the Location header must resolve to a real GET-able job resource");
    }

    [Fact]
    public async Task Post_job_with_unknown_profile_returns_400_via_argument_exception_path()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(JobsPath, new
        {
            workspaceId = $"ws-job-bad-{Guid.NewGuid():N}",
            profileId = "no-such-profile"
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Be("Request failed");
    }

    [Fact]
    public async Task Get_jobs_requires_workspaceId_query_parameter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        // workspaceId is a required minimal-API query binding; omitting it
        // surfaces ASP.NET's own 400 with a parameter-binding diagnostic
        // BEFORE the handler runs, so the body shape differs from the
        // hand-rolled "workspaceId query parameter is required" path. We only
        // assert the status code here — the handler-level message is reached
        // only when the parameter is present-but-empty (covered separately).
        using var response = await client.GetAsync(JobsPath, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_jobs_with_empty_workspaceId_returns_handler_validation_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"{JobsPath}?workspaceId=", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("workspaceId");
    }

    [Fact]
    public async Task Get_jobs_lists_workspace_jobs_newest_first()
    {
        // Exercises the ORDER BY created_at DESC contract end-to-end. The
        // 25 ms inter-create delay is the smallest gap that survives DuckDB's
        // TIMESTAMP_S column resolution (1 second on some platforms — but the
        // generated_jobs.created_at column is TIMESTAMP with microsecond
        // resolution, so 25 ms is plenty of headroom while keeping the test
        // sub-second).
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var workspaceId = $"ws-order-{Guid.NewGuid():N}";

        var createdJobIds = new List<string>();
        foreach (var profileId in (string[])["minimal", "genai", "errors"])
        {
            using var createResponse = await client.PostAsJsonAsync(JobsPath, new
            {
                workspaceId,
                profileId
            }, ct);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            createdJobIds.Add(createdBody.GetProperty("jobId").GetString()!);

            await Task.Delay(TimeSpan.FromMilliseconds(25), ct);
        }

        using var listResponse = await client.GetAsync($"{JobsPath}?workspaceId={workspaceId}", ct);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

        body.GetProperty("total").GetInt32().Should().Be(3);

        var returnedIds = body.GetProperty("items").EnumerateArray()
            .Select(static j => j.GetProperty("jobId").GetString())
            .ToArray();

        var expectedOrder = createdJobIds.AsEnumerable().Reverse().ToArray();
        returnedIds.Should().Equal(expectedOrder,
            "DuckDbStore.GetGenerationJobsByWorkspaceAsync orders by created_at DESC; the most recently enqueued job must appear first");
    }

    [Fact]
    public async Task Cancel_pending_job_marks_it_cancelled_and_second_cancel_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var workspaceId = $"ws-cancel-{Guid.NewGuid():N}";

        using var createResponse = await client.PostAsJsonAsync(JobsPath, new
        {
            workspaceId,
            profileId = "minimal"
        }, ct);
        var jobId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(ct))
            .GetProperty("jobId").GetString();

        using var cancelResponse = await client.PostAsync($"{JobsPath}/{jobId}/cancel", content: null, ct);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelBody = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        cancelBody.GetProperty("status").GetString().Should().Be("cancelled");
        cancelBody.GetProperty("jobId").GetString().Should().Be(jobId);

        using var getResponse = await client.GetAsync($"{JobsPath}/{jobId}", ct);
        var jobAfter = await getResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        jobAfter.GetProperty("status").GetString().Should().Be("cancelled",
            "CancelJobAsync writes the new status via UpdateGenerationJobAsync; GET must observe it");
        jobAfter.GetProperty("completedAt").GetString().Should().NotBeNullOrWhiteSpace(
            "CompletedAt is stamped at cancellation time");

        // CancelJobAsync only transitions pending → cancelled; trying again
        // when status is already "cancelled" goes through the
        // `existing.Status != "pending"` guard and returns false → 404.
        using var secondCancelResponse = await client.PostAsync($"{JobsPath}/{jobId}/cancel", content: null, ct);
        secondCancelResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cancelling a job that is no longer in 'pending' status must surface as 404");
    }

    [Fact]
    public async Task Cancel_unknown_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var unknownJobId = $"gen-{Guid.NewGuid():N}"[..24];

        using var response = await client.PostAsync($"{JobsPath}/{unknownJobId}/cancel", content: null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_job_returns_404_for_unknown_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var unknownJobId = $"gen-{Guid.NewGuid():N}"[..24];

        using var response = await client.GetAsync($"{JobsPath}/{unknownJobId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public sealed class CollectorFactory : WebApplicationFactory<Program>
    {
        // Named in-memory DuckDB: ":memory:<name>" gives each fixture its own
        // catalog. The bare ":memory:" alias (used by HealthUiEndpointTests)
        // points every connection at the same process-global in-memory DB, so
        // when xUnit boots multiple functional test class fixtures their
        // migration runs collide with "Catalog write-write conflict on alter".
        // The unique suffix isolates fixtures without writing to disk. Same
        // pattern as ObserveSubscriptionEndpointsTests and
        // SchemaPromotionEndpointsTests.
        private readonly string _dataPath = $":memory:qyl-prov-{Guid.NewGuid():N}";

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

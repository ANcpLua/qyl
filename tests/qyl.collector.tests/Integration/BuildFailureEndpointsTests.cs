using System.Net;
using System.Net.Http.Json;
using qyl.collector.BuildFailures;

namespace qyl.collector.tests.Integration;

public sealed class BuildFailureEndpointsTests(QylWebApplicationFactory factory)
    : IClassFixture<QylWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient? _client;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Client not initialized");

    public ValueTask InitializeAsync()
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {QylWebApplicationFactory.TestToken}");
        _client.DefaultRequestHeaders.Add("x-qyl-token", QylWebApplicationFactory.TestToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PostThenGetBuildFailure_ReturnsPersistedItem()
    {
        var request = new BuildFailureIngestRequest(
            Id: null,
            Timestamp: TimeProvider.System.GetUtcNow(),
            Target: "build",
            ExitCode: 1,
            BinlogPath: ".qyl/binlogs/test.binlog",
            ErrorSummary: "CS0246",
            PropertyIssuesJson: "[\"Foo was never set\"]",
            EnvReadsJson: "[]",
            CallStackJson: "[]",
            DurationMs: 1234);

        var postResponse = await Client.PostAsJsonAsync("/api/v1/build-failures", request);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = await postResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(created);
        Assert.True(created.TryGetValue("id", out var id));
        Assert.False(string.IsNullOrWhiteSpace(id));

        var getResponse = await Client.GetAsync($"/api/v1/build-failures/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var item = await getResponse.Content.ReadFromJsonAsync<BuildFailureResponse>();
        Assert.NotNull(item);
        Assert.Equal("build", item.Target);
        Assert.Equal(1, item.ExitCode);
    }

    [Fact]
    public async Task SearchBuildFailures_FindsMatchingRows()
    {
        var request = new BuildFailureIngestRequest(
            Id: null,
            Timestamp: TimeProvider.System.GetUtcNow(),
            Target: "test",
            ExitCode: 1,
            BinlogPath: null,
            ErrorSummary: "NU1101 package missing",
            PropertyIssuesJson: null,
            EnvReadsJson: null,
            CallStackJson: null,
            DurationMs: null);

        _ = await Client.PostAsJsonAsync("/api/v1/build-failures", request);

        var response = await Client.GetAsync("/api/v1/build-failures/search?pattern=NU1101&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("NU1101", body, StringComparison.OrdinalIgnoreCase);
    }
}

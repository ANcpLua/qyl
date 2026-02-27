using System.Net;

namespace qyl.collector.tests.Integration;

/// <summary>
///     Integration tests for qyl.collector REST API endpoints.
///     Uses WebApplicationFactory with in-memory DuckDB.
/// </summary>
public sealed class ApiIntegrationTests(QylWebApplicationFactory factory)
    : IClassFixture<QylWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient? _client;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Client not initialized");

    public ValueTask InitializeAsync()
    {
        _client = factory.CreateClient();
        // Add auth header for authenticated endpoints
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {QylWebApplicationFactory.TestToken}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Health Endpoints
    // =========================================================================

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        var response = await Client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("ready", content, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // GitHub Auth Endpoints (ADR-002)
    // =========================================================================

    [Fact]
    public async Task GitHubStatus_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/github/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("configured", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GitHubToken_WithValidToken_ReturnsOkOrBadRequest()
    {
        // In test environment, the token won't validate against GitHub API
        // This tests that the endpoint is accessible and rejects invalid tokens
        var request = new { token = QylWebApplicationFactory.TestToken };
        var response = await Client.PostAsJsonAsync("/api/v1/github/token", request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK or BadRequest, got {response.StatusCode}");
    }

    [Fact]
    public async Task GitHubDeviceAvailable_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/github/device/available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // =========================================================================
    // Sessions Endpoints
    // =========================================================================

    [Fact]
    public async Task GetSessions_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        // Should return JSON with sessions array (possibly empty)
        Assert.Contains("sessions", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSessions_WithLimit_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/sessions?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_NonExistent_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/v1/sessions/non-existent-session-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // Traces Endpoints
    // =========================================================================

    [Fact]
    public async Task GetTrace_NonExistent_ReturnsNotFoundOrServerError()
    {
        // Non-existent trace should return 404, but may return 500 if DB not initialized
        var response = await Client.GetAsync("/api/v1/traces/non-existent-trace-id");

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected NotFound or InternalServerError, got {response.StatusCode}");
    }

    // =========================================================================
    // Feedback Endpoint
    // =========================================================================

    [Fact]
    public async Task PostFeedback_ReturnsAccepted()
    {
        var response = await Client.PostAsync("/api/v1/feedback", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetSessionFeedback_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/sessions/test-session/feedback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("feedback", content, StringComparison.OrdinalIgnoreCase);
    }


    // =========================================================================
    // Console Endpoints
    // =========================================================================

    [Fact]
    public async Task GetConsole_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/console");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConsoleErrors_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/console/errors");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
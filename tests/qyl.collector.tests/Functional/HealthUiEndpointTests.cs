using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Qyl.Collector.Tests.Functional.Health;

public sealed class HealthUiEndpointTests : IClassFixture<HealthUiEndpointTests.CollectorFactory>
{
    private readonly CollectorFactory _factory;

    public HealthUiEndpointTests(CollectorFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHealthUi_returns_200_with_expected_envelope()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ui", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        payload.GetProperty("status").GetString()
            .Should().BeOneOf("healthy", "degraded", "unhealthy");

        payload.GetProperty("uptimeSeconds").GetInt64()
            .Should().BeGreaterThanOrEqualTo(0);

        payload.GetProperty("version").GetString()
            .Should().NotBeNullOrWhiteSpace();

        payload.GetProperty("checkedAt").GetString()
            .Should().NotBeNullOrWhiteSpace();

        var components = payload.GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToArray();

        components.Should().Contain(["duckdb", "disk", "memory", "ingestion"]);
    }

    [Fact]
    public async Task GetHealthUi_does_not_require_auth_token()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        // No QYL_TOKEN header — endpoint is in the auth allowlist.
        var response = await client.GetAsync("/health/ui", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public sealed class CollectorFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration(static (_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // ":memory:" DuckDB keeps the test fully in-process. Real file-backed
                    // DuckDB behaviour (recovery, concurrency, persistence) belongs in
                    // qyl-integration-tests, not here.
                    ["QYL_DATA_PATH"] = ":memory:",
                    ["QYL_OTLP_AUTH_MODE"] = "Unsecured",
                });
            });
        }
    }
}

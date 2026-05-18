using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qyl.Collector.Auth;

namespace Qyl.Collector.Tests.Functional.Health;

[Collection(FunctionalCollection.Name)]
public sealed class HealthUiEndpointTests : IClassFixture<HealthUiEndpointTests.CollectorFactory>
{
    private const string ConfiguredToken = "test-secret-token";

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
    public async Task HealthUi_is_accessible_without_token_even_when_one_is_configured()
    {
        // QYL_TOKEN is set on the factory so TokenAuthMiddleware is meaningfully
        // configured. /health/ui is in TokenAuthOptions.ExcludedPaths and must
        // still serve without any Authorization header / cookie / query token.
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ui", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenAuthMiddleware_redirects_when_valid_query_token_set_on_non_allowlisted_path()
    {
        // This test exists to PROVE TokenAuthMiddleware is wired into the
        // pipeline — without it, the auth-allowlist guarantee from the previous
        // test is hollow (both tests would pass even if auth were disabled).
        //
        // Mechanic: the middleware (services/qyl.collector/Auth/TokenAuth.cs)
        // detects a valid ?t=<token> query parameter on any non-allowlisted
        // path, sets an auth cookie, and returns a 302 redirect to the clean
        // URL (token stripped). That redirect is a side effect ONLY produced
        // by the middleware; if it were absent from the pipeline, the request
        // would fall through to routing and return the endpoint's own
        // response (likely 404 or 200, but never 302 with a Location header
        // missing the token).
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // "/protected-route-for-auth-test" is deliberately a path that is
        // (a) not in TokenAuthOptions.ExcludedPaths and (b) not a registered
        // endpoint — both criteria keep the test independent of unrelated
        // route additions/removals.
        var response = await client.GetAsync(
            $"/protected-route-for-auth-test?t={ConfiguredToken}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var location = response.Headers.Location;
        location.Should().NotBeNull();
        location!.OriginalString.Should().NotContain("t=" + ConfiguredToken,
            because: "the middleware strips the token from the redirect target");
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
                    // ":memory:" DuckDB keeps the test fully in-process. Real
                    // file-backed DuckDB behaviour (recovery, concurrency,
                    // persistence) belongs in qyl-integration-tests, not here.
                    ["QYL_DATA_PATH"] = ":memory:",
                    ["QYL_OTLP_AUTH_MODE"] = "Unsecured",
                });
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            // Lock TokenAuthOptions.Token to a known value AFTER the production
            // wiring has run. CollectorAuthExtensions reads QYL_TOKEN via
            // config["QYL_TOKEN"] and falls back to TokenGenerator.Generate()
            // when unset — mutating the resolved singleton sidesteps any
            // config-source priority surprises and lets the redirect test
            // construct a query token deterministically. TokenAuthOptions is
            // a plain singleton with a settable Token property, so the
            // mutation is safe.
            host.Services.GetRequiredService<TokenAuthOptions>().Token = ConfiguredToken;

            return host;
        }
    }
}

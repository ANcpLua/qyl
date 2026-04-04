using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Qyl.Collector.Health;
using Qyl.Models;
using Xunit;

namespace Qyl.Collector.Tests.Health;

/// <summary>
///     Integration tests for health endpoints: /alive, /health, /ready, /health/ui.
///     All tests skipped: source-generated OTel interceptors call UseOtlpExporter before
///     WebApplicationFactory.ConfigureWebHost can disable it, causing double-registration.
/// </summary>
public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SkipReason =
        "OTel source-generated interceptor conflicts with WebApplicationFactory";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(Skip = SkipReason)]
    public async Task Alive_Returns200_WhenHealthy()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<HealthResponse>(response);
        body.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact(Skip = SkipReason)]
    public async Task Health_Returns200_WhenAllDepsHealthy()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<HealthResponse>(response);
        body.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
        body.Version.Should().NotBe(default);
        body.UptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact(Skip = SkipReason)]
    public async Task Ready_Returns200_WhenAllDepsHealthy()
    {
        var response = await _client.GetAsync("/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<HealthResponse>(response);
        body.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
    }

    [Fact(Skip = SkipReason)]
    public async Task HealthUi_ReturnsAllComponents()
    {
        var response = await _client.GetAsync("/health/ui", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await DeserializeAsync<HealthUiResponse>(response);
        body.Components.Count.Should().BeGreaterThanOrEqualTo(3);

        var names = body.Components.Select(static c => c.Name).ToList();
        names.Should().Contain("duckdb");
        names.Should().Contain("disk");
        names.Should().Contain("memory");
        body.CheckedAt.Should().NotBeEmpty();
    }

    [Fact(Skip = SkipReason)]
    public async Task ResponseHeaders_NoCache()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        response.Headers.TryGetValues("X-Content-Type-Options", out var xcto).Should().BeTrue();
        xcto!.Single().Should().Be("nosniff");

        response.Content.Headers.ContentType.Should().NotBeNull();
        var cacheControl = response.Headers.CacheControl;
        cacheControl.Should().NotBeNull();
        cacheControl!.NoStore.Should().BeTrue();
    }

    [Fact(Skip = SkipReason)]
    public async Task AllProbeEndpoints_HaveNoStoreHeader()
    {
        string[] endpoints = ["/alive", "/health", "/ready", "/health/ui"];

        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint, TestContext.Current.CancellationToken);
            var cacheControl = response.Headers.CacheControl;
            cacheControl.Should().NotBeNull();
            cacheControl!.NoStore.Should().BeTrue($"{endpoint} missing Cache-Control: no-store");
        }
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter<HealthStatus>(JsonNamingPolicy.CamelCase) }
            })!;
    }
}

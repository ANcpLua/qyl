using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using qyl.collector.Health;
using Qyl.Models;
using Xunit;

namespace qyl.collector.tests.Health;

/// <summary>
///     Integration tests for health endpoints: /alive, /health, /ready, /health/ui.
/// </summary>
public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Alive_Returns200_WhenHealthy()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<HealthResponse>(response);
        Assert.Equal(HealthStatus.Healthy, body.Status);
    }

    [Fact]
    public async Task Health_Returns200_WhenAllDepsHealthy()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<HealthResponse>(response);
        Assert.True(
            body.Status is HealthStatus.Healthy or HealthStatus.Degraded,
            $"Expected Healthy or Degraded, got {body.Status}");
        Assert.NotEqual(default, body.Version);
        Assert.True(body.UptimeSeconds >= 0);
    }

    [Fact]
    public async Task Ready_Returns200_WhenAllDepsHealthy()
    {
        var response = await _client.GetAsync("/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<HealthResponse>(response);
        Assert.True(
            body.Status is HealthStatus.Healthy or HealthStatus.Degraded,
            $"Expected Healthy or Degraded, got {body.Status}");
    }

    [Fact]
    public async Task HealthUi_ReturnsAllComponents()
    {
        var response = await _client.GetAsync("/health/ui", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<HealthUiResponse>(response);
        Assert.True(body.Components.Count >= 3,
            $"Expected >= 3 components, got {body.Components.Count}");

        var names = body.Components.Select(static c => c.Name).ToList();
        Assert.Contains("duckdb", names);
        Assert.Contains("disk", names);
        Assert.Contains("memory", names);
        Assert.NotEmpty(body.CheckedAt);
    }

    [Fact]
    public async Task ResponseHeaders_NoCache()
    {
        var response = await _client.GetAsync("/alive", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var xcto));
        Assert.Equal("nosniff", xcto!.Single());

        Assert.NotNull(response.Content.Headers.ContentType);
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.NoStore);
    }

    [Fact]
    public async Task AllProbeEndpoints_HaveNoStoreHeader()
    {
        string[] endpoints = ["/alive", "/health", "/ready", "/health/ui"];

        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint, TestContext.Current.CancellationToken);
            var cacheControl = response.Headers.CacheControl;
            Assert.NotNull(cacheControl);
            Assert.True(cacheControl!.NoStore, $"{endpoint} missing Cache-Control: no-store");
        }
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter<HealthStatus>(JsonNamingPolicy.CamelCase) }
        })!;
    }
}

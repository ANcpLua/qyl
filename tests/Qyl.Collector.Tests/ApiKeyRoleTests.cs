using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;

namespace Qyl.Collector.Tests;

/// <summary>QYL-A2: collector key roles, parsed from configuration and enforced per route.</summary>
public sealed class ApiKeyRoleTests
{
    [Fact]
    public void Every_key_declares_its_roles_explicitly()
    {
        var options = Parse(CollectorApiTestHost.ProjectKeys);

        Assert.Equal(ApiKeyRoles.Ingest | ApiKeyRoles.Read, Resolve(options, "ingest-reader").Roles);
        Assert.Equal(ApiKeyRoles.Read, Resolve(options, "reader").Roles);
        Assert.Equal(ApiKeyRoles.Read | ApiKeyRoles.Write, Resolve(options, "writer").Roles);
        Assert.Equal(ApiKeyRoles.Delete, Resolve(options, "deleter").Roles);
        Assert.Equal(ApiKeyRoles.Ingest, Resolve(options, "ingester").Roles);
        Assert.Equal("beta", Resolve(options, "beta-all").ProjectId);
        Assert.Equal(
            ApiKeyRoles.Ingest | ApiKeyRoles.Read | ApiKeyRoles.Write | ApiKeyRoles.Delete,
            Resolve(options, "beta-all").Roles);
    }

    [Fact]
    public void Accepted_roles_match_the_shared_contract()
    {
        var contractRoles = Enum.GetNames<Qyl.Api.Contracts.Common.CollectorKeyRole>()
            .Select(static name => name.ToLowerInvariant())
            .Order()
            .ToArray();
        var collectorRoles = new[]
        {
            ApiKeyRoleNames.Ingest, ApiKeyRoleNames.Read, ApiKeyRoleNames.Write, ApiKeyRoleNames.Delete
        }.Order().ToArray();

        Assert.Equal(contractRoles, collectorRoles);
        Assert.All(contractRoles, name => Assert.True(ApiKeyRoleNames.TryParse(name, out _)));
    }

    [Theory]
    [InlineData("""{"alpha":[{"key":"k","roles":[]}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["admin"]}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["read","read"]}]}""")]
    [InlineData("""{"alpha":[{"key":"k"}]}""")]
    [InlineData("""{"alpha":[{"key":"","roles":["read"]}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["read"],"scope":"all"}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["read"]},"k"]}""")]
    [InlineData("""{"alpha":[42]}""")]
    [InlineData("""{"alpha":["k"]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":null}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":"read"}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["Read"]}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":[1]}]}""")]
    [InlineData("""{"alpha":[{"key":"first","key":"second","roles":["read"]}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["read"],"roles":["write"]}]}""")]
    [InlineData("""{"alpha":[{"key":"k","roles":["read"]}],"beta":[{"key":"k","roles":["write"]}]}""")]
    public void Malformed_role_credentials_fail_start_up(string keys) =>
        Assert.Throws<InvalidOperationException>(() => Parse(keys));

    [Theory]
    // A combined ingest/read credential cannot write or delete.
    [InlineData("GET", "/api/v1/traces", "ingest-reader", HttpStatusCode.OK)]
    [InlineData("POST", "/api/v1/annotations", "ingest-reader", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/api/v1/annotations/missing", "ingest-reader", HttpStatusCode.Forbidden)]
    // Roles are independent: an ingest key reads nothing, a delete key creates nothing.
    [InlineData("GET", "/api/v1/traces", "ingester", HttpStatusCode.Forbidden)]
    [InlineData("GET", "/api/v1/saved-queries", "deleter", HttpStatusCode.Forbidden)]
    [InlineData("POST", "/api/v1/saved-queries", "deleter", HttpStatusCode.Forbidden)]
    [InlineData("PATCH", "/api/v1/saved-queries/missing", "reader", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/api/v1/saved-queries/missing", "writer", HttpStatusCode.Forbidden)]
    // Running a saved query is a read even though it is a POST.
    [InlineData("POST", "/api/v1/saved-queries/missing:run", "reader", HttpStatusCode.NotFound)]
    [InlineData("POST", "/api/v1/saved-queries/missing:run", "ingester", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/api/v1/saved-queries/missing", "deleter", HttpStatusCode.PreconditionRequired)]
    [InlineData("GET", "/api/v1/traces", "unknown", HttpStatusCode.Unauthorized)]
    public async Task Each_route_requires_its_declared_role(
        string method,
        string path,
        string key,
        HttpStatusCode expected)
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);

        using var response = await host.SendAsync(new HttpMethod(method), path, key,
            method is "POST" or "PATCH" ? "{}" : null);

        Assert.Equal(expected, response.StatusCode);
        if (expected is HttpStatusCode.Forbidden)
        {
            var problem = await CollectorApiTestHost.JsonAsync(response);
            Assert.Equal(403, problem.GetProperty("status").GetInt32());
            Assert.Equal(
                method switch
                {
                    "GET" => "read",
                    "DELETE" => "delete",
                    "POST" when path.EndsWith(":run", StringComparison.Ordinal) => "read",
                    _ => "write"
                },
                problem.GetProperty("required_permission").GetString());
        }
    }

    [Fact]
    public async Task Otlp_ingest_requires_the_ingest_role()
    {
        await using var host = await CollectorApiTestHost.StartAsync(TestContext.Current.CancellationToken);

        using var refused = await host.SendAsync(HttpMethod.Post, "/v1/traces", "reader", "{}");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var accepted = await host.SendAsync(HttpMethod.Post, "/v1/traces", "ingester", "{}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public void Every_product_route_declares_the_role_the_contract_names()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder();
        builder.Services.AddQylCollectorCore(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton<Qyl.Collector.Storage.IQylStore>(_ => throw new NotSupportedException());
        using var app = builder.Build();
        app.MapQylCollectorEndpoints();

        var routes = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(static endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(routes);
        Assert.All(routes, endpoint =>
        {
            var role = endpoint.Metadata.GetMetadata<ApiKeyRoleRequirement>();
            Assert.True(role is not null, $"{endpoint.DisplayName} declares no key role.");
            var method = Assert.Single(endpoint.Metadata
                .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods);
            var expected = method switch
            {
                "GET" => ApiKeyRoles.Read,
                "DELETE" => ApiKeyRoles.Delete,
                "POST" when endpoint.RoutePattern.RawText!.EndsWith(":run", StringComparison.Ordinal) || endpoint.RoutePattern.RawText.StartsWith("/api/v1/analysis/", StringComparison.Ordinal) => ApiKeyRoles.Read,
                _ => ApiKeyRoles.Write
            };
            Assert.Equal(expected, role!.Role);
        });
    }

    private static OtlpApiKeyOptions Parse(string keys)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QYL_OTLP_AUTH_MODE"] = "ApiKey",
                ["QYL_OTLP_PROJECT_KEYS"] = keys
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddQylCollectorAuth(config, new ProductionEnvironment())
            .BuildServiceProvider();
        return provider.GetRequiredService<OtlpApiKeyOptions>();
    }

    private static ProjectApiKey Resolve(OtlpApiKeyOptions options, string key) =>
        Assert.IsType<ProjectApiKey>(OtlpApiKeyValidator.Resolve(key, options));

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "qyl-tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;

namespace Qyl.Collector.Tests;

/// <summary>QYL-A2: collector key roles, parsed from configuration and enforced per route.</summary>
[TestClass]
public sealed class ApiKeyRoleTests(TestContext testContext)
{
    [TestMethod]
    public void Every_key_declares_its_roles_explicitly()
    {
        var options = Parse(CollectorApiTestHost.ProjectKeys);

        Assert.AreEqual(ApiKeyRoles.Ingest | ApiKeyRoles.Read, Resolve(options, "ingest-reader").Roles);
        Assert.AreEqual(ApiKeyRoles.Read, Resolve(options, "reader").Roles);
        Assert.AreEqual(ApiKeyRoles.Read | ApiKeyRoles.Write, Resolve(options, "writer").Roles);
        Assert.AreEqual(ApiKeyRoles.Delete, Resolve(options, "deleter").Roles);
        Assert.AreEqual(ApiKeyRoles.Ingest, Resolve(options, "ingester").Roles);
        Assert.AreEqual("beta", Resolve(options, "beta-all").ProjectId);
        Assert.AreEqual(
            ApiKeyRoles.Ingest | ApiKeyRoles.Read | ApiKeyRoles.Write | ApiKeyRoles.Delete,
            Resolve(options, "beta-all").Roles);
    }

    [TestMethod]
    public void Accepted_roles_match_the_shared_contract()
    {
        var contractRoles = Enum.GetNames<Api.Contracts.Common.CollectorKeyRole>()
            .Select(static name => name.ToLowerInvariant())
            .Order()
            .ToArray();
        var collectorRoles = new[]
        {
            ApiKeyRoleNames.Ingest, ApiKeyRoleNames.Read, ApiKeyRoleNames.Write, ApiKeyRoleNames.Delete
        }.Order().ToArray();

        Assert.AreSequenceEqual(contractRoles, collectorRoles);
        contractRoles.ToList().ForEach(name => Assert.IsTrue(ApiKeyRoleNames.TryParse(name, out _)));
    }

    [TestMethod]
    [DataRow("""{"alpha":[{"key":"k","roles":[]}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["admin"]}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["read","read"]}]}""")]
    [DataRow("""{"alpha":[{"key":"k"}]}""")]
    [DataRow("""{"alpha":[{"key":"","roles":["read"]}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["read"],"scope":"all"}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["read"]},"k"]}""")]
    [DataRow("""{"alpha":[42]}""")]
    [DataRow("""{"alpha":["k"]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":null}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":"read"}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["Read"]}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":[1]}]}""")]
    [DataRow("""{"alpha":[{"key":"first","key":"second","roles":["read"]}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["read"],"roles":["write"]}]}""")]
    [DataRow("""{"alpha":[{"key":"k","roles":["read"]}],"beta":[{"key":"k","roles":["write"]}]}""")]
    public void Malformed_role_credentials_fail_start_up(string keys) =>
        Assert.ThrowsExactly<OptionsValidationException>(() => Parse(keys));

    [TestMethod]
    // A combined ingest/read credential cannot write or delete.
    [DataRow("GET", "/api/v1/traces", "ingest-reader", HttpStatusCode.OK)]
    [DataRow("POST", "/api/v1/annotations", "ingest-reader", HttpStatusCode.Forbidden)]
    [DataRow("DELETE", "/api/v1/annotations/missing", "ingest-reader", HttpStatusCode.Forbidden)]
    // Roles are independent: an ingest key reads nothing, a delete key creates nothing.
    [DataRow("GET", "/api/v1/traces", "ingester", HttpStatusCode.Forbidden)]
    [DataRow("GET", "/api/v1/saved-queries", "deleter", HttpStatusCode.Forbidden)]
    [DataRow("POST", "/api/v1/saved-queries", "deleter", HttpStatusCode.Forbidden)]
    [DataRow("PATCH", "/api/v1/saved-queries/missing", "reader", HttpStatusCode.Forbidden)]
    [DataRow("DELETE", "/api/v1/saved-queries/missing", "writer", HttpStatusCode.Forbidden)]
    // Running a saved query is a read even though it is a POST.
    [DataRow("POST", "/api/v1/saved-queries/missing:run", "reader", HttpStatusCode.NotFound)]
    [DataRow("POST", "/api/v1/saved-queries/missing:run", "ingester", HttpStatusCode.Forbidden)]
    // Analysis reads use the shared handler; failure mutations retain their explicit write route.
    [DataRow("POST", "/api/v1/analysis/triage", "reader", HttpStatusCode.OK)]
    [DataRow("POST", "/api/v1/analysis/triage", "ingester", HttpStatusCode.Forbidden)]
    [DataRow("PATCH", "/api/v1/failures/missing", "reader", HttpStatusCode.Forbidden)]
    [DataRow("PATCH", "/api/v1/failures/missing", "writer", HttpStatusCode.PreconditionRequired)]
    [DataRow("POST", "/api/v1/analysis/update_failure_status", "writer", HttpStatusCode.NotFound)]
    [DataRow("DELETE", "/api/v1/saved-queries/missing", "deleter", HttpStatusCode.PreconditionRequired)]
    [DataRow("GET", "/api/v1/traces", "unknown", HttpStatusCode.Unauthorized)]
    public async Task Each_route_requires_its_declared_role(
        string method,
        string path,
        string key,
        HttpStatusCode expected)
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);

        using var response = await host.SendAsync(new HttpMethod(method), path, key,
            method is "POST" or "PATCH" ? "{}" : null);

        Assert.AreEqual(expected, response.StatusCode);
        if (expected is HttpStatusCode.Forbidden)
        {
            var problem = await host.JsonAsync(response);
            Assert.AreEqual(403, problem.GetProperty("status").GetInt32());
            Assert.AreEqual(
                method switch
                {
                    "GET" => "read",
                    "DELETE" => "delete",
                    "POST" when path.EndsWith(":run", StringComparison.Ordinal)
                                || path.StartsWith("/api/v1/analysis/", StringComparison.Ordinal) => "read",
                    _ => "write"
                },
                problem.GetProperty("required_permission").GetString());
        }
    }

    [TestMethod]
    public async Task Otlp_ingest_requires_the_ingest_role()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);

        using var refused = await host.SendAsync(HttpMethod.Post, "/v1/traces", "reader", "{}");
        Assert.AreEqual(HttpStatusCode.Forbidden, refused.StatusCode);

        using var accepted = await host.SendAsync(HttpMethod.Post, "/v1/traces", "ingester", "{}");
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
    }

    [TestMethod]
    public void Every_product_route_declares_the_role_the_contract_names()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder();
        builder.Services.AddQylCollectorCore(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton<Storage.IQylStore>(_ => throw new NotSupportedException());
        using var app = builder.Build();
        app.MapQylCollectorEndpoints();

        var routes = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(static endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.IsNotEmpty(routes);
        routes.ToList().ForEach(endpoint =>
        {
            var role = endpoint.Metadata.GetMetadata<ApiKeyRoleRequirement>();
            Assert.IsNotNull(role, $"{endpoint.DisplayName} declares no key role.");
            var method = Assert.ContainsSingle(endpoint.Metadata
                .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods);
            var path = endpoint.RoutePattern.RawText;
            Assert.IsNotNull(path);
            var expected = method switch
            {
                "GET" => ApiKeyRoles.Read,
                "DELETE" => ApiKeyRoles.Delete,
                "POST" when path.EndsWith(":run", StringComparison.Ordinal) || path.StartsWith("/api/v1/analysis/", StringComparison.Ordinal) => ApiKeyRoles.Read,
                _ => ApiKeyRoles.Write
            };
            Assert.AreEqual(expected, role.Role);
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
        return provider.GetRequiredService<IOptions<OtlpApiKeyOptions>>().Value;
    }

    private static ProjectApiKey Resolve(OtlpApiKeyOptions options, string key) =>
        Assert.IsExactInstanceOfType<ProjectApiKey>(OtlpApiKeyValidator.Resolve(key, options));

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "qyl-tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

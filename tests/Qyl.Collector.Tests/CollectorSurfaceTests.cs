using System.Net;
using System.Net.Http.Headers;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Ingestion;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Tests;

// The Collector's HTTP answers outside the analysis routes: unmapped paths, a removed operation,
// malformed queries and an unsupported OTLP encoding.
[TestClass]
public sealed class CollectorSurfaceTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("/")]
    [DataRow("/traces")]
    [DataRow("/assets/missing.js")]
    [DataRow("/v1")]
    [DataRow("/v1development/nope")]
    public async Task Unmapped_non_api_paths_are_empty_not_found_responses(string path)
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var response = await host.SendAsync(HttpMethod.Get, path, "reader");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.IsNull(response.Content.Headers.ContentType);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync(testContext.CancellationToken));
    }

    [TestMethod]
    public async Task Removed_api_operation_preserves_the_generated_not_found_contract()
    {
        const string Path = "/api/v1/search/query";
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var response = await host.SendAsync(HttpMethod.Post, Path, "writer", "{}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
        var problem = await host.JsonAsync(response);
        Assert.AreEqual(404, problem.GetProperty("status").GetInt32());
        Assert.AreEqual(Path, problem.GetProperty("resource_id").GetString());
    }

    [TestMethod]
    [DataRow("/api/v1/sessions?is_active=perhaps", "is_active", "query.invalid_boolean")]
    [DataRow("/api/v1/traces?limit=many", "limit", "query.invalid_integer")]
    [DataRow("/api/v1/logs?start_time=yesterday", "start_time", "query.invalid_date_time")]
    public async Task Malformed_query_keeps_its_validation_contract(string path, string field, string code)
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var response = await host.SendAsync(HttpMethod.Get, path, "reader");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, response.Content.Headers.ContentType?.MediaType);
        var problem = await host.JsonAsync(response);
        var error = Assert.ContainsSingle(problem.GetProperty("errors").EnumerateArray());
        Assert.AreEqual(field, error.GetProperty("field").GetString());
        Assert.AreEqual(code, error.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task Unsupported_otlp_encoding_preserves_the_protobuf_error_envelope()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/traces");
        request.Headers.Add(OtlpConstants.ApiKeyHeaderName, "ingester");
        request.Content = new ByteArrayContent([]);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(OtlpPayloadParser.ProtobufContentType);
        request.Content.Headers.ContentEncoding.Add("br");
        using var response = await host.Client.SendAsync(request, testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.ProtobufContentType, response.Content.Headers.ContentType?.MediaType);
        var status = RpcStatus.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(testContext.CancellationToken));
        Assert.AreEqual(
            "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.",
            status.Message);
    }
}

using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ContractErrorResultsTests(TestContext testContext)
{
    [TestMethod]
    public async Task Analysis_failures_preserve_the_generated_problem_contract()
    {
        foreach (var (result, status, code) in new[]
        {
            (ContractErrorResults.InternalServerError("analysis.response_invalid"), 500, ProblemCode.InternalError),
            (ContractErrorResults.AnalysisDeadlineExceeded(), 504, ProblemCode.ServiceUnavailable)
        })
        {
            var context = await ExecuteAsync(result);
            Assert.AreEqual(status, context.Response.StatusCode);
            Assert.AreEqual(ProblemDetailsMediaType.Value, context.Response.ContentType);
            var body = await JsonSerializer.DeserializeAsync(context.Response.Body,
                QylSerializerContext.Default.ProblemDetails, testContext.CancellationToken);
            Assert.IsNotNull(body);
            Assert.AreEqual(status, body.Status);
            Assert.AreEqual(code, body.Code);
        }
    }

    [TestMethod]
    public async Task Endpoint_problem_results_use_the_generated_media_type_and_source_generated_json()
    {
        var validation = await ExecuteAsync(ContractErrorResults.Validation(
            "limit",
            "Limit is invalid.",
            "limit.invalid",
            "0"));

        Assert.AreEqual(StatusCodes.Status400BadRequest, validation.Response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, validation.Response.ContentType);
        var validationBody = await JsonSerializer.DeserializeAsync(
            validation.Response.Body,
            QylSerializerContext.Default.ValidationError,
            testContext.CancellationToken);
        Assert.AreEqual("limit.invalid", Assert.ContainsSingle(Assert.IsExactInstanceOfType<ValidationError>(validationBody).Errors).Code);

        var notFound = await ExecuteAsync(ContractErrorResults.NotFound("trace", "trace-42"));

        Assert.AreEqual(StatusCodes.Status404NotFound, notFound.Response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, notFound.Response.ContentType);
        var notFoundBody = await JsonSerializer.DeserializeAsync(
            notFound.Response.Body,
            QylSerializerContext.Default.NotFoundError,
            testContext.CancellationToken);
        Assert.AreEqual("trace-42", Assert.IsExactInstanceOfType<NotFoundError>(notFoundBody).ResourceId);
    }

    [TestMethod]
    public async Task Api_key_rejection_preserves_the_generated_media_type_status_and_challenge_header()
    {
        var nextInvoked = false;
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("tenant-a", "programmatic-test-key", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
        };
        var middleware = new CollectorApiKeyMiddleware(
            _ =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            options);
        var context = CreateContext();
        context.Request.Path = "/api/v1/logs";

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextInvoked);
        Assert.AreEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, context.Response.ContentType);
        Assert.AreEqual(
            "x-otlp-api-key realm=\"qyl-otlp\"",
            context.Response.Headers.WWWAuthenticate.ToString());
        Assert.AreEqual("private, no-store", context.Response.Headers.CacheControl.ToString());
        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.UnauthorizedError,
            testContext.CancellationToken);
        Assert.AreEqual(StatusCodes.Status401Unauthorized, Assert.IsExactInstanceOfType<UnauthorizedError>(error).Status);
    }

    [TestMethod]
    [DataRow(OtlpPayloadParser.ProtobufContentType)]
    [DataRow(OtlpPayloadParser.JsonContentType)]
    public async Task Otlp_api_key_rejection_preserves_the_official_request_wire_envelope(
        string contentType)
    {
        var middleware = new CollectorApiKeyMiddleware(
            _ => throw new InvalidOperationException("Rejected OTLP request reached the endpoint."),
            new OtlpApiKeyOptions { AuthMode = "ApiKey", Keys = [new ProjectApiKey("tenant-a", "real-key", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))] });
        var context = CreateContext();
        context.Request.Path = "/v1/traces";
        context.Request.ContentType = contentType;

        await middleware.InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        RpcStatus status;
        if (contentType == OtlpPayloadParser.ProtobufContentType)
        {
            status = RpcStatus.Parser.ParseFrom(context.Response.Body);
        }
        else
        {
            using var reader = new StreamReader(context.Response.Body);
            status = JsonParser.Default.Parse<RpcStatus>(
                await reader.ReadToEndAsync(testContext.CancellationToken));
        }

        Assert.AreEqual("Missing or invalid API key.", status.Message);
    }

    [TestMethod]
    public async Task Product_api_cache_is_private_and_whitespace_keys_are_never_credentials()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("tenant-a", "real-key", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
        };
        Assert.IsNull(OtlpApiKeyValidator.Resolve("   ", options)?.ProjectId);
        Assert.AreEqual("tenant-a", OtlpApiKeyValidator.Resolve("real-key", options)?.ProjectId);

        var nextInvoked = false;
        var middleware = new CollectorApiKeyMiddleware(
            context =>
            {
                nextInvoked = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            options);
        var context = CreateContext();
        context.Request.Path = "/api/v1/logs";
        context.Request.Headers[OtlpConstants.ApiKeyHeaderName] = "real-key";

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextInvoked);
        Assert.AreEqual("private, no-store", context.Response.Headers.CacheControl.ToString());
        Assert.AreEqual("tenant-a", AuthenticatedProject.ForHttpRead(context));
    }

    [TestMethod]
    public async Task Credentialed_specific_origin_cors_responses_vary_by_origin()
    {
        var middleware = new OtlpCorsMiddleware(
            _ => Task.CompletedTask,
            new OtlpCorsOptions { AllowedOrigins = "https://console.example" });
        var context = CreateContext();
        context.Request.Path = "/v1/traces";
        context.Request.Headers.Origin = "https://console.example";

        await middleware.InvokeAsync(context);

        Assert.AreEqual("https://console.example", context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.AreEqual(
            "content-type, x-otlp-api-key",
            context.Response.Headers.AccessControlAllowHeaders.ToString());
        Assert.Contains("Origin", context.Response.Headers.Vary.ToString(), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Startup_banner_does_not_claim_a_dashboard_for_api_key_deployments()
    {
        Assert.AreEqual(
            "product API",
            StartupBanner.HttpSurfaceLabel(new OtlpApiKeyOptions { AuthMode = "ApiKey" }));
        Assert.AreEqual(
            "dashboard + API",
            StartupBanner.HttpSurfaceLabel(new OtlpApiKeyOptions { AuthMode = "Unsecured" }));
    }

    [TestMethod]
    public async Task Exception_handler_preserves_the_generated_media_type_status_and_trace_header()
    {
        var context = CreateContext();
        context.TraceIdentifier = "trace-from-server";
        context.Request.Method = HttpMethods.Get;

        await CollectorMiddlewareExtensions.WriteUnhandledExceptionAsync(context);

        Assert.AreEqual(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.AreEqual(ProblemDetailsMediaType.Value, context.Response.ContentType);
        Assert.AreEqual("trace-from-server", context.Response.Headers["X-Trace-Id"].ToString());
        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ContractInternalServerError,
            testContext.CancellationToken);
        var problem = Assert.IsExactInstanceOfType<InternalServerError>(error);
        Assert.AreEqual(ProblemType.InternalError, problem.Type);
        Assert.AreEqual(ProblemCode.InternalError, problem.Code);
        Assert.AreEqual("collector.unhandled_exception", problem.Detail);
    }

    private static async Task<DefaultHttpContext> ExecuteAsync(IResult result)
    {
        var context = CreateContext();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return context;
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider(),
            Request =
            {
                Method = HttpMethods.Get
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }
}

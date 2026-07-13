using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Tests;

public sealed class ContractErrorResultsTests
{
    [Fact]
    public async Task Endpoint_problem_results_use_the_generated_media_type_and_source_generated_json()
    {
        var validation = await ExecuteAsync(ContractErrorResults.Validation(
            "limit",
            "Limit is invalid.",
            "limit.invalid",
            "0"));

        Assert.Equal(StatusCodes.Status400BadRequest, validation.Response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, validation.Response.ContentType);
        var validationBody = await JsonSerializer.DeserializeAsync(
            validation.Response.Body,
            QylSerializerContext.Default.ValidationError,
            TestContext.Current.CancellationToken);
        Assert.Equal("limit.invalid", Assert.Single(Assert.IsType<ValidationError>(validationBody).Errors).Code);

        var notFound = await ExecuteAsync(ContractErrorResults.NotFound("trace", "trace-42"));

        Assert.Equal(StatusCodes.Status404NotFound, notFound.Response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, notFound.Response.ContentType);
        var notFoundBody = await JsonSerializer.DeserializeAsync(
            notFound.Response.Body,
            QylSerializerContext.Default.NotFoundError,
            TestContext.Current.CancellationToken);
        Assert.Equal("trace-42", Assert.IsType<NotFoundError>(notFoundBody).ResourceId);
    }

    [Fact]
    public async Task Api_key_rejection_preserves_the_generated_media_type_status_and_challenge_header()
    {
        var nextInvoked = false;
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            PrimaryApiKey = "programmatic-test-key"
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

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, context.Response.ContentType);
        Assert.Equal(
            "x-otlp-api-key realm=\"qyl-otlp\"",
            context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.UnauthorizedError,
            TestContext.Current.CancellationToken);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<UnauthorizedError>(error).Status);
    }

    [Theory]
    [InlineData(OtlpPayloadParser.ProtobufContentType)]
    [InlineData(OtlpPayloadParser.JsonContentType)]
    public async Task Otlp_api_key_rejection_preserves_the_official_request_wire_envelope(
        string contentType)
    {
        var middleware = new CollectorApiKeyMiddleware(
            _ => throw new InvalidOperationException("Rejected OTLP request reached the endpoint."),
            new OtlpApiKeyOptions { AuthMode = "ApiKey", PrimaryApiKey = "real-key" });
        var context = CreateContext();
        context.Request.Path = "/v1/traces";
        context.Request.ContentType = contentType;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
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
                await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal("Missing or invalid API key.", status.Message);
    }

    [Fact]
    public async Task Product_api_cache_is_private_and_whitespace_secondary_keys_are_never_credentials()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            PrimaryApiKey = "real-key",
            SecondaryApiKey = "   "
        };
        Assert.False(OtlpApiKeyValidator.IsValid("   ", options));
        Assert.True(OtlpApiKeyValidator.IsValid("real-key", options));

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
        context.Request.Headers[options.HeaderName] = options.PrimaryApiKey;

        await middleware.InvokeAsync(context);

        Assert.True(nextInvoked);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Credentialed_specific_origin_cors_responses_vary_by_origin()
    {
        var middleware = new OtlpCorsMiddleware(
            _ => Task.CompletedTask,
            new OtlpCorsOptions { AllowedOrigins = "https://console.example" });
        var context = CreateContext();
        context.Request.Path = "/v1/traces";
        context.Request.Headers.Origin = "https://console.example";

        await middleware.InvokeAsync(context);

        Assert.Equal("https://console.example", context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.Contains("Origin", context.Response.Headers.Vary.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_banner_does_not_claim_a_dashboard_for_api_key_deployments()
    {
        Assert.Equal(
            "product API",
            StartupBanner.HttpSurfaceLabel(new OtlpApiKeyOptions { AuthMode = "ApiKey" }));
        Assert.Equal(
            "dashboard + API",
            StartupBanner.HttpSurfaceLabel(new OtlpApiKeyOptions { AuthMode = "Unsecured" }));
    }

    [Fact]
    public async Task Exception_handler_preserves_the_generated_media_type_status_and_trace_header()
    {
        var context = CreateContext();
        context.TraceIdentifier = "trace-from-server";
        context.Request.Method = HttpMethods.Get;

        await CollectorMiddlewareExtensions.WriteUnhandledExceptionAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, context.Response.ContentType);
        Assert.Equal("trace-from-server", context.Response.Headers["X-Trace-Id"].ToString());
        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ContractInternalServerError,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "collector.unhandled_exception",
            Assert.IsType<Qyl.Api.Contracts.Common.Errors.InternalServerError>(error).ErrorCode);
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
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }
}

using System.Text.Json.Serialization.Metadata;
using Qyl.Api.Contracts.Common.Errors;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;

namespace Qyl.Collector;

internal static class ContractErrorResults
{
    internal static IResult NotFound(string resourceType, string resourceId) =>
        Results.Json(
            CreateNotFound(resourceType, resourceId),
            QylSerializerContext.Default.NotFoundError,
            statusCode: StatusCodes.Status404NotFound,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult Validation(
        string field,
        string message,
        string code,
        string? rejectedValue = null) =>
        Results.Json(
            CreateValidation(field, message, code, rejectedValue),
            QylSerializerContext.Default.ValidationError,
            statusCode: StatusCodes.Status400BadRequest,
            contentType: ProblemDetailsMediaType.Value);

    internal static Task WriteValidationAsync(
        HttpResponse response,
        string field,
        string message,
        string code,
        string? rejectedValue = null,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            CreateValidation(field, message, code, rejectedValue),
            QylSerializerContext.Default.ValidationError,
            StatusCodes.Status400BadRequest,
            cancellationToken);

    internal static Task WriteUnauthorizedAsync(
        HttpResponse response,
        string detail,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            CreateUnauthorized(detail),
            QylSerializerContext.Default.UnauthorizedError,
            StatusCodes.Status401Unauthorized,
            cancellationToken);

    internal static Task WriteInternalServerErrorAsync(
        HttpResponse response,
        string errorCode,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            CreateInternalServerError(errorCode),
            QylSerializerContext.Default.ContractInternalServerError,
            StatusCodes.Status500InternalServerError,
            cancellationToken);

    private static NotFoundError CreateNotFound(string resourceType, string resourceId) =>
        new()
        {
            ProblemType = new Uri("about:blank"),
            Title = "Not Found",
            Status = StatusCodes.Status404NotFound,
            ResourceType = resourceType,
            ResourceId = resourceId
        };

    private static ValidationError CreateValidation(
        string field,
        string message,
        string code,
        string? rejectedValue) =>
        new()
        {
            ProblemType = new Uri("about:blank"),
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Errors =
            [
                new ValidationErrorDetail
                {
                    Field = field,
                    Message = message,
                    Code = code,
                    RejectedValue = rejectedValue
                }
            ]
        };

    private static UnauthorizedError CreateUnauthorized(string detail) =>
        new()
        {
            ProblemType = new Uri("about:blank"),
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = detail
        };

    private static ContractInternalServerError CreateInternalServerError(string errorCode) =>
        new()
        {
            ProblemType = new Uri("about:blank"),
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            ErrorCode = errorCode
        };

    private static Task WriteAsync<T>(
        HttpResponse response,
        T error,
        JsonTypeInfo<T> jsonTypeInfo,
        int statusCode,
        CancellationToken cancellationToken)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(
            error,
            jsonTypeInfo,
            contentType: ProblemDetailsMediaType.Value,
            cancellationToken);
    }
}

using System.Text.Json.Serialization.Metadata;
using Qyl.Api.Contracts.Common.Errors;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;

namespace Qyl.Collector;

internal static class ContractErrorResults
{
    internal static IResult InternalServerError(string errorCode) =>
        Results.Json(
            CreateInternalServerError(errorCode),
            QylSerializerContext.Default.ContractInternalServerError,
            statusCode: StatusCodes.Status500InternalServerError,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult AnalysisDeadlineExceeded() =>
        Results.Json(
            new ProblemDetails
            {
                Type = ProblemType.ServiceUnavailable,
                Code = ProblemCode.ServiceUnavailable,
                Title = "Analysis deadline exceeded",
                Status = StatusCodes.Status504GatewayTimeout,
                Detail = "Narrow the time window or filters and retry."
            },
            QylSerializerContext.Default.ProblemDetails,
            statusCode: StatusCodes.Status504GatewayTimeout,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult NotFound(string resourceType, string? resourceId, string? detail = null) =>
        Results.Json(
            CreateNotFound(resourceType, resourceId, detail),
            QylSerializerContext.Default.NotFoundError,
            statusCode: StatusCodes.Status404NotFound,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult Validation(
        string field,
        string message,
        string code,
        string? rejectedValue = null,
        IReadOnlyList<string>? allowed = null,
        string? hint = null) =>
        Results.Json(
            CreateValidation(field, message, code, rejectedValue, allowed, hint),
            QylSerializerContext.Default.ValidationError,
            statusCode: StatusCodes.Status400BadRequest,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult Conflict(string resourceId, string detail) =>
        Results.Json(
            new ConflictError
            {
                Type = ProblemType.Conflict,
                Code = ProblemCode.Conflict,
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = detail,
                ConflictingResource = resourceId
            },
            QylSerializerContext.Default.ConflictError,
            statusCode: StatusCodes.Status409Conflict,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult PreconditionFailed(string? currentEtag) =>
        Results.Json(
            new PreconditionFailedError
            {
                Type = ProblemType.PreconditionFailed,
                Code = ProblemCode.PreconditionFailed,
                Title = "Precondition Failed",
                Status = StatusCodes.Status412PreconditionFailed,
                Detail = "If-Match does not name the current version. Read the resource again and retry with its etag.",
                CurrentEtag = currentEtag
            },
            QylSerializerContext.Default.PreconditionFailedError,
            statusCode: StatusCodes.Status412PreconditionFailed,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult PreconditionRequired() =>
        Results.Json(
            new PreconditionRequiredError
            {
                Type = ProblemType.PreconditionRequired,
                Code = ProblemCode.PreconditionRequired,
                Title = "Precondition Required",
                Status = StatusCodes.Status428PreconditionRequired,
                Detail = "Send the etag of the version being changed in If-Match.",
                RequiredHeader = "If-Match"
            },
            QylSerializerContext.Default.PreconditionRequiredError,
            statusCode: StatusCodes.Status428PreconditionRequired,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult ServiceUnavailable(string reason) =>
        Results.Json(
            CreateServiceUnavailable(reason),
            QylSerializerContext.Default.ServiceUnavailableError,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            contentType: ProblemDetailsMediaType.Value);

    internal static Task WriteNotFoundAsync(
        HttpResponse response,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            CreateNotFound(resourceType, resourceId),
            QylSerializerContext.Default.NotFoundError,
            StatusCodes.Status404NotFound,
            cancellationToken);

    internal static Task WriteValidationAsync(
        HttpResponse response,
        string field,
        string message,
        string code,
        string? rejectedValue = null,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            CreateValidation(field, message, code, rejectedValue, null, null),
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

    internal static Task WriteForbiddenAsync(
        HttpResponse response,
        string requiredPermission,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            new ForbiddenError
            {
                Type = ProblemType.Forbidden,
                Code = ProblemCode.Forbidden,
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = $"The API key lacks the {requiredPermission} role.",
                RequiredPermission = requiredPermission
            },
            QylSerializerContext.Default.ForbiddenError,
            StatusCodes.Status403Forbidden,
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

    internal static Task WriteServiceUnavailableAsync(
        HttpResponse response,
        string reason,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            response,
            CreateServiceUnavailable(reason),
            QylSerializerContext.Default.ServiceUnavailableError,
            StatusCodes.Status503ServiceUnavailable,
            cancellationToken);

    // A 404 names what is missing; an analysis 404 also names the tool that lists what exists.
    private static NotFoundError CreateNotFound(string resourceType, string? resourceId, string? detail = null) =>
        new()
        {
            Type = ProblemType.NotFound,
            Code = ProblemCode.NotFound,
            Title = "Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = detail ?? $"{resourceType} {resourceId} was not found in this project.",
            ResourceType = resourceType,
            ResourceId = resourceId
        };

    private static ValidationError CreateValidation(
        string field,
        string message,
        string code,
        string? rejectedValue,
        IReadOnlyList<string>? allowed,
        string? hint) =>
        new()
        {
            Type = ProblemType.ValidationFailed,
            Code = ProblemCode.ValidationFailed,
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Errors =
            [
                new ValidationErrorDetail
                {
                    Field = field,
                    Message = message,
                    Code = code,
                    RejectedValue = rejectedValue,
                    Allowed = allowed is null ? null : [.. allowed],
                    Hint = hint
                }
            ]
        };

    private static UnauthorizedError CreateUnauthorized(string detail) =>
        new()
        {
            Type = ProblemType.Unauthorized,
            Code = ProblemCode.Unauthorized,
            Title = "Unauthorized",
            Status = StatusCodes.Status401Unauthorized,
            Detail = detail
        };

    private static ContractInternalServerError CreateInternalServerError(string errorCode) =>
        new()
        {
            Type = ProblemType.InternalError,
            Code = ProblemCode.InternalError,
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = errorCode
        };

    private static ServiceUnavailableError CreateServiceUnavailable(string reason) =>
        new()
        {
            Type = ProblemType.ServiceUnavailable,
            Code = ProblemCode.ServiceUnavailable,
            Title = "Service Unavailable",
            Status = StatusCodes.Status503ServiceUnavailable,
            Detail = "The collector cannot accept this operation at present. Retry later.",
            Reason = reason
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

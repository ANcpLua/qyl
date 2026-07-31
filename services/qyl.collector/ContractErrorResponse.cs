using System.Text.Json.Serialization.Metadata;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Workflow;
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

    internal static IResult Conflict(string resourceId, string detail) =>
        Results.Json(
            new ConflictError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = detail,
                ConflictingResource = resourceId
            },
            QylSerializerContext.Default.ConflictError,
            statusCode: StatusCodes.Status409Conflict,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult ServiceUnavailable(string reason) =>
        Results.Json(
            CreateServiceUnavailable(reason),
            QylSerializerContext.Default.ServiceUnavailableError,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult WorkflowCursor(
        WorkflowCursorKind kind,
        WorkflowCursorFailureReason reason,
        string currentGeneration) =>
        Results.Json(
            new WorkflowCursorError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Invalid Workflow Cursor",
                Status = StatusCodes.Status409Conflict,
                Detail = "The cursor cannot be used for this workflow projection.",
                CursorKind = kind,
                Reason = reason,
                CurrentGeneration = new WorkflowGeneration(currentGeneration)
            },
            QylSerializerContext.Default.WorkflowCursorError,
            statusCode: StatusCodes.Status409Conflict,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult WorkflowRunDeleted(string runId) =>
        Results.Json(
            new WorkflowRunDeletedError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Workflow Run Deleted",
                Status = StatusCodes.Status410Gone,
                Detail = "The workflow run was durably deleted and cannot accept further work.",
                RunId = new WorkflowRunId(runId)
            },
            QylSerializerContext.Default.WorkflowRunDeletedError,
            statusCode: StatusCodes.Status410Gone,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult WorkflowProjectionUnavailable(
        WorkflowProjectionStatus status) =>
        Results.Json(
            new WorkflowProjectionUnavailableError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Workflow Projection Unavailable",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "The workflow projection is temporarily unavailable. Retry later.",
                ProjectionStatus = status
            },
            QylSerializerContext.Default.WorkflowProjectionUnavailableError,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            contentType: ProblemDetailsMediaType.Value);

    internal static IResult WorkflowProjectionCorrupt(
        string generation,
        string reason) =>
        Results.Json(
            new WorkflowProjectionCorruptError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Workflow Projection Corrupt",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "The committed workflow projection could not be reconstructed safely.",
                ProjectionStatus = new CorruptWorkflowProjectionStatus
                {
                    Generation = new WorkflowGeneration(generation),
                    Reason = reason
                }
            },
            QylSerializerContext.Default.WorkflowProjectionCorruptError,
            statusCode: StatusCodes.Status500InternalServerError,
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

    private static ServiceUnavailableError CreateServiceUnavailable(string reason) =>
        new()
        {
            ProblemType = new Uri("about:blank"),
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

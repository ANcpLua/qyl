using Qyl.Api.Contracts.Common.Errors;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;

namespace Qyl.Collector;

internal static class ContractErrorFactory
{
    internal static NotFoundError NotFound(string resourceType, string resourceId) =>
        new()
        {
            _ = 404,
            Title = "Not Found",
            ResourceType = resourceType,
            ResourceId = resourceId
        };

    internal static ValidationError Validation(string field, string message, string code, string? rejectedValue = null) =>
        new()
        {
            _ = 400,
            Title = "Validation Failed",
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

    internal static UnauthorizedError Unauthorized(string authenticationScheme) =>
        new()
        {
            _ = 401,
            Title = "Unauthorized",
            WwwAuthenticate = authenticationScheme
        };

    internal static ContractInternalServerError InternalServerError(string errorCode) =>
        new()
        {
            _ = 500,
            Title = "Internal Server Error",
            ErrorCode = errorCode
        };
}

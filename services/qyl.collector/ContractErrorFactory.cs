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

    internal static ContractInternalServerError InternalServerError(string errorCode) =>
        new()
        {
            _ = 500,
            Title = "Internal Server Error",
            ErrorCode = errorCode
        };
}

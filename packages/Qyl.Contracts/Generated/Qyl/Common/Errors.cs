#nullable enable

namespace Qyl.Common.Errors;

public sealed class ProblemDetails
{
    public required Uri ProblemType { get; init; }
    public required string Title { get; init; }
    public required int Status { get; init; }
    public string? Detail { get; init; }
    public Uri? Instance { get; init; }
    public string? TraceId { get; init; }
    public string? RequestId { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}

public sealed class NotFoundError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
}

public sealed class ValidationError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<Qyl.Common.Errors.ValidationErrorDetail> Errors { get; init; }
}

public sealed class ValidationErrorDetail
{
    public required string Field { get; init; }
    public required string Message { get; init; }
    public required string Code { get; init; }
    public string? RejectedValue { get; init; }
}

public sealed class UnauthorizedError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public string? WwwAuthenticate { get; init; }
}

public sealed class ForbiddenError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public string? RequiredPermission { get; init; }
}

public sealed class ConflictError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public string? ConflictingResource { get; init; }
}

public sealed class RateLimitError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public required int RetryAfter { get; init; }
    public int? RateLimit { get; init; }
    public int? RateLimitRemaining { get; init; }
}

public sealed class InternalServerError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public string? ErrorCode { get; init; }
}

public sealed class ServiceUnavailableError
{
    public required double _ { get; init; }
    public required string Title { get; init; }
    public int? RetryAfter { get; init; }
    public object? Reason { get; init; }
}

// services/qyl.mcp/Errors/QylDataException.cs

namespace qyl.mcp.Errors;

/// <summary>
///     Base exception for qyl data access errors thrown by MCP tools.
///     Caught by the MCP framework and returned as isError: true responses.
/// </summary>
public abstract class QylDataException : Exception
{
    protected QylDataException() { }

    protected QylDataException(string? message) : base(message) { }

    protected QylDataException(string? message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
///     Resource not found. Message contains only the resource type (e.g. "Trace"),
///     never the user-supplied ID — prevents information leakage.
/// </summary>
public sealed class QylNotFoundException : QylDataException
{
    public QylNotFoundException() { }

    public QylNotFoundException(string resourceType)
        : base($"{resourceType} not found. Use the corresponding search tool to find valid IDs.")
    {
    }

    public QylNotFoundException(string? message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
///     Query parsing or execution error. Message is sanitized before construction.
/// </summary>
public sealed class QylQueryException : QylDataException
{
    public QylQueryException() { }

    public QylQueryException(string sanitizedMessage) : base(sanitizedMessage) { }

    public QylQueryException(string? message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
///     Insufficient permissions for the requested operation.
/// </summary>
public sealed class QylPermissionException : QylDataException
{
    public QylPermissionException() : base("Access denied. Check your granted skills.") { }

    public QylPermissionException(string? message) : base(message) { }

    public QylPermissionException(string? message, Exception? innerException) : base(message, innerException) { }
}


namespace qyl.mcp.Errors;

public abstract class QylDataException : Exception
{
    protected QylDataException() { }

    protected QylDataException(string? message) : base(message) { }

    protected QylDataException(string? message, Exception? innerException) : base(message, innerException) { }
}

public sealed class QylNotFoundException : QylDataException
{
    public QylNotFoundException() { }

    public QylNotFoundException(string resourceType)
        : base($"{resourceType} not found. Use the corresponding search tool to find valid IDs.")
    {
    }

    public QylNotFoundException(string? message, Exception? innerException) : base(message, innerException) { }
}

public sealed class QylQueryException : QylDataException
{
    public QylQueryException() { }

    public QylQueryException(string sanitizedMessage) : base(sanitizedMessage) { }

    public QylQueryException(string? message, Exception? innerException) : base(message, innerException) { }
}

public sealed class QylPermissionException : QylDataException
{
    public QylPermissionException() : base("Access denied. Check your granted skills.") { }

    public QylPermissionException(string? message) : base(message) { }

    public QylPermissionException(string? message, Exception? innerException) : base(message, innerException) { }
}

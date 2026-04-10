// src/qyl.mcp/Errors/QylDataException.cs

namespace qyl.mcp.Errors;

/// <summary>
///     Base exception for qyl data access errors thrown by MCP tools.
///     Caught by the MCP framework and returned as isError: true responses.
/// </summary>
public abstract class QylDataException(string message) : Exception(message);

/// <summary>
///     Resource not found. Message contains only the resource type (e.g. "Trace"),
///     never the user-supplied ID — prevents information leakage.
/// </summary>
public sealed class QylNotFoundException(string resourceType)
    : QylDataException($"{resourceType} not found. Use the corresponding search tool to find valid IDs.");

/// <summary>
///     Query parsing or execution error. Message is sanitized before construction.
/// </summary>
public sealed class QylQueryException(string sanitizedMessage)
    : QylDataException(sanitizedMessage);

/// <summary>
///     Insufficient permissions for the requested operation.
/// </summary>
public sealed class QylPermissionException()
    : QylDataException("Access denied. Check your granted skills.");

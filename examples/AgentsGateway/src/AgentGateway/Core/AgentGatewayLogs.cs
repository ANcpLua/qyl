namespace AgentGateway.Core;

internal static partial class AgentGatewayLogs
{
    [LoggerMessage(Level = LogLevel.Error,
        Message =
            "Missing configuration section '{TokenValidationSectionName}'. This section is required to be present in appsettings.json")]
    public static partial void LogMissingConfiguration(ILogger logger, string tokenValidationSectionName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "TOKEN Validated")]
    public static partial void LogTokenValidated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Forbidden: {Result}")]
    public static partial void LogForbidden(ILogger logger, string result);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auth Failed")]
    public static partial void LogAuthFailed(ILogger logger, Exception ex);
}
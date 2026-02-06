namespace qyl.collector;

/// <summary>Response from clearing telemetry data.</summary>
public sealed record ClearTelemetryResponse(
    int SpansDeleted,
    int LogsDeleted,
    int SessionsDeleted,
    int ConsoleCleared,
    string Type);
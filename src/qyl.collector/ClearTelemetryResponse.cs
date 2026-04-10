namespace Qyl.Collector;

/// <summary>Response from clearing telemetry data.</summary>
public sealed record ClearTelemetryResponse(
    int SpansDeleted,
    int LogsDeleted,
    int ProfilesDeleted,
    int SessionsDeleted,
    int ConsoleCleared,
    string Type);

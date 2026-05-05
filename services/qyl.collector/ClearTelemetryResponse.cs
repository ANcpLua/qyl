namespace Qyl.Collector;

public sealed record ClearTelemetryResponse(
    int SpansDeleted,
    int LogsDeleted,
    int ProfilesDeleted,
    int SessionsDeleted,
    int ConsoleCleared,
    string Type);

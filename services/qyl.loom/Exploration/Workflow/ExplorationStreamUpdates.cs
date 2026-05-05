
using Qyl.Contracts.Copilot;

namespace Qyl.Loom.Exploration.Workflow;

internal static class ExplorationStreamUpdates
{
    public static StreamUpdate Progress(int percent, string message) => new()
    {
        Kind = StreamUpdateKind.Progress,
        Progress = percent,
        Content = message,
        Timestamp = TimeProvider.System.GetUtcNow()
    };

    public static StreamUpdate Content(string content, string? toolName = null) => new()
    {
        Kind = StreamUpdateKind.Content,
        Content = content,
        ToolName = toolName,
        Timestamp = TimeProvider.System.GetUtcNow()
    };

    public static StreamUpdate Error(string error) => new()
    {
        Kind = StreamUpdateKind.Error, Error = error, Timestamp = TimeProvider.System.GetUtcNow()
    };

    public static StreamUpdate Completed() => new()
    {
        Kind = StreamUpdateKind.Completed, Timestamp = TimeProvider.System.GetUtcNow()
    };
}

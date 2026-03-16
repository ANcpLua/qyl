using Microsoft.Extensions.AI;
using Qyl.Contracts.Copilot;

namespace Qyl.Loom;

/// <summary>
///     Interactive debugging agent that streams its reasoning via AG-UI SSE.
///     Drives the "Start Loom" → monologue → root cause → solution flow.
/// </summary>
public sealed partial class LoomExplorerService(
    DuckDbStore store,
    IssueService issueService,
    ILogger<LoomExplorerService> logger,
    IChatClient? llm = null)
{
    /// <summary>
    ///     Runs the interactive exploration pipeline for an issue, yielding
    ///     <see cref="StreamUpdate" /> events for real-time SSE rendering.
    /// </summary>
    public async IAsyncEnumerable<StreamUpdate> ExploreAsync(
        string issueId, string? userContext,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (llm is null)
        {
            yield return MakeError("No LLM configured — cannot start Loom exploration.");
            yield break;
        }

        // ── Phase 1: Ingest context ──────────────────────────────────────────
        yield return MakeProgress(0, "Ingesting qyl data...");

        var issue = await store.GetIssueByIdAsync(issueId, ct).ConfigureAwait(false);
        if (issue is null)
        {
            yield return MakeError($"Issue '{issueId}' not found.");
            yield break;
        }

        var events = await issueService
            .GetEventsAsync(issueId, 5, ct).ConfigureAwait(false);

        var contextBlock = BuildContextBlock(issue, events, userContext);
        LogExplorationStarted(issueId, events.Count);

        // ── Phase 2: Stream root cause investigation ─────────────────────────
        yield return MakeProgress(20, "Figuring out the root cause...");

        var monologuePrompt = $"""
                               {LoomPrompts.ExplorerMonologue}

                               Error context:
                               {contextBlock}
                               """;

        List<StreamUpdate> monologueUpdates = [];
        string? fullMonologue = null;
        var interrupted = false;

        try
        {
            fullMonologue = await StreamMonologueAsync(monologuePrompt, monologueUpdates, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            interrupted = true;
        }

        foreach (var update in monologueUpdates)
            yield return update;

        if (interrupted || fullMonologue is null)
        {
            yield return MakeProgress(100, "Exploration interrupted.");
            yield return MakeCompleted();
            yield break;
        }

        // ── Phase 3: Parse structured root cause from monologue ──────────────
        yield return MakeProgress(60, "Synthesizing root cause...");

        var rootCause = TryParseRootCause(fullMonologue);
        if (rootCause is not null)
        {
            yield return MakeContent(
                JsonSerializer.Serialize(rootCause, LoomJsonContext.Default.LoomRootCause),
                "root_cause");
        }

        // ── Phase 4: Solution planning ───────────────────────────────────────
        yield return MakeProgress(80, "Planning solution...");

        var solution = await RunSolutionPlanAsync(fullMonologue, ct).ConfigureAwait(false);
        if (solution is not null)
        {
            yield return MakeContent(
                JsonSerializer.Serialize(solution, LoomJsonContext.Default.LoomSolution),
                "solution");
        }

        // ── Phase 5: Complete ────────────────────────────────────────────────
        yield return MakeProgress(100, "Formatting for human consumption...");
        yield return MakeCompleted();

        LogExplorationCompleted(issueId,
            rootCause?.Steps.Count ?? 0,
            solution?.Steps.Count ?? 0);
    }

    // ── LLM streaming ─────────────────────────────────────────────────────────

    private async Task<string> StreamMonologueAsync(
        string prompt, List<StreamUpdate> updates, CancellationToken ct)
    {
        StringBuilder fullText = new();
        var now = TimeProvider.System.GetUtcNow();

        await foreach (var chunk in llm!.GetStreamingResponseAsync(prompt, cancellationToken: ct)
                           .ConfigureAwait(false))
        {
            var text = chunk.Text;
            if (text is null) continue;

            fullText.Append(text);
            updates.Add(new StreamUpdate { Kind = StreamUpdateKind.Content, Content = text, Timestamp = now });
        }

        return fullText.ToString();
    }

    private async Task<LoomSolution?> RunSolutionPlanAsync(string monologue, CancellationToken ct)
    {
        var prompt = $"""
                      {LoomPrompts.SolutionPlanning}

                      Root cause analysis:
                      {monologue}
                      """;

        try
        {
            var response = await llm!.GetResponseAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            return TryParseSolution(response.Text ?? "{}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSolutionPlanFailed(ex);
            return null;
        }
    }

    // ── Context building ──────────────────────────────────────────────────────

    private static string BuildContextBlock(
        IssueSummary issue,
        IReadOnlyList<ErrorIssueEventRow> events,
        string? userContext)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Error type: {issue.ErrorType}");
        sb.AppendLine($"Message: {issue.ErrorMessage ?? "N/A"}");
        sb.AppendLine($"Occurrences: {issue.EventCount}");
        sb.AppendLine($"First seen: {issue.FirstSeen:O}");
        sb.AppendLine($"Last seen: {issue.LastSeen:O}");

        if (events.Count > 0)
        {
            sb.AppendLine("\nRecent events:");
            foreach (var e in events)
            {
                sb.AppendLine($"  [{e.Timestamp:O}] {e.Message ?? "no message"}");
                if (e.StackTrace is not null)
                    sb.AppendLine($"    Stack: {e.StackTrace[..Math.Min(800, e.StackTrace.Length)]}");
                if (e.Environment is not null)
                    sb.AppendLine($"    Env: {e.Environment}");
            }
        }

        if (userContext is not null)
            sb.AppendLine($"\nAdditional context from user:\n{userContext}");

        return sb.ToString();
    }

    // ── JSON parsing ──────────────────────────────────────────────────────────

    internal static LoomRootCause? TryParseRootCause(string text)
    {
        var jsonStart = text.IndexOf("{\"summary\"");
        if (jsonStart < 0)
            jsonStart = text.IndexOf("```json");

        if (jsonStart < 0) return null;

        // Skip ```json marker if present
        if (text[jsonStart] == '`')
        {
            jsonStart = text.IndexOf('{', jsonStart);
            if (jsonStart < 0) return null;
        }

        var json = ExtractJsonObject(text, jsonStart);
        if (json == "{}") return null;

        try
        {
            return JsonSerializer.Deserialize(json, LoomExplorerJsonContext.Default.LoomRootCause);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static LoomSolution? TryParseSolution(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var json = ExtractJsonObject(text, start);
        if (json == "{}") return null;

        try
        {
            return JsonSerializer.Deserialize(json, LoomExplorerJsonContext.Default.LoomSolution);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractJsonObject(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            if (depth == 0) return text[start..(i + 1)];
        }

        return "{}";
    }

    // ── StreamUpdate factories ────────────────────────────────────────────────

    private static StreamUpdate MakeProgress(int percent, string message) => new()
    {
        Kind = StreamUpdateKind.Progress,
        Progress = percent,
        Content = message,
        Timestamp = TimeProvider.System.GetUtcNow()
    };

    private static StreamUpdate MakeContent(string content, string? toolName = null) => new()
    {
        Kind = StreamUpdateKind.Content,
        Content = content,
        ToolName = toolName,
        Timestamp = TimeProvider.System.GetUtcNow()
    };

    private static StreamUpdate MakeError(string error) => new()
    {
        Kind = StreamUpdateKind.Error, Error = error, Timestamp = TimeProvider.System.GetUtcNow()
    };

    private static StreamUpdate MakeCompleted() => new()
    {
        Kind = StreamUpdateKind.Completed, Timestamp = TimeProvider.System.GetUtcNow()
    };

    // ── Log methods ───────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Loom exploration started for issue {IssueId} with {EventCount} events")]
    private partial void LogExplorationStarted(string issueId, int eventCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Loom exploration completed for issue {IssueId}: {RcaSteps} RCA steps, {SolutionSteps} solution steps")]
    private partial void LogExplorationCompleted(string issueId, int rcaSteps, int solutionSteps);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom solution planning failed")]
    private partial void LogSolutionPlanFailed(Exception ex);
}

[JsonSerializable(typeof(LoomRootCause))]
[JsonSerializable(typeof(LoomSolution))]
internal partial class LoomExplorerJsonContext : JsonSerializerContext;

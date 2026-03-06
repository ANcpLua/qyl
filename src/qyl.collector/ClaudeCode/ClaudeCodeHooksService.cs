using System.Text.Json;
using System.Text.Json.Nodes;

namespace qyl.collector.ClaudeCode;

/// <summary>
///     Manages qyl hook entries in ~/.claude/hooks/hooks.json.
///     Hooks are identified by URL pattern — no comments/tags available in the schema.
///     Uses FileStream with FileShare.None for atomic read-modify-write.
/// </summary>
public sealed partial class ClaudeCodeHooksService(ILogger<ClaudeCodeHooksService> logger)
{
    private const string HookUrl = "http://localhost:5100/api/v1/claude-code/hooks";

    private static readonly string[] HookTypes = ["PostToolUse", "SubagentStart", "SubagentStop"];

    private static string HooksFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "hooks",
            "hooks.json");

    public async Task<bool> IsAttachedAsync(CancellationToken ct = default)
    {
        var root = await ReadHooksFileAsync(ct).ConfigureAwait(false);
        if (root is null)
            return false;

        return HookTypes.All(hookType => ContainsQylHook(root, hookType));
    }

    public async Task AttachAsync(CancellationToken ct = default)
    {
        var root = await ReadHooksFileAsync(ct).ConfigureAwait(false) ?? new JsonObject();

        foreach (var hookType in HookTypes)
        {
            if (ContainsQylHook(root, hookType))
                continue;

            var hookEntry = new JsonObject
            {
                ["hooks"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "http",
                        ["url"] = HookUrl,
                        ["timeout"] = 2
                    }
                }
            };

            if (root[hookType] is JsonArray existing)
            {
                existing.Add(hookEntry);
            }
            else
            {
                root[hookType] = new JsonArray { hookEntry };
            }
        }

        await WriteHooksFileAsync(root, ct).ConfigureAwait(false);
        Log.HooksAttached(logger, HooksFilePath);
    }

    public async Task DetachAsync(CancellationToken ct = default)
    {
        var root = await ReadHooksFileAsync(ct).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var hookType in HookTypes)
        {
            if (root[hookType] is not JsonArray entries)
                continue;

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (IsQylHookEntry(entries[i]))
                    entries.RemoveAt(i);
            }

            if (entries.Count is 0)
                root.Remove(hookType);
        }

        await WriteHooksFileAsync(root, ct).ConfigureAwait(false);
        Log.HooksDetached(logger, HooksFilePath);
    }

    private static bool ContainsQylHook(JsonObject root, string hookType) =>
        root[hookType] is JsonArray entries && entries.Any(IsQylHookEntry);

    private static bool IsQylHookEntry(JsonNode? entry) =>
        entry?["hooks"] is JsonArray hooks
        && hooks.Any(static h => h?["url"]?.GetValue<string>() == HookUrl);

    private static async Task<JsonObject?> ReadHooksFileAsync(CancellationToken ct)
    {
        var path = HooksFilePath;
        if (!File.Exists(path))
            return null;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var node = await JsonNode.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return node as JsonObject;
    }

    private static async Task WriteHooksFileAsync(JsonObject root, CancellationToken ct)
    {
        var path = HooksFilePath;
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        root.WriteTo(writer);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "qyl hooks attached to {Path}")]
        public static partial void HooksAttached(ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Information, Message = "qyl hooks detached from {Path}")]
        public static partial void HooksDetached(ILogger logger, string path);
    }
}

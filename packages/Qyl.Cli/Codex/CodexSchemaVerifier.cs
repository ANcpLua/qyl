using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qyl.Cli.Codex;

internal static class CodexSchemaVerifier
{
    private static readonly string[] s_requiredClientMethods =
    [
        "initialize",
        "thread/resume",
        "turn/start",
        "turn/steer",
        "turn/interrupt"
    ];

    private static readonly string[] s_requiredNotifications =
    [
        "thread/started",
        "thread/status/changed",
        "turn/started",
        "turn/completed",
        "item/started",
        "item/completed",
        "serverRequest/resolved"
    ];

    public static async Task<CodexSchemaIdentity> GenerateAndVerifyAsync(
        string codexExecutable,
        string schemaRoot,
        CancellationToken cancellationToken)
    {
        var version = await CaptureAsync(
            codexExecutable,
            ["--version"],
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException("The installed Codex executable returned an empty version.");

        Directory.CreateDirectory(schemaRoot);
        var versionHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(version.Trim())))[..16];
        var target = Path.Combine(schemaRoot, versionHash);
        var temporary = Path.Combine(schemaRoot, $".{versionHash}.{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(temporary);
        try
        {
            await RunAsync(
                codexExecutable,
                ["app-server", "generate-json-schema", "--experimental", "--out", temporary],
                cancellationToken).ConfigureAwait(false);
            var digest = VerifyDirectory(temporary);

            if (Directory.Exists(target))
            {
                var existingDigest = VerifyDirectory(target);
                if (existingDigest == digest)
                {
                    Directory.Delete(temporary, recursive: true);
                    return new CodexSchemaIdentity(version.Trim(), target, digest);
                }
                Directory.Delete(target, recursive: true);
            }
            Directory.Move(temporary, target);
            return new CodexSchemaIdentity(version.Trim(), target, digest);
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    internal static string VerifyDirectory(string directory)
    {
        var bundlePath = Path.Combine(directory, "codex_app_server_protocol.schemas.json");
        var v2BundlePath = Path.Combine(directory, "codex_app_server_protocol.v2.schemas.json");
        if (!File.Exists(bundlePath) || !File.Exists(v2BundlePath))
        {
            throw new InvalidDataException(
                "Codex app-server schema generation did not produce both protocol bundles.");
        }

        var bundle = File.ReadAllText(bundlePath);
        var v2Bundle = File.ReadAllText(v2BundlePath);
        foreach (var method in s_requiredClientMethods)
            RequireJsonString(bundle, method, "client method");
        foreach (var method in s_requiredNotifications)
            RequireJsonString(bundle, method, "notification");

        RequireProperties(
            Path.Combine(directory, "v2", "TurnSteerParams.json"),
            "threadId",
            "expectedTurnId",
            "input");
        RequireProperties(
            Path.Combine(directory, "v2", "TurnInterruptParams.json"),
            "threadId",
            "turnId");
        RequireProperties(
            Path.Combine(directory, "v2", "TurnStartParams.json"),
            "threadId",
            "input");
        RequireProperties(
            Path.Combine(directory, "v2", "ItemStartedNotification.json"),
            "threadId",
            "turnId",
            "item",
            "startedAtMs");
        RequireProperties(
            Path.Combine(directory, "v2", "ItemCompletedNotification.json"),
            "threadId",
            "turnId",
            "item",
            "completedAtMs");

        foreach (var marker in new[]
                 {
                     "collabAgentToolCall",
                     "senderThreadId",
                     "receiverThreadIds",
                     "commandExecution",
                     "fileChange",
                     "mcpToolCall"
                 })
        {
            RequireJsonString(v2Bundle, marker, "thread item field");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData([0]);
            hash.AppendData(File.ReadAllBytes(path));
            hash.AppendData([0]);
        }
        return $"sha256:{Convert.ToHexStringLower(hash.GetHashAndReset())}";
    }

    private static void RequireProperties(string path, params string[] properties)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"Codex app-server schema omitted '{Path.GetFileName(path)}'.");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var names = new HashSet<string>(StringComparer.Ordinal);
        CollectPropertyNames(document.RootElement, names);
        foreach (var property in properties)
        {
            if (!names.Contains(property))
            {
                throw new InvalidDataException(
                    $"Codex app-server schema '{Path.GetFileName(path)}' does not contain required property '{property}'.");
            }
        }
    }

    private static void CollectPropertyNames(JsonElement element, HashSet<string> names)
    {
        if (element.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("properties") &&
                    property.Value.ValueKind is JsonValueKind.Object)
                {
                    foreach (var declared in property.Value.EnumerateObject())
                        names.Add(declared.Name);
                }
                CollectPropertyNames(property.Value, names);
            }
        }
        else if (element.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectPropertyNames(item, names);
        }
    }

    private static void RequireJsonString(string json, string value, string kind)
    {
        var encoded = JsonSerializer.Serialize(value, CodexObserverStateJsonContext.Default.String);
        if (!json.Contains(encoded, StringComparison.Ordinal))
            throw new InvalidDataException($"Codex app-server schema does not expose required {kind} '{value}'.");
    }

    private static async Task<string> CaptureAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(executable, arguments, cancellationToken).ConfigureAwait(false);
        return result.StandardOutput;
    }

    private static async Task RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        _ = await RunProcessAsync(executable, arguments, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var result = new ProcessResult(
            await stdout.ConfigureAwait(false),
            await stderr.ConfigureAwait(false));
        if (process.ExitCode is not 0)
        {
            throw new InvalidOperationException(
                $"'{executable} {string.Join(' ', arguments)}' exited with code {process.ExitCode}: {result.StandardError.Trim()}");
        }
        return result;
    }

    private readonly record struct ProcessResult(string StandardOutput, string StandardError);
}

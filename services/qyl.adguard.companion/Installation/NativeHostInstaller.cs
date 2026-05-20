using System.Text.Json;
using System.Text.Json.Serialization;
using Qyl.AdGuard.Companion.Messaging;

namespace Qyl.AdGuard.Companion.Installation;

internal static class NativeHostInstaller
{
    public static async Task<int> InstallAsync(string[] args, CancellationToken cancellationToken)
    {
        var browser = ReadOption(args, "--browser") ?? "chrome";
        var extensionId = ReadOption(args, "--extension-id");
        var hostPath = ReadOption(args, "--host-path") ?? Environment.ProcessPath;

        if (!IsValidChromeExtensionId(extensionId))
        {
            await Console.Error.WriteLineAsync(
                "Missing or invalid --extension-id. Chrome extension ids are 32 lowercase a-p characters.")
                .ConfigureAwait(false);
            return 2;
        }

        if (string.IsNullOrWhiteSpace(hostPath) || !Path.IsPathFullyQualified(hostPath) || !File.Exists(hostPath))
        {
            await Console.Error.WriteLineAsync(
                "Unable to resolve a published host executable. Run from the published binary or pass --host-path <absolute-path>.")
                .ConfigureAwait(false);
            return 2;
        }

        var manifestPath = GetManifestPath(browser);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        var manifest = new NativeHostManifest
        {
            Name = NativeHostConstants.Name,
            Description = NativeHostConstants.Description,
            Path = hostPath,
            Type = "stdio",
            AllowedOrigins = [$"chrome-extension://{extensionId}/"]
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            CompanionJsonContext.Default.NativeHostManifest);
        await File.WriteAllBytesAsync(manifestPath, payload, cancellationToken).ConfigureAwait(false);

        await Console.Error.WriteLineAsync($"Installed {NativeHostConstants.Name} for {browser}.")
            .ConfigureAwait(false);
        await Console.Error.WriteLineAsync(manifestPath).ConfigureAwait(false);
        return 0;
    }

    public static string GetManifestPath(string browser)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = browser.ToLowerInvariant() switch
        {
            "chrome" => Path.Combine(home, "Library", "Application Support", "Google", "Chrome"),
            "chromium" => Path.Combine(home, "Library", "Application Support", "Chromium"),
            "chrome-for-testing" => Path.Combine(home, "Library", "Application Support", "Google", "Chrome for Testing"),
            _ => throw new ArgumentException($"Unsupported browser '{browser}'. Use chrome, chromium, or chrome-for-testing.", nameof(browser))
        };

        return Path.Combine(root, "NativeMessagingHosts", $"{NativeHostConstants.Name}.json");
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static bool IsValidChromeExtensionId(string? extensionId)
    {
        if (extensionId is null || extensionId.Length != 32)
            return false;

        return extensionId.All(static c => c is >= 'a' and <= 'p');
    }
}

internal sealed class NativeHostManifest
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Path { get; init; }

    public required string Type { get; init; }

    [JsonPropertyName("allowed_origins")]
    public required string[] AllowedOrigins { get; init; }
}

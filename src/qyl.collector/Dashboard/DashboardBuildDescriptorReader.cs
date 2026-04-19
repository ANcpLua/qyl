namespace Qyl.Collector.Dashboard;

internal sealed record DashboardBuildDescriptor(
    string? BuildId,
    string? EntryAsset,
    string? BuiltAtUtc);

internal static class DashboardBuildDescriptorReader
{
    private const string DescriptorFileName = "dashboard-build.json";
    private const string EmbeddedResourcePrefix = "Qyl.Collector.wwwroot.";
    private const string LegacyEntryAssetMarker = "/assets/index-";

    public static DashboardBuildDescriptor? TryRead(IWebHostEnvironment env)
    {
        if (TryReadDescriptorJson(env, out var descriptorJson)
            && TryParseDescriptor(descriptorJson, out var descriptor))
        {
            return descriptor;
        }

        var legacyEntryAsset = TryReadLegacyEntryAsset(env);
        if (legacyEntryAsset is null)
        {
            return null;
        }

        return new DashboardBuildDescriptor(
            BuildId: Path.GetFileName(legacyEntryAsset),
            EntryAsset: legacyEntryAsset,
            BuiltAtUtc: null);
    }

    private static bool TryReadDescriptorJson(
        IWebHostEnvironment env,
        out string? descriptorJson)
    {
        var webRootPath = ResolveWebRootPath(env);
        if (webRootPath is { Length: > 0 })
        {
            var descriptorPath = Path.Combine(webRootPath, DescriptorFileName);
            if (File.Exists(descriptorPath))
            {
                descriptorJson = File.ReadAllText(descriptorPath);
                return true;
            }
        }

        return TryReadEmbeddedText(DescriptorFileName, out descriptorJson);
    }

    private static bool TryParseDescriptor(
        string? descriptorJson,
        out DashboardBuildDescriptor? descriptor)
    {
        descriptor = null;

        if (string.IsNullOrWhiteSpace(descriptorJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(descriptorJson);
            var root = document.RootElement;
            var buildId = ReadString(root, "buildId");
            var entryAsset = ReadString(root, "entryAsset");

            if (buildId is null && entryAsset is null)
            {
                return false;
            }

            descriptor = new DashboardBuildDescriptor(
                BuildId: buildId ?? Path.GetFileName(entryAsset),
                EntryAsset: entryAsset,
                BuiltAtUtc: ReadString(root, "builtAtUtc"));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryReadLegacyEntryAsset(IWebHostEnvironment env)
    {
        string? html = null;
        var webRootPath = ResolveWebRootPath(env);
        if (webRootPath is { Length: > 0 })
        {
            var indexPath = Path.Combine(webRootPath, "index.html");
            if (File.Exists(indexPath))
            {
                html = File.ReadAllText(indexPath);
            }
        }

        if (html is null && !TryReadEmbeddedText("index.html", out html))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var markerIndex = html.IndexOfOrdinal(LegacyEntryAssetMarker);
        if (markerIndex < 0)
        {
            return null;
        }

        var assetStart = markerIndex + 1;
        var assetEnd = html.IndexOf(".js", assetStart, StringComparison.Ordinal);
        if (assetEnd < 0)
        {
            return null;
        }

        return html[assetStart..(assetEnd + 3)];
    }

    private static string? ResolveWebRootPath(IWebHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(env.WebRootPath))
        {
            return env.WebRootPath;
        }

        var candidate = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static bool TryReadEmbeddedText(string relativePath, out string? text)
    {
        try
        {
            var resourceName = $"{EmbeddedResourcePrefix}{relativePath.Replace('/', '.')}";
            using var stream = typeof(EmbeddedDashboardMiddleware).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                text = null;
                return false;
            }

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
            return true;
        }
        catch
        {
            text = null;
            return false;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

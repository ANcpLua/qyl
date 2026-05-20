namespace Qyl.AdGuard.Companion.Diagnostics;

internal static class PageSnapshotAnalyzer
{
    public static PageSnapshotResult Analyze(PageSnapshotParams parameters)
    {
        var uri = TryCreateUri(parameters.Url);
        var scheme = uri?.Scheme ?? "unknown";
        var host = uri?.Host ?? "unknown";
        var protectedBrowserPage = scheme is "chrome" or "chrome-extension" or "edge" or "about" ||
                                   host.Equals("chrome.google.com", StringComparison.OrdinalIgnoreCase);

        var notes = new List<string>();
        if (protectedBrowserPage)
            notes.Add("Browser-owned or store pages are usually outside extension injection and webRequest visibility.");

        if (parameters.HasLargeMediaCount)
            notes.Add("Large media-heavy pages are good candidates for DNS plus extension-layer comparison.");

        if ((parameters.BlockedPlaceholders ?? 0) > 0)
            notes.Add("Page reported blocked placeholders; cosmetic rule review may be useful.");

        if (parameters.AdGuardDetected is true)
            notes.Add("The page surface reports AdGuard-style artefacts.");

        notes.Add("Companion only receives summarized page signals, not raw page text.");

        return new PageSnapshotResult(
            UrlHost: host,
            Scheme: scheme,
            IsHttpSurface: scheme is "http" or "https",
            IsBrowserProtectedSurface: protectedBrowserPage,
            TitleLength: parameters.Title?.Length ?? 0,
            ScriptCount: parameters.ScriptCount ?? 0,
            ImageCount: parameters.ImageCount ?? 0,
            IframeCount: parameters.IframeCount ?? 0,
            BlockedPlaceholders: parameters.BlockedPlaceholders ?? 0,
            Notes: notes.ToArray());
    }

    private static Uri? TryCreateUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}

internal sealed class PageSnapshotParams
{
    public string? Url { get; init; }

    public string? Title { get; init; }

    public int? ScriptCount { get; init; }

    public int? ImageCount { get; init; }

    public int? IframeCount { get; init; }

    public int? BlockedPlaceholders { get; init; }

    public bool? AdGuardDetected { get; init; }

    public bool HasLargeMediaCount => (ImageCount ?? 0) + (IframeCount ?? 0) > 80;
}

internal sealed record PageSnapshotResult(
    string UrlHost,
    string Scheme,
    bool IsHttpSurface,
    bool IsBrowserProtectedSurface,
    int TitleLength,
    int ScriptCount,
    int ImageCount,
    int IframeCount,
    int BlockedPlaceholders,
    string[] Notes);

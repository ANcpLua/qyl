
using Qyl.Instrumentation;

namespace Qyl.Collector.Dashboard;

internal sealed partial class EmbeddedDashboardMiddleware
{
    private const string ResourcePrefix = "Qyl.Collector.wwwroot.";
    private readonly string _basePath;
    private readonly CachedResource? _indexHtml;
    private readonly ILogger<EmbeddedDashboardMiddleware> _logger;

    private readonly RequestDelegate _next;
    private readonly FrozenDictionary<string, CachedResource> _resources;

    public EmbeddedDashboardMiddleware(
        RequestDelegate next,
        ILogger<EmbeddedDashboardMiddleware> logger,
        string basePath = "")
    {
        _next = next;
        _logger = logger;
        _basePath = basePath.TrimStart('/').TrimEnd('/');
        _resources = LoadEmbeddedResources();
        _resources.TryGetValue("index.html", out _indexHtml);
    }

    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Debug,
        Message = "Loaded embedded resource: {Path} ({Size} bytes)")]
    private partial void LogResourceLoaded(string path, int size);

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Information,
        Message = "Loaded {Count} embedded dashboard resources")]
    private partial void LogResourcesLoaded(int count);

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimStart('/') ?? "";

        if (!string.IsNullOrEmpty(_basePath) && path.StartsWithIgnoreCase(_basePath))
        {
            path = path[_basePath.Length..].TrimStart('/');
        }

        if (path.StartsWithIgnoreCase("api/") ||
            path.StartsWithIgnoreCase("v1/") ||
            IsEndpointPath(path, QylEndpoints.Health) ||
            IsEndpointPath(path, QylEndpoints.Alive))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!_resources.TryGetValue(path, out var resource))
        {
            if (!Path.HasExtension(path) && _indexHtml is not null)
            {
                resource = _indexHtml;
            }
            else
            {
                await _next(context).ConfigureAwait(false);
                return;
            }
        }

        var requestETag = context.Request.Headers.IfNoneMatch.FirstOrDefault();
        if (requestETag == resource.ETag)
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = resource.ContentType;
        context.Response.Headers.ETag = resource.ETag;
        context.Response.Headers.CacheControl = resource.IsCacheable
            ? "public, max-age=31536000, immutable"
            : "no-cache";

        var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
        if (resource.CompressedContent is not null &&
            acceptEncoding.ContainsIgnoreCase("gzip"))
        {
            context.Response.Headers.ContentEncoding = "gzip";
            context.Response.ContentLength = resource.CompressedContent.Length;
            await context.Response.Body.WriteAsync(resource.CompressedContent, context.RequestAborted)
                .ConfigureAwait(false);
        }
        else
        {
            context.Response.ContentLength = resource.Content.Length;
            await context.Response.Body.WriteAsync(resource.Content, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static bool IsEndpointPath(string path, string endpoint)
    {
        var normalized = endpoint.TrimStart('/');
        return string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithIgnoreCase(normalized + "/");
    }

    private FrozenDictionary<string, CachedResource> LoadEmbeddedResources()
    {
        var resources = new Dictionary<string, CachedResource>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(EmbeddedDashboardMiddleware).Assembly;

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWithIgnoreCase(ResourcePrefix))
                continue;

            if (assembly.GetManifestResourceStream(name) is not { } stream) continue;

            var content = new byte[stream.Length];
            _ = stream.Read(content, 0, content.Length);

            var path = name[ResourcePrefix.Length..].Replace('.', '/');
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash > 0)
            {
                path = path[..lastSlash] + "." + path[(lastSlash + 1)..];
            }

            var contentType = GetContentType(path);
            var isCacheable = IsImmutableAsset(path);

            byte[]? compressed = null;
            if (content.Length > 1024)
            {
                using var ms = new MemoryStream();
                using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, true))
                {
                    gzip.Write(content, 0, content.Length);
                }

                var gzipped = ms.ToArray();
                if (gzipped.Length < content.Length * 0.9)
                {
                    compressed = gzipped;
                }
            }

            var hash = SHA256.HashData(content);
            var etag = $"\"{Convert.ToHexString(hash)[..16]}\"";

            resources[path] = new CachedResource(content, compressed, etag, contentType, isCacheable);
            LogResourceLoaded(path, content.Length);
        }

        LogResourcesLoaded(resources.Count);
        return resources.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".HTML" => "text/html; charset=utf-8",
        ".CSS" => "text/css; charset=utf-8",
        ".JS" => "application/javascript; charset=utf-8",
        ".JSON" => "application/json; charset=utf-8",
        ".SVG" => "image/svg+xml",
        ".PNG" => "image/png",
        ".JPG" or ".JPEG" => "image/jpeg",
        ".GIF" => "image/gif",
        ".ICO" => "image/x-icon",
        ".WOFF" => "font/woff",
        ".WOFF2" => "font/woff2",
        ".TTF" => "font/ttf",
        ".EOT" => "application/vnd.ms-fontobject",
        ".WEBP" => "image/webp",
        ".WEBM" => "video/webm",
        ".MP4" => "video/mp4",
        ".WASM" => "application/wasm",
        _ => "application/octet-stream"
    };

    private static bool IsImmutableAsset(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.ContainsOrdinal("-") || name.ContainsOrdinal(".") ||
               path.StartsWithIgnoreCase("assets/");
    }
}

internal sealed record CachedResource(
    byte[] Content,
    byte[]? CompressedContent,
    string ETag,
    string ContentType,
    bool IsCacheable);

internal static class EmbeddedDashboardExtensions
{
    private static readonly bool s_cachedHasEmbeddedDashboard =
        typeof(EmbeddedDashboardMiddleware).Assembly
            .GetManifestResourceNames()
            .Any(static n => n.StartsWithOrdinal("Qyl.Collector.wwwroot."));

    public static IApplicationBuilder UseEmbeddedDashboard(
        this IApplicationBuilder app,
        string basePath = "") =>
        app.UseMiddleware<EmbeddedDashboardMiddleware>(basePath);

    public static bool HasEmbeddedDashboard() => s_cachedHasEmbeddedDashboard;
}

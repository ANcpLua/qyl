// Copyright (c) 2025-2026 ancplua

using System.Net.Http;
using Aspire.Hosting.ApplicationModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qyl.Fleet.Hosting;

/// <summary>
/// In-process reverse proxy that serves the qyl dashboard and routes every REST / SSE request
/// to one of the <see cref="QylCollectorAnnotation"/>-registered collector backends by a
/// <c>{backendPrefix}/{tail}</c> path convention. Fleet listing is the only fan-out endpoint —
/// everything else is single-backend to keep writes deterministic.
/// </summary>
internal sealed partial class QylDashboardAggregatorHostedService : IAsyncDisposable
{
    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "qyl dashboard aggregator started on port {Port}")]
    private static partial void LogStarted(ILogger logger, int port);

    private readonly QylDashboardResource _resource;
    private readonly ILogger _logger;
    private WebApplication? _app;

    public QylDashboardAggregatorHostedService(QylDashboardResource resource, ILogger logger)
    {
        _resource = resource;
        _logger = logger;
    }

    internal int AllocatedPort { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddHttpClient("qyl-dashboard-proxy")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        _app = builder.Build();
        _app.Urls.Add($"http://127.0.0.1:{_resource.Port ?? 0}");
        MapRoutes(_app);

        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        if (addresses is not null)
        {
            AllocatedPort = new Uri(addresses.Addresses.First()).Port;
            LogStarted(_logger, AllocatedPort);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null) await _app.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    private void MapRoutes(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        app.MapGet("/api/v1/fleet", AggregateFleet);

        // Every REST + SSE request carries a collector prefix as the first path segment:
        // `/api/v1/traces/dev/<traceId>` → backend `dev` gets `/api/v1/traces/<traceId>`.
        // Keeps writes deterministic (no silent fan-out).
        app.Map("/api/v1/{**path}", RouteAsync);
    }

    private IResult AggregateFleet()
    {
        var backends = ResolveBackends();
        var rows = _resource.Annotations.OfType<QylCollectorAnnotation>().Select(a =>
        {
            var prefix = a.IdPrefix ?? a.Collector.Name;
            backends.TryGetValue(prefix, out var url);
            return new
            {
                id = $"{prefix}/{a.Info.Id}",
                name = a.Info.Name,
                description = a.Info.Description,
                environment = a.Info.Environment,
                region = a.Info.Region,
                _backend = prefix,
                _endpoint = url,
                _state = url is null ? "pending" : "running",
            };
        });
        return Results.Json(new { collectors = rows });
    }

    private async Task RouteAsync(HttpContext context, string? path)
    {
        var (backendUrl, actualPath) = ResolveByPrefix(path ?? string.Empty);
        if (backendUrl is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        byte[]? body = null;
        if (context.Request.ContentLength > 0)
        {
            using var ms = new MemoryStream();
            await context.Request.Body.CopyToAsync(ms, context.RequestAborted).ConfigureAwait(false);
            body = ms.ToArray();
        }

        var streaming = context.Request.Headers.Accept.Any(a => a is not null && a.Contains("text/event-stream"));
        var target = $"/api/v1/{actualPath}{context.Request.QueryString}";
        await ProxyRequestAsync(context, backendUrl, target, body, streaming).ConfigureAwait(false);
    }

    // Backend resolution is intentionally uncached — late-allocated collectors must be visible.
    private Dictionary<string, string> ResolveBackends()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var annotation in _resource.Annotations.OfType<QylCollectorAnnotation>())
        {
            if (annotation.Collector is not IResourceWithEndpoints withEndpoints) continue;
            var endpoint = withEndpoints.GetEndpoint("http");
            if (endpoint.IsAllocated) map[annotation.IdPrefix ?? annotation.Collector.Name] = endpoint.Url;
        }
        return map;
    }

    private (string? BackendUrl, string ActualPath) ResolveByPrefix(string prefixedPath)
    {
        var slash = prefixedPath.IndexOf('/');
        if (slash <= 0) return (null, prefixedPath);

        var prefix = prefixedPath[..slash];
        var backends = ResolveBackends();
        return backends.TryGetValue(prefix, out var url)
            ? (url, prefixedPath[(slash + 1)..])
            : (null, prefixedPath);
    }

    private static async Task ProxyRequestAsync(
        HttpContext context, string backendUrl, string path, byte[]? bodyBytes, bool streaming = false)
    {
        var factory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("qyl-dashboard-proxy");
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), new Uri(new Uri(backendUrl), path));

        foreach (var header in context.Request.Headers)
        {
            if (IsHopByHopHeader(header.Key)) continue;
            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        if (bodyBytes is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            if (context.Request.ContentType is not null)
            {
                request.Content.Headers.ContentType =
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        var completion = streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
        using var response = await client.SendAsync(request, completion, context.RequestAborted).ConfigureAwait(false);

        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers.Where(h => !IsHopByHopHeader(h.Key)))
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }
        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        if (streaming && response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
        }
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsHopByHopHeader(string name) =>
        name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Connection", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Host", StringComparison.OrdinalIgnoreCase);
}

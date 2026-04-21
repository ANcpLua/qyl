// Copyright (c) 2025-2026 ancplua

using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Qyl.Fleet.Hosting;

/// <summary>
/// In-process reverse proxy that serves the qyl dashboard and routes every REST / SSE request
/// to one of the configured collector backends by a <c>{id}/{tail}</c> path convention. Fleet
/// listing is the only fan-out endpoint — everything else is single-backend to keep writes
/// deterministic.
/// </summary>
internal sealed partial class QylDashboardAggregatorHostedService(
    IOptions<QylFleetOptions> options,
    ILogger<QylDashboardAggregatorHostedService> logger) : IHostedService, IAsyncDisposable
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "qyl fleet aggregator started on {Host}:{Port} with {Count} collector(s)")]
    private static partial void LogStarted(ILogger logger, string host, int port, int count);

    private readonly QylFleetOptions _options = options.Value;
    private readonly ILogger _logger = logger;
    private WebApplication? _app;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddHttpClient("qyl-fleet-proxy")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        _app = builder.Build();
        _app.Urls.Add($"http://{_options.Host}:{_options.Port}");
        MapRoutes(_app);
        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        var bound = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.First();
        var port = bound is null ? _options.Port : new Uri(bound).Port;
        LogStarted(_logger, _options.Host, port, _options.Collectors.Count);
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

        // Every REST + SSE request carries a collector id as the first path segment:
        // `/api/v1/traces/dev/<traceId>` → backend `dev` gets `/api/v1/traces/<traceId>`.
        // Keeps writes deterministic (no silent fan-out).
        app.Map("/api/v1/{**path}", RouteAsync);
    }

    private IResult AggregateFleet() => Results.Json(new
    {
        collectors = _options.Collectors.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            description = c.Description,
            environment = c.Environment,
            region = c.Region,
            endpoint = c.Endpoint.ToString(),
        }),
    });

    private async Task RouteAsync(HttpContext context, string? path)
    {
        var (backend, actualPath) = ResolveByPrefix(path ?? string.Empty);
        if (backend is null)
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
        await ProxyRequestAsync(context, backend, target, body, streaming).ConfigureAwait(false);
    }

    private (QylCollectorInfo? Backend, string ActualPath) ResolveByPrefix(string prefixedPath)
    {
        var slash = prefixedPath.IndexOf('/');
        if (slash <= 0) return (null, prefixedPath);

        var id = prefixedPath[..slash];
        var backend = _options.Collectors.FirstOrDefault(c => c.Id.Equals(id, StringComparison.Ordinal));
        return backend is null ? (null, prefixedPath) : (backend, prefixedPath[(slash + 1)..]);
    }

    private static async Task ProxyRequestAsync(
        HttpContext context, QylCollectorInfo backend, string path, byte[]? bodyBytes, bool streaming = false)
    {
        var factory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("qyl-fleet-proxy");
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), new Uri(backend.Endpoint, path));

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

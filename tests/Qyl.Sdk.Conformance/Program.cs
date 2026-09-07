using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qyl;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddQyl();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/conformance", RunConformanceAsync);

await app.RunAsync().ConfigureAwait(false);

static async Task<IResult> RunConformanceAsync(
    HttpContext context,
    ILogger<Program> logger,
    IHostApplicationLifetime lifetime,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken)
{
    context.Response.OnCompleted(
        static async state =>
        {
            await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
            ((IHostApplicationLifetime)state).StopApplication();
        },
        lifetime);

    var inbound = Activity.Current;
    // Since Qyl.Telemetry.AutoInstrumentation 14.1.0 there is one SERVER span per request and it is
    // ASP.NET Core's own: qyl enriches that activity instead of starting a second one. So the proof
    // that AddQyl() did its inbound work is no longer a qyl-owned span, which deliberately no longer
    // exists — it is the framework's span carrying qyl's domain. The tag is the load-bearing half:
    // asserting the source alone would pass even if AddQyl() had done nothing at all, because
    // ASP.NET Core creates that activity on its own.
    if (inbound is null ||
        !string.Equals(inbound.Source.Name, "Microsoft.AspNetCore", StringComparison.Ordinal) ||
        inbound.GetTagItem("qyl.instrumentation.domain") is not "aspnetcore.server")
    {
        throw new InvalidOperationException(
            "builder.AddQyl() did not enrich ASP.NET Core's inbound server span with the qyl domain.");
    }

    var stub = LoopbackHttpStub.Start();
    try
    {
        var http = httpClientFactory.CreateClient();
        using var response = await http.GetAsync(stub.Uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(body, LoopbackHttpStub.ResponseBody, StringComparison.Ordinal))
            throw new InvalidOperationException($"Local HTTP stub returned an unexpected body: {body}");

        await stub.Completion.ConfigureAwait(false);
    }
    finally
    {
        stub.Stop();
    }

    var traceId = inbound.TraceId.ToHexString();
    ConformanceLog.WorkCompleted(logger, traceId);

    context.Response.Headers["X-Qyl-Conformance-Trace-Id"] = traceId;
    context.Response.Headers["X-Qyl-Conformance-Span-Id"] = inbound.SpanId.ToHexString();
    return Results.Text("qyl-sdk-conformance-ok", "text/plain", Encoding.UTF8);
}

internal static partial class ConformanceLog
{
    [LoggerMessage(1, LogLevel.Information, "Qyl SDK conformance work completed for trace {TraceId}")]
    internal static partial void WorkCompleted(ILogger logger, string traceId);
}

internal sealed class LoopbackHttpStub
{
    internal const string ResponseBody = "qyl-stub-ready";

    private readonly TcpListener _listener;

    private LoopbackHttpStub(TcpListener listener)
    {
        _listener = listener;
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Uri = new Uri($"http://127.0.0.1:{port}/stub", UriKind.Absolute);
        Completion = ServeOnceAsync(listener);
    }

    internal Uri Uri { get; }

    internal Task Completion { get; }

    internal static LoopbackHttpStub Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);
        return new LoopbackHttpStub(listener);
    }

    internal void Stop() => _listener.Stop();

    private static async Task ServeOnceAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        while (await reader.ReadLineAsync().ConfigureAwait(false) is { Length: > 0 })
        {
        }

        var body = Encoding.UTF8.GetBytes(ResponseBody);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers).ConfigureAwait(false);
        await stream.WriteAsync(body).ConfigureAwait(false);
    }
}

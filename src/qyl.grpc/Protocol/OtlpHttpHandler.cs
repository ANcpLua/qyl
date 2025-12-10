using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using qyl.grpc.Abstractions;
using qyl.grpc.Models;
using qyl.grpc.Stores;

namespace qyl.grpc.Protocol;

/// <summary>
/// Generic handler for OTLP/HTTP endpoints.
/// Consolidates the duplicated parsing and processing logic for traces, metrics, and logs.
/// </summary>
public static class OtlpHttpHandler
{
    /// <summary>
    /// Handles an OTLP/HTTP export request for any telemetry type.
    /// </summary>
    /// <typeparam name="TRequest">The protobuf request type</typeparam>
    /// <typeparam name="TModel">The internal model type</typeparam>
    public static async Task<IResult> HandleExportAsync<TRequest, TModel>(
        HttpContext ctx,
        MessageParser<TRequest> parser,
        Func<TRequest, IEnumerable<TModel>> converter,
        ITelemetryStore<TModel> store,
        ITelemetryBroadcaster broadcaster,
        ServiceRegistry serviceRegistry,
        TelemetrySignal signal,
        Func<TModel, ResourceModel> resourceSelector,
        Action<TModel>? postProcess = null)
        where TRequest : class, IMessage<TRequest>, new()
        where TModel : class
    {
        try
        {
            var request = await ParseRequestAsync(ctx, parser);
            if (request is null)
                return Results.BadRequest("Unsupported content type. Use application/x-protobuf or application/json");

            var items = converter(request).ToList();
            foreach (var item in items)
            {
                store.Add(item);
                serviceRegistry.RegisterFromResource(resourceSelector(item));
                postProcess?.Invoke(item);
            }

            if (items.Count > 0)
                await broadcaster.BroadcastAsync(signal, items);

            return Results.Ok(new { partialSuccess = new { } });
        }
        catch (InvalidProtocolBufferException ex)
        {
            return Results.BadRequest($"Invalid protobuf data: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<TRequest?> ParseRequestAsync<TRequest>(
        HttpContext ctx,
        MessageParser<TRequest> parser)
        where TRequest : class, IMessage<TRequest>, new()
    {
        var contentType = ctx.Request.ContentType ?? "";

        if (contentType.Contains("application/x-protobuf"))
        {
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms);
            ms.Position = 0;
            return parser.ParseFrom(ms);
        }

        if (contentType.Contains("application/json"))
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
            return JsonParser.Default.Parse<TRequest>(json);
        }

        return null;
    }
}

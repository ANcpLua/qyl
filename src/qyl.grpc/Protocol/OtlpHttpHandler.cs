using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using qyl.grpc.Abstractions;
using qyl.grpc.Models;
using qyl.grpc.Stores;

namespace qyl.grpc.Protocol;

public static class OtlpHttpHandler
{
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
            TRequest? request = await ParseRequestAsync(ctx, parser);
            if (request is null)
                return Results.BadRequest("Unsupported content type. Use application/x-protobuf or application/json");

            var items = converter(request).ToList();
            foreach (TModel item in items)
            {
                store.Add(item);
                serviceRegistry.RegisterFromResource(resourceSelector(item));
                postProcess?.Invoke(item);
            }

            if (items.Count > 0)
                await broadcaster.BroadcastAsync(signal, items, ctx.RequestAborted);

            return Results.Ok(new
            {
                partialSuccess = new
                {
                }
            });
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
        string contentType = ctx.Request.ContentType ?? "";

        if (contentType.Contains("application/x-protobuf", StringComparison.Ordinal))
        {
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
            ms.Position = 0;
            return parser.ParseFrom(ms);
        }

        if (contentType.Contains("application/json", StringComparison.Ordinal))
        {
            using var reader = new StreamReader(ctx.Request.Body);
            string json = await reader.ReadToEndAsync(ctx.RequestAborted);
            return JsonParser.Default.Parse<TRequest>(json);
        }

        return null;
    }
}

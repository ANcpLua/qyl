using System.Text.Json.Serialization;

namespace qyl.collector.Observe;

internal static class ObserveEndpoints
{
    public static WebApplication MapObserveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/observe");

        group.MapGet("/", ListSubscriptions);
        group.MapGet("/catalog", GetCatalog);
        group.MapPost("/", Subscribe);
        group.MapDelete("/{id}", Unsubscribe);

        return app;
    }

    // GET /api/v1/observe/catalog — domain discovery and attribute manifests
    private static IResult GetCatalog(SubscriptionManager manager)
        => Results.Ok(ObserveCatalog.Build(manager));

    // GET /api/v1/observe — list active subscriptions
    private static IResult ListSubscriptions(SubscriptionManager manager)
    {
        var items = manager.GetAll()
            .Select(static s => new SubscriptionDto(
                s.Id, s.Filter, s.Endpoint, s.CreatedAt,
                s.ContractHash, s.SchemaVersion))
            .ToArray();

        return Results.Ok(new { subscriptions = items });
    }

    // POST /api/v1/observe — activate a new subscription
    private static IResult Subscribe(SubscribeRequest req, SubscriptionManager manager)
    {
        if (string.IsNullOrWhiteSpace(req.Filter))
            return Results.BadRequest(new { error = "filter is required" });

        if (string.IsNullOrWhiteSpace(req.Endpoint))
            return Results.BadRequest(new { error = "endpoint is required" });

        if (!Uri.TryCreate(req.Endpoint, UriKind.Absolute, out _))
            return Results.BadRequest(new { error = "endpoint must be an absolute URI" });

        var negotiation = SchemaVersionNegotiator.Negotiate(req.SchemaVersion);

        return negotiation switch
        {
            SchemaVersionNegotiator.NegotiationResult.Reject r =>
                Results.Conflict(new
                {
                    error            = r.Reason,
                    collector_version = r.CollectorVersion,
                    requested_version = r.RequestedVersion
                }),

            SchemaVersionNegotiator.NegotiationResult.Accept a =>
                CreateSubscription(req, manager, a),

            // Transform: not yet implemented — treat as Accept
            SchemaVersionNegotiator.NegotiationResult.Transform t =>
                CreateSubscription(req, manager,
                    new SchemaVersionNegotiator.NegotiationResult.Accept(
                        t.CollectorVersion, t.RequestedVersion)),

            _ => Results.StatusCode(500)
        };
    }

    private static IResult CreateSubscription(
        SubscribeRequest req,
        SubscriptionManager manager,
        SchemaVersionNegotiator.NegotiationResult.Accept negotiation)
    {
        var subscription = manager.Subscribe(req.Filter, req.Endpoint, req.SchemaVersion);
        var dto = new SubscriptionDto(
            subscription.Id, subscription.Filter, subscription.Endpoint,
            subscription.CreatedAt, subscription.ContractHash, subscription.SchemaVersion);

        // Surface version delta as a warning field if versions differ
        if (negotiation.RequestedVersion is not null &&
            !string.Equals(negotiation.RequestedVersion, negotiation.CollectorVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new
            {
                subscription      = dto,
                schema_warning    = $"Semconv version mismatch: collector={negotiation.CollectorVersion}, requested={negotiation.RequestedVersion}. Schema attributes may differ.",
                collector_version = negotiation.CollectorVersion,
                requested_version = negotiation.RequestedVersion
            });
        }

        return Results.Ok(dto);
    }

    // DELETE /api/v1/observe/{id} — tear down a subscription
    private static IResult Unsubscribe(string id, SubscriptionManager manager)
    {
        return manager.Unsubscribe(id)
            ? Results.NoContent()
            : Results.NotFound(new { error = $"subscription '{id}' not found" });
    }
}

internal sealed record SubscribeRequest(
    [property: JsonPropertyName("filter")]         string Filter,
    [property: JsonPropertyName("endpoint")]       string Endpoint,
    [property: JsonPropertyName("schema_version")] string? SchemaVersion = null);

internal sealed record SubscriptionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("filter")] string Filter,
    [property: JsonPropertyName("endpoint")] string Endpoint,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("contract_hash"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null,
    [property: JsonPropertyName("schema_version"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SchemaVersion = null);

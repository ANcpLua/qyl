namespace Qyl.Collector.Observe;

internal static class ObserveEndpoints
{
    [QylMapEndpoints]
    public static WebApplication MapObserveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/observe");

        group.MapGet("/", ListSubscriptions);
        group.MapGet("/catalog", GetCatalog);
        group.MapPost("/", Subscribe);
        group.MapDelete("/{id}", Unsubscribe);

        return app;
    }

    private static Ok<CatalogResponse> GetCatalog(SubscriptionManager manager)
        => TypedResults.Ok(ObserveCatalog.Build(manager));

    private static IResult ListSubscriptions(SubscriptionManager manager)
    {
        var items = manager.GetAll()
            .Select(static s => new SubscriptionDto(
                s.Id, s.Filter, s.Endpoint, s.CreatedAt,
                s.ContractHash, s.SchemaVersion))
            .ToArray();

        return Results.Ok(new { subscriptions = items });
    }

    private static IResult Subscribe(SubscribeRequest req, SubscriptionManager manager)
    {
        if (string.IsNullOrWhiteSpace(req.Filter))
            return TypedResults.BadRequest(new { error = "filter is required" });

        if (string.IsNullOrWhiteSpace(req.Endpoint))
            return TypedResults.BadRequest(new { error = "endpoint is required" });

        if (!Uri.TryCreate(req.Endpoint, UriKind.Absolute, out _))
            return TypedResults.BadRequest(new { error = "endpoint must be an absolute URI" });

        var negotiation = SchemaVersionNegotiator.Negotiate(req.SchemaVersion);

        return negotiation switch
        {
            SchemaVersionNegotiator.NegotiationResult.Reject r =>
                TypedResults.Conflict(new
                {
                    error = r.Reason, collector_version = r.DeployedVersion, requested_version = r.RequestedVersion
                }),

            SchemaVersionNegotiator.NegotiationResult.Accept a =>
                CreateSubscription(req, manager, a),

            SchemaVersionNegotiator.NegotiationResult.Transform t =>
                CreateSubscription(req, manager,
                    new SchemaVersionNegotiator.NegotiationResult.Accept(
                        t.DeployedVersion, t.RequestedVersion)),

            _ => TypedResults.StatusCode(500)
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

        if (negotiation.RequestedVersion is not null &&
            !string.Equals(negotiation.RequestedVersion, negotiation.DeployedVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Ok(new
            {
                subscription = dto,
                schema_warning =
                    $"Semconv version mismatch: collector={negotiation.DeployedVersion}, requested={negotiation.RequestedVersion}. Schema attributes may differ.",
                collector_version = negotiation.DeployedVersion,
                requested_version = negotiation.RequestedVersion
            });
        }

        return TypedResults.Ok(dto);
    }

    private static IResult Unsubscribe(string id, SubscriptionManager manager) =>
        manager.Unsubscribe(id)
            ? TypedResults.NoContent()
            : TypedResults.NotFound(new { error = $"subscription '{id}' not found" });
}

internal sealed record SubscribeRequest(
    [property: JsonPropertyName("filter")] string Filter,
    [property: JsonPropertyName("endpoint")]
    string Endpoint,
    [property: JsonPropertyName("schema_version")]
    string? SchemaVersion = null);

internal sealed record SubscriptionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("filter")] string Filter,
    [property: JsonPropertyName("endpoint")]
    string Endpoint,
    [property: JsonPropertyName("created_at")]
    DateTimeOffset CreatedAt,
    [property: JsonPropertyName("contract_hash")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContractHash = null,
    [property: JsonPropertyName("schema_version")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SchemaVersion = null);

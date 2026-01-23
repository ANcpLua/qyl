using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using qyl.AspNetCore.Example.Models.Telemetry;
using qyl.AspNetCore.Example.Telemetry;

namespace qyl.AspNetCore.Example.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public ActionResult<Order> Create([FromBody] CreateOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = AppTelemetry.Source.StartActivity("Order.Create", System.Diagnostics.ActivityKind.Internal);

        Log.ProcessingOrderRequest(_logger);

        var stopwatch = Stopwatch.StartNew();
        var order = BuildOrder(request);
        stopwatch.Stop();

        AppTelemetry.OrdersCreated.Add(1);
        AppTelemetry.OrderProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        Log.OrderCreated(_logger);

        return Ok(order);
    }

    [HttpGet("{orderId:int}")]
    public ActionResult<Order> Get(int orderId)
    {
        using var activity = AppTelemetry.Source.StartActivity("Order.Get", System.Diagnostics.ActivityKind.Internal);

        var order = BuildOrder(orderId);

        Log.OrderRetrieved(_logger);

        return Ok(order);
    }

    private static Order BuildOrder(CreateOrderRequest request)
    {
        var items = request.Items
            .Select(item => new OrderItem { Quantity = item.Quantity })
            .ToList();

        return new Order
        {
            Id = RandomNumberGenerator.GetInt32(1000, 9999),
            CustomerId = request.CustomerId,
            Amount = request.Items.Sum(item => item.Quantity * item.UnitPrice),
            Status = "created",
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime,
            Items = new Collection<OrderItem>(items)
        };
    }

    private static Order BuildOrder(int orderId)
    {
        var items = new Collection<OrderItem>
        {
            new() { Quantity = RandomNumberGenerator.GetInt32(1, 5) }
        };

        return new Order
        {
            Id = orderId,
            CustomerId = $"customer-{RandomNumberGenerator.GetInt32(100, 999)}",
            Amount = items.Sum(item => item.Quantity) * 12.5m,
            Status = "created",
            CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime,
            Items = items
        };
    }
}

// qyl OpenTelemetry ASP.NET Core Example
// Demonstrates manual span creation and custom metrics

namespace qyl.AspNetCore.Example.Controllers;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using qyl.AspNetCore.Example.Logging;
using qyl.AspNetCore.Example.Models;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private static readonly HttpClient HttpClient = new();

    private readonly ILogger<WeatherForecastController> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _freezingDaysCounter;
    private readonly Counter<long> _requestCounter;

    public WeatherForecastController(
        ILogger<WeatherForecastController> logger,
        InstrumentationSource instrumentationSource)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(instrumentationSource);

        _activitySource = instrumentationSource.ActivitySource;
        _freezingDaysCounter = instrumentationSource.FreezingDaysCounter;
        _requestCounter = instrumentationSource.RequestCounter;
    }

    [HttpGet]
    public IEnumerable<WeatherForecast> Get()
    {
        // Track request count
        _requestCounter.Add(1);

        // Create a logging scope with correlation ID
        using var scope = _logger.BeginIdScope(Guid.NewGuid().ToString("N"));

        // Example: HTTP call that will be auto-instrumented as child span
        // Uncomment to test HTTP client instrumentation:
        // var res = HttpClient.GetStringAsync(new Uri("http://httpbin.org/get")).Result;

        // Manual span creation - becomes child of ASP.NET Core request span
        using var activity = _activitySource.StartActivity("CalculateForecast");
        activity?.SetTag("forecast.days", 5);

        var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = RandomNumberGenerator.GetInt32(-20, 55),
            Summary = Summaries[RandomNumberGenerator.GetInt32(Summaries.Length)],
        })
        .ToArray();

        // Track freezing days metric
        var freezingDays = forecast.Count(f => f.TemperatureC < 0);
        _freezingDaysCounter.Add(freezingDays);

        // Add span attributes
        activity?.SetTag("forecast.freezing_days", freezingDays);

        // Structured logging
        _logger.WeatherForecastGenerated(LogLevel.Information, forecast.Length, forecast);

        return forecast;
    }

    [HttpGet("{days:int}")]
    public IEnumerable<WeatherForecast> Get(int days)
    {
        _requestCounter.Add(1);

        using var activity = _activitySource.StartActivity("CalculateForecast");
        activity?.SetTag("forecast.days", days);

        var forecast = Enumerable.Range(1, days).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = RandomNumberGenerator.GetInt32(-20, 55),
            Summary = Summaries[RandomNumberGenerator.GetInt32(Summaries.Length)],
        })
        .ToArray();

        var freezingDays = forecast.Count(f => f.TemperatureC < 0);
        _freezingDaysCounter.Add(freezingDays);
        activity?.SetTag("forecast.freezing_days", freezingDays);

        return forecast;
    }
}

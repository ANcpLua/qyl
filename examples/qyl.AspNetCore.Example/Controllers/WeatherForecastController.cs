using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using qyl.AspNetCore.Example.Logging;
using qyl.AspNetCore.Example.Models;

namespace qyl.AspNetCore.Example.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private static readonly HttpClient HttpClient = new();
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _freezingDaysCounter;

    private readonly ILogger<WeatherForecastController> _logger;
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
        _requestCounter.Add(1);

        using var scope = _logger.BeginIdScope(Guid.NewGuid().ToString("N"));

        using var activity = _activitySource.StartActivity("CalculateForecast");
        activity?.SetTag("forecast.days", 5);

        WeatherForecast[] forecast =
        [
            .. Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = TimeProvider.System.GetLocalNow().DateTime.AddDays(index),
                TemperatureC = RandomNumberGenerator.GetInt32(-20, 55),
                Summary = Summaries[RandomNumberGenerator.GetInt32(Summaries.Length)]
            })
        ];

        var freezingDays = forecast.Count(f => f.TemperatureC < 0);
        _freezingDaysCounter.Add(freezingDays);

        activity?.SetTag("forecast.freezing_days", freezingDays);

        _logger.WeatherForecastGenerated(LogLevel.Information, forecast.Length, forecast);

        return forecast;
    }

    [HttpGet("{days:int}")]
    public IEnumerable<WeatherForecast> Get(int days)
    {
        _requestCounter.Add(1);

        using var activity = _activitySource.StartActivity("CalculateForecast");
        activity?.SetTag("forecast.days", days);

        WeatherForecast[] forecast =
        [
            .. Enumerable.Range(1, days).Select(index => new WeatherForecast
            {
                Date = TimeProvider.System.GetLocalNow().DateTime.AddDays(index),
                TemperatureC = RandomNumberGenerator.GetInt32(-20, 55),
                Summary = Summaries[RandomNumberGenerator.GetInt32(Summaries.Length)]
            })
        ];

        var freezingDays = forecast.Count(f => f.TemperatureC < 0);
        _freezingDaysCounter.Add(freezingDays);
        activity?.SetTag("forecast.freezing_days", freezingDays);

        return forecast;
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qyl.NextDns.Ingester;

var options = IngesterOptions.TryFromEnvironment();
if (options is null)
{
    await Console.Error.WriteLineAsync(
        "qyl-nextdns-ingester refuses to start: NEXTDNS_API_KEY and NEXTDNS_PROFILE_ID must be set.")
        .ConfigureAwait(false);
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddSimpleConsole(formatter => formatter.SingleLine = true);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(options);
builder.Services
    .AddHttpClient<NextDnsClient>(http => http.BaseAddress = new Uri(options.BaseUrl))
    .AddStandardResilienceHandler();

builder.Services.AddHostedService<NextDnsLogPoller>();

var tracerBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(static resource => resource.AddService("qyl-nextdns-ingester"))
    .WithTracing(tracing =>
    {
        tracing.AddSource(IngesterTelemetry.SourceName);
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
    });

using var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
return 0;

# Instrumentation

Manual instrumentation for OpenTelemetry .NET.

> **Note**: On this page you will learn how you can add traces, metrics and logs to your
> code manually. You are not limited to using one kind of instrumentation: you
> can also use automatic instrumentation to get started and then enrich your code
> with manual instrumentation as needed.
>
> Also, for libraries your code depends on, you don't have to write
> instrumentation code yourself, since they might be already instrumented or
> there are instrumentation libraries for them.

## A note on terminology

.NET is different from other languages/runtimes that support OpenTelemetry. The
Tracing API is implemented by the
[System.Diagnostics](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics)
API, repurposing existing constructs like `ActivitySource` and `Activity` to be
OpenTelemetry-compliant under the covers.

However, there are parts of the OpenTelemetry API and terminology that .NET
developers must still know to be able to instrument their applications, which
are covered here as well as the `System.Diagnostics` API.

If you prefer to use OpenTelemetry APIs instead of `System.Diagnostics` APIs,
you can refer to the OpenTelemetry API Shim docs for tracing.

## Manual instrumentation setup

### Dependencies

Install the following OpenTelemetry NuGet packages:

```sh
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
```

For ASP.NET Core-based applications, also install the AspNetCore instrumentation
package:

```sh
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

### Initialize the SDK

> **Note**: If you're instrumenting a library, you don't need to initialize the SDK.

It is important to configure an instance of the OpenTelemetry SDK as early as
possible in your application.

To initialize the OpenTelemetry SDK for an ASP.NET Core app, update the content
of the `Program.cs` file with the following code:

```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Ideally, you will want this name to come from a config file, constants file, etc.
var serviceName = "dice-server";
var serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: serviceName,
        serviceVersion: serviceVersion))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(serviceName)
        .AddConsoleExporter());

builder.Logging.AddOpenTelemetry(options => options
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
        serviceName: serviceName,
        serviceVersion: serviceVersion))
    .AddConsoleExporter());

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
```

If initializing the OpenTelemetry SDK for a console app, add the following code
at the beginning of your program, during any important startup operations.

```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

//...

var serviceName = "MyServiceName";
var serviceVersion = "1.0.0";

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .ConfigureResource(resource =>
        resource.AddService(
          serviceName: serviceName,
          serviceVersion: serviceVersion))
    .AddConsoleExporter()
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(serviceName)
    .AddConsoleExporter()
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddConsoleExporter();
    });
});

//...

tracerProvider.Dispose();
meterProvider.Dispose();
loggerFactory.Dispose();
```

For debugging and local development purposes, the example exports telemetry to
the console. After you have finished setting up manual instrumentation, you need
to configure an appropriate exporter to export the app's telemetry data to one or
more telemetry backends.

The example also sets up the mandatory SDK default attribute `service.name`,
which holds the logical name of the service, and the optional, but highly
encouraged, attribute `service.version`, which holds the version of the service
API or implementation.

## Traces

### Initialize Tracing

> **Note**: If you're instrumenting a library, you don't need to initialize a
> TracerProvider.

To enable tracing in your app, you'll need to have an initialized
`TracerProvider` that will let you create a `Tracer`.

If a `TracerProvider` is not created, the OpenTelemetry APIs for tracing will
use a no-op implementation and fail to generate data.

If you followed the instructions to initialize the SDK above, you have a
`TracerProvider` setup for you already. You can continue with setting up an
ActivitySource.

### Setting up an ActivitySource

Anywhere in your application where you write manual tracing code should
configure an `ActivitySource`, which will be how you trace operations with
`Activity` elements.

It's generally recommended to define `ActivitySource` once per app/service that
is been instrumented, but you can instantiate several `ActivitySource`s if that
suits your scenario.

Create a new file `Instrumentation.cs` as a custom type to hold reference for
the ActivitySource.

```csharp
using System.Diagnostics;

/// <summary>
/// It is recommended to use a custom type to hold references for ActivitySource.
/// This avoids possible type collisions with other components in the DI container.
/// </summary>
public class Instrumentation : IDisposable
{
    internal const string ActivitySourceName = "dice-server";
    internal const string ActivitySourceVersion = "1.0.0";

    public Instrumentation()
    {
        this.ActivitySource = new ActivitySource(ActivitySourceName, ActivitySourceVersion);
    }

    public ActivitySource ActivitySource { get; }

    public void Dispose()
    {
        this.ActivitySource.Dispose();
    }
}
```

Then update the `Program.cs` to add the Instrument object as a dependency
injection:

```csharp
//...

// Register the Instrumentation class as a singleton in the DI container.
builder.Services.AddSingleton<Instrumentation>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
```

### Create Activities

Now that you have activitySources initialized, you can create activities.

```csharp
public List<int> rollTheDice(int rolls)
{
    List<int> results = new List<int>();

    // It is recommended to create activities, only when doing operations
    // that are worth measuring independently.
    // Too many activities makes it harder to visualize in tools like Jaeger.
    using (var myActivity = activitySource.StartActivity("rollTheDice"))
    {
        for (int i = 0; i < rolls; i++)
        {
            results.Add(rollOnce());
        }

        return results;
    }
}
```

### Create nested Activities

Nested spans let you track work that's nested in nature. For example, the
`rollOnce()` function below represents a nested operation. The following sample
creates a nested span that tracks `rollOnce()`:

```csharp
private int rollOnce()
{
    using (var childActivity = activitySource.StartActivity("rollOnce"))
    {
      int result;

      result = Random.Shared.Next(min, max + 1);

      return result;
    }
}
```

When you view the spans in a trace visualization tool, `rollOnce` childActivity
will be tracked as a nested operation under `rollTheDice` activity.

### Get the current Activity

Sometimes it's helpful to do something with the current/active Activity/Span at
a particular point in program execution.

```csharp
var activity = Activity.Current;
```

### Activity Tags

Tags (the equivalent of Attributes) let you attach key/value pairs to an
`Activity` so it carries more information about the current operation that it's
tracking.

```csharp
private int rollOnce()
{
  using (var childActivity = activitySource.StartActivity("rollOnce"))
    {
      int result;

      result = Random.Shared.Next(min, max + 1);
      childActivity?.SetTag("dicelib.rolled", result);

      return result;
    }
}
```

### Add Events to Activities

Spans can be annotated with named events (called Span Events) that can carry
zero or more Span Attributes, each of which itself is a key:value map paired
automatically with a timestamp.

```csharp
myActivity?.AddEvent(new("Init"));
...
myActivity?.AddEvent(new("End"));
```

```csharp
var eventTags = new ActivityTagsCollection
{
    { "operation", "calculate-pi" },
    { "result", 3.14159 }
};

// Use TimeProvider.System.GetUtcNow() for timestamps in production code
activity?.AddEvent(new("End Computation", timestamp, eventTags));
```

### Create Activities with links

A Span may be linked to zero or more other Spans that are causally related via a
Span Link. Links can be used to represent batched operations where a Span was
initiated by multiple initiating Spans, each representing a single incoming item
being processed in the batch.

```csharp
var links = new List<ActivityLink>
{
    new ActivityLink(activityContext1),
    new ActivityLink(activityContext2),
    new ActivityLink(activityContext3)
};

var activity = MyActivitySource.StartActivity(
    ActivityKind.Internal,
    name: "activity-with-links",
    links: links);
```

### Set Activity status

It can be a good idea to record exceptions when they happen. It's recommended to
do this in conjunction with setting span status.

```csharp
private int rollOnce()
{
    using (var childActivity = activitySource.StartActivity("rollOnce"))
    {
        int result;

        try
        {
            result = Random.Shared.Next(min, max + 1);
            childActivity?.SetTag("dicelib.rolled", result);
        }
        catch (Exception ex)
        {
            childActivity?.SetStatus(ActivityStatusCode.Error, "Something bad happened!");
            childActivity?.AddException(ex);
            throw;
        }

        return result;
    }
}
```

## Next steps

After you've set up manual instrumentation, you may want to use instrumentation
libraries. As the name suggests, they will instrument relevant libraries you're
using and generate spans (activities) for things like inbound and outbound HTTP
requests and more.

You'll also want to configure an appropriate exporter to export your telemetry
data to one or more telemetry backends.

You can also check the automatic instrumentation for .NET, which is currently in
beta.

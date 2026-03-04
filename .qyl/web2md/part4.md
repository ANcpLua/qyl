---
Author: Andrew Lock
Site Name: Andrew Lock | .NET Escapades
Url: https://andrewlock.net/recording-metrics-in-process-using-meterlistener/
Published: 2026-02-24
Extracted Date: 2026-03-04
---

February 24, 2026 ~14 min read

[System.Diagnostics.Metrics APIs - Part 4](https://andrewlock.net/series/system-diagnostics-metrics-apis/)

This is the fourth post in the series: [System.Diagnostics.Metrics APIs](https://andrewlock.net/series/system-diagnostics-metrics-apis/).

1.  [Part 1 - Creating and consuming metrics with System.Diagnostics.Metrics APIs](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/)
2.  [Part 2 - Exploring the (underwhelming) System.Diagnostics.Metrics source generators](https://andrewlock.net/creating-strongly-typed-metics-with-a-source-generator/)
3.  [Part 3 - Creating standard and "observable" instruments](https://andrewlock.net/creating-standard-and-observable-instruments/)
4.  Part 4 - Recording metrics in-process using MeterListener (this post)

So far in this series I've described how to [create and consume metrics using `dotnet-counters`](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/), how to [create each of the different `Instrument` types](https://andrewlock.net/creating-standard-and-observable-instruments/#system-diagnostics-metrics-apis) exposed by the _System.Diagnostics.Metrics_ APIs, and how to [use a source generator to produce values](https://andrewlock.net/creating-strongly-typed-metics-with-a-source-generator/). In this post, I look at how to _consume_ the stream of values produced by `Instrument` implementations in-process, using the `MeterListener` type.

I start by describing the scenario of an app that wants to record and process a specific subset of metrics exposed via the _System.Diagnostics.Metrics_ APIs. We'll create a simple app that generates some load, use `MeterListener` to listen for `Instrument` measurements, and display the results in a table using [Spectre.Console](https://spectreconsole.net/) (because everyone loves [Spectre.Console](https://spectreconsole.net/))!

> Note that I'm _not_ suggesting you use `MeterListener` directly in your production apps. In production, you'll likely want to use a solution like OpenTelemetry or Datadog that does all this work for you!

## [Creating the test ASP.NET Core app](#creating-the-test-asp-net-core-app)

As described above, for the purposes of this post, I created a simple "hello world" ASP.NET Core app using `dotnet new web`, and tweaked it so that it will send requests to itself, as long as the app is running:

    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    
    // Very basic hello-world app
    var builder = WebApplication.CreateBuilder(args);
    var app = builder.Build();
    
    app.MapGet("/", () => "Hello World!");
    
    var task = app.RunAsync();
    
    // Grab the address Kestrel's listening on
    var address = app.Services.GetRequiredService<IServer>()!
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
    
    try
    {
        // Run 4 loops in parallel, sending HTTP requests continuously
        // until the app gets the shut down notification
        await Parallel.ForAsync(0, 4, app.Lifetime.ApplicationStopping, async (i, ct) =>
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(address),
            };
    
            // Just keep hammering requests!
            while (!ct.IsCancellationRequested)
            {
                string _ = await httpClient.GetStringAsync("/");
            }
        });
    }
    catch (OperationCanceledException)
    {
        // expected on shutdown
    }
    
    // Wait for the final cleanup
    await task;
    

The code above isn't particularly pretty, but it does the following:

*   Creates a "hello world" minimal API ASP.NET Core app.
*   After the app starts up, it starts 4 parallel jobs
*   Each job has its own `HttpClient` and continuously makes HTTP requests to the app
*   ctrl+c in the console stops the requests and shut's down the app.

Now that we have this app, we can start grabbing some metrics out of it. We're aiming for something like the following, which shows the majority of metrics from [my previous post](https://andrewlock.net/creating-standard-and-observable-instruments/#system-diagnostics-metrics-apis/) in a [live-updating Spectre.Console](https://spectreconsole.net/console/how-to/live-rendering-and-dynamic-updates) [table](https://spectreconsole.net/console/how-to/displaying-tabular-data):

                                      ASP.NET Core Metrics                                  
    ┌────────────────────────────────────────────┬─────────────────────────┬─────────────┐
    │ Metric                                     │ Type                    │       Value │
    ├────────────────────────────────────────────┼─────────────────────────┼─────────────┤
    │ aspnetcore.routing.match_attempts          │ Counter                 │     250,428 │
    │ dotnet.gc.heap.total_allocated             │ ObservableCounter       │ 849,743,376 │
    │ http.server.active_requests                │ UpDownCounter           │           4 │
    │ dotnet.gc.last_collection.heap.size (gen0) │ ObservableUpDownCounter │   2,497,080 │
    │ dotnet.gc.last_collection.heap.size (gen1) │ ObservableUpDownCounter │     774,872 │
    │ dotnet.gc.last_collection.heap.size (gen2) │ ObservableUpDownCounter │   1,219,120 │
    │ dotnet.gc.last_collection.heap.size (loh)  │ ObservableUpDownCounter │      98,384 │
    │ dotnet.gc.last_collection.heap.size (poh)  │ ObservableUpDownCounter │      65,728 │
    │ process.cpu.utilization                    │ ObservableGauge         │         36% │
    │ http.server.request.duration               │ Histogram               │     0.011ms │
    │ http.server.request.duration (count)       │ Histogram               │     250,425 │
    └────────────────────────────────────────────┴─────────────────────────┴─────────────┘
    

To record the values from these metrics, we're going to use the `MeterListener` type.

## [Recording metrics with `MeterListener`](#recording-metrics-with-meterlistener)

In my previous post I discussed how `Instrument`s have both a consumer and a producer side. To consume the output of `Instrument`s inside your app you must subscribe to them using a `MeterListener`. To manage all this configuration, we'll create a helper type called `MetricManager`.

### [Creating a wrapper `MetricManager` for working with metrics](#creating-a-wrapper-metricmanager-for-working-with-metrics)

To encapsulate the collection and aggregation of metrics emitted by the _System.Diagnostics.Metrics_ APIs, I'm going to create a type called `MetricManager`. This is entirely optional, it's just helpful for my scenario. The public API for this type is shown below, which we'll be fleshing out shortly.

    public class MetricManager : IDisposable
    {
        public void Dispose();
        public MetricValues GetMetrics();
    }
    

The `MetricManager` is responsible for interacting with the _System.Diagnostics.Metrics_ APIs. And when you call `GetMetrics()`, the manager returns the values for each of the `Instruments` we listed above:

    public readonly record struct MetricValues(
        long TotalMatchAttempts,
        long TotalHeapAllocated,
        long ActiveRequests,
        long HeapSizeGen0,
        long HeapSizeGen1,
        long HeapSizeGen2,
        long HeapSizeLoh,
        long HeapSizePoh,
        double CpuUtilization,
        double AverageDuration,
        long TotalRequests);
    

Just to reiterate, this is not _required_. It's just how I've chosen in this post to expose the interactions with the _System.Diagnostics.Metrics_ APIs.

> Note also that I'm creating a very well-defined API here. If you want to have more of a "generalised" listener, that can listen to _all_ metrics, and records all the tags for those metrics, I strongly recommend looking at OpenTelemetry instead!

So we have our basic public API, now let's create a `MeterListener` and hook it up.

### [Creating a `MeterListener` and configuring callbacks](#creating-a-meterlistener-and-configuring-callbacks)

One of the design tenants of the _System.Diagnostics.Metrics_ APIs is that they should be high performance. Commonly for .NET, that mostly means "you don't need additional allocations". That shows up in some of the design of the `MeterListener` as you'll see shortly.

The code below shows how we would extend `MetricManager` to create a `MeterListener`, initialize it, and configure callbacks:

    public class MetricManager : IDisposable
    {
        private readonly MeterListener _listener;
    
        public MetricManager()
        {
            // Create a MeterListener, and configure the method to call
            // when a new instrument is published in the application
            _listener = new()
            {
                InstrumentPublished = OnInstrumentPublished
            };
    
            // Configure the callbacks to invoke when an Instrument emits a value.
            // In this case, we know that the .NET runtime instruments we listen to only
            // produce long or double values, so that's all we listen for here
            _listener.SetMeasurementEventCallback<long>(OnMeasurementRecordedLong);
            _listener.SetMeasurementEventCallback<double>(OnMeasurementRecordedDouble);
    
            // Start the listener, which invokes OnInstrumentPublished for already-published Instruments
            _listener.Start();
        }
    
        // Call Dispose on the listener to prevent further callbacks being invoked
        public void Dispose() => _listener.Dispose();
    
        // Callback invoked whenever an instrument is published
        private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
        {
            // ...
        }
    
        // Callback invoked whenever a `long` measurement is recorded
        private static void OnMeasurementRecordedLong(Instrument instrument, long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            // ...
        }
    
        // Callback invoked whenever a `double` measurement is recorded
        private static void OnMeasurementRecordedDouble(Instrument instrument, double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            // ...
        }
    }
    

I've heavily commented the code above, but I'll highlight some interesting points.

Firstly, the `OnInstrument` callback allows the listener to choose which `Meter`s and `Instrument`s it wants to subscribe to. This callback is invoked once for each existing `Instrument` when you call `MeterListener.Start()`, and is then subsequently invoked whenever a new `Meter` or `Instrument` is subsequently registered.

In addition, we have the `SetMeasurementEventCallback<T>()` method. This is a generic method, because it allows you to register a different callback for each _type_ of `Instrument` measurement you might receive. Instruments can be created with `byte`, `short`, `int`, `long`, `float`, `double`, and `decimal` types, so [it's recommended](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection#create-a-custom-collection-tool-using-the-net-meterlistener-api) that you register a callback for each of these types.

> Note that if you use a generic argument that _isn't_ one of these types, you'll get an exception at runtime.

This kind of API might seem a little unusual; having to register virtually identical callbacks for each different type feels a bit clumsy. But it's written this way for performance reasons. By having a dedicated callback for each supported `T`, you can avoid any allocation or overhead that would come from having a "generic" callback that would only work with `object`.

Also note that the callback you register doesn't _have_ to be different methods like I have used above. You _could_ also have a single generic method with a signature like this:

    static void OnMeasurementRecorded<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state);
    

However, you would still need to call `SetMeasurementEventCallback<T>` once for each measurement type you want to handle, for example:

    _listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
    _listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
    

We are yet to implement these measurement callbacks, but before we get to that, we'll take a look at the `OnInstrumentPublished()` callback.

### [Selecting which `Instrument`s to listen to](#selecting-which-instruments-to-listen-to)

The `MeterListener` is "connected" to all of the `Meter`s and `Instrument`s in the application, but it won't automatically receive measurements from all of them unless you enable each one. For this demo, we only care about a subset of `Meter`s and `Instrument`s, so our `OnInstrumentPublished()` callback uses a switch expression to check for specific values of `Instrument.Name` and `Meter.Name`:

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        string meterName = instrument.Meter.Name;
        string instrumentName = instrument.Name;
    
        // Is this a Meter and Instrument we care about?
        var enable = meterName switch
        {
            "Microsoft.AspNetCore.Routing" => instrumentName == "aspnetcore.routing.match_attempts",
            "System.Runtime"               => instrumentName is "dotnet.gc.heap.total_allocated"
                                                                or "dotnet.gc.last_collection.heap.size",
            "Microsoft.AspNetCore.Hosting" => instrumentName is "http.server.active_requests"
                                                                or "http.server.request.duration",
            "Microsoft.Extensions.Diagnostics.ResourceMonitoring" => instrumentName == "process.cpu.utilization",
            _ => false
        };
    
        if (enable)
        {
            // If yes, enable measurements, and pass the `MetricManager` as "state"
            listener.EnableMeasurementEvents(instrument, state: this);
        }
    }
    

To enable measurements, you call `MeterListener.EnableMeasurementEvents()`, passing in the `Instrument` to listen to. One interesting point here is that we're also passing the `MetricManager` as the `state` variable. This variable is passed in to our `OnMeasurementRecorded` callbacks and is a way of avoiding closures or expensive lookups in the callback events. You'll see how it's used shortly.

Note that if we were creating a generic implementation that listened to _all_ `Insturment`s emitted by the app, we could implement this method very simply:

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
        => listener.EnableMeasurementEvents(instrument, state: this);
    

So at this point we've enabled the instruments, we've called `MeterListener.Start()`, and it's time to start receiving some measurements!

### [Triggering `ObservableInstrument`s to emit measurements](#triggering-observableinstruments-to-emit-measurements)

Now that we've subscribed to the instruments, the `OnMeasurementRecorded` callbacks are invoked whenever an `Instrument` emits a value. For "standard" `Instrument`s, that happens immediately, whenever a value is recorded: add a value to a `Counter<long>`, and our `OnMeasurementRecorded` callback is immediately called. But that's not how it works for observable instruments.

In my [previous post](https://andrewlock.net/creating-standard-and-observable-instruments/#what-is-an-observable-instrument-), I described how observable instruments don't emit any values until the consumer _asks_ them to. Well, the consumer here is `MeterListener`, and it needs to ask all the `Instrument`s it is interested in to emit values when `GetMetrics()` is called:

    public MetricValues GetMetrics()
    {
        // This triggers the observable metrics to go and read the values and
        // then invoke the OnMeasurementRecorded callback to send the values to us
        _listener.RecordObservableInstruments();
    
        // ...
    }
    

Calling `RecordObservableInstruments()` triggers all the observable instruments that we enabled to emit a measurement (by invoking their associated callbacks, such as [those described in my previous post](https://andrewlock.net/creating-standard-and-observable-instruments/#observablecountert)). These measurements are then reported via the callbacks registered with the `MeterListener`.

Our `MeterListener` is now completely configured, so it's time to flesh out the `OnMeasurementRecorded` callbacks.

### [Recording `Instrument` measurements](#recording-instrument-measurements)

Whenever a measurement is recorded by an `Instrument`, the registered callback of the appropriate type is invoked (if you haven't registered an appropriate callback, none will be invoked). Exactly what you should _do_ with that metric depends on how you want to aggregate your data.

The following implementation of the `OnMeasurementRecordedLong` method shows one way to aggregate the data, focusing on displaying long running totals for the duration of the app:

    private static void OnMeasurementRecordedLong(Instrument instrument, long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var handler = (MetricManager)state!;
        switch (instrument.Name)
        {
            // Counter
            case "aspnetcore.routing.match_attempts":
                Interlocked.Add(ref handler._matchAttempts, measurement);
                break;
    
            // ObservableCounter
            case "dotnet.gc.heap.total_allocated":
                Interlocked.Exchange(ref handler._totalHeapAllocated, measurement);
                break;
    
            // UpDownCounter
            case "http.server.active_requests":
                Interlocked.Add(ref handler._activeRequests, measurement);
                break;
    
            // ObservableUpDownCounter
            case "dotnet.gc.last_collection.heap.size":
                foreach (var tag in tags)
                {
                    if (tag is { Key: "gc.heap.generation", Value: string gen })
                    {
                        switch (gen)
                        {
                            case "gen0": Interlocked.Exchange(ref handler._heapSizeGen0, measurement); break;
                            case "gen1": Interlocked.Exchange(ref handler._heapSizeGen1, measurement); break;
                            case "gen2": Interlocked.Exchange(ref handler._heapSizeGen2, measurement); break;
                            case "loh": Interlocked.Exchange(ref handler._heapSizeLoh, measurement); break;
                            case "poh": Interlocked.Exchange(ref handler._heapSizePoh, measurement); break;
                        }
                    }
                }
    
                break;
        }
    }
    

The first step is to cast the `state` object back to the `MetricManager` instance that we passed in when calling `EnableMeasurementEvents()`. We then switch based on the instrument name, and handle the measurement value differently depending on the instrument type:

*   For `Counter` and `UpDownCounter`, the callback is invoked once for every time a new value is recorded, with the `measurement` value as the increment. To create a running total of values, you must _add_ the new measurement to the current running total.
*   For `ObservableCounter` and `ObservableUpDownCounter`, the callback is only invoked when you call `RecordObservableInstruments()`. The `measurement` value in these cases _aren't_ incremental values, but rather they're the "final" current value, so you can use the value "as is" for the current running total.

You can see these rules applied in the above method, where the `Counter` and `UpDownCounter` metrics are aggregated using `Interlocked.Add()`, whereas the `ObservableCounter` and `ObservableUpDownCounter` metrics are "aggregated" by using `Interlocked.Exchange`.

Another interesting aspect is the handling of tags. The `"dotnet.gc.last_collection.heap.size"` is an `ObservableUpDownCounter`, so the values are emitted only when you call `RecordObservableInstruments()`. In this case, we receive one invocation of the callback _per generation_, with the `gc.heap.generation` tag indicating to which generation the current measurement applies.

In addition to the `OnMeasurementRecordedLong` callback, we also have the `OnMeasurementRecordedDouble` callback, which we use to record the `ObservableGauge` and `Histogram` metrics:

    private static void OnMeasurementRecordedDouble(Instrument instrument, double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var handler = (MetricManager)state!;
        switch (instrument.Name)
        {
            // ObservableGauge
            case "process.cpu.utilization":
                Interlocked.Exchange(ref handler._cpuUtilization, measurement);
                break;
    
            // Histogram
            case "http.server.request.duration":
                Interlocked.Increment(ref handler._totalRequestCount);
                lock (handler._durationLock)
                {
                    handler._intervalRequests++;
                    handler._totalDuration += measurement;
                }
    
                break;
        }
    }
    

The structure for this callback is very similar to the previous one:

*   We cast the `state` variable to our `MetricManager` instance that we passed in when registering the callback.
*   For the `ObservableGauge` (as for all of the observable instruments), we _replace_ our recorded value, using `Interlocked.Exchange()`
*   For the `Histogram`, there are many different ways we could aggregate the data, especially considering that these measurements contain a lot of high cardinality tags. I chose to calculate just two values from this data:
    *   The total number of requests since app start, stored in `_totalRequestCount`.
    *   The average request duration in the current time interval. This requires recording the number of requests (`_intervalRequests`) during the interval, and the sum of the durations of requests during the interval (`_totalDuration`). We'll use these values to calculate the average shortly.

Some of these measurements may be recorded concurrently with when while we are reading the values, which is why I've used `Interlocked` where possible, to make updates atomic. Where I couldn't use `Interlocked`, I used a `lock` for simplicity, though you should be careful about this in practice; in high performance applications it might be possible to run into lock contention, if many requests are trying to increment these values.

Now that all of our `Instrument`s are recording values, both standard and observable, it's time to report the results.

### [Reporting the results from `GetMetrics`](#reporting-the-results-from-getmetrics)

I have already partially shown the `GetMetrics()` implementation, in so far as it's where we called `RecordObservableInstruments()`. Other than triggering the observable measurements to be taken, all `GetMetrics()` does is read the values recorded in the fields, calculate the average duration, and return a `MetricValues` instance:

    public MetricValues GetMetrics()
    {
        // This triggers the observable metrics to go and read the values
        // and then call the OnMeasurement callbacks to send the values to us
        _listener.RecordObservableInstruments();
    
        // Read all of the values from the fields and return a MetricValues object
        return new MetricValues(
            TotalMatchAttempts: Interlocked.Read(ref _matchAttempts),
            TotalHeapAllocated: Interlocked.Read(ref _totalHeapAllocated),
            ActiveRequests: Interlocked.Read(ref _activeRequests),
            HeapSizeGen0: Interlocked.Read(ref _heapSizeGen0),
            HeapSizeGen1: Interlocked.Read(ref _heapSizeGen1),
            HeapSizeGen2: Interlocked.Read(ref _heapSizeGen2),
            HeapSizeLoh: Interlocked.Read(ref _heapSizeLoh),
            HeapSizePoh: Interlocked.Read(ref _heapSizePoh),
            CpuUtilization: Volatile.Read(ref _cpuUtilization),
            AverageDuration: ComputeAndResetAverageDuration(),
            TotalRequests: Interlocked.Read(ref _totalRequestCount)
        );
    
        double ComputeAndResetAverageDuration()
        {
            long count;
            double sum;
            lock (_durationLock)
            {
                // Grab the current values
                count = _intervalRequests;
                sum = _totalDuration;
                // Reset the values
                _intervalRequests = 0;
                _totalDuration = 0;
            }
    
            // Do the calculation
            return count > 0 ? sum / count : 0;
        }
    }
    

And with that, the implementation of `MetricManager` and its usage of `MeterListener` is complete. All that remains is to plug the listener into our app.

## [Creating a service to display the results](#creating-a-service-to-display-the-results)

To view the metrics being collected by `MetricManager` and its `MeterListener`, I created a `BackgroundService` that would render a [Spectre.Console](https://spectreconsole.net/) live table to the console, and update it periodically:

    using MyMetrics;
    using Spectre.Console;
    
    internal class MetricDisplayService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var manager = new MetricManager();
            
            var table = new Table()
                .Title("[bold]ASP.NET Core Metrics[/]")
                .Border(TableBorder.Rounded)
                .AddColumn("Metric")
                .AddColumn("Type")
                .AddColumn(new TableColumn("Value").RightAligned());
    
            table.AddRow("aspnetcore.routing.match_attempts", "Counter", "0");
            table.AddRow("dotnet.gc.heap.total_allocated", "ObservableCounter", "0");
            table.AddRow("http.server.active_requests", "UpDownCounter", "0");
            table.AddRow("dotnet.gc.last_collection.heap.size (gen0)", "ObservableUpDownCounter", "0");
            table.AddRow("dotnet.gc.last_collection.heap.size (gen1)", "ObservableUpDownCounter", "0");
            table.AddRow("dotnet.gc.last_collection.heap.size (gen2)", "ObservableUpDownCounter", "0");
            table.AddRow("dotnet.gc.last_collection.heap.size (loh)", "ObservableUpDownCounter", "0");
            table.AddRow("dotnet.gc.last_collection.heap.size (poh)", "ObservableUpDownCounter", "0");
            table.AddRow("process.cpu.utilization", "ObservableGauge", "0%");
            table.AddRow("http.server.request.duration", "Histogram", "0.000ms");
            table.AddRow("http.server.request.duration (count)", "Histogram", "0");
    
            await AnsiConsole.Live(table).StartAsync(async ctx =>
            {
                // This is the update loop, where we poll the `MetricManager`
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    RenderMetricValues(table, ctx, manager.GetMetrics());
                }
            });
        }
    
        private void RenderMetricValues(Table table, LiveDisplayContext ctx, in MetricManager.MetricValues values)
        {
            table.UpdateCell(0, 2, values.TotalMatchAttempts.ToString("N0"));
            table.UpdateCell(1, 2, values.TotalHeapAllocated.ToString("N0"));
            table.UpdateCell(2, 2, values.ActiveRequests.ToString("N0"));
            table.UpdateCell(3, 2, values.HeapSizeGen0.ToString("N0"));
            table.UpdateCell(4, 2, values.HeapSizeGen1.ToString("N0"));
            table.UpdateCell(5, 2, values.HeapSizeGen2.ToString("N0"));
            table.UpdateCell(6, 2, values.HeapSizeLoh.ToString("N0"));
            table.UpdateCell(7, 2, values.HeapSizePoh.ToString("N0"));
            table.UpdateCell(8, 2, $"{values.CpuUtilization:F0}%");
            table.UpdateCell(9, 2, $"{values.AverageDuration * 1000:F3}ms");
            table.UpdateCell(10, 2, values.TotalRequests.ToString("N0"));
            ctx.Refresh();
        }
    }
    

Most of this code is simply setting up the table, the "important" part in terms of the interaction with the `MetricManager` all takes place in the `AnsiConsole.Live` block:

    // As long as the app keeps running...
    while (!stoppingToken.IsCancellationRequested)
    {
        // ...wait 1 second...
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        // ...and then grab the metrics, and render them
        RenderMetricValues(table, ctx, manager.GetMetrics());
    }
    

All that remains is to plug our background service into our app:

    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Register the MetricDisplayService as an `IHostedService`
    builder.Services.AddHostedService<MetricDisplayService>();
    
    // Add the ResourceMonitoring package so that we can retrieve "process.cpu.utilization"
    builder.Services.AddResourceMonitoring();
    var app = builder.Build();
    
    app.MapGet("/", () => "Hello World!");
    
    app.Run();
    

and that's it! If we run the app, and generate some load, we'll see our metrics being reported to the console 🎉

    ┌────────────────────────────────────────────┬─────────────────────────┬─────────────┐
    │ Metric                                     │ Type                    │       Value │
    ├────────────────────────────────────────────┼─────────────────────────┼─────────────┤
    │ aspnetcore.routing.match_attempts          │ Counter                 │     250,428 │
    │ dotnet.gc.heap.total_allocated             │ ObservableCounter       │ 849,743,376 │
    │ http.server.active_requests                │ UpDownCounter           │           4 │
    │ dotnet.gc.last_collection.heap.size (gen0) │ ObservableUpDownCounter │   2,497,080 │
    │ dotnet.gc.last_collection.heap.size (gen1) │ ObservableUpDownCounter │     774,872 │
    │ dotnet.gc.last_collection.heap.size (gen2) │ ObservableUpDownCounter │   1,219,120 │
    │ dotnet.gc.last_collection.heap.size (loh)  │ ObservableUpDownCounter │      98,384 │
    │ dotnet.gc.last_collection.heap.size (poh)  │ ObservableUpDownCounter │      65,728 │
    │ process.cpu.utilization                    │ ObservableGauge         │         36% │
    │ http.server.request.duration               │ Histogram               │     0.011ms │
    │ http.server.request.duration (count)       │ Histogram               │     250,425 │
    └────────────────────────────────────────────┴─────────────────────────┴─────────────┘
    

And with that we reach the end. Our app is able to report metrics about itself, and report those in any way it sees fit. In this example we just blindly report them to the console, but you could do anything with them. That said, if you're thinking of doing anything _serious_ with these metrics, you should likely consider using [the OpenTelemetry libraries](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel) instead!

## [Summary](#summary)

In this post I describe the scenario of an app that wants to record and process a specific subset of metrics exposed via the _System.Diagnostics.Metrics_ APIs. I then show a simple app that generates some load, use `MeterListener` to listen for `Instrument` measurements, and display the results in a table using [Spectre.Console](https://spectreconsole.net/). Along the way I show the difference between the standard `Instrument` and `ObservableInstrument` measurements, show how to trigger observable measurements to be reported, and discuss performance aspects, such as passing state to the callback functions.

 [![Creating standard and "observable" instruments](https://andrewlock.net/content/images/2026/instruments.png) Previous Creating standard and "observable" instruments: System.Diagnostics.Metrics APIs - Part 3](https://andrewlock.net/creating-standard-and-observable-instruments/)
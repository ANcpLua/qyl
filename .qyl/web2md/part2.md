---
Author: Andrew Lock
Site Name: Andrew Lock | .NET Escapades
Url: https://andrewlock.net/creating-strongly-typed-metics-with-a-source-generator/
Published: 2026-02-03
Extracted Date: 2026-03-04
---

February 03, 2026 ~13 min read

[System.Diagnostics.Metrics APIs - Part 2](https://andrewlock.net/series/system-diagnostics-metrics-apis/)

This is the second post in the series: [System.Diagnostics.Metrics APIs](https://andrewlock.net/series/system-diagnostics-metrics-apis/).

1.  [Part 1 - Creating and consuming metrics with System.Diagnostics.Metrics APIs](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/)
2.  Part 2 - Exploring the (underwhelming) System.Diagnostics.Metrics source generators (this post)
3.  [Part 3 - Creating standard and "observable" instruments](https://andrewlock.net/creating-standard-and-observable-instruments/)
4.  [Part 4 - Recording metrics in-process using MeterListener](https://andrewlock.net/recording-metrics-in-process-using-meterlistener/)

In my [previous post](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/) I provided an introduction to the _System.Diagnostics.Metrics_ APIs introduced in .NET 6. In this post I show how to use the [Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions) source generator, explore how it changes the code you need to write, and explore the generated code.

I start the post with a quick refresher on the basics of the _System.Diagnostics.Metrics_ APIs and the sample app we wrote [last time](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/). I then show how we can update this code to use the _Microsoft.Extensions.Telemetry.Abstractions_ source generator instead. Finally, I show how we can also update our metric definitions to use strongly-typed tag objects for additional type-safety. In both cases, we'll update our sample app to use the new approach, and explore the generated code.

> You can read about the source generators I discuss in this post in the Microsoft documentation [here](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-generator) and [here](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-strongly-typed).

## [Background: System.Diagnostics.Metrics APIs](#background-system-diagnostics-metrics-apis)

The _System.Diagnostics.Metrics_ APIs were introduced in .NET 6 but are available in earlier runtimes (including .NET Framework) by using the [_System.Diagnostics.DiagnosticSource_](https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/) NuGet package. There are two primary concepts exposed by these APIs; `Instrument` and `Meter`:

*   `Instrument`: An instrument records the values for a single metric of interest. You might have separate `Instrument`s for "products sold", "invoices created", "invoice total", or "GC heap size".
*   `Meter`: A `Meter` is a logical grouping of multiple instruments. For example, the [`System.Runtime` `Meter`](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime) contains multiple `Instrument`s about the workings of the runtime, while [the `Microsoft.AspNetCore.Hosting` `Meter`](https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/built-in?view=aspnetcore-10.0#microsoftaspnetcorehosting) contains `Instrument`s about the HTTP requests received by ASP.NET Core.

There are also multiple types of `Instrument`: `Counter<T>`, `UpDownCounter<T>`, `Gauge<T>`, and `Histogram<T>` (as well as "observable" versions, which I'll cover in a future post). To create a custom metric, you need to choose the type of `Instrument` to use, and associate it with a `Meter`. In my [previous post](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/) I created a simple `Counter<T>` for tracking how often a product page was viewed.

## [Background: sample app with manual boilerplate](#background-sample-app-with-manual-boilerplate)

In this post I'm going to start from where we left off in the previous post, and update it to use a source generator instead. So that we know where we're coming from, the full code for that sample is shown below, annotated to explain what's going on; for the full details, see my [previous post](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/)

    using System.Diagnostics.Metrics;
    using Microsoft.Extensions.Diagnostics.Metrics;
    
    var builder = WebApplication.CreateBuilder(args);
    
    // 👇 Register our "metrics helper" in DI
    builder.Services.AddSingleton<ProductMetrics>();
    
    var app = builder.Build();
    
    // Inject the "metrics helper" into the API handler 👇 
    app.MapGet("/product/{id}", (int id, ProductMetrics metrics) =>
    {
        metrics.PricingPageViewed(id); // 👈 Record the metric
        return $"Details for product {id}";
    });
    
    app.Run();
    
    
    // The "metrics helper" class for our metrics
    public class ProductMetrics
    {
        private readonly Counter<long> _pricingDetailsViewed;
    
        public ProductMetrics(IMeterFactory meterFactory)
        {
            // Create a meter called MyApp.Products
            var meter = meterFactory.Create("MyApp.Products");
    
            // Create an instrument, and associate it with our meter
            _pricingDetailsViewed = meter.CreateCounter<int>(
                "myapp.products.pricing_page_requests",
                unit: "requests",
                description: "The number of requests to the pricing details page for the product with the given product_id");
    
        }
    
        // A convenience method for adding to the metric
        public void PricingPageViewed(int id)
        {
            // Ensure we add the correct tag to the metric
            _pricingDetailsViewed.Add(delta: 1, new KeyValuePair<string, object?>("product_id", id));
        }
    }
    

In summary, we have a `ProductMetrics` "metrics helper" class which is responsible for creating the `Meter` and `Instrument` definitions, as well as providing helper methods for recording page views.

When we run the app and [monitor it with `dotnet-counters`](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/#collecting-metrics-with-dotnet-counters) we can see our metric being recorded:

![Showing the metrics being reported using dotnet-counters](https://andrewlock.net/content/images/2026/metrics.png)

Now that we have our sample app ready, lets explore replacing some of the boilerplate with a source generator.

## [Replacing boiler plate with a source generator](#replacing-boiler-plate-with-a-source-generator)

The [Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions) NuGet package includes a source generator which, according to [the documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-generator?tabs=dotnet-cli), generates code which:

> …exposes strongly typed metering types and methods that you can invoke to record metric values. The generated methods are implemented in a highly efficient form, which reduces computation overhead as compared to traditional metering solutions.

In this section we'll replace some of the code we wrote above with the source generated equivalent!

First you'll need to install the [Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions) package in your project using:

    dotnet add package Microsoft.Extensions.Telemetry.Abstractions
    

Alternatively, update your project with a `<PackageReference>`:

    <ItemGroup>
      <PackageReference Include="Microsoft.Extensions.Telemetry.Abstractions" Version="10.2.0" />
    </ItemGroup>
    

> Note that in this post I'm using the latest stable version of the package, 10.2.0.

Now that we have the source generator running in our app, we can put it to use.

### [Creating the "metrics helper" class](#creating-the-metrics-helper-class)

The main difference when you switch to the source generator is in the "metrics helper" class. There's a lot of different ways you _could_ structure these—what I've shown below is a relatively close direct conversion of the previous code. But as I'll discuss later, this isn't necessarily the way you'll always want to use them.

As is typical for source generators, the metrics generator is driven by specific attributes. There's a different attribute for each `Instrument` type, and you apply them to a `partial` method definition which creates a strongly-typed metric, called `PricingPageViewed` in this case:

    private static partial class Factory
    {
        [Counter<int>("product_id", Name = "myapp.products.pricing_page_requests")]
        internal static partial PricingPageViewed CreatePricingPageViewed(Meter meter);
    }
    

The example above uses the `[Counter<T>]` attribute, but there are equivalent versions for `[Gauge<T>]` and `[Histogram<T>]` too.

This creates the "factory" methods for defining a metric, but we still need to update the `ProductMetrics` type to _use_ this factory method instead of our hand-rolled versions:

    // Note, must be partial
    public partial class ProductMetrics
    {
        public ProductMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("MyApp.Products");
            PricingPageViewed = Factory.CreatePricingPageViewed(meter);
        }
    
        internal PricingPageViewed PricingPageViewed { get; }
    
        private static partial class Factory
        {
            [Counter<int>("product_id", Name = "myapp.products.pricing_page_requests")]
            internal static partial PricingPageViewed CreatePricingPageViewed(Meter meter);
        }
    }
    

If you compare that to the code we wrote previously, there are two main differences:

*   The `[Counter<T>]` attribute is missing the "description" and "units" that we previously added.
*   The `PricingPageViewed` metric is exposed directly (which we'll look at shortly), instead of exposing a `PricingPageViewed()` method for recording values.

The first point is just a limitation of the current API. We actually _can_ specify the units on the attribute, but if we do, we need to add a `#pragma` as this API is currently experimental:

    private static partial class Factory
    {
        #pragma warning disable EXTEXP0003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    
                                                            //   Add the Unit here 👇
        [Counter<int>("product_id", Name = "myapp.products.pricing_page_requests", Unit = "views")]
        internal static partial PricingPageViewed CreatePricingPageViewed(Meter meter);
    }
    

The second point is more interesting, and we'll dig into it when we look at the generated code.

### [Updating our app](#updating-our-app)

Before we get to the generated code, lets look at how we use our updated `ProductMetrics`. We keep the existing DI registration of our `ProductMetrics` type, the only change is how we _record_ a view of the page

    using System.Diagnostics.Metrics;
    using System.Globalization;
    using Microsoft.Extensions.Diagnostics.Metrics;
    
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSingleton<ProductMetrics>();
    var app = builder.Build();
    
    app.MapGet("/product/{id}", (int id, ProductMetrics metrics) =>
    {
        // Update to call PricingPageViewed.Add() instead of PricingPageViewed(id)
        metrics.PricingPageViewed.Add(value: 1, product_id: id);
        return $"Details for product {id}";
    });
    
    app.Run();
    

As you can see, there's not much change there. Instead of calling `PricingPageViewed(id)`, which internally adds a metric and tag, we call the `Add()` method, which is a source-generated method on the `PricingPageViewed` type. Let's take a look at all that generated code now, so we can see what's going on behind the scenes.

### [Exploring the generated code](#exploring-the-generated-code)

We have various generated methods to look at, so we'll start with our factory methods and work our way through from there.

> Note that in most IDEs you can navigate to the definitions of these partial methods and they'll show you the generated code.

Starting with our `Factory` method, the generated code looks like this:

    public partial class ProductMetrics 
    {
        private static partial class Factory 
        {
            internal static partial PricingPageViewed CreatePricingPageViewed(Meter meter)
                => GeneratedInstrumentsFactory.CreatePricingPageViewed(meter);
        }
    }
    

So the generated code is calling a _different_ generated type, which looks like this:

    internal static partial class GeneratedInstrumentsFactory
    {
        private static ConcurrentDictionary<Meter, PricingPageViewed> _pricingPageViewedInstruments = new();
    
        internal static PricingPageViewed CreatePricingPageViewed(Meter meter)
        {
            return _pricingPageViewedInstruments.GetOrAdd(meter, static _meter =>
                {
                    var instrument = _meter.CreateCounter<int>(@"myapp.products.pricing_page_requests", @"views");
                    return new PricingPageViewed(instrument);
                });
        }
    }
    

This definition shows something interesting, in that it shows the source generator is catering to a pattern I was somewhat surprised to see. This code seems to be catering to adding the same `Instrument` to _multiple_ `Meter`s.

> That seems a little surprising to me, but that's possibly because I'm used to thinking in terms of OpenTelemetry expectations, which doesn't have the concept of `Meter`s (as far as I know), and completely ignores it. It seems like you would get some weird duplication issues if you tried to use this source-generator-suggested pattern with OpenTelemetry, so I personally wouldn't recommend it.

Other than the "dictionary" aspect, this generated code is basically creating the `Counter` instance, just as we were doing before, but is then passing it to a different generated type, the `PricingPageViewed` type:

    internal sealed class PricingPageViewed
    {
        private readonly Counter<int> _counter;
        public PricingPageViewed(Counter<int> counter)
        {
            _counter = counter;
        }
    
        public void Add(int value, object? product_id)
        {
            var tagList = new TagList
            {
                new KeyValuePair<string, object?>("product_id", product_id),
            };
    
            _counter.Add(value, tagList);
        }
    }
    

This generated type provides roughly the same "public" API for recording metrics as we provided before:

    public class ProductMetrics
    {
        // Previous implementation
        public void PricingPageViewed(int id)
        {
            _pricingDetailsViewed.Add(delta: 1, new KeyValuePair<string, object?>("product_id", id));
        }
    }
    

However, there are some differences. The generated code uses a more "generic" version that wraps the type in a `TagList`. This is a `struct`, which can support adding multiple tags without needing to allocate an array on the heap, so it's _generally_ very efficient. But in this case, it doesn't add anything over the "manual" version I implemented.

So given all that, is this generated code actually _useful_?

### [Is the generated code worth it?](#is-the-generated-code-worth-it-)

I love source generators, I think they're a great way to reduce boilerplate and make code easier to read and write in many cases, but frankly, I don't really see the value of this metrics source generator.

For a start, the source generator is only really changing how we define and create metrics. Which is generally 1 line of code to create the metric, and then a helper method for defining the tags etc (i.e. the `PricingPageViewed()` method). Is a source generator _really_ necessary for that?

Also, the generator is limited in the API it provides compared to calling the _System.Diagnostics.Metrics_ APIs directly. You can't provide a `Description` for a metric, for example, and providing a `Unit` needs a `#pragma`…

What's more, the fact that the generated code is generic, means that the resulting usability is actually _worse_ in my example, because you have to call:

    metrics.PricingPageViewed.Add(value: 1, product_id: id);
    

and specify an "increment" value, as opposed to simply being

    metrics.PricingPageViewed(productId: id);
    

(also note the "correct" argument names in my "manual case"). The source generator also seems to support scenarios that I don't envision needing (the same `Instrument` registered with multiple `Meter`), so that's extra work that need not happen in the source generated case.

So unfortunately, in this simple example, the source generator seems like a net loss. But there's an additional scenario it supports: strongly-typed tag objects

## [Using strongly-typed tag objects](#using-strongly-typed-tag-objects)

There's a common programming bug when calling methods that have multiple parameters of the same type: accidentally passing values in the wrong position:

    Add(order.Id, product.Id); // Oops, those are wrong, but it's not obvious!
    
    public void Add(int productId, int orderId) { /* */ }
    

One partial solution to this issue is to use strongly-typed objects to try to make the mistake more obvious. For example, if the method above instead took an object:

    public void Add(Details details) { /* */ }
    
    public readonly struct Details
    {
        public required int OrderId { get; init; }
        public required int ProductId { get; init; }
    }
    

Then at the callsite, you're _less_ likely to make the same mistake:

    // Still wrong, but the error is more obvious! 😅
    Add(new()
    {
        OrderId = product.Id,
        ProductId = order.Id,
    });
    

It turns out that passing lots of similar values is exactly the issue you run into when you need to add multiple tags when recording a value with an `Instrument`. To help with this, the source generator code can optionally use strongly-typed tag objects instead of a list of parameters.

### [Updating the holder class with strongly-typed tags](#updating-the-holder-class-with-strongly-typed-tags)

In the examples I've shown so far, I've only been attaching a single tag to the `PricingPageViewed` metric, but I'll add an additional one, `environment` just for demonstration purposes.

Let's again start by updating the `Factory` class to use a strongly-typed object instead of "manually" defining the tags:

    private static partial class Factory
    {
        // A Type that defines the tags 👇
        [Counter<int>(typeof(PricingPageTags), Name = "myapp.products.pricing_page_requests")]
        internal static partial PricingPageViewed CreatePricingPageViewed(Meter meter);
        // previously:
        // [Counter<int>("product_id", Name = "myapp.products.pricing_page_requests")]
        // internal static partial PricingPageViewed CreatePricingPageViewed(Meter meter);
    }
    
    public readonly struct PricingPageTags
    {
        [TagName("product_id")]
        public required string ProductId { get; init; }
        public required Environment Environment { get; init; }
    }
    
    public enum Environment
    {
        Development,
        QA,
        Production,
    }
    

So we have two changes:

*   We're passing a `Type` in the `[Counter<T>]` attribute, instead of a list of tag arguments.
*   We've defined a struct type that includes all the tags we want to add to a value.
    *   This is defined as a `readonly struct` to avoid additional allocations.
    *   We specific the tag name for `ProductId`. By default, `Environment` uses the name `"Environment"` (which may not be what you want, but this is for demo reasons!).
    *   We can only use `string` or `enum` types in the tags

The source generator then does its thing, and so we need to update our API callsite to this:

    app.MapGet("/product/{id}", (int id, ProductMetrics metrics) =>
    {
        metrics.PricingPageViewed.Add(1, new PricingPageTags()
        {
             ProductId = id.ToString(CultureInfo.InvariantCulture),
             Environment = ProductMetrics.Environment.Production,
        });
        return $"Details for product {id}";
    });
    

In the generated code we need to pass a `PricingPageTags` object into the `Add()` method, instead of individually passing each tag value.

> Note that we had to pass a `string` for `ProductId`, we can't use an `int` like we were before. That's not _great_ perf wise, but previously we were boxing the `int` to an `object?` so _that_ wasn't great either😅 Avoiding this allocation would be recommended if possible, but that's out of the scope for this post!

As before, let's take a look at the generated code.

### [Exploring the generated code](#exploring-the-generated-code-1)

The generated code in this case is almost identical to before. The only difference is in the generated `Add` method:

    internal sealed class PricingPageViewed
    {
        private readonly Counter<int> _counter;
    
        public PricingPageViewed(Counter<int> counter)
        {
            _counter = counter;
        }
    
        public void Add(int value, PricingPageTags o)
        {
            var tagList = new TagList
            {
                new KeyValuePair<string, object?>("product_id", o.ProductId!),
                new KeyValuePair<string, object?>("Environment", o.Environment.ToString()),
            };
    
            _counter.Add(value, tagList);
        }
    }
    

This generated code is _almost_ the same as before. The only difference is that it's "splatting" the `PricingPageTags` object as individual tags in a `TagList`. So, does _this_ mean the source generator is worth it?

## [Are the source generators worth using?](#are-the-source-generators-worth-using-)

From my point of view, the strongly-typed tags scenario doesn't change any of the arguments I raised previously against the source generator. It's still mostly obfuscating otherwise simple APIs, not adding anything performance-wise as far as I can tell, and it still supports the "`Instrument` in multiple `Meter` scenario" that seems unlikely to be useful (to me, anyway).

The strongly-typed tags approach shown here, while nice, can just as easily be implemented manually. The generated code isn't really _adding_ much. And in fact, given that it's calling `ToString()` on an `enum` ([which is known to be slow](https://andrewlock.net/updates-to-netescapaades-enumgenerators-new-apis-and-system-memory-support/#why-should-you-use-an-enum-source-generator-)), the "manual" version can _likely_ also provide better opportunities for performance optimizations.

About the only argument I can see in favour of using the source generator is if you're using the "`Instrument` in multiple `Meter`" approach (let me know in the comments if you are, I feel like I'm missing something!). Or, I guess, if you just _like_ the attribute-based generator approach and aren't worried about the points I raised. I'm a fan of source generators in general, but in this case, I don't think I would bother with them personally.

Overall, the fact the generators don't really add much maybe just points to the _System.Diagnostics.Metrics_ APIs being well defined? If you don't need much boilerplate to create the metrics, and you get the "best performance" by default, _without_ needing a generator, then that seems like a _good_ thing 😄

## [Summary](#summary)

In this post I showed how to use the source generators that ship in the [Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions) to help generating metrics with the _System.Diagnostics.Metrics_ APIs. I show how the source generator changes the way you define your metric, but fundamentally generates roughly the same code as [in my previous post](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/). I then show how you can also create strongly-typed tags, which helps avoid a typical class of bugs.

Overall, I didn't feel like the source generator saved much in the way of the code you write or provides performance benefits, unlike many other built-in source generators. The generated code caters to additional scenarios, such as registering the same `Instrument` with multiple `Meter`s, but that seems like a niche scenario.

 [![Creating and consuming metrics with System.Diagnostics.Metrics APIs](https://andrewlock.net/content/images/2026/metrics_banner.png) Previous Creating and consuming metrics with System.Diagnostics.Metrics APIs: System.Diagnostics.Metrics APIs - Part 1](https://andrewlock.net/creating-and-consuming-metrics-with-system-diagnostics-metrics-apis/) [![Creating standard and "observable" instruments](https://andrewlock.net/content/images/2026/instruments.png) Next Creating standard and "observable" instruments: System.Diagnostics.Metrics APIs - Part 3](https://andrewlock.net/creating-standard-and-observable-instruments/)
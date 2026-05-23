using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Qyl.Instrumentation.Generators;
using Xunit;

namespace Qyl.Instrumentation.Generators.Tests;

public sealed class MeterEmitterTests
{
    [Fact]
    public void Multi_Tag_Measurements_Use_TagList_Instead_Of_Array()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class TagAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Histogram("myapp.request.duration", Unit = "ms", Description = "Request duration.")]
                    public static partial void RecordRequest(
                        double value,
                        [Tag("route")] string route,
                        [Tag("status_code")] int statusCode);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("var tags = new global::System.Diagnostics.TagList { { \"route\", route }, { \"status_code\", statusCode } };")
            .And.Contain("_myappRequestDuration.Record(value, in tags);")
            .And.NotContain("new global::System.Collections.Generic.KeyValuePair<string, object?>[]");
    }

    [Fact]
    public void Gauge_Measurements_Record_Through_Standard_Gauge()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class GaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class TagAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Gauge("myapp.queue.depth", Unit = "{item}", Description = "Queued items.")]
                    public static partial void RecordQueueDepth(
                        long value,
                        [Tag("queue")] string queue,
                        [Tag("priority")] string priority);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("private static readonly global::System.Diagnostics.Metrics.Gauge<long> _myappQueueDepth =")
            .And.Contain("_meter.CreateGauge<long>(\"myapp.queue.depth\", \"{item}\", \"Queued items.\");")
            .And.Contain("var tags = new global::System.Diagnostics.TagList { { \"queue\", queue }, { \"priority\", priority } };")
            .And.Contain("_myappQueueDepth.Record(value, in tags);")
            .And.NotContain("ObservableGauge")
            .And.NotContain("_currentMyappQueueDepth");
    }

    [Fact]
    public void Standard_Instruments_Without_Value_Parameters_Are_Not_Emitted_Except_Parameterless_Counters()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class GaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class UpDownCounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.events")]
                    public static partial void AddEvent();

                    [Histogram("myapp.latency")]
                    private static partial void RecordLatency();

                    [Gauge("myapp.depth")]
                    private static partial void RecordDepth();

                    [UpDownCounter("myapp.inflight")]
                    private static partial void AddInflight();
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("_meter.CreateCounter<long>(\"myapp.events\")")
            .And.Contain("_myappEvents.Add(1);")
            .And.NotContain("myapp.latency")
            .And.NotContain("myapp.depth")
            .And.NotContain("myapp.inflight")
            .And.NotContain("Record(value)")
            .And.NotContain("Add(value)");
    }

    [Fact]
    public void Instruments_With_Unsupported_Value_Types_Are_Not_Emitted()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableGaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.events")]
                    public static partial void AddEvent();

                    [Histogram("myapp.label")]
                    private static partial void RecordLabel(string value);

                    [Counter("myapp.flag")]
                    private static partial void AddFlag(bool value);

                    [ObservableGauge("myapp.state")]
                    private static string ObserveState() => "ready";
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("_meter.CreateCounter<long>(\"myapp.events\")")
            .And.NotContain("myapp.label")
            .And.NotContain("myapp.flag")
            .And.NotContain("myapp.state")
            .And.NotContain("Histogram<string>")
            .And.NotContain("Counter<bool>")
            .And.NotContain("ObservableGauge<string>");
    }

    [Fact]
    public void Colliding_Metric_Names_Get_Unique_Field_Names()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.cache-hit")]
                    public static partial void RecordCacheHyphenHit();

                    [Counter("myapp.cache.hit")]
                    public static partial void RecordCacheDotHit();
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("private static readonly global::System.Diagnostics.Metrics.Counter<long> _myappCacheHit =")
            .And.Contain("private static readonly global::System.Diagnostics.Metrics.Counter<long> _myappCacheHit2 =")
            .And.Contain("_myappCacheHit.Add(1);")
            .And.Contain("_myappCacheHit2.Add(1);");
    }

    [Fact]
    public void Valued_Counter_Uses_Method_Value_Type()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class TagAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.cost", Unit = "USD", Description = "Cost total.")]
                    public static partial void AddCost(double value, [Tag("provider")] string provider);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("private static readonly global::System.Diagnostics.Metrics.Counter<double> _myappCost =")
            .And.Contain("_meter.CreateCounter<double>(\"myapp.cost\", \"USD\", \"Cost total.\");")
            .And.Contain("_myappCost.Add(value, new global::System.Collections.Generic.KeyValuePair<string, object?>(\"provider\", provider));");
    }

    [Fact]
    public void Standard_Metric_Partial_Implementations_Preserve_Method_Accessibility()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.public")]
                    public static partial void AddPublic();

                    [Counter("myapp.internal")]
                    internal static partial void AddInternal(long value);

                    [Histogram("myapp.private")]
                    private static partial void RecordPrivate(double value);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("public static partial void AddPublic()")
            .And.Contain("internal static partial void AddInternal(long value)")
            .And.Contain("private static partial void RecordPrivate(double value)")
            .And.NotContain("public static partial void AddInternal")
            .And.NotContain("public static partial void RecordPrivate");
    }

    [Fact]
    public void Meter_Partial_Class_Preserves_Source_Type_Modifiers()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.requests")]
                    public static partial void AddRequest();
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("public static partial class MyAppMetrics")
            .And.NotContain("\n    partial class MyAppMetrics");
    }

    [Fact]
    public void Nested_Meter_Class_Generates_Inside_Containing_Partial_Type()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableGaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                public static partial class Diagnostics
                {
                    [Meter("myapp.metrics")]
                    public static partial class MyAppMetrics
                    {
                        [ObservableGauge("myapp.queue.depth")]
                        private static long ObserveQueueDepth() => 42;
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("    public static partial class Diagnostics\n    {\n        public static partial class MyAppMetrics")
            .And.Contain("[global::System.Runtime.CompilerServices.ModuleInitializer]")
            .And.Contain("internal static void __QylInitializeObservableInstruments()")
            .And.Contain("MyAppMetrics.__QylInitializeObservableInstruments();")
            .And.NotContain("global::MyApp.Diagnostics.MyAppMetrics.__QylInitializeObservableInstruments();")
            .And.NotContain("QylObservableMeterInitializer")
            .And.NotContain("\n    public static partial class MyAppMetrics");
    }

    [Fact]
    public void Private_Nested_Observable_Meter_Generates_Accessible_Module_Initializer()
    {
        const string source = """
            using System;

            namespace System.Diagnostics.Metrics
            {
                public sealed class Meter
                {
                    public Meter(string name)
                    {
                    }

                    public ObservableGauge<T> CreateObservableGauge<T>(
                        string name,
                        Func<T> observeValue,
                        string? unit = null,
                        string? description = null) => new();
                }

                public sealed class ObservableGauge<T>;
            }

            namespace Qyl.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
                public sealed class GeneratedMeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }

                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableGaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                public static partial class Diagnostics
                {
                    [Meter("myapp.metrics")]
                    private static partial class MyAppMetrics
                    {
                        [ObservableGauge("myapp.queue.depth")]
                        private static long ObserveQueueDepth() => 42;
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);
        GeneratorTestHelper.AssertCompilationSucceeds<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("    public static partial class Diagnostics\n    {\n        private static partial class MyAppMetrics")
            .And.Contain("[global::System.Runtime.CompilerServices.ModuleInitializer]")
            .And.Contain("internal static void __QylInitializeObservableInstruments()")
            .And.Contain("_ = _myappQueueDepth;")
            .And.Contain("MyAppMetrics.__QylInitializeObservableInstruments();")
            .And.NotContain("global::MyApp.Diagnostics.MyAppMetrics.__QylInitializeObservableInstruments();")
            .And.NotContain("QylObservableMeterInitializer");
    }

    [Fact]
    public void Nested_Meter_Class_Under_Non_Partial_Containing_Type_Is_Not_Emitted()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.valid")]
                public static partial class ValidMetrics
                {
                    [Counter("myapp.valid.requests")]
                    public static partial void AddRequest();
                }

                public class Diagnostics
                {
                    [Meter("myapp.invalid")]
                    public static partial class InvalidMetrics
                    {
                        [Counter("myapp.invalid.requests")]
                        public static partial void AddInvalidRequest();
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("myapp.valid.requests")
            .And.NotContain("myapp.invalid")
            .And.NotContain("InvalidMetrics")
            .And.NotContain("AddInvalidRequest");
    }

    [Fact]
    public void Generic_Meter_Type_Shapes_Are_Not_Emitted()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.valid")]
                public static partial class ValidMetrics
                {
                    [Counter("myapp.valid.requests")]
                    public static partial void AddRequest();
                }

                [Meter("myapp.generic")]
                public static partial class GenericMetrics<T>
                {
                    [Counter("myapp.generic.requests")]
                    public static partial void AddGenericRequest();
                }

                public partial class Diagnostics<T>
                {
                    [Meter("myapp.containing.generic")]
                    public static partial class NestedMetrics
                    {
                        [Counter("myapp.containing.generic.requests")]
                        public static partial void AddNestedRequest();
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("myapp.valid.requests")
            .And.NotContain("myapp.generic")
            .And.NotContain("myapp.containing.generic")
            .And.NotContain("GenericMetrics")
            .And.NotContain("NestedMetrics")
            .And.NotContain("AddGenericRequest")
            .And.NotContain("AddNestedRequest");
    }

    [Fact]
    public void Escaped_CSharp_Identifiers_Are_Preserved_In_Generated_Meter_Code()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class TagAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace @event
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.keyword")]
                public static partial class @class
                {
                    [Counter("myapp.keyword.requests")]
                    public static partial void @default(long @long, [Tag("route")] string @string);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("namespace @event")
            .And.Contain("public static partial class @class")
            .And.Contain("public static partial void @default(long @long, string @string)")
            .And.Contain("_myappKeywordRequests.Add(@long, new global::System.Collections.Generic.KeyValuePair<string, object?>(\"route\", @string));")
            .And.NotContain("namespace event")
            .And.NotContain("partial class class")
            .And.NotContain("partial void default")
            .And.NotContain("long long")
            .And.NotContain("string string");
    }

    [Fact]
    public void Standard_Metric_Partial_Implementations_Preserve_Value_Parameter_Name()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class TagAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Histogram("myapp.request.duration")]
                    public static partial void RecordDuration(
                        double durationMs,
                        [Tag("route")] string route);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("public static partial void RecordDuration(double durationMs, string route)")
            .And.Contain("_myappRequestDuration.Record(durationMs, new global::System.Collections.Generic.KeyValuePair<string, object?>(\"route\", route));")
            .And.NotContain("RecordDuration(double value, string route)")
            .And.NotContain("_myappRequestDuration.Record(value");
    }

    [Fact]
    public void Standard_Instruments_With_Unsupported_Partial_Method_Shapes_Are_Not_Emitted()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.valid")]
                    public static partial void AddValid();

                    [Counter("myapp.returning")]
                    public static partial long AddReturning();

                    [Histogram("myapp.generic")]
                    public static partial void RecordGeneric<T>(double value);

                    [Counter("myapp.byref")]
                    public static partial void AddByRef(ref long value);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generated.Should()
            .Contain("_meter.CreateCounter<long>(\"myapp.valid\")")
            .And.NotContain("myapp.returning")
            .And.NotContain("myapp.generic")
            .And.NotContain("myapp.byref")
            .And.NotContain("AddReturning")
            .And.NotContain("RecordGeneric")
            .And.NotContain("AddByRef");
    }

    [Fact]
    public void Observable_Gauge_Callback_Generates_Observable_Instrument_And_Module_Initializer()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableGaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [ObservableGauge("myapp.queue.depth", Unit = "{item}", Description = "Queued items.")]
                    private static long ObserveQueueDepth() => 42;
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("private static readonly global::System.Diagnostics.Metrics.ObservableGauge<long> _myappQueueDepth =")
            .And.Contain("_meter.CreateObservableGauge<long>(\"myapp.queue.depth\", new global::System.Func<long>(ObserveQueueDepth), \"{item}\", \"Queued items.\");")
            .And.Contain("internal static void __QylInitializeObservableInstruments()")
            .And.Contain("_ = _myappQueueDepth;")
            .And.Contain("[global::System.Runtime.CompilerServices.ModuleInitializer]")
            .And.NotContain("global::MyApp.MyAppMetrics.__QylInitializeObservableInstruments();")
            .And.NotContain("QylObservableMeterInitializer")
            .And.NotContain("public static partial void ObserveQueueDepth");
    }

    [Fact]
    public void Observable_Counter_Callback_Can_Return_Tagged_Measurements()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Diagnostics.Metrics;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableCounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [ObservableCounter("myapp.requests", Unit = "{request}", Description = "Observed requests.")]
                    private static IEnumerable<Measurement<long>> ObserveRequests() =>
                    [
                        new(10, new KeyValuePair<string, object?>("route", "/")),
                        new(7, new KeyValuePair<string, object?>("route", "/checkout"))
                    ];
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("private static readonly global::System.Diagnostics.Metrics.ObservableCounter<long> _myappRequests =")
            .And.Contain("_meter.CreateObservableCounter<long>(\"myapp.requests\", new global::System.Func<global::System.Collections.Generic.IEnumerable<global::System.Diagnostics.Metrics.Measurement<long>>>(ObserveRequests), \"{request}\", \"Observed requests.\");")
            .And.Contain("_ = _myappRequests;")
            .And.NotContain("public static partial void ObserveRequests");
    }

    [Fact]
    public void Observable_UpDownCounter_Callback_Can_Return_Tagged_Measurement()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Diagnostics.Metrics;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableUpDownCounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.metrics")]
                public static partial class MyAppMetrics
                {
                    [ObservableUpDownCounter("myapp.work.items", Unit = "{item}", Description = "Observed work.")]
                    private static Measurement<double> ObserveWorkItems() =>
                        new(1.5, new KeyValuePair<string, object?>("queue", "main"));
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("private static readonly global::System.Diagnostics.Metrics.ObservableUpDownCounter<double> _myappWorkItems =")
            .And.Contain("_meter.CreateObservableUpDownCounter<double>(\"myapp.work.items\", new global::System.Func<global::System.Diagnostics.Metrics.Measurement<double>>(ObserveWorkItems), \"{item}\", \"Observed work.\");")
            .And.Contain("_ = _myappWorkItems;")
            .And.NotContain("public static partial void ObserveWorkItems");
    }

    [Fact]
    public void Global_Namespace_Meter_Class_Generates_Valid_Partial_Class()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObservableGaugeAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            [Qyl.Instrumentation.Instrumentation.Meter("global.metrics")]
            public static partial class GlobalMetrics
            {
                [Qyl.Instrumentation.Instrumentation.ObservableGauge("global.queue.depth")]
                private static long ObserveQueueDepth() => 1;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("partial class GlobalMetrics")
            .And.Contain("[global::System.Runtime.CompilerServices.ModuleInitializer]")
            .And.NotContain("global::GlobalMetrics.__QylInitializeObservableInstruments();")
            .And.NotContain("namespace <global namespace>");
    }

    [Fact]
    public void String_Metadata_Is_Emitted_As_CSharp_Literals()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class HistogramAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }

                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class TagAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.\"metrics", Version = "2026\n05")]
                public static partial class MyAppMetrics
                {
                    [Histogram("myapp.request.\"duration", Unit = "ms\n", Description = "Request \"duration\".\nLine two.")]
                    public static partial void RecordRequest(
                        double value,
                        [Tag("http.route\"quoted")] string route);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("new global::System.Diagnostics.Metrics.Meter(\"myapp.\\\"metrics\", \"2026\\n05\")")
            .And.Contain("_meter.CreateHistogram<double>(\"myapp.request.\\\"duration\", \"ms\\n\", \"Request \\\"duration\\\".\\nLine two.\");")
            .And.Contain("new global::System.Collections.Generic.KeyValuePair<string, object?>(\"http.route\\\"quoted\", route)");
    }

    [Fact]
    public void Non_Ascii_Metadata_Is_Emitted_As_Escaped_CSharp_Literals()
    {
        const string source = """
            using System;

            namespace Qyl.Instrumentation
            {
                public static class QylServiceDefaults;
            }

            namespace Qyl.Instrumentation.Instrumentation
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class MeterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Version { get; set; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class CounterAttribute(string name) : Attribute
                {
                    public string Name { get; } = name;
                    public string? Unit { get; set; }
                    public string? Description { get; set; }
                }
            }

            namespace MyApp
            {
                using Qyl.Instrumentation.Instrumentation;

                [Meter("myapp.m\u00e9trics")]
                public static partial class MyAppMetrics
                {
                    [Counter("myapp.r\u00e9quests", Description = "D\u00e9j\u00e0 vu.")]
                    public static partial void AddRequest();
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator<ServiceDefaultsSourceGenerator>(source);

        var generatedTree = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MeterImplementations.g.cs", StringComparison.Ordinal));
        var generated = generatedTree.ToString();

        generatedTree.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Should().BeEmpty();
        generated.Should()
            .Contain("[assembly: global::Qyl.Instrumentation.GeneratedMeterAttribute(\"myapp.m\\u00e9trics\")]")
            .And.Contain("_meter.CreateCounter<long>(\"myapp.r\\u00e9quests\", null, \"D\\u00e9j\\u00e0 vu.\");");
    }
}

namespace Qyl.Instrumentation.Generators.Tests;

/// <summary>
/// Shared boilerplate for <c>MeterEmitterTests</c>: every test fixture needs the same
/// Qyl.Instrumentation marker class plus the full set of attribute declarations.
/// Extracting them here lets each test focus on the meter-and-instrument code that
/// is unique to that scenario.
/// </summary>
internal static class MeterTestSources
{
    public const string Preamble = """
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

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class ObservableGaugeAttribute(string name) : Attribute
            {
                public string Name { get; } = name;
                public string? Unit { get; set; }
                public string? Description { get; set; }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class ObservableCounterAttribute(string name) : Attribute
            {
                public string Name { get; } = name;
                public string? Unit { get; set; }
                public string? Description { get; set; }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class ObservableUpDownCounterAttribute(string name) : Attribute
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
        """;

    /// <summary>
    /// Wraps the supplied meter code in <c>namespace MyApp { using Qyl.Instrumentation.Instrumentation; … }</c>
    /// — the most common shape used by these tests.
    /// </summary>
    public static string InMyAppNamespace(string meterCode) =>
        Preamble + "\n\nnamespace MyApp\n{\n    using Qyl.Instrumentation.Instrumentation;\n\n" + meterCode + "\n}\n";
}

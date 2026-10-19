using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

/// <summary>
/// Exception text is sanitized by its content alone. The flaky failure came from a cold sanitizer on a
/// loaded machine: its patterns build their matching state on first use, a 100 ms wall-clock deadline
/// ran out while the thread waited for a core, and valid text became the failure marker. Each fresh
/// load context here holds a cold sanitizer, used first by many threads while every core is busy.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ExceptionSanitizationLoadTests
{
    private const string Text = """
        A dependency failed; diagnostic context follows.
        Server=db;User ID=synthetic-user;Password=synthetic-password with spaces&suffix;Database=orders;
        Authorization: Bearer synthetic-bearer-token
        X-API-Key: synthetic-api-key;synthetic-api-key-suffix
        token=synthetic-assigned-token; continued
        eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzeW50aGV0aWMifQ.synthetic-signature
        sk-proj-0123456789ABCDEF0123456789 ghp_0123456789ABCDEF0123456789 npm_0123456789ABCDEF0123456789
        https://synthetic-user:synthetic-url-password@example.invalid/route?api_key=synthetic-query-secret#synthetic-fragment-secret
           at Checkout.Handler.Place() in /src/Checkout.cs:line 42
        """;

    [TestMethod]
    public void A_cold_sanitizer_on_a_saturated_machine_returns_what_an_idle_one_returns()
    {
        var expected = ExceptionTextSanitizer.Sanitize("exception.message", new AnyValue { StringValue = Text });
        Assert.Contains("[REDACTED]", expected, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-password", expected, StringComparison.Ordinal);

        var results = new ConcurrentQueue<string>();
        var contexts = Enumerable.Range(0, 4).Select(_ => new ColdIngestion()).ToList();
        var busy = true;
        var burners = Enumerable.Range(0, Environment.ProcessorCount * 32)
            .Select(_ => new Thread(() => { while (Volatile.Read(ref busy)) Thread.SpinWait(64); }) { IsBackground = true }).ToList();
        burners.ForEach(static burner => burner.Start());
        try
        {
            var workers = contexts.SelectMany(context => Enumerable.Range(0, Environment.ProcessorCount * 4)
                .Select(_ => new Thread(() => results.Enqueue(context.Sanitize(Text))))).ToList();
            workers.ForEach(static worker => worker.Start());
            workers.ForEach(static worker => worker.Join());
        }
        finally
        {
            Volatile.Write(ref busy, false);
            burners.ForEach(static burner => burner.Join());
            contexts.ForEach(static context => context.Unload());
        }

        Assert.HasCount(contexts.Count * Environment.ProcessorCount * 4, results);
        Assert.AreEqual(0, results.Count(static result => result == "[SANITIZATION_FAILED]"), "A loaded machine turned valid text into the failure marker.");
        Assert.IsTrue(results.All(result => result == expected), "A conversion under load sanitized differently than an idle one.");
    }

    /// <summary>No pattern carries a wall-clock deadline, so no machine load can decide a sanitization outcome.</summary>
    [TestMethod]
    public void No_sanitizer_pattern_has_a_wall_clock_deadline()
    {
        var patterns = typeof(ExceptionTextSanitizer).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(static method => method.ReturnType == typeof(Regex) && method.GetParameters().Length is 0)
            .Select(static method => (Regex)method.Invoke(null, null)!).ToList();

        Assert.IsNotEmpty(patterns);
        Assert.IsTrue(patterns.All(static pattern => pattern.MatchTimeout == Regex.InfiniteMatchTimeout),
            string.Join(", ", patterns.Select(static pattern => pattern.MatchTimeout)));
        Assert.IsTrue(patterns.All(static pattern => pattern.Options.HasFlag(RegexOptions.NonBacktracking)), "Without a deadline every pattern must match in linear time.");
    }

    /// <summary>A second copy of the ingestion assembly, so its sanitizer patterns have never matched.</summary>
    private sealed class ColdIngestion : AssemblyLoadContext
    {
        private readonly Type _valueType;
        private readonly MethodInfo _sanitize;

        public ColdIngestion() : base(isCollectible: true)
        {
            var ingestion = LoadFromAssemblyPath(typeof(ExceptionTextSanitizer).Assembly.Location);
            _valueType = ingestion.GetType(typeof(AnyValue).FullName!, throwOnError: true)!;
            _sanitize = ingestion.GetType(typeof(ExceptionTextSanitizer).FullName!, throwOnError: true)!
                .GetMethod(nameof(ExceptionTextSanitizer.Sanitize), BindingFlags.Static | BindingFlags.NonPublic)!;
        }

        public string Sanitize(string text)
        {
            var value = Activator.CreateInstance(_valueType)!;
            _valueType.GetProperty(nameof(AnyValue.StringValue))!.SetValue(value, text);
            return (string)_sanitize.Invoke(null, ["exception.message", value])!;
        }

        // Every other assembly is the one the test already loaded.
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}

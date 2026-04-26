using System.Globalization;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public sealed class DeprecatedAttributeAnalyzerTests
{
    // android.state → android.app.state (status=renamed, mode=direct)
    private const string DeprecatedCode = """
                                          class C
                                          {
                                              void M(object activity)
                                              {
                                                  var x = activity.SetTag("android.state", "background");
                                              }
                                          }
                                          """;

    private const string CurrentCode = """
                                       class C
                                       {
                                           void M(object activity)
                                           {
                                               var x = activity.SetTag("android.app.state", "background");
                                           }
                                       }
                                       """;

    private static readonly DeprecatedAttributeAnalyzer s_analyzer = new();

    [Fact]
    public async Task FiresOnDeprecatedStringLiteralWithPerEntryRuleIdAsync()
    {
        var diags = await RoslynTestHelper.GetDiagnosticsAsync(DeprecatedCode, s_analyzer);

        Assert.Single(diags);
        // android.state is the first alphabetically-sorted entry (folder=android)
        // → QYLSC0001 in the generated table.
        Assert.Equal("QYLSC0001", diags[0].Id);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
    }

    [Fact]
    public async Task MessageContainsOldAndNewIdsAsync()
    {
        var diags = await RoslynTestHelper.GetDiagnosticsAsync(DeprecatedCode, s_analyzer);

        var msg = diags[0].GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("android.state", msg, StringComparison.Ordinal);
        Assert.Contains("android.app.state", msg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotFireOnCurrentAttributeAsync()
    {
        var diags = await RoslynTestHelper.GetDiagnosticsAsync(CurrentCode, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task FiresOnAzNamespaceRenamedAsync()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("az.namespace", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        var msg = diags[0].GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("azure.resource_provider.namespace", msg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiresOnAddTagAsWellAsync()
    {
        const string code = """
                            class C { void M(object a) { a.AddTag("android.state", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        Assert.Equal("QYLSC0001", diags[0].Id);
    }

    [Fact]
    public async Task DoesNotFireOnNonTagMethodAsync()
    {
        const string code = """
                            class C { void M() { var x = SomeHelper.Do("android.state"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        // SomeHelper.Do is not in the tag-method list → no diagnostic
        Assert.Empty(diags);
    }

    [Fact]
    public async Task FiresOnRemovedAttributeWithPerEntryDescriptorAsync()
    {
        // db.jdbc.driver_classname — status=obsoleted, mode=removed
        const string code = """
                            class C { void M(object a) { a.SetTag("db.jdbc.driver_classname", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        var entry = DeprecatedDiagnostics.ByDeprecatedId["db.jdbc.driver_classname"];
        Assert.Equal(entry.RuleId, diags[0].Id);
        Assert.Equal(DeprecatedReplacementMode.Removed, entry.Mode);
    }

    [Fact]
    public async Task FiresOnUncategorizedEntryAsync()
    {
        // code.function — status=uncategorized, mode=integrate
        const string code = """
                            class C { void M(object a) { a.SetTag("code.function", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        var entry = DeprecatedDiagnostics.ByDeprecatedId["code.function"];
        Assert.Equal(entry.RuleId, diags[0].Id);
        Assert.Equal("uncategorized", ExpectStatus(entry));
    }

    [Fact]
    public async Task FiresOnAlternativeModeAsync()
    {
        // http.host — mode=alternative, replacements=[server.address, client.address, http.request.header.host]
        const string code = """
                            class C { void M(object a) { a.SetTag("http.host", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        var entry = DeprecatedDiagnostics.ByDeprecatedId["http.host"];
        Assert.Equal(entry.RuleId, diags[0].Id);
        Assert.Equal(DeprecatedReplacementMode.Alternative, entry.Mode);
        Assert.True(entry.Replacements.Length > 1);
    }

    [Fact]
    public void GeneratedTableContainsExpectedBucketCounts()
    {
        // Guard against drift: regenerate if these counts change.
        Assert.Equal(245, DeprecatedDiagnostics.ByDeprecatedId.Count);
        Assert.Equal(245, DeprecatedDiagnostics.AllDescriptors.Length);
        Assert.Equal(245, DeprecatedDiagnostics.AllRuleIds.Length);

        var byMode = DeprecatedDiagnostics.ByDeprecatedId.Values
            .GroupBy(e => e.Mode)
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(193, byMode[DeprecatedReplacementMode.Direct]);
        Assert.Equal(31, byMode[DeprecatedReplacementMode.Removed]);
        Assert.Equal(6, byMode[DeprecatedReplacementMode.Alternative]);
    }

    // http.request.header is tested above; entries for status-bucket naming aren't on
    // DeprecatedEntry directly (status isn't generated — only mode/kind/basis are). The
    // mode proxies status for the three required buckets.
    private static string ExpectStatus(DeprecatedEntry entry)
    {
        return entry.Mode switch
        {
            DeprecatedReplacementMode.Removed => "obsoleted",
            DeprecatedReplacementMode.Direct when entry.ResolutionBasis == "deprecated.renamed_to" => "renamed",
            _ => "uncategorized"
        };
    }
}

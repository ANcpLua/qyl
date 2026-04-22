using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public sealed class MagicStringAnalyzerTests
{
    private static readonly MagicStringAnalyzer s_analyzer = new();

    /// <summary>
    /// Get a known-valid (replacement) ID from the deprecation index so the test
    /// is data-driven and stays correct if the YAML changes.
    /// </summary>
    private static string AnyValidId()
    {
        // android.app.state is the replacement for android.state
        return "android.app.state";
    }

    [Fact]
    public async Task Fires_on_known_valid_string_literal()
    {
        var validId = AnyValidId();
        var code = $$"""
            class C { void M(object a) { a.SetTag("{{validId}}", "x"); } }
            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        Assert.Equal(MagicStringAnalyzer.DiagnosticId, diags[0].Id);
        Assert.Equal(DiagnosticSeverity.Info, diags[0].Severity);
    }

    [Fact]
    public async Task Does_not_fire_on_deprecated_string()
    {
        // Deprecated IDs are handled by QYL-SEMCONV-001, not 002
        const string code = """
            class C { void M(object a) { a.SetTag("android.state", "x"); } }
            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task Does_not_fire_on_unknown_string()
    {
        // Completely unknown IDs are handled by QYL-SEMCONV-003
        const string code = """
            class C { void M(object a) { a.SetTag("totally.custom.attr", "x"); } }
            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        // This ID is neither deprecated nor known-valid → 002 should not fire
        Assert.Empty(diags);
    }

    [Fact]
    public async Task Does_not_fire_on_non_string_argument()
    {
        const string code = """
            class C { static string Key = "android.app.state"; void M(object a) { a.SetTag(Key, "x"); } }
            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        // First argument is an identifier, not a literal → no diagnostic
        Assert.Empty(diags);
    }
}

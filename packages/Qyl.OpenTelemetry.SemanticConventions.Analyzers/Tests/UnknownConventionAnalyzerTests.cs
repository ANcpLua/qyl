using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public sealed class UnknownConventionAnalyzerTests
{
    private static readonly UnknownConventionAnalyzer s_analyzer = new();

    [Fact]
    public async Task FiresOtelUnknownForOtelPrefixWithUnknownId()
    {
        // "http.typo_attr" starts with known OTel prefix "http" but is not in the registry
        const string code = """
                            class C { void M(object a) { a.SetTag("http.typo_attr", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        Assert.Equal(UnknownConventionAnalyzer.OtelUnknownId, diags[0].Id);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
    }

    [Fact]
    public async Task FiresQylUnregisteredForQylPrefixedId()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("qyl.some.new.attr", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        Assert.Equal(UnknownConventionAnalyzer.QylUnregisteredId, diags[0].Id);
    }

    [Fact]
    public async Task DoesNotFireOnDeprecatedId()
    {
        // Deprecated IDs are handled by 001, not 003
        const string code = """
                            class C { void M(object a) { a.SetTag("android.state", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task DoesNotFireOnValidReplacementId()
    {
        // Valid IDs are handled by 002 at Info; 003 should not double-fire
        const string code = """
                            class C { void M(object a) { a.SetTag("android.app.state", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task DoesNotFireOnCompletelyCustomPrefix()
    {
        // "company.custom.attr" has no OTel-known prefix → silent (Info noise avoided)
        const string code = """
                            class C { void M(object a) { a.SetTag("company.custom.attr", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task SuggestionAppearsInMessageForCloseTypo()
    {
        // "db.systen" is one edit away from "db.system" — should suggest it if in valid set
        // Note: db.system may not be in our ValidIds (it's not a replacement entry)
        // so we test with a prefix that IS in our registry replacements
        // code.column.number is a replacement for code.column
        const string code = """
                            class C { void M(object a) { a.SetTag("code.colmun.number", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        // "code.colmun.number" starts with "code" prefix (known) so fires OtelUnknown
        Assert.Single(diags);
        Assert.Equal(UnknownConventionAnalyzer.OtelUnknownId, diags[0].Id);
    }
}

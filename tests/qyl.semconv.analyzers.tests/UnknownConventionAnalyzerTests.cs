using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public sealed class UnknownConventionAnalyzerTests
{
    private static readonly UnknownConventionAnalyzer s_analyzer = new();

    [Fact]
    public async Task FiresOtelUnknownForOtelPrefixWithUnknownId()
    {
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
        const string code = """
                            class C { void M(object a) { a.SetTag("android.state", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task DoesNotFireOnValidReplacementId()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("android.app.state", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task DoesNotFireOnCompletelyCustomPrefix()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("company.custom.attr", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task SuggestionAppearsInMessageForCloseTypo()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("code.colmun.number", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Single(diags);
        Assert.Equal(UnknownConventionAnalyzer.OtelUnknownId, diags[0].Id);
    }
}

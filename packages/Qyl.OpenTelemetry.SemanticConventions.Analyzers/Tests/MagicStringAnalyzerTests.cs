using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public sealed class MagicStringAnalyzerTests
{
    private static readonly MagicStringAnalyzer s_analyzer = new();

    private static string AnyValidId()
    {
        return "android.app.state";
    }

    [Fact]
    public async Task FiresOnKnownValidStringLiteral()
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
    public async Task DoesNotFireOnDeprecatedString()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("android.state", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task DoesNotFireOnUnknownString()
    {
        const string code = """
                            class C { void M(object a) { a.SetTag("totally.custom.attr", "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }

    [Fact]
    public async Task DoesNotFireOnNonStringArgument()
    {
        const string code = """
                            class C { static string Key = "android.app.state"; void M(object a) { a.SetTag(Key, "x"); } }
                            """;

        var diags = await RoslynTestHelper.GetDiagnosticsAsync(code, s_analyzer);

        Assert.Empty(diags);
    }
}

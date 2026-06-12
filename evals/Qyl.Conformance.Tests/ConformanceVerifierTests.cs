using System.Text.Json;

namespace Qyl.Conformance.Tests;

public sealed class ConformanceVerifierTests
{
    private static ConformancePlan Plan(params ExpectedSignal[] signals) => new()
    {
        SchemaVersion = "1",
        GraphSchemaVersion = "1",
        Services =
        [
            new PlannedService { ServiceName = "qyl.collector", ProfileId = "qyl-default", ExpectedSignals = signals },
        ],
    };

    private static ExpectedSignal HttpServerSpan(
        IReadOnlyList<string>? required = null,
        IReadOnlyList<string>? recommended = null,
        IReadOnlyList<string>? optIn = null) => new()
    {
        Kind = "span",
        Name = "http.server.request",
        RequiredAttributes = required ?? ["http.request.method"],
        RecommendedAttributes = recommended ?? [],
        OptInAttributes = optIn ?? [],
    };

    private static ObservedSignal Observed(string service = "qyl.collector", string kind = "span",
        string name = "http.server.request", string[]? keys = null) =>
        new() { ServiceName = service, Kind = kind, Name = name, AttributeKeys = keys ?? [] };

    [Fact]
    public void A_fully_conformant_snapshot_yields_no_findings_and_passes_the_gate()
    {
        var report = ConformanceVerifier.Verify(
            Plan(HttpServerSpan()),
            [Observed(keys: ["http.request.method"])]);

        Assert.True(report.Conformant);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void A_declared_signal_that_is_never_observed_is_an_error()
    {
        var report = ConformanceVerifier.Verify(Plan(HttpServerSpan()), []);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(ConformanceFindingKind.DeclaredMissing, finding.Kind);
        Assert.Equal(ConformanceSeverity.Error, finding.Severity);
        Assert.False(report.Conformant);
    }

    [Fact]
    public void Telemetry_from_a_service_outside_the_graph_is_an_error()
    {
        var report = ConformanceVerifier.Verify(
            Plan(HttpServerSpan()),
            [Observed(keys: ["http.request.method"]), Observed(service: "shadow.service", keys: ["x"])]);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(ConformanceFindingKind.UndeclaredEmitted, finding.Kind);
        Assert.Equal("shadow.service", finding.ServiceName);
        Assert.False(report.Conformant);
    }

    [Fact]
    public void An_undeclared_signal_on_a_known_service_is_an_error()
    {
        var report = ConformanceVerifier.Verify(
            Plan(HttpServerSpan()),
            [Observed(keys: ["http.request.method"]), Observed(name: "db.client.operation")]);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(ConformanceFindingKind.UndeclaredEmitted, finding.Kind);
        Assert.Equal("db.client.operation", finding.SignalName);
    }

    [Fact]
    public void A_required_attribute_missing_on_any_occurrence_is_an_error_with_occurrence_counts()
    {
        var report = ConformanceVerifier.Verify(
            Plan(HttpServerSpan()),
            [Observed(keys: ["http.request.method"]), Observed(keys: ["url.path"])]);

        var drift = Assert.Single(report.Findings, static f => f.Kind == ConformanceFindingKind.AttributeDrift && f.Severity == ConformanceSeverity.Error);
        Assert.Equal("http.request.method", drift.AttributeKey);
        Assert.Contains("1/2", drift.Detail);
        Assert.False(report.Conformant);
    }

    [Fact]
    public void Recommended_and_unknown_attributes_warn_but_do_not_fail_the_gate()
    {
        var report = ConformanceVerifier.Verify(
            Plan(HttpServerSpan(recommended: ["http.response.status_code"])),
            [Observed(keys: ["http.request.method", "qyl.shadow.key"])]);

        Assert.True(report.Conformant);
        Assert.Equal(2, report.Findings.Count);
        Assert.All(report.Findings, static f => Assert.Equal(ConformanceSeverity.Warning, f.Severity));
        Assert.Contains(report.Findings, static f => f.AttributeKey == "http.response.status_code");
        Assert.Contains(report.Findings, static f => f.AttributeKey == "qyl.shadow.key");
    }

    [Fact]
    public void The_wire_contract_round_trips_snake_case_plan_and_snapshot_lines()
    {
        const string planJson = """
            {
              "schema_version": "1",
              "graph_schema_version": "1",
              "services": [{
                "service_name": "qyl.collector",
                "profile_id": "qyl-default",
                "expected_signals": [{
                  "kind": "span",
                  "name": "http.server.request",
                  "required_attributes": ["http.request.method"],
                  "recommended_attributes": [],
                  "opt_in_attributes": []
                }]
              }]
            }
            """;
        var plan = JsonSerializer.Deserialize<ConformancePlan>(planJson)!;
        var observed = ObservedSignal.FromJsonLines(
        [
            """{"service_name":"qyl.collector","kind":"span","name":"http.server.request","attribute_keys":["http.request.method"]}""",
            "",
        ]);

        var report = ConformanceVerifier.Verify(plan, observed);

        Assert.True(report.Conformant);
        var json = JsonSerializer.Serialize(report);
        Assert.Contains("\"graph_schema_version\":\"1\"", json);
        Assert.Contains("\"conformant\":true", json);
    }
}

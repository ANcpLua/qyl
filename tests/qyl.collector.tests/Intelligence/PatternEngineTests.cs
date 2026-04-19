using AwesomeAssertions;
using Xunit;
using Qyl.Collector.Intelligence;
using Qyl.Contracts.Intelligence;

namespace Qyl.Collector.Tests.Intelligence;

public sealed class PatternEngineTests
{
    private static PatternEngine CreateEngine() =>
        new(DiagnosticPatterns.All, CausalRules.All, InvestigationStrategies.All);

    private static Signal Observed(string attribute, string? value = null) =>
        new() { Attribute = attribute, Operator = SignalOperator.Eq, Value = value };

    // ==========================================================================
    // Seed pattern registry integrity
    // ==========================================================================

    [Fact]
    public void SeedPatterns_AllHaveUniqueIds()
    {
        var ids = DiagnosticPatterns.All.Select(static p => p.Id).ToList();
        ids.Distinct(StringComparer.Ordinal).Count().Should().Be(ids.Count);
    }

    [Fact]
    public void SeedRules_All_Contains6Rules() => CausalRules.All.Count.Should().Be(6);

    [Fact]
    public void SeedRules_ReferenceExistingPatterns()
    {
        var patternIds = DiagnosticPatterns.All.Select(static p => p.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var rule in CausalRules.All)
        {
            patternIds.Should().Contain(rule.CausePattern);
            patternIds.Should().Contain(rule.EffectPattern);
        }
    }

    [Fact]
    public void SeedStrategies_AllHaveSteps()
    {
        foreach (var strategy in InvestigationStrategies.All)
        {
            strategy.Steps.Should().NotBeEmpty();
        }
    }

    // ==========================================================================
    // Pattern matching: positive cases — 10 seed patterns
    // ==========================================================================

    [Fact]
    public void Evaluate_GenAiRateLimit_MatchesWhenAllSignalsPresent()
    {
        var engine = CreateEngine();
        var signals = new List<Signal>
        {
            Observed("status_code", "2"),
            Observed("gen_ai_provider_name", "openai"),
            Observed("error_type", "rate_limit_exceeded")
        };

        var matches = engine.Evaluate(signals);

        var match = matches.Should().ContainSingle(static m => m.Pattern.Id == "genai_rate_limit").Which;
        match.Score.Should().BeApproximately(0.9, 1e-5);
    }

    [Fact]
    public void Evaluate_GenAiTokenExhaustion_MatchesOnStopReasonLength()
    {
        var engine = CreateEngine();
        var signals = new List<Signal> { Observed("gen_ai_stop_reason", "length") };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "genai_token_exhaustion");
    }

    [Fact]
    public void Evaluate_GenAiContentFilter_MatchesOnStopReasonContainingContentFilter()
    {
        var engine = CreateEngine();
        var signals = new List<Signal> { Observed("gen_ai_stop_reason", "content_filter_triggered") };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "genai_content_filter");
    }

    [Fact]
    public void Evaluate_DbTimeout_MatchesOnTimeoutExceptionWithDbAndDuration()
    {
        var engine = CreateEngine();
        var signals = new List<Signal>
        {
            Observed("exception_type", "TimeoutException"),
            Observed("db.system.name", "postgresql"),
            Observed("duration_ns", "3000000000")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "db_timeout");
    }

    [Fact]
    public void Evaluate_DbNPlusOne_MatchesOnHighSpanCountUnderParent()
    {
        var engine = CreateEngine();
        var signals = new List<Signal>
        {
            Observed("db.system.name", "postgresql"),
            Observed("parent_span_id", "abc123"),
            Observed("span_count_under_parent", "25")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "db_n_plus_one");
    }

    [Fact]
    public void Evaluate_Http5xxCluster_MatchesWithResolvedThresholds()
    {
        // Seed pattern uses symbolic "baseline*3" — test with pre-resolved numeric threshold
        var resolvedPattern = new DiagnosticPattern
        {
            Id = "http_5xx_cluster",
            Category = PatternCategory.Error,
            Signals =
            [
                new Signal { Attribute = "http.response.status_code", Operator = SignalOperator.Gte, Value = "500" },
                new Signal { Attribute = "occurrence_rate", Operator = SignalOperator.Gt, Value = "9" }
            ],
            Hypothesis = "Server error spike.",
            Confidence = 0.75
        };
        var engine = new PatternEngine([resolvedPattern], CausalRules.All, InvestigationStrategies.All);
        var signals = new List<Signal>
        {
            Observed("http.response.status_code", "503"), Observed("occurrence_rate", "15")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "http_5xx_cluster");
    }

    [Fact]
    public void Evaluate_DeploymentRegression_MatchesWithResolvedThresholds()
    {
        // Seed pattern uses symbolic "last_deployment_time" — test with resolved numeric timestamp
        var resolvedPattern = new DiagnosticPattern
        {
            Id = "deployment_regression",
            Category = PatternCategory.Error,
            Signals =
            [
                new Signal { Attribute = "error_type", Operator = SignalOperator.Exists },
                new Signal { Attribute = "first_seen_at", Operator = SignalOperator.Gt, Value = "1711100000" }
            ],
            Hypothesis = "New error class after deployment.",
            Confidence = 0.80
        };
        var engine = new PatternEngine([resolvedPattern], CausalRules.All, InvestigationStrategies.All);
        var signals = new List<Signal>
        {
            Observed("error_type", "NullReferenceException"), Observed("first_seen_at", "1711108800")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "deployment_regression");
    }

    [Fact]
    public void Evaluate_CascadingTimeout_MatchesOnTimeoutWithDownstreamError()
    {
        var engine = CreateEngine();
        var signals = new List<Signal>
        {
            Observed("exception_type", "TaskCanceledException: The request was canceled due to Timeout"),
            Observed("downstream_service_error", "true")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "cascading_timeout");
    }

    [Fact]
    public void Evaluate_MemoryPressureLatency_MatchesWithResolvedThresholds()
    {
        // Seed pattern uses symbolic "p99_baseline" — test with resolved numeric value
        var resolvedPattern = new DiagnosticPattern
        {
            Id = "memory_pressure_latency",
            Category = PatternCategory.Latency,
            Signals =
            [
                new Signal
                {
                    Attribute = "process.runtime.dotnet.gc.duration", Operator = SignalOperator.Gt, Value = "100"
                },
                new Signal { Attribute = "avg_latency", Operator = SignalOperator.Gt, Value = "200" }
            ],
            Hypothesis = "GC pressure causing latency.",
            Confidence = 0.65
        };
        var engine = new PatternEngine([resolvedPattern], CausalRules.All, InvestigationStrategies.All);
        var signals = new List<Signal>
        {
            Observed("process.runtime.dotnet.gc.duration", "250"), Observed("avg_latency", "500")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "memory_pressure_latency");
    }

    [Fact]
    public void Evaluate_CostSpike_MatchesWithResolvedThresholds()
    {
        // Seed pattern uses symbolic "daily_average*3" — test with resolved numeric value
        var resolvedPattern = new DiagnosticPattern
        {
            Id = "cost_spike",
            Category = PatternCategory.Cost,
            Signals =
            [
                new Signal { Attribute = "gen_ai_cost_usd", Operator = SignalOperator.Gt, Value = "30.0" }
            ],
            Hypothesis = "Abnormal cost increase.",
            Confidence = 0.75
        };
        var engine = new PatternEngine([resolvedPattern], CausalRules.All, InvestigationStrategies.All);
        var signals = new List<Signal> { Observed("gen_ai_cost_usd", "150.0") };

        var matches = engine.Evaluate(signals);

        matches.Should().ContainSingle(static m => m.Pattern.Id == "cost_spike");
    }

    // ==========================================================================
    // Pattern matching: negative cases
    // ==========================================================================

    [Fact]
    public void Evaluate_GenAiRateLimit_DoesNotMatch_WhenMissingProviderName()
    {
        var engine = CreateEngine();
        var signals = new List<Signal> { Observed("status_code", "2"), Observed("error_type", "rate_limit") };

        var matches = engine.Evaluate(signals);

        matches.Should().NotContain(static m => m.Pattern.Id == "genai_rate_limit");
    }

    [Fact]
    public void Evaluate_DbTimeout_DoesNotMatch_WhenDurationBelowThreshold()
    {
        var engine = CreateEngine();
        var signals = new List<Signal>
        {
            Observed("exception_type", "TimeoutException"),
            Observed("db.system.name", "postgresql"),
            Observed("duration_ns", "1000000000")
        };

        var matches = engine.Evaluate(signals);

        matches.Should().NotContain(static m => m.Pattern.Id == "db_timeout");
    }

    [Fact]
    public void Evaluate_NoSignals_ReturnsEmpty()
    {
        var engine = CreateEngine();

        var matches = engine.Evaluate([]);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_UnrelatedSignals_ReturnsEmpty()
    {
        var engine = CreateEngine();
        var signals = new List<Signal> { Observed("http.method", "GET"), Observed("user_agent", "Mozilla/5.0") };

        var matches = engine.Evaluate(signals);

        matches.Should().BeEmpty();
    }

    // ==========================================================================
    // Ranking
    // ==========================================================================

    [Fact]
    public void Evaluate_ReturnsMatches_SortedByScoreDescending()
    {
        var engine = CreateEngine();
        // Signals that match multiple patterns (genai_rate_limit @ 0.9 and cascading_timeout @ 0.7)
        var signals = new List<Signal>
        {
            Observed("status_code", "2"),
            Observed("gen_ai_provider_name", "openai"),
            Observed("error_type", "rate_limit"),
            Observed("exception_type", "TaskCanceledException: Timeout occurred"),
            Observed("downstream_service_error", "true")
        };

        var matches = engine.Evaluate(signals);

        matches.Count.Should().BeGreaterThanOrEqualTo(2);
        for (var i = 1; i < matches.Count; i++)
        {
            matches[i - 1].Score.Should().BeGreaterThanOrEqualTo(matches[i].Score,
                $"Match at {i - 1} (score {matches[i - 1].Score}) should rank above {i} (score {matches[i].Score})");
        }
    }

    // ==========================================================================
    // Causal graph
    // ==========================================================================

    [Fact]
    public void BuildCausalGraph_DeployRegressionAndHttp5xx_CreatesEdgeWithStrength085()
    {
        var engine = CreateEngine();
        var deployPattern = DiagnosticPatterns.All.First(static p => p.Id == "deployment_regression");
        var httpPattern = DiagnosticPatterns.All.First(static p => p.Id == "http_5xx_cluster");

        var matches = new List<PatternMatch>
        {
            new(deployPattern, deployPattern.Confidence, deployPattern.Signals),
            new(httpPattern, httpPattern.Confidence, httpPattern.Signals)
        };

        var graph = engine.BuildCausalGraph(matches);

        var edge = graph.Edges.Should().ContainSingle().Which;
        edge.CausePatternId.Should().Be("deployment_regression");
        edge.EffectPatternId.Should().Be("http_5xx_cluster");
        edge.Strength.Should().BeApproximately(0.85, 1e-5);
    }

    [Fact]
    public void BuildCausalGraph_RootCause_IsPatternWithNoIncomingEdges()
    {
        var engine = CreateEngine();
        var deployPattern = DiagnosticPatterns.All.First(static p => p.Id == "deployment_regression");
        var httpPattern = DiagnosticPatterns.All.First(static p => p.Id == "http_5xx_cluster");

        var matches = new List<PatternMatch>
        {
            new(deployPattern, deployPattern.Confidence, deployPattern.Signals),
            new(httpPattern, httpPattern.Confidence, httpPattern.Signals)
        };

        var graph = engine.BuildCausalGraph(matches);

        graph.RootCauses.Should().Contain("deployment_regression");
        graph.RootCauses.Should().NotContain("http_5xx_cluster");
    }

    [Fact]
    public void BuildCausalGraph_ChainedCausality_IdentifiesDeepRootCause()
    {
        var engine = CreateEngine();
        // Chain: db_n_plus_one → db_timeout → http_5xx_cluster
        var nPlusOne = DiagnosticPatterns.All.First(static p => p.Id == "db_n_plus_one");
        var dbTimeout = DiagnosticPatterns.All.First(static p => p.Id == "db_timeout");
        var http5xx = DiagnosticPatterns.All.First(static p => p.Id == "http_5xx_cluster");

        var matches = new List<PatternMatch>
        {
            new(nPlusOne, nPlusOne.Confidence, nPlusOne.Signals),
            new(dbTimeout, dbTimeout.Confidence, dbTimeout.Signals),
            new(http5xx, http5xx.Confidence, http5xx.Signals)
        };

        var graph = engine.BuildCausalGraph(matches);

        graph.Edges.Count.Should().Be(2);
        graph.RootCauses.Should().Contain("db_n_plus_one");
        graph.RootCauses.Should().NotContain("db_timeout");
        graph.RootCauses.Should().NotContain("http_5xx_cluster");
    }

    [Fact]
    public void BuildCausalGraph_SingleMatch_HasNoEdgesAndIsRootCause()
    {
        var engine = CreateEngine();
        var pattern = DiagnosticPatterns.All.First(static p => p.Id == "genai_content_filter");

        var matches = new List<PatternMatch> { new(pattern, pattern.Confidence, pattern.Signals) };

        var graph = engine.BuildCausalGraph(matches);

        graph.Edges.Should().BeEmpty();
        graph.RootCauses.Should().ContainSingle().Which.Should().Be("genai_content_filter");
    }

    [Fact]
    public void BuildCausalGraph_EmptyMatches_ReturnsEmptyGraph()
    {
        var engine = CreateEngine();

        var graph = engine.BuildCausalGraph([]);

        graph.Edges.Should().BeEmpty();
        graph.RootCauses.Should().BeEmpty();
    }

    // ==========================================================================
    // Strategy selection
    // ==========================================================================

    [Fact]
    public void SelectStrategy_ErrorCategory_ReturnsInvestigateErrorIssue()
    {
        var engine = CreateEngine();
        var pattern = DiagnosticPatterns.All.First(static p => p.Id == "http_5xx_cluster");
        var match = new PatternMatch(pattern, pattern.Confidence, pattern.Signals);

        var strategy = engine.SelectStrategy(match);

        strategy.Should().NotBeNull();
        strategy.Id.Should().Be("investigate_error_issue");
    }

    [Fact]
    public void SelectStrategy_LatencyCategory_ReturnsInvestigateLatency()
    {
        var engine = CreateEngine();
        var pattern = DiagnosticPatterns.All.First(static p => p.Id == "cascading_timeout");
        var match = new PatternMatch(pattern, pattern.Confidence, pattern.Signals);

        var strategy = engine.SelectStrategy(match);

        strategy.Should().NotBeNull();
        strategy.Id.Should().Be("investigate_latency");
    }

    [Fact]
    public void SelectStrategy_CostCategory_ReturnsInvestigateCost()
    {
        var engine = CreateEngine();
        var pattern = DiagnosticPatterns.All.First(static p => p.Id == "cost_spike");
        var match = new PatternMatch(pattern, pattern.Confidence, pattern.Signals);

        var strategy = engine.SelectStrategy(match);

        strategy.Should().NotBeNull();
        strategy.Id.Should().Be("investigate_cost");
    }

    [Fact]
    public void SelectStrategy_GenAiCategory_ReturnsInvestigateGenAi()
    {
        var engine = CreateEngine();
        var pattern = DiagnosticPatterns.All.First(static p => p.Id == "genai_rate_limit");
        var match = new PatternMatch(pattern, pattern.Confidence, pattern.Signals);

        var strategy = engine.SelectStrategy(match);

        strategy.Should().NotBeNull();
        strategy.Id.Should().Be("investigate_genai");
    }

    [Fact]
    public void SelectStrategy_DataCategory_ReturnsNull_NoDataCategoryStrategy()
    {
        var engine = CreateEngine();
        var pattern = DiagnosticPatterns.All.First(static p => p.Id == "db_timeout");
        var match = new PatternMatch(pattern, pattern.Confidence, pattern.Signals);

        var strategy = engine.SelectStrategy(match);

        // No strategy with trigger "category:data" exists in seed data
        strategy.Should().BeNull();
    }
}

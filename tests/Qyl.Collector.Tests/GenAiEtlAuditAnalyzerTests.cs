using Qyl.Collector.Cost;

namespace Qyl.Collector.Tests;

public sealed class GenAiEtlAuditAnalyzerTests
{
    [Fact]
    public void Value_formula_calculates_covered_gross_and_net_value()
    {
        var result = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            Calls: 100,
            Coverage: 0.4,
            FrontierCostPerCall: 0.2,
            AlternativeCostPerCall: 0.05,
            MaintenanceCost: 1,
            ErrorCost: 2));

        Assert.Equal(GenAiEtlAuditValueStatus.Calculated, result.Status);
        AssertClose(20, result.FrontierSpend);
        AssertClose(8, result.CoveredFrontierSpend);
        AssertClose(2, result.CoveredAlternativeSpend);
        AssertClose(6, result.GrossReplaceableValue);
        AssertClose(3, result.NetReplaceableValue);
    }

    [Fact]
    public void Missing_frontier_cost_is_explicit_and_produces_no_calculated_values()
    {
        var result = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            Calls: 10,
            Coverage: 0.8,
            FrontierCostPerCall: null,
            AlternativeCostPerCall: 0.01,
            MaintenanceCost: 0,
            ErrorCost: 0));

        Assert.Equal(GenAiEtlAuditValueStatus.MissingFrontierCost, result.Status);
        Assert.Null(result.FrontierSpend);
        Assert.Null(result.CoveredFrontierSpend);
        Assert.Null(result.CoveredAlternativeSpend);
        Assert.Null(result.GrossReplaceableValue);
        Assert.Null(result.NetReplaceableValue);
    }

    [Fact]
    public void Formula_retains_negative_gross_and_net_value_when_alternative_is_more_expensive()
    {
        var result = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            Calls: 10,
            Coverage: 0.5,
            FrontierCostPerCall: 1,
            AlternativeCostPerCall: 2,
            MaintenanceCost: 1,
            ErrorCost: 0.5));

        Assert.Equal(GenAiEtlAuditValueStatus.Calculated, result.Status);
        AssertClose(-5, result.GrossReplaceableValue);
        AssertClose(-6.5, result.NetReplaceableValue);
    }

    [Fact]
    public void Zero_calls_have_zero_gross_value_but_still_include_explicit_operating_costs()
    {
        var result = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            Calls: 0,
            Coverage: 1,
            FrontierCostPerCall: 1,
            AlternativeCostPerCall: 0,
            MaintenanceCost: 2,
            ErrorCost: 1));

        Assert.Equal(GenAiEtlAuditValueStatus.Calculated, result.Status);
        Assert.Equal(0, result.FrontierSpend);
        Assert.Equal(0, result.GrossReplaceableValue);
        Assert.Equal(-3, result.NetReplaceableValue);
    }

    [Fact]
    public void Missing_formula_inputs_only_expose_values_supported_by_prior_evidence()
    {
        var missingCoverage = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            10, null, 2, 1, 0, 0));
        var missingAlternative = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            10, 0.5, 2, null, 0, 0));
        var missingMaintenance = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            10, 0.5, 2, 1, null, 0));
        var missingError = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
            10, 0.5, 2, 1, 0, null));

        Assert.Equal(GenAiEtlAuditValueStatus.MissingCoverage, missingCoverage.Status);
        Assert.Equal(20, missingCoverage.FrontierSpend);
        Assert.Null(missingCoverage.GrossReplaceableValue);

        Assert.Equal(GenAiEtlAuditValueStatus.MissingAlternativeCost, missingAlternative.Status);
        Assert.Equal(10, missingAlternative.CoveredFrontierSpend);
        Assert.Null(missingAlternative.GrossReplaceableValue);

        Assert.Equal(GenAiEtlAuditValueStatus.MissingMaintenanceCost, missingMaintenance.Status);
        Assert.Equal(5, missingMaintenance.GrossReplaceableValue);
        Assert.Null(missingMaintenance.NetReplaceableValue);

        Assert.Equal(GenAiEtlAuditValueStatus.MissingErrorCost, missingError.Status);
        Assert.Equal(5, missingError.GrossReplaceableValue);
        Assert.Null(missingError.NetReplaceableValue);
    }

    [Fact]
    public void Json_output_is_a_structured_extraction_hypothesis_with_task_specific_validation()
    {
        var cluster = AnalyzeSingle(Operation: "chat", OutputType: "application/json");

        Assert.Equal(GenAiEtlAuditOutputShape.Record, cluster.Inference.OutputContract);
        Assert.Equal(GenAiEtlAuditTaskFamily.StructuredExtraction, cluster.Inference.TaskFamily);
        Assert.Equal(
            GenAiEtlAuditInferenceStatus.InferredFromExplicitTelemetry,
            cluster.Inference.Status);
        Assert.Equal(GenAiEtlAuditCandidateStatus.HypothesisOnly, cluster.Candidate.Status);
        Assert.Equal(
            GenAiEtlAuditCandidatePath.ParserValidatorOrSmallExtractor,
            cluster.Candidate.Path);
        Assert.Equal(
            GenAiEtlAuditMetric.FieldExactMatchAndSchemaValidity,
            cluster.Candidate.Metric);
        Assert.Equal(
            GenAiEtlAuditResidualPath.FrontierModelOrHumanReviewOnValidationFailure,
            cluster.Candidate.ResidualPath);
    }

    [Fact]
    public void Embeddings_operation_is_a_similarity_hypothesis_even_without_output_type()
    {
        var cluster = AnalyzeSingle(Operation: "embeddings", OutputType: null);

        Assert.Equal(GenAiEtlAuditOutputShape.Embedding, cluster.Inference.OutputContract);
        Assert.Equal(GenAiEtlAuditTaskFamily.SimilarityAndClustering, cluster.Inference.TaskFamily);
        Assert.Equal(
            GenAiEtlAuditCandidatePath.BoundedRetrievalOrSimilarityIndex,
            cluster.Candidate.Path);
        Assert.Equal(GenAiEtlAuditMetric.MatchAccuracyRecallAtKOrMrr, cluster.Candidate.Metric);
        Assert.Equal(
            GenAiEtlAuditResidualPath.FrontierModelBelowConfidence,
            cluster.Candidate.ResidualPath);
    }

    [Fact]
    public void Explicit_text_output_remains_an_open_ended_frontier_hypothesis()
    {
        var cluster = AnalyzeSingle(Operation: "chat", OutputType: "text");

        Assert.Equal(GenAiEtlAuditOutputShape.Prose, cluster.Inference.OutputContract);
        Assert.Equal(
            GenAiEtlAuditTaskFamily.OpenEndedReasoningAndGeneration,
            cluster.Inference.TaskFamily);
        Assert.Equal(GenAiEtlAuditCandidatePath.KeepFrontierModel, cluster.Candidate.Path);
        Assert.Equal(GenAiEtlAuditMetric.HumanRubricOrTaskSuccess, cluster.Candidate.Metric);
        Assert.Equal(GenAiEtlAuditResidualPath.NotApplicable, cluster.Candidate.ResidualPath);
    }

    [Fact]
    public void Chat_without_an_output_type_does_not_invent_a_prose_contract()
    {
        var cluster = AnalyzeSingle(Operation: "chat", OutputType: null);

        Assert.Equal(GenAiEtlAuditOutputShape.Unknown, cluster.Inference.OutputContract);
        Assert.Equal(GenAiEtlAuditTaskFamily.Unknown, cluster.Inference.TaskFamily);
        Assert.Equal(GenAiEtlAuditInferenceStatus.InsufficientEvidence, cluster.Inference.Status);
        Assert.Equal(GenAiEtlAuditCandidateStatus.InsufficientEvidence, cluster.Candidate.Status);
    }

    [Fact]
    public void Unsupported_image_output_does_not_fall_back_to_a_prose_contract()
    {
        var cluster = AnalyzeSingle(Operation: "generate_content", OutputType: "image");

        Assert.Equal(GenAiEtlAuditOutputShape.Unknown, cluster.Inference.OutputContract);
        Assert.Equal(GenAiEtlAuditTaskFamily.Unknown, cluster.Inference.TaskFamily);
        Assert.Equal(GenAiEtlAuditInferenceStatus.InsufficientEvidence, cluster.Inference.Status);
        Assert.Equal(GenAiEtlAuditCandidateStatus.InsufficientEvidence, cluster.Candidate.Status);
    }

    [Fact]
    public void Unknown_operation_without_output_type_does_not_invent_a_contract_or_candidate()
    {
        var cluster = AnalyzeSingle(Operation: "execute_tool", OutputType: null);

        Assert.Equal(GenAiEtlAuditOutputShape.Unknown, cluster.Inference.OutputContract);
        Assert.Equal(GenAiEtlAuditTaskFamily.Unknown, cluster.Inference.TaskFamily);
        Assert.Equal(GenAiEtlAuditInferenceStatus.InsufficientEvidence, cluster.Inference.Status);
        Assert.Equal(GenAiEtlAuditCandidateStatus.InsufficientEvidence, cluster.Candidate.Status);
        Assert.Equal(GenAiEtlAuditCandidatePath.Undetermined, cluster.Candidate.Path);
        Assert.Equal(GenAiEtlAuditMetric.Undetermined, cluster.Candidate.Metric);
        Assert.Equal(GenAiEtlAuditResidualPath.Undetermined, cluster.Candidate.ResidualPath);
    }

    [Fact]
    public void Conflicting_embedding_telemetry_is_reported_instead_of_guessed()
    {
        var cluster = AnalyzeSingle(Operation: "embeddings", OutputType: "text");

        Assert.Equal(GenAiEtlAuditInferenceStatus.ConflictingTelemetry, cluster.Inference.Status);
        Assert.Equal(GenAiEtlAuditOutputShape.Unknown, cluster.Inference.OutputContract);
        Assert.Equal(GenAiEtlAuditTaskFamily.Unknown, cluster.Inference.TaskFamily);
        Assert.Equal(GenAiEtlAuditCandidateStatus.InsufficientEvidence, cluster.Candidate.Status);
    }

    [Fact]
    public void Analyzer_groups_privacy_safe_dimensions_and_computes_weighted_ratios()
    {
        var dimensions = new GenAiEtlAuditDimensions(
            "checkout-api",
            "chat",
            "json",
            "anthropic",
            "claude-sonnet");
        var result = GenAiEtlAuditAnalyzer.Analyze(
        [
            new GenAiEtlAuditObservation(
                dimensions,
                Calls: 30,
                FrontierCostPerCall: 2,
                AlternativeCostPerCall: 1,
                ReplacementCoverage: 0.5,
                MaintenanceCost: 2,
                ErrorCost: 1,
                MeasurableCoverage: 0.8,
                SafeDeferral: 0.6),
            new GenAiEtlAuditObservation(
                dimensions with { ServiceName = " CHECKOUT-API ", OperationName = "CHAT" },
                Calls: 10,
                FrontierCostPerCall: 4,
                AlternativeCostPerCall: 1,
                ReplacementCoverage: 1,
                MaintenanceCost: 1,
                ErrorCost: 0,
                MeasurableCoverage: 0.4,
                SafeDeferral: 0.2)
        ]);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(40, cluster.Calls);
        AssertClose(100, cluster.FrontierSpend);
        AssertClose(45, cluster.GrossReplaceableValue);
        AssertClose(41, cluster.NetReplaceableValue);
        AssertClose(0.625, cluster.ReplacementCoverage);
        AssertClose(0.7, cluster.MeasurableCoverage);
        AssertClose(0.5, cluster.SafeDeferral);
        AssertClose(1, cluster.SpendShare);
        AssertClose(1, result.EconomicConcentration);
    }

    [Fact]
    public void Totals_and_ratios_remain_null_when_any_cluster_lacks_cost_evidence()
    {
        var result = GenAiEtlAuditAnalyzer.Analyze(
        [
            CompleteObservation(
                new GenAiEtlAuditDimensions("service-a", "chat", "text", "openai", "frontier"),
                calls: 10,
                frontierCost: 2),
            new GenAiEtlAuditObservation(
                new GenAiEtlAuditDimensions("service-b", "chat", "json", "anthropic", "frontier"),
                Calls: 20,
                FrontierCostPerCall: null,
                AlternativeCostPerCall: 0.1,
                ReplacementCoverage: 0.5,
                MaintenanceCost: 0,
                ErrorCost: 0)
        ]);

        Assert.Equal(30, result.TotalCalls);
        Assert.Null(result.TotalFrontierSpend);
        Assert.Null(result.TotalGrossReplaceableValue);
        Assert.Null(result.TotalNetReplaceableValue);
        Assert.Null(result.EconomicConcentration);
        Assert.All(result.Clusters, cluster => Assert.Null(cluster.SpendShare));
        Assert.Contains(result.Clusters, cluster =>
            cluster.ValueStatus == GenAiEtlAuditValueStatus.MissingFrontierCost &&
            cluster.FrontierSpend is null);
    }

    [Fact]
    public void Economic_mass_orders_clusters_and_uses_the_requested_top_n()
    {
        var result = GenAiEtlAuditAnalyzer.Analyze(
        [
            CompleteObservation(Dimensions("small"), calls: 5, frontierCost: 1),
            CompleteObservation(Dimensions("largest"), calls: 10, frontierCost: 5),
            CompleteObservation(Dimensions("middle"), calls: 10, frontierCost: 2)
        ], economicConcentrationClusterCount: 2);

        Assert.Equal("largest", result.Clusters[0].Dimensions.ServiceName);
        Assert.Equal("middle", result.Clusters[1].Dimensions.ServiceName);
        Assert.Equal("small", result.Clusters[2].Dimensions.ServiceName);
        AssertClose(75, result.TotalFrontierSpend);
        AssertClose(70d / 75d, result.EconomicConcentration);
        AssertClose(50d / 75d, result.Clusters[0].SpendShare);
    }

    [Fact]
    public void Zero_call_clusters_do_not_claim_ratio_evidence_from_an_empty_denominator()
    {
        var observation = CompleteObservation(Dimensions("empty"), calls: 0, frontierCost: 1) with
        {
            MeasurableCoverage = 0.8,
            SafeDeferral = 0.8
        };

        var result = GenAiEtlAuditAnalyzer.Analyze([observation]);
        var cluster = Assert.Single(result.Clusters);

        Assert.Equal(0, result.TotalFrontierSpend);
        Assert.Null(result.EconomicConcentration);
        Assert.Null(cluster.SpendShare);
        Assert.Null(cluster.ReplacementCoverage);
        Assert.Null(cluster.MeasurableCoverage);
        Assert.Null(cluster.SafeDeferral);
    }

    [Fact]
    public void Empty_input_has_no_cost_totals_or_economic_ratio()
    {
        var result = GenAiEtlAuditAnalyzer.Analyze([]);

        Assert.Equal(0, result.TotalCalls);
        Assert.Null(result.TotalFrontierSpend);
        Assert.Null(result.TotalGrossReplaceableValue);
        Assert.Null(result.TotalNetReplaceableValue);
        Assert.Null(result.EconomicConcentration);
        Assert.Empty(result.Clusters);
    }

    [Fact]
    public void Evidence_inventory_and_all_six_promotion_gates_are_explicit_without_claiming_passage()
    {
        var observation = CompleteObservation(Dimensions("gated"), calls: 10, frontierCost: 1) with
        {
            AdditionalEvidence =
            [
                GenAiEtlAuditEvidence.StableContractWindow,
                GenAiEtlAuditEvidence.OfflineReplay,
                GenAiEtlAuditEvidence.RollbackPolicy
            ]
        };

        var cluster = Assert.Single(GenAiEtlAuditAnalyzer.Analyze([observation]).Clusters);

        Assert.Contains(GenAiEtlAuditEvidence.FrontierUnitCost, cluster.PresentEvidence);
        Assert.Contains(GenAiEtlAuditEvidence.StableContractWindow, cluster.PresentEvidence);
        Assert.Contains(GenAiEtlAuditEvidence.CalibratedConfidence, cluster.MissingEvidence);
        Assert.Equal(6, cluster.PromotionGates.Count);
        Assert.Equal(
            GenAiEtlAuditGateEvidenceStatus.EvidenceAvailable,
            Gate(cluster, GenAiEtlAuditPromotionGate.ContractStability).Status);
        Assert.Equal(
            GenAiEtlAuditGateEvidenceStatus.EvidenceAvailable,
            Gate(cluster, GenAiEtlAuditPromotionGate.OfflineReplay).Status);
        Assert.Equal(
            GenAiEtlAuditGateEvidenceStatus.EvidenceMissing,
            Gate(cluster, GenAiEtlAuditPromotionGate.CalibratedConfidence).Status);
        Assert.Equal(
            GenAiEtlAuditGateEvidenceStatus.EvidenceMissing,
            Gate(cluster, GenAiEtlAuditPromotionGate.ShadowTraffic).Status);
        Assert.Equal(
            GenAiEtlAuditGateEvidenceStatus.EvidenceMissing,
            Gate(cluster, GenAiEtlAuditPromotionGate.LimitedServing).Status);

        var rollbackGate = Gate(cluster, GenAiEtlAuditPromotionGate.RollbackAndResidualPolicy);
        Assert.Equal(GenAiEtlAuditGateEvidenceStatus.EvidenceMissing, rollbackGate.Status);
        Assert.Equal([GenAiEtlAuditEvidence.ResidualPolicy], rollbackGate.MissingEvidence);
    }

    [Fact]
    public void Observations_reject_invalid_ratios_and_non_finite_costs_before_grouping()
    {
        var invalidCoverage = CompleteObservation(Dimensions("invalid"), 1, 1) with
        {
            ReplacementCoverage = 1.1
        };
        var nonFiniteCost = CompleteObservation(Dimensions("invalid"), 1, 1) with
        {
            FrontierCostPerCall = double.PositiveInfinity
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GenAiEtlAuditAnalyzer.Analyze([invalidCoverage]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GenAiEtlAuditAnalyzer.Analyze([nonFiniteCost]));
    }

    private static GenAiEtlAuditAnalysisCluster AnalyzeSingle(string? Operation, string? OutputType) =>
        Assert.Single(GenAiEtlAuditAnalyzer.Analyze(
        [
            CompleteObservation(
                new GenAiEtlAuditDimensions("test-service", Operation, OutputType, "provider", "model"),
                calls: 1,
                frontierCost: 1)
        ]).Clusters);

    private static GenAiEtlAuditGateAssessment Gate(
        GenAiEtlAuditAnalysisCluster cluster,
        GenAiEtlAuditPromotionGate gate) =>
        Assert.Single(cluster.PromotionGates, assessment => assessment.Gate == gate);

    private static GenAiEtlAuditObservation CompleteObservation(
        GenAiEtlAuditDimensions dimensions,
        long calls,
        double frontierCost) =>
        new(
            dimensions,
            calls,
            FrontierCostPerCall: frontierCost,
            AlternativeCostPerCall: 0,
            ReplacementCoverage: 1,
            MaintenanceCost: 0,
            ErrorCost: 0,
            MeasurableCoverage: 1,
            SafeDeferral: 1);

    private static GenAiEtlAuditDimensions Dimensions(string serviceName) =>
        new(serviceName, "chat", "json", "provider", "model");

    private static void AssertClose(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Value, 10);
    }
}

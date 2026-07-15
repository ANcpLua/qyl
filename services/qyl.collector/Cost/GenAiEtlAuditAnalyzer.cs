namespace Qyl.Collector.Cost;

internal enum GenAiEtlAuditEvidence
{
    CallCount,
    ExplicitOperationName,
    ExplicitOutputType,
    PromptFamily,
    InputClass,
    OutputSchema,
    ReferenceData,
    FrontierUnitCost,
    AlternativeUnitCost,
    ReplacementCoverage,
    MeasurableCoverage,
    SafeDeferral,
    MaintenanceCost,
    ErrorCost,
    Latency,
    RetryCount,
    GenerationConstraints,
    DownstreamFate,
    StableContractWindow,
    OfflineReplay,
    CalibratedConfidence,
    ShadowTraffic,
    LimitedServing,
    RollbackPolicy,
    ResidualPolicy
}

internal enum GenAiEtlAuditOutputShape
{
    Unknown,
    Prose,
    Label,
    SpanList,
    Record,
    CatalogReference,
    Number,
    Boolean,
    Embedding
}

internal enum GenAiEtlAuditTaskFamily
{
    Unknown,
    OpenEndedReasoningAndGeneration,
    Classification,
    SequenceLabeling,
    StructuredExtraction,
    RetrievalAndEntityResolution,
    SimilarityAndClustering,
    NormalizationAndTransformation,
    NumericAndAnalyticalComputation
}

internal enum GenAiEtlAuditInferenceStatus
{
    InferredFromExplicitTelemetry,
    InsufficientEvidence,
    ConflictingTelemetry
}

internal enum GenAiEtlAuditCandidateStatus
{
    HypothesisOnly,
    InsufficientEvidence
}

internal enum GenAiEtlAuditCandidatePath
{
    Undetermined,
    KeepFrontierModel,
    DeterministicCode,
    ParserValidatorOrSmallExtractor,
    SmallClassifierOrExtractor,
    BoundedRetrievalOrSimilarityIndex
}

internal enum GenAiEtlAuditMetric
{
    Undetermined,
    HumanRubricOrTaskSuccess,
    MacroF1AndCalibration,
    SpanPrecisionRecallF1,
    FieldExactMatchAndSchemaValidity,
    MatchAccuracyRecallAtKOrMrr,
    ExactMatch,
    ExactOrToleranceNumericMatch
}

internal enum GenAiEtlAuditResidualPath
{
    Undetermined,
    NotApplicable,
    FrontierModelBelowConfidence,
    FrontierModelOrHumanReviewOnValidationFailure
}

internal enum GenAiEtlAuditPromotionGate
{
    ContractStability,
    OfflineReplay,
    CalibratedConfidence,
    ShadowTraffic,
    LimitedServing,
    RollbackAndResidualPolicy
}

internal enum GenAiEtlAuditGateEvidenceStatus
{
    EvidenceAvailable,
    EvidenceMissing
}

internal enum GenAiEtlAuditValueStatus
{
    Calculated,
    MissingFrontierCost,
    MissingCoverage,
    MissingAlternativeCost,
    MissingMaintenanceCost,
    MissingErrorCost
}

internal sealed record GenAiEtlAuditDimensions(
    string? ServiceName,
    string? OperationName,
    string? OutputType,
    string? ProviderName,
    string? ModelName);

internal sealed record GenAiEtlAuditObservation(
    GenAiEtlAuditDimensions Dimensions,
    long Calls,
    double? FrontierCostPerCall = null,
    double? AlternativeCostPerCall = null,
    double? ReplacementCoverage = null,
    double? MaintenanceCost = null,
    double? ErrorCost = null,
    double? MeasurableCoverage = null,
    double? SafeDeferral = null,
    IReadOnlyCollection<GenAiEtlAuditEvidence>? AdditionalEvidence = null);

internal sealed record GenAiEtlAuditValueInput(
    long Calls,
    double? Coverage,
    double? FrontierCostPerCall,
    double? AlternativeCostPerCall,
    double? MaintenanceCost,
    double? ErrorCost);

internal sealed record GenAiEtlAuditValueResult(
    GenAiEtlAuditValueStatus Status,
    double? FrontierSpend,
    double? CoveredFrontierSpend,
    double? CoveredAlternativeSpend,
    double? GrossReplaceableValue,
    double? NetReplaceableValue);

internal sealed record GenAiEtlAuditInference(
    GenAiEtlAuditOutputShape OutputContract,
    GenAiEtlAuditTaskFamily TaskFamily,
    GenAiEtlAuditInferenceStatus Status);

internal sealed record GenAiEtlAuditCandidate(
    GenAiEtlAuditCandidateStatus Status,
    GenAiEtlAuditCandidatePath Path,
    GenAiEtlAuditMetric Metric,
    GenAiEtlAuditResidualPath ResidualPath);

internal sealed record GenAiEtlAuditGateAssessment(
    GenAiEtlAuditPromotionGate Gate,
    GenAiEtlAuditGateEvidenceStatus Status,
    IReadOnlyList<GenAiEtlAuditEvidence> RequiredEvidence,
    IReadOnlyList<GenAiEtlAuditEvidence> MissingEvidence);

internal sealed record GenAiEtlAuditAnalysisCluster(
    GenAiEtlAuditDimensions Dimensions,
    long Calls,
    GenAiEtlAuditInference Inference,
    GenAiEtlAuditCandidate Candidate,
    GenAiEtlAuditValueStatus ValueStatus,
    double? FrontierSpend,
    double? GrossReplaceableValue,
    double? NetReplaceableValue,
    double? SpendShare,
    double? ReplacementCoverage,
    double? MeasurableCoverage,
    double? SafeDeferral,
    IReadOnlyList<GenAiEtlAuditEvidence> PresentEvidence,
    IReadOnlyList<GenAiEtlAuditEvidence> MissingEvidence,
    IReadOnlyList<GenAiEtlAuditGateAssessment> PromotionGates);

internal sealed record GenAiEtlAuditResult(
    long TotalCalls,
    double? TotalFrontierSpend,
    double? TotalGrossReplaceableValue,
    double? TotalNetReplaceableValue,
    int EconomicConcentrationClusterCount,
    double? EconomicConcentration,
    IReadOnlyList<GenAiEtlAuditAnalysisCluster> Clusters);

internal static class GenAiEtlAuditAnalyzer
{
    private static readonly GenAiEtlAuditEvidence[] s_allEvidence =
        Enum.GetValues<GenAiEtlAuditEvidence>();

    private static readonly GateDefinition[] s_gateDefinitions =
    [
        new(
            GenAiEtlAuditPromotionGate.ContractStability,
            [GenAiEtlAuditEvidence.StableContractWindow]),
        new(
            GenAiEtlAuditPromotionGate.OfflineReplay,
            [GenAiEtlAuditEvidence.OfflineReplay]),
        new(
            GenAiEtlAuditPromotionGate.CalibratedConfidence,
            [GenAiEtlAuditEvidence.CalibratedConfidence]),
        new(
            GenAiEtlAuditPromotionGate.ShadowTraffic,
            [GenAiEtlAuditEvidence.ShadowTraffic]),
        new(
            GenAiEtlAuditPromotionGate.LimitedServing,
            [GenAiEtlAuditEvidence.LimitedServing]),
        new(
            GenAiEtlAuditPromotionGate.RollbackAndResidualPolicy,
            [GenAiEtlAuditEvidence.RollbackPolicy, GenAiEtlAuditEvidence.ResidualPolicy])
    ];

    internal static GenAiEtlAuditResult Analyze(
        IEnumerable<GenAiEtlAuditObservation> observations,
        int economicConcentrationClusterCount = 3)
    {
        observations = Require(observations, nameof(observations));
        if (economicConcentrationClusterCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(economicConcentrationClusterCount),
                economicConcentrationClusterCount,
                "The economic concentration cluster count must be positive.");
        }

        var materialized = observations.ToArray();
        foreach (var observation in materialized)
        {
            ValidateObservation(observation);
        }

        var clusters = materialized
            .GroupBy(static observation => CreateClusterKey(observation.Dimensions))
            .Select(static group => AnalyzeCluster(group.ToArray()))
            .OrderByDescending(static cluster => cluster.FrontierSpend.HasValue)
            .ThenByDescending(static cluster => cluster.FrontierSpend)
            .ThenByDescending(static cluster => cluster.Calls)
            .ThenBy(static cluster => cluster.Dimensions.ServiceName, StringComparer.Ordinal)
            .ThenBy(static cluster => cluster.Dimensions.OperationName, StringComparer.Ordinal)
            .ThenBy(static cluster => cluster.Dimensions.OutputType, StringComparer.Ordinal)
            .ThenBy(static cluster => cluster.Dimensions.ProviderName, StringComparer.Ordinal)
            .ThenBy(static cluster => cluster.Dimensions.ModelName, StringComparer.Ordinal)
            .ToArray();

        var totalCalls = SumCalls(materialized.Select(static observation => observation.Calls));
        var totalFrontierSpend = SumOnlyWhenComplete(clusters.Select(static cluster => cluster.FrontierSpend));
        var totalGrossValue = SumOnlyWhenComplete(clusters.Select(static cluster => cluster.GrossReplaceableValue));
        var totalNetValue = SumOnlyWhenComplete(clusters.Select(static cluster => cluster.NetReplaceableValue));

        double? economicConcentration = null;
        if (totalFrontierSpend is > 0)
        {
            economicConcentration = DivideFinite(
                clusters
                    .Take(economicConcentrationClusterCount)
                    .Sum(static cluster => cluster.FrontierSpend ?? 0),
                totalFrontierSpend.Value,
                nameof(economicConcentration));

            clusters = clusters
                .Select(cluster => cluster with
                {
                    SpendShare = DivideFinite(
                        cluster.FrontierSpend!.Value,
                        totalFrontierSpend.Value,
                        nameof(GenAiEtlAuditAnalysisCluster.SpendShare))
                })
                .ToArray();
        }

        return new GenAiEtlAuditResult(
            totalCalls,
            totalFrontierSpend,
            totalGrossValue,
            totalNetValue,
            economicConcentrationClusterCount,
            economicConcentration,
            clusters);
    }

    internal static GenAiEtlAuditValueResult Evaluate(GenAiEtlAuditValueInput input)
    {
        input = Require(input, nameof(input));
        ValidateCalls(input.Calls);
        ValidateRatio(input.Coverage, nameof(input.Coverage));
        ValidateNonNegative(input.FrontierCostPerCall, nameof(input.FrontierCostPerCall));
        ValidateNonNegative(input.AlternativeCostPerCall, nameof(input.AlternativeCostPerCall));
        ValidateNonNegative(input.MaintenanceCost, nameof(input.MaintenanceCost));
        ValidateNonNegative(input.ErrorCost, nameof(input.ErrorCost));

        if (!input.FrontierCostPerCall.HasValue)
        {
            return MissingValue(GenAiEtlAuditValueStatus.MissingFrontierCost);
        }

        var frontierSpend = MultiplyFinite(
            input.Calls,
            input.FrontierCostPerCall.Value,
            nameof(GenAiEtlAuditValueResult.FrontierSpend));
        if (!input.Coverage.HasValue)
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingCoverage,
                frontierSpend,
                null,
                null,
                null,
                null);
        }

        var coveredCalls = MultiplyFinite(
            input.Calls,
            input.Coverage.Value,
            nameof(input.Coverage));
        var coveredFrontierSpend = MultiplyFinite(
            coveredCalls,
            input.FrontierCostPerCall.Value,
            nameof(GenAiEtlAuditValueResult.CoveredFrontierSpend));
        if (!input.AlternativeCostPerCall.HasValue)
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingAlternativeCost,
                frontierSpend,
                coveredFrontierSpend,
                null,
                null,
                null);
        }

        var coveredAlternativeSpend = MultiplyFinite(
            coveredCalls,
            input.AlternativeCostPerCall.Value,
            nameof(GenAiEtlAuditValueResult.CoveredAlternativeSpend));
        var grossValue = SubtractFinite(
            coveredFrontierSpend,
            coveredAlternativeSpend,
            nameof(GenAiEtlAuditValueResult.GrossReplaceableValue));
        if (!input.MaintenanceCost.HasValue)
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingMaintenanceCost,
                frontierSpend,
                coveredFrontierSpend,
                coveredAlternativeSpend,
                grossValue,
                null);
        }

        if (!input.ErrorCost.HasValue)
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingErrorCost,
                frontierSpend,
                coveredFrontierSpend,
                coveredAlternativeSpend,
                grossValue,
                null);
        }

        var netValue = SubtractFinite(
            SubtractFinite(
                grossValue,
                input.MaintenanceCost.Value,
                nameof(GenAiEtlAuditValueResult.NetReplaceableValue)),
            input.ErrorCost.Value,
            nameof(GenAiEtlAuditValueResult.NetReplaceableValue));

        return new GenAiEtlAuditValueResult(
            GenAiEtlAuditValueStatus.Calculated,
            frontierSpend,
            coveredFrontierSpend,
            coveredAlternativeSpend,
            grossValue,
            netValue);
    }

    private static GenAiEtlAuditAnalysisCluster AnalyzeCluster(GenAiEtlAuditObservation[] observations)
    {
        var first = observations[0];
        var calls = SumCalls(observations.Select(static observation => observation.Calls));
        var inference = Infer(first.Dimensions.OperationName, first.Dimensions.OutputType);
        var candidate = ChooseCandidate(inference.TaskFamily);
        var values = AggregateValues(observations);
        var presentEvidence = CollectPresentEvidence(observations);
        var missingEvidence = s_allEvidence.Except(presentEvidence).ToArray();

        return new GenAiEtlAuditAnalysisCluster(
            NormalizeDimensions(first.Dimensions),
            calls,
            inference,
            candidate,
            values.Status,
            values.FrontierSpend,
            values.GrossReplaceableValue,
            values.NetReplaceableValue,
            null,
            WeightedRatio(observations, static observation => observation.ReplacementCoverage),
            WeightedRatio(observations, static observation => observation.MeasurableCoverage),
            WeightedRatio(observations, static observation => observation.SafeDeferral),
            presentEvidence,
            missingEvidence,
            AssessPromotionGates(presentEvidence));
    }

    private static GenAiEtlAuditValueResult AggregateValues(GenAiEtlAuditObservation[] observations)
    {
        if (observations.Any(static observation => !observation.FrontierCostPerCall.HasValue))
        {
            return MissingValue(GenAiEtlAuditValueStatus.MissingFrontierCost);
        }

        var frontierSpend = SumFinite(observations.Select(static observation =>
            MultiplyFinite(
                observation.Calls,
                observation.FrontierCostPerCall!.Value,
                nameof(GenAiEtlAuditValueResult.FrontierSpend))),
            nameof(GenAiEtlAuditValueResult.FrontierSpend));
        if (observations.Any(static observation => !observation.ReplacementCoverage.HasValue))
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingCoverage,
                frontierSpend,
                null,
                null,
                null,
                null);
        }

        var coveredFrontierSpend = SumFinite(observations.Select(static observation =>
        {
            var coveredCalls = MultiplyFinite(
                observation.Calls,
                observation.ReplacementCoverage!.Value,
                nameof(observation.ReplacementCoverage));
            return MultiplyFinite(
                coveredCalls,
                observation.FrontierCostPerCall!.Value,
                nameof(GenAiEtlAuditValueResult.CoveredFrontierSpend));
        }), nameof(GenAiEtlAuditValueResult.CoveredFrontierSpend));

        if (observations.Any(static observation => !observation.AlternativeCostPerCall.HasValue))
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingAlternativeCost,
                frontierSpend,
                coveredFrontierSpend,
                null,
                null,
                null);
        }

        var coveredAlternativeSpend = SumFinite(observations.Select(static observation =>
        {
            var coveredCalls = MultiplyFinite(
                observation.Calls,
                observation.ReplacementCoverage!.Value,
                nameof(observation.ReplacementCoverage));
            return MultiplyFinite(
                coveredCalls,
                observation.AlternativeCostPerCall!.Value,
                nameof(GenAiEtlAuditValueResult.CoveredAlternativeSpend));
        }), nameof(GenAiEtlAuditValueResult.CoveredAlternativeSpend));
        var grossValue = SubtractFinite(
            coveredFrontierSpend,
            coveredAlternativeSpend,
            nameof(GenAiEtlAuditValueResult.GrossReplaceableValue));

        if (observations.Any(static observation => !observation.MaintenanceCost.HasValue))
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingMaintenanceCost,
                frontierSpend,
                coveredFrontierSpend,
                coveredAlternativeSpend,
                grossValue,
                null);
        }

        if (observations.Any(static observation => !observation.ErrorCost.HasValue))
        {
            return new GenAiEtlAuditValueResult(
                GenAiEtlAuditValueStatus.MissingErrorCost,
                frontierSpend,
                coveredFrontierSpend,
                coveredAlternativeSpend,
                grossValue,
                null);
        }

        var maintenanceCost = SumFinite(
            observations.Select(static observation => observation.MaintenanceCost!.Value),
            nameof(GenAiEtlAuditObservation.MaintenanceCost));
        var errorCost = SumFinite(
            observations.Select(static observation => observation.ErrorCost!.Value),
            nameof(GenAiEtlAuditObservation.ErrorCost));
        var netValue = SubtractFinite(
            SubtractFinite(
                grossValue,
                maintenanceCost,
                nameof(GenAiEtlAuditValueResult.NetReplaceableValue)),
            errorCost,
            nameof(GenAiEtlAuditValueResult.NetReplaceableValue));

        return new GenAiEtlAuditValueResult(
            GenAiEtlAuditValueStatus.Calculated,
            frontierSpend,
            coveredFrontierSpend,
            coveredAlternativeSpend,
            grossValue,
            netValue);
    }

    private static GenAiEtlAuditInference Infer(string? operationName, string? outputType)
    {
        var operation = NormalizeSemanticValue(operationName);
        var output = NormalizeSemanticValue(outputType);

        if (operation is "embeddings" or "embedding")
        {
            if (output is not null && !IsEmbeddingOutput(output))
            {
                return new GenAiEtlAuditInference(
                    GenAiEtlAuditOutputShape.Unknown,
                    GenAiEtlAuditTaskFamily.Unknown,
                    GenAiEtlAuditInferenceStatus.ConflictingTelemetry);
            }

            return Inferred(
                GenAiEtlAuditOutputShape.Embedding,
                GenAiEtlAuditTaskFamily.SimilarityAndClustering);
        }

        var outputInference = InferFromOutput(output);
        if (outputInference is not null)
        {
            return outputInference;
        }

        if (output is not null)
        {
            return new GenAiEtlAuditInference(
                GenAiEtlAuditOutputShape.Unknown,
                GenAiEtlAuditTaskFamily.Unknown,
                GenAiEtlAuditInferenceStatus.InsufficientEvidence);
        }

        return operation switch
        {
            "chat" or "text_completion" or "generate_content" or "completion" =>
                new GenAiEtlAuditInference(
                    GenAiEtlAuditOutputShape.Unknown,
                    GenAiEtlAuditTaskFamily.Unknown,
                    GenAiEtlAuditInferenceStatus.InsufficientEvidence),
            "classify" or "classification" => Inferred(
                GenAiEtlAuditOutputShape.Label,
                GenAiEtlAuditTaskFamily.Classification),
            "extract" or "extraction" => Inferred(
                GenAiEtlAuditOutputShape.Record,
                GenAiEtlAuditTaskFamily.StructuredExtraction),
            "retrieve" or "retrieval" or "entity_resolution" =>
                new GenAiEtlAuditInference(
                    GenAiEtlAuditOutputShape.Unknown,
                    GenAiEtlAuditTaskFamily.Unknown,
                    GenAiEtlAuditInferenceStatus.InsufficientEvidence),
            "similarity" or "cluster" or "clustering" or "rerank" => Inferred(
                GenAiEtlAuditOutputShape.Embedding,
                GenAiEtlAuditTaskFamily.SimilarityAndClustering),
            "normalize" or "normalization" or "transform" => Inferred(
                GenAiEtlAuditOutputShape.Unknown,
                GenAiEtlAuditTaskFamily.NormalizationAndTransformation),
            "compute" or "calculation" or "numeric" => Inferred(
                GenAiEtlAuditOutputShape.Number,
                GenAiEtlAuditTaskFamily.NumericAndAnalyticalComputation),
            _ => new GenAiEtlAuditInference(
                GenAiEtlAuditOutputShape.Unknown,
                GenAiEtlAuditTaskFamily.Unknown,
                GenAiEtlAuditInferenceStatus.InsufficientEvidence)
        };
    }

    private static GenAiEtlAuditInference? InferFromOutput(string? output) => output switch
    {
        "json" or "application/json" or "json_schema" or "object" or "record" or "record{schema}" =>
            Inferred(
                GenAiEtlAuditOutputShape.Record,
                GenAiEtlAuditTaskFamily.StructuredExtraction),
        "text" or "text/plain" or "prose" =>
            Inferred(
                GenAiEtlAuditOutputShape.Prose,
                GenAiEtlAuditTaskFamily.OpenEndedReasoningAndGeneration),
        "label" or "class" or "classification" =>
            Inferred(
                GenAiEtlAuditOutputShape.Label,
                GenAiEtlAuditTaskFamily.Classification),
        "span" or "spans" or "span[]" or "entities" =>
            Inferred(
                GenAiEtlAuditOutputShape.SpanList,
                GenAiEtlAuditTaskFamily.SequenceLabeling),
        "catalog_ref" or "catalog_reference" or "entity_ref" =>
            Inferred(
                GenAiEtlAuditOutputShape.CatalogReference,
                GenAiEtlAuditTaskFamily.RetrievalAndEntityResolution),
        "number" or "numeric" =>
            Inferred(
                GenAiEtlAuditOutputShape.Number,
                GenAiEtlAuditTaskFamily.NumericAndAnalyticalComputation),
        "boolean" or "bool" =>
            Inferred(
                GenAiEtlAuditOutputShape.Boolean,
                GenAiEtlAuditTaskFamily.Classification),
        "embedding" or "embeddings" or "vector" or "float[]" =>
            Inferred(
                GenAiEtlAuditOutputShape.Embedding,
                GenAiEtlAuditTaskFamily.SimilarityAndClustering),
        _ => null
    };

    private static GenAiEtlAuditInference Inferred(
        GenAiEtlAuditOutputShape outputContract,
        GenAiEtlAuditTaskFamily taskFamily) =>
        new(outputContract, taskFamily, GenAiEtlAuditInferenceStatus.InferredFromExplicitTelemetry);

    private static GenAiEtlAuditCandidate ChooseCandidate(GenAiEtlAuditTaskFamily family) => family switch
    {
        GenAiEtlAuditTaskFamily.OpenEndedReasoningAndGeneration => Hypothesis(
            GenAiEtlAuditCandidatePath.KeepFrontierModel,
            GenAiEtlAuditMetric.HumanRubricOrTaskSuccess,
            GenAiEtlAuditResidualPath.NotApplicable),
        GenAiEtlAuditTaskFamily.Classification => Hypothesis(
            GenAiEtlAuditCandidatePath.SmallClassifierOrExtractor,
            GenAiEtlAuditMetric.MacroF1AndCalibration,
            GenAiEtlAuditResidualPath.FrontierModelBelowConfidence),
        GenAiEtlAuditTaskFamily.SequenceLabeling => Hypothesis(
            GenAiEtlAuditCandidatePath.SmallClassifierOrExtractor,
            GenAiEtlAuditMetric.SpanPrecisionRecallF1,
            GenAiEtlAuditResidualPath.FrontierModelBelowConfidence),
        GenAiEtlAuditTaskFamily.StructuredExtraction => Hypothesis(
            GenAiEtlAuditCandidatePath.ParserValidatorOrSmallExtractor,
            GenAiEtlAuditMetric.FieldExactMatchAndSchemaValidity,
            GenAiEtlAuditResidualPath.FrontierModelOrHumanReviewOnValidationFailure),
        GenAiEtlAuditTaskFamily.RetrievalAndEntityResolution => Hypothesis(
            GenAiEtlAuditCandidatePath.BoundedRetrievalOrSimilarityIndex,
            GenAiEtlAuditMetric.MatchAccuracyRecallAtKOrMrr,
            GenAiEtlAuditResidualPath.FrontierModelBelowConfidence),
        GenAiEtlAuditTaskFamily.SimilarityAndClustering => Hypothesis(
            GenAiEtlAuditCandidatePath.BoundedRetrievalOrSimilarityIndex,
            GenAiEtlAuditMetric.MatchAccuracyRecallAtKOrMrr,
            GenAiEtlAuditResidualPath.FrontierModelBelowConfidence),
        GenAiEtlAuditTaskFamily.NormalizationAndTransformation => Hypothesis(
            GenAiEtlAuditCandidatePath.DeterministicCode,
            GenAiEtlAuditMetric.ExactMatch,
            GenAiEtlAuditResidualPath.FrontierModelOrHumanReviewOnValidationFailure),
        GenAiEtlAuditTaskFamily.NumericAndAnalyticalComputation => Hypothesis(
            GenAiEtlAuditCandidatePath.DeterministicCode,
            GenAiEtlAuditMetric.ExactOrToleranceNumericMatch,
            GenAiEtlAuditResidualPath.FrontierModelOrHumanReviewOnValidationFailure),
        _ => new GenAiEtlAuditCandidate(
            GenAiEtlAuditCandidateStatus.InsufficientEvidence,
            GenAiEtlAuditCandidatePath.Undetermined,
            GenAiEtlAuditMetric.Undetermined,
            GenAiEtlAuditResidualPath.Undetermined)
    };

    private static GenAiEtlAuditCandidate Hypothesis(
        GenAiEtlAuditCandidatePath path,
        GenAiEtlAuditMetric metric,
        GenAiEtlAuditResidualPath residualPath) =>
        new(GenAiEtlAuditCandidateStatus.HypothesisOnly, path, metric, residualPath);

    private static IReadOnlyList<GenAiEtlAuditEvidence> CollectPresentEvidence(
        GenAiEtlAuditObservation[] observations)
    {
        var present = new HashSet<GenAiEtlAuditEvidence>();
        foreach (var observation in observations)
        {
            if (observation.AdditionalEvidence is not null)
            {
                present.UnionWith(observation.AdditionalEvidence);
            }
        }

        present.Add(GenAiEtlAuditEvidence.CallCount);
        var dimensions = observations[0].Dimensions;
        if (NormalizeSemanticValue(dimensions.OperationName) is not null)
        {
            present.Add(GenAiEtlAuditEvidence.ExplicitOperationName);
        }

        if (NormalizeSemanticValue(dimensions.OutputType) is not null)
        {
            present.Add(GenAiEtlAuditEvidence.ExplicitOutputType);
        }

        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.FrontierUnitCost,
            observations,
            static observation => observation.FrontierCostPerCall.HasValue);
        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.AlternativeUnitCost,
            observations,
            static observation => observation.AlternativeCostPerCall.HasValue);
        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.ReplacementCoverage,
            observations,
            static observation => observation.ReplacementCoverage.HasValue);
        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.MeasurableCoverage,
            observations,
            static observation => observation.MeasurableCoverage.HasValue);
        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.SafeDeferral,
            observations,
            static observation => observation.SafeDeferral.HasValue);
        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.MaintenanceCost,
            observations,
            static observation => observation.MaintenanceCost.HasValue);
        AddWhenComplete(
            present,
            GenAiEtlAuditEvidence.ErrorCost,
            observations,
            static observation => observation.ErrorCost.HasValue);

        return present.Order().ToArray();
    }

    private static void AddWhenComplete(
        ISet<GenAiEtlAuditEvidence> evidence,
        GenAiEtlAuditEvidence kind,
        GenAiEtlAuditObservation[] observations,
        Func<GenAiEtlAuditObservation, bool> predicate)
    {
        if (observations.All(predicate)) evidence.Add(kind);
    }

    private static IReadOnlyList<GenAiEtlAuditGateAssessment> AssessPromotionGates(
        IReadOnlyCollection<GenAiEtlAuditEvidence> presentEvidence)
    {
        var present = presentEvidence.ToHashSet();
        return s_gateDefinitions.Select(definition =>
        {
            var missing = definition.RequiredEvidence.Where(evidence => !present.Contains(evidence)).ToArray();
            return new GenAiEtlAuditGateAssessment(
                definition.Gate,
                missing.Length == 0
                    ? GenAiEtlAuditGateEvidenceStatus.EvidenceAvailable
                    : GenAiEtlAuditGateEvidenceStatus.EvidenceMissing,
                definition.RequiredEvidence,
                missing);
        }).ToArray();
    }

    private static double? WeightedRatio(
        GenAiEtlAuditObservation[] observations,
        Func<GenAiEtlAuditObservation, double?> selector)
    {
        if (observations.Any(observation => !selector(observation).HasValue)) return null;

        var calls = SumCalls(observations.Select(static observation => observation.Calls));
        if (calls == 0) return null;

        var numerator = SumFinite(observations.Select(observation =>
            MultiplyFinite(observation.Calls, selector(observation)!.Value, nameof(selector))),
            nameof(selector));
        return DivideFinite(numerator, calls, nameof(selector));
    }

    private static double? SumOnlyWhenComplete(IEnumerable<double?> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 || materialized.Any(static value => !value.HasValue)
            ? null
            : SumFinite(materialized.Select(static value => value!.Value), nameof(values));
    }

    private static long SumCalls(IEnumerable<long> calls)
    {
        var result = 0L;
        foreach (var callCount in calls)
        {
            result = checked(result + callCount);
        }

        return result;
    }

    private static double SumFinite(IEnumerable<double> values, string parameterName)
    {
        var result = 0d;
        foreach (var value in values)
        {
            result += value;
            if (!double.IsFinite(result))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    result,
                    "The calculated value must be finite.");
            }
        }

        return result;
    }

    private static double MultiplyFinite(double left, double right, string parameterName)
    {
        var result = left * right;
        if (!double.IsFinite(result))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                result,
                "The calculated value must be finite.");
        }

        return result;
    }

    private static double SubtractFinite(double left, double right, string parameterName)
    {
        var result = left - right;
        if (!double.IsFinite(result))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                result,
                "The calculated value must be finite.");
        }

        return result;
    }

    private static double DivideFinite(double numerator, double denominator, string parameterName)
    {
        var result = numerator / denominator;
        if (!double.IsFinite(result))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                result,
                "The calculated ratio must be finite.");
        }

        return result;
    }

    private static void ValidateObservation(GenAiEtlAuditObservation observation)
    {
        observation = Require(observation, nameof(observation));

        if (observation.Dimensions is null)
        {
            throw new ArgumentException("Observation dimensions are required.", nameof(observation));
        }
        ValidateCalls(observation.Calls);
        ValidateNonNegative(observation.FrontierCostPerCall, nameof(observation.FrontierCostPerCall));
        ValidateNonNegative(observation.AlternativeCostPerCall, nameof(observation.AlternativeCostPerCall));
        ValidateRatio(observation.ReplacementCoverage, nameof(observation.ReplacementCoverage));
        ValidateNonNegative(observation.MaintenanceCost, nameof(observation.MaintenanceCost));
        ValidateNonNegative(observation.ErrorCost, nameof(observation.ErrorCost));
        ValidateRatio(observation.MeasurableCoverage, nameof(observation.MeasurableCoverage));
        ValidateRatio(observation.SafeDeferral, nameof(observation.SafeDeferral));
    }

    private static void ValidateCalls(long calls)
    {
        if (calls < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(calls), calls, "Calls must be non-negative.");
        }
    }

    private static void ValidateNonNegative(double? value, string parameterName)
    {
        if (value is not { } actual) return;
        if (!double.IsFinite(actual) || actual < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                actual,
                "The value must be finite and non-negative.");
        }
    }

    private static void ValidateRatio(double? value, string parameterName)
    {
        if (value is not { } actual) return;
        if (!double.IsFinite(actual) || actual is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                actual,
                "The ratio must be finite and between zero and one, inclusive.");
        }
    }

    private static GenAiEtlAuditValueResult MissingValue(GenAiEtlAuditValueStatus status) =>
        new(status, null, null, null, null, null);

    private static T Require<T>(T? value, string parameterName) where T : class
    {
        if (value is null) ThrowNull(parameterName);
        return value;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowNull(string parameterName) =>
        throw new ArgumentNullException(parameterName);

    private static GenAiEtlAuditDimensions NormalizeDimensions(GenAiEtlAuditDimensions dimensions) =>
        new(
            CleanDimension(dimensions.ServiceName),
            NormalizeSemanticValue(dimensions.OperationName),
            NormalizeSemanticValue(dimensions.OutputType),
            CleanDimension(dimensions.ProviderName),
            CleanDimension(dimensions.ModelName));

    private static ClusterKey CreateClusterKey(GenAiEtlAuditDimensions dimensions) =>
        new(
            NormalizeKey(dimensions.ServiceName),
            NormalizeKey(dimensions.OperationName),
            NormalizeKey(dimensions.OutputType),
            NormalizeKey(dimensions.ProviderName),
            NormalizeKey(dimensions.ModelName));

    private static string? CleanDimension(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSemanticValue(string? value) =>
        CleanDimension(value)?.ToLowerInvariant();

    private static string NormalizeKey(string? value) =>
        CleanDimension(value)?.ToUpperInvariant() ?? string.Empty;

    private static bool IsEmbeddingOutput(string output) =>
        output is "embedding" or "embeddings" or "vector" or "float[]";

    private sealed record GateDefinition(
        GenAiEtlAuditPromotionGate Gate,
        IReadOnlyList<GenAiEtlAuditEvidence> RequiredEvidence);

    private sealed record ClusterKey(
        string ServiceName,
        string OperationName,
        string OutputType,
        string ProviderName,
        string ModelName);
}

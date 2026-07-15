using System.Text;
using CostContracts = Qyl.Api.Contracts.Cost;

namespace Qyl.Collector.Cost;

internal sealed record GenAiEtlAuditValidationFailure(
    string Field,
    string Message,
    string Code,
    string? RejectedValue = null);

internal sealed record GenAiEtlAuditEvaluationOutcome(
    CostContracts.GenAiEtlAuditEvaluationResponse? Response,
    GenAiEtlAuditValidationFailure? Failure);

internal sealed class GenAiEtlAuditService
{
    private static readonly CostContracts.GenAiEtlEvidenceSignal[] s_allEvidenceSignals =
        Enum.GetValues<CostContracts.GenAiEtlEvidenceSignal>();

    private readonly IQylStore _store;
    private readonly IReadOnlyList<IProviderCostSource> _sources;
    private readonly ProviderCostSyncOptions _options;
    private readonly GenAiEtlCatalogEstimator _catalogEstimator;
    private readonly ModelPricingCatalogStateService _catalogState;
    private readonly TimeProvider _timeProvider;

    public GenAiEtlAuditService(
        IQylStore store,
        IEnumerable<IProviderCostSource> sources,
        ProviderCostSyncOptions options,
        GenAiEtlCatalogEstimator catalogEstimator,
        ModelPricingCatalogStateService catalogState,
        TimeProvider timeProvider)
    {
        _store = store;
        _sources = [.. sources];
        _options = options;
        _catalogEstimator = catalogEstimator;
        _catalogState = catalogState;
        _timeProvider = timeProvider;
    }

    public async Task<CostContracts.GenAiEtlAuditReport> GetReportAsync(
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        int limit,
        CancellationToken ct = default)
    {
        var audit = await BuildAuditAsync(projectId, periodStart, periodEnd, ct).ConfigureAwait(false);
        var returned = audit.Clusters.Take(limit).Select(static cluster => cluster.Contract).ToArray();
        var returnedCatalogCost = returned
            .Select(static cluster => cluster.CatalogTokenEstimate)
            .OfType<CostContracts.GenAiEtlCatalogTokenCalculatedEstimate>()
            .Sum(static estimate => estimate.EstimatedCatalogTokenCostUsd);
        double? economicConcentration = audit.EstimatedCatalogTokenCostUsd is > 0
            ? returnedCatalogCost / audit.EstimatedCatalogTokenCostUsd.Value
            : null;

        return new CostContracts.GenAiEtlAuditReport
        {
            GeneratedAt = _timeProvider.GetUtcNow(),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Summary = new CostContracts.GenAiEtlAuditSummary
            {
                TotalCalls = audit.TotalCalls,
                TotalInputTokens = audit.TotalInputTokens,
                TotalOutputTokens = audit.TotalOutputTokens,
                EstimatedCatalogTokenCostUsd = audit.EstimatedCatalogTokenCostUsd,
                CatalogTokenPricedCallCoverage = audit.TotalCalls > 0
                    ? (double)audit.PricedCalls / audit.TotalCalls
                    : 0,
                EstimatedTokenEconomicConcentration = economicConcentration,
                MeasurableCoverage = null,
                SafeDeferralCoverage = null,
                CandidateEtlEstimatedTokenSpendShare = audit.CandidateEtlSpendShare
            },
            BillingSources = audit.BillingSources,
            CatalogSources = audit.CatalogSources,
            Clusters = returned
        };
    }

    public async Task<GenAiEtlAuditEvaluationOutcome> EvaluateAsync(
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CostContracts.GenAiEtlAuditEvaluationRequest request,
        CancellationToken ct = default)
    {
        if (request.Scenarios is not { Count: >= 1 and <= 100 } scenarios)
        {
            return Invalid(
                "scenarios",
                "Between 1 and 100 scenarios are required.",
                "scenarios.count_out_of_range",
                request.Scenarios?.Count.ToString(CultureInfo.InvariantCulture));
        }

        var duplicate = scenarios
            .Where(static scenario => !string.IsNullOrWhiteSpace(scenario.ClusterId))
            .GroupBy(static scenario => scenario.ClusterId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            return Invalid(
                "scenarios.clusterId",
                "Each cluster may appear only once in an evaluation batch.",
                "scenario.duplicate_cluster",
                duplicate.Key);
        }

        for (var index = 0; index < scenarios.Count; index++)
        {
            var scenario = scenarios[index];
            var prefix = $"scenarios[{index.ToString(CultureInfo.InvariantCulture)}]";
            if (string.IsNullOrWhiteSpace(scenario.ClusterId))
            {
                return Invalid(
                    $"{prefix}.clusterId",
                    "Cluster ID is required.",
                    "scenario.cluster_id_required");
            }

            if (!IsRatio(scenario.Coverage))
            {
                return Invalid(
                    $"{prefix}.coverage",
                    "Coverage must be finite and between zero and one.",
                    "scenario.coverage_out_of_range",
                    FormatNumber(scenario.Coverage));
            }

            if (scenario.FrontierCostPerCallUsd is { } frontier && !IsNonNegative(frontier))
            {
                return Invalid(
                    $"{prefix}.frontierCostPerCallUsd",
                    "Frontier cost must be finite and non-negative.",
                    "scenario.cost_out_of_range",
                    FormatNumber(frontier));
            }

            if (!IsNonNegative(scenario.AlternativeCostPerCallUsd) ||
                !IsNonNegative(scenario.PeriodMaintenanceCostUsd) ||
                !IsNonNegative(scenario.PeriodErrorCostUsd))
            {
                return Invalid(
                    prefix,
                    "Alternative, maintenance, and error costs must be finite and non-negative.",
                    "scenario.cost_out_of_range");
            }
        }

        var audit = await BuildAuditAsync(projectId, periodStart, periodEnd, ct).ConfigureAwait(false);
        var clusters = audit.Clusters.ToDictionary(static cluster => cluster.Contract.ClusterId, StringComparer.Ordinal);
        var results = new List<CostContracts.GenAiEtlClusterEvaluation>(scenarios.Count);

        foreach (var scenario in scenarios)
        {
            if (!clusters.TryGetValue(scenario.ClusterId, out var cluster))
            {
                return Invalid(
                    "scenarios.clusterId",
                    "The cluster does not exist in the requested audit period.",
                    "scenario.cluster_not_found",
                    scenario.ClusterId);
            }

            var explicitFrontier = scenario.FrontierCostPerCallUsd;
            var catalogEstimate = cluster.Contract.CatalogTokenEstimate as
                CostContracts.GenAiEtlCatalogTokenCalculatedEstimate;
            var frontier = explicitFrontier ?? catalogEstimate?.EstimatedCatalogTokenCostPerCallUsd;
            var value = GenAiEtlAuditAnalyzer.Evaluate(new GenAiEtlAuditValueInput(
                cluster.Contract.CallCount,
                scenario.Coverage,
                frontier,
                scenario.AlternativeCostPerCallUsd,
                scenario.PeriodMaintenanceCostUsd,
                scenario.PeriodErrorCostUsd));

            if (explicitFrontier.HasValue)
            {
                results.Add(new CostContracts.GenAiEtlScenarioClusterEvaluation
                {
                    ClusterId = scenario.ClusterId,
                    Status = "calculated",
                    CallCount = cluster.Contract.CallCount,
                    Coverage = scenario.Coverage,
                    ServedCallCount = cluster.Contract.CallCount * scenario.Coverage,
                    ResidualCallCount = cluster.Contract.CallCount * (1 - scenario.Coverage),
                    FrontierCostPerCallUsd = frontier!.Value,
                    AlternativeCostPerCallUsd = scenario.AlternativeCostPerCallUsd,
                    CurrentPeriodCostUsd = value.FrontierSpend!.Value,
                    GrossReplaceableValueUsd = value.GrossReplaceableValue!.Value,
                    PeriodMaintenanceCostUsd = scenario.PeriodMaintenanceCostUsd,
                    PeriodErrorCostUsd = scenario.PeriodErrorCostUsd,
                    NetReplaceableValueUsd = value.NetReplaceableValue!.Value
                });
            }
            else if (catalogEstimate is not null)
            {
                results.Add(new CostContracts.GenAiEtlCatalogTokenClusterEvaluation
                {
                    ClusterId = scenario.ClusterId,
                    Status = "calculated",
                    CallCount = cluster.Contract.CallCount,
                    Coverage = scenario.Coverage,
                    ServedCallCount = cluster.Contract.CallCount * scenario.Coverage,
                    ResidualCallCount = cluster.Contract.CallCount * (1 - scenario.Coverage),
                    FrontierCostPerCallUsd = catalogEstimate.EstimatedCatalogTokenCostPerCallUsd,
                    CatalogProvenance = catalogEstimate.Provenance,
                    AlternativeCostPerCallUsd = scenario.AlternativeCostPerCallUsd,
                    CurrentPeriodCostUsd = value.FrontierSpend!.Value,
                    GrossReplaceableValueUsd = value.GrossReplaceableValue!.Value,
                    PeriodMaintenanceCostUsd = scenario.PeriodMaintenanceCostUsd,
                    PeriodErrorCostUsd = scenario.PeriodErrorCostUsd,
                    NetReplaceableValueUsd = value.NetReplaceableValue!.Value
                });
            }
            else
            {
                results.Add(new CostContracts.GenAiEtlUnavailableClusterEvaluation
                {
                    ClusterId = scenario.ClusterId,
                    Status = "missing_frontier_cost",
                    CallCount = cluster.Contract.CallCount,
                    Coverage = scenario.Coverage,
                    ServedCallCount = cluster.Contract.CallCount * scenario.Coverage,
                    ResidualCallCount = cluster.Contract.CallCount * (1 - scenario.Coverage),
                    AlternativeCostPerCallUsd = scenario.AlternativeCostPerCallUsd,
                    PeriodMaintenanceCostUsd = scenario.PeriodMaintenanceCostUsd,
                    PeriodErrorCostUsd = scenario.PeriodErrorCostUsd
                });
            }
        }

        return new GenAiEtlAuditEvaluationOutcome(
            new CostContracts.GenAiEtlAuditEvaluationResponse
            {
                GeneratedAt = _timeProvider.GetUtcNow(),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Results = results
            },
            null);
    }

    private async Task<AuditData> BuildAuditAsync(
        string projectId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct)
    {
        var snapshot = await _store.GetGenAiEtlAuditSnapshotAsync(
                projectId,
                periodStart,
                periodEnd,
                ct)
            .ConfigureAwait(false);
        var activeScopeKeys = _sources.ToDictionary(
            static source => NormalizeKey(source.Provider),
            source => _options.ScopeFor(source.Provider).CreateStableKey(source.Provider),
            StringComparer.Ordinal);
        var rows = snapshot.AuditRows;
        var buckets = snapshot.CostBuckets
            .Where(bucket => activeScopeKeys.TryGetValue(NormalizeKey(bucket.Provider), out var scopeKey) &&
                             string.Equals(bucket.ProviderScopeKey, scopeKey, StringComparison.Ordinal))
            .ToArray();
        var syncRows = snapshot.CostSyncRows
            .Where(sync => activeScopeKeys.TryGetValue(NormalizeKey(sync.Provider), out var scopeKey) &&
                           string.Equals(sync.ProviderScopeKey, scopeKey, StringComparison.Ordinal))
            .ToArray();
        var syncByProvider = syncRows.ToDictionary(
            static sync => NormalizeKey(sync.Provider),
            StringComparer.Ordinal);
        var costGroups = BuildCostGroups(buckets, periodStart, periodEnd);
        var seeds = rows.Select(CreateSeed).ToArray();
        var estimateTask = _catalogEstimator.EstimateAsync(rows, snapshot.UsageBuckets, ct);
        var catalogSourceTask = _catalogState.GetAsync(ct);
        await Task.WhenAll(estimateTask, catalogSourceTask).ConfigureAwait(false);
        var estimates = await estimateTask.ConfigureAwait(false);

        var contractClusters = new List<AuditCluster>(seeds.Length);
        for (var index = 0; index < seeds.Length; index++)
        {
            var seed = seeds[index];
            var estimate = estimates[index];
            var analyzed = GenAiEtlAuditAnalyzer.Analyze(
            [
                new GenAiEtlAuditObservation(
                    new GenAiEtlAuditDimensions(
                        seed.Row.ServiceName,
                        seed.Row.OperationName,
                        seed.Row.OutputType,
                        seed.Row.ProviderName,
                        seed.Row.ModelName),
                    seed.Row.CallCount,
                    FrontierCostPerCall: null)
            ]).Clusters[0];
            var candidatePath = MapCandidatePath(analyzed.Candidate.Path);
            var evidence = BuildEvidence(
                seed.Row,
                analyzed,
                estimate.Status is ModelPricingEstimateStatus.Calculated);
            var contract = new CostContracts.GenAiEtlAuditCluster
            {
                ClusterId = seed.ClusterId,
                WorkflowKey = seed.WorkflowKey,
                ServiceName = seed.Row.ServiceName,
                OperationName = seed.Row.OperationName,
                Provider = seed.Row.ProviderName,
                ModelName = seed.Row.ModelName,
                OutputContract = MapOutputContract(analyzed.Inference.OutputContract),
                TaskFamily = MapTaskFamily(analyzed.Inference.TaskFamily),
                CallCount = seed.Row.CallCount,
                InputTokens = seed.Row.InputTokens,
                OutputTokens = seed.Row.OutputTokens,
                CacheReadInputTokens = seed.Row.CacheReadInputTokens,
                CacheCreationInputTokens = seed.Row.CacheCreationInputTokens,
                ReasoningOutputTokens = seed.Row.ReasoningOutputTokens,
                ErrorCount = seed.Row.ErrorCount,
                ErrorRate = seed.Row.CallCount > 0 ? (double)seed.Row.ErrorCount / seed.Row.CallCount : 0,
                AverageLatencyMs = seed.Row.AverageLatencyMs,
                P95LatencyMs = seed.Row.P95LatencyMs,
                CatalogTokenEstimate = MapCatalogEstimate(estimate),
                MeasurableCoverage = null,
                SafeDeferralCoverage = null,
                CandidateStatus = MapCandidateStatus(analyzed.Candidate.Status),
                CandidatePath = candidatePath,
                ValidationMetrics = MapValidationMetrics(analyzed.Candidate.Metric),
                ResidualPath = MapResidualPath(analyzed.Candidate.ResidualPath),
                EvidenceSignals = evidence,
                MissingEvidence = s_allEvidenceSignals.Except(evidence).ToArray(),
                PromotionGates = MapPromotionGates(analyzed.PromotionGates)
            };
            contractClusters.Add(new AuditCluster(
                contract,
                estimate.EstimatedTokenCostUsd is { } cost ? (double)cost : null,
                IsEtlCandidate(candidatePath)));
        }

        var clustersWithShares = contractClusters
            .OrderByDescending(static cluster => cluster.EstimatedCatalogTokenCostUsd.HasValue)
            .ThenByDescending(static cluster => cluster.EstimatedCatalogTokenCostUsd)
            .ThenByDescending(static cluster => cluster.Contract.CallCount)
            .ThenBy(static cluster => cluster.Contract.WorkflowKey, StringComparer.Ordinal)
            .ToArray();

        var costSources = BuildCostSources(
            costGroups,
            syncByProvider);
        var totalCalls = SumChecked(rows.Select(static row => row.CallCount));
        var calculated = contractClusters
            .Where(static cluster => cluster.EstimatedCatalogTokenCostUsd.HasValue)
            .ToArray();
        var estimatedCatalogTokenCostUsd = calculated.Length > 0
            ? calculated.Sum(static cluster => cluster.EstimatedCatalogTokenCostUsd!.Value)
            : (double?)null;
        var candidateCost = calculated
            .Where(static cluster => cluster.IsEtlCandidate)
            .Sum(static cluster => cluster.EstimatedCatalogTokenCostUsd!.Value);
        var candidateSpendShare = estimatedCatalogTokenCostUsd is > 0
            ? candidateCost / estimatedCatalogTokenCostUsd.Value
            : (double?)null;

        return new AuditData(
            totalCalls,
            SumChecked(rows.Select(static row => row.InputTokens)),
            SumChecked(rows.Select(static row => row.OutputTokens)),
            SumChecked(estimates.Select(static estimate => estimate.PricedCallCount)),
            estimatedCatalogTokenCostUsd,
            candidateSpendShare,
            costSources,
            MapCatalogSource(await catalogSourceTask.ConfigureAwait(false)),
            clustersWithShares);
    }

    private IReadOnlyList<CostContracts.ProviderBillingSource> BuildCostSources(
        IReadOnlyList<CostGroup> groups,
        IReadOnlyDictionary<string, ProviderCostSyncRow> syncByProvider)
    {
        var result = new List<CostContracts.ProviderBillingSource>();
        foreach (var source in _sources.OrderBy(static source => source.Provider, StringComparer.Ordinal))
        {
            var provider = NormalizeKey(source.Provider);
            syncByProvider.TryGetValue(provider, out var sync);
            var providerGroups = groups.Where(group => group.Provider == provider).ToArray();
            if (providerGroups.Length == 0)
            {
                result.Add(new CostContracts.ProviderBillingSource
                {
                    Provider = provider,
                    Status = ResolveSourceStatus(provider, sync),
                    SourceEndpoint = sync?.SourceEndpoint ?? source.SourceEndpoint.AbsoluteUri,
                    Attribution = MapAttribution(sync?.Attribution),
                    LastAttemptAt = sync?.LastAttemptAt,
                    LastSuccessAt = sync?.LastSuccessAt,
                    PeriodStart = sync?.PeriodStart,
                    PeriodEnd = sync?.PeriodEnd,
                    FailureCategory = sync?.FailureCategory
                });
                continue;
            }

            foreach (var group in providerGroups)
            {
                var isUsd = string.Equals(group.CurrencyCode, "USD", StringComparison.Ordinal);
                var isNonNegative = group.Amount >= 0;
                result.Add(new CostContracts.ProviderBillingSource
                {
                    Provider = provider,
                    Status = ResolveSourceStatus(provider, sync),
                    SourceEndpoint = group.SourceEndpoint,
                    Attribution = MapAttribution(group.Attribution),
                    LastAttemptAt = sync?.LastAttemptAt,
                    LastSuccessAt = sync?.LastSuccessAt,
                    PeriodStart = group.PeriodStart,
                    PeriodEnd = group.PeriodEnd,
                    CurrencyCode = group.CurrencyCode,
                    ReportedCostUsd = isUsd && isNonNegative ? (double)group.Amount : null,
                    ModelName = group.ModelName,
                    FailureCategory = sync?.FailureCategory
                });
            }
        }

        return result;
    }

    private CostContracts.ProviderBillingSourceStatus ResolveSourceStatus(
        string provider,
        ProviderCostSyncRow? sync)
    {
        if (sync is null)
        {
            return IsCredentialConfigured(provider)
                ? CostContracts.ProviderBillingSourceStatus.Pending
                : CostContracts.ProviderBillingSourceStatus.Unconfigured;
        }

        var status = sync.Status switch
        {
            "unconfigured" => CostContracts.ProviderBillingSourceStatus.Unconfigured,
            "sync_failed" => CostContracts.ProviderBillingSourceStatus.SyncFailed,
            "current" => CostContracts.ProviderBillingSourceStatus.Current,
            _ => CostContracts.ProviderBillingSourceStatus.SyncFailed
        };
        if (status is CostContracts.ProviderBillingSourceStatus.Current)
        {
            if (sync.LastSuccessAt is not { } lastSuccess)
            {
                status = CostContracts.ProviderBillingSourceStatus.Stale;
            }
            else
            {
                var freshness = TimeSpan.FromTicks(Math.Max(
                    _options.SyncInterval.Ticks * 2,
                    TimeSpan.FromMinutes(30).Ticks));
                if (_timeProvider.GetUtcNow() - lastSuccess > freshness)
                    status = CostContracts.ProviderBillingSourceStatus.Stale;
            }
        }

        return status;
    }

    private bool IsCredentialConfigured(string provider) => provider switch
    {
        "openai" => ProviderCostFailureMapper.IsCredentialUsable(_options.OpenAiAdminKey),
        "anthropic" => ProviderCostFailureMapper.IsCredentialUsable(_options.AnthropicAdminKey),
        _ => false
    };

    private static IReadOnlyList<CostGroup> BuildCostGroups(
        IReadOnlyList<ProviderCostBucketRow> buckets,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd) =>
        buckets
            .Where(bucket => bucket.PeriodStart >= periodStart && bucket.PeriodEnd <= periodEnd)
            .GroupBy(static bucket => new
            {
                Provider = NormalizeKey(bucket.Provider),
                Model = bucket.ModelKey == "*" ? null : NormalizeKey(bucket.ModelKey),
                bucket.SourceEndpoint,
                bucket.SourceKind,
                bucket.Attribution,
                Currency = bucket.CurrencyCode.ToUpperInvariant()
            })
            .Select(group => new CostGroup(
                group.Key.Provider,
                group.Key.Model,
                group.First().ModelKey == "*" ? null : group.First().ModelKey,
                group.Key.SourceEndpoint,
                group.Key.SourceKind,
                group.Key.Attribution,
                group.Key.Currency,
                group.Min(static bucket => bucket.PeriodStart),
                group.Max(static bucket => bucket.PeriodEnd),
                group.Sum(static bucket => bucket.Amount)))
            .OrderBy(static group => group.Provider, StringComparer.Ordinal)
            .ThenBy(static group => group.Model, StringComparer.Ordinal)
            .ThenBy(static group => group.CurrencyCode, StringComparer.Ordinal)
            .ToArray();

    private static ClusterSeed CreateSeed(GenAiEtlAuditStorageRow row)
    {
        var dimensions = new[]
        {
            NormalizeKey(row.ServiceName),
            NormalizeNullableKey(row.OperationName),
            NormalizeNullableKey(row.OutputType),
            NormalizeNullableKey(row.ProviderName),
            NormalizeNullableIdentity(row.ModelName),
            NormalizeNullableKey(row.ModelIdentityBasis)
        };
        var clusterId = CreateStableId("cluster", dimensions);
        var workflowKey = string.Join('/', new[]
        {
            dimensions[0],
            dimensions[1] ?? "operation:unknown",
            dimensions[2] ?? "output:unknown",
            dimensions[3] ?? "provider:unknown",
            dimensions[4] ?? "model:unknown",
            dimensions[5] ?? "model_identity:unknown"
        });
        return new ClusterSeed(
            clusterId,
            workflowKey,
            dimensions[3],
            dimensions[4],
            row);
    }

    private static IReadOnlyList<CostContracts.GenAiEtlEvidenceSignal> BuildEvidence(
        GenAiEtlAuditStorageRow row,
        GenAiEtlAuditAnalysisCluster analyzed,
        bool hasCatalogTokenEstimate)
    {
        var evidence = new HashSet<CostContracts.GenAiEtlEvidenceSignal>();
        if (analyzed.Inference.Status is GenAiEtlAuditInferenceStatus.InferredFromExplicitTelemetry &&
            analyzed.Inference.OutputContract is not GenAiEtlAuditOutputShape.Unknown)
            evidence.Add(CostContracts.GenAiEtlEvidenceSignal.OutputContract);
        if (row.ProviderName is not null && row.ModelName is not null)
            evidence.Add(CostContracts.GenAiEtlEvidenceSignal.ProviderModel);
        if (row.CallCount > 0 && row.TokenUsageCallCount == row.CallCount)
            evidence.Add(CostContracts.GenAiEtlEvidenceSignal.TokenUsage);
        if (hasCatalogTokenEstimate)
            evidence.Add(CostContracts.GenAiEtlEvidenceSignal.CatalogTokenEstimate);
        return evidence.Order().ToArray();
    }

    private static IReadOnlyList<CostContracts.GenAiEtlPromotionGate> MapPromotionGates(
        IReadOnlyList<GenAiEtlAuditGateAssessment> gates) =>
        [.. gates.Select(static gate => new CostContracts.GenAiEtlPromotionGate
        {
            Gate = gate.Gate switch
            {
                GenAiEtlAuditPromotionGate.ContractStability => CostContracts.GenAiEtlPromotionGateKind.ContractStability,
                GenAiEtlAuditPromotionGate.OfflineReplay => CostContracts.GenAiEtlPromotionGateKind.OfflineReplay,
                GenAiEtlAuditPromotionGate.CalibratedConfidence => CostContracts.GenAiEtlPromotionGateKind.CalibratedConfidence,
                GenAiEtlAuditPromotionGate.ShadowTraffic => CostContracts.GenAiEtlPromotionGateKind.ShadowTraffic,
                GenAiEtlAuditPromotionGate.LimitedServing => CostContracts.GenAiEtlPromotionGateKind.LimitedServing,
                _ => CostContracts.GenAiEtlPromotionGateKind.RollbackResidualPolicy
            },
            State = gate.Status is GenAiEtlAuditGateEvidenceStatus.EvidenceAvailable
                ? CostContracts.GenAiEtlPromotionGateState.NotEvaluated
                : CostContracts.GenAiEtlPromotionGateState.BlockedMissingEvidence,
            Reason = gate.MissingEvidence.Count == 0
                ? "evidence_available_not_evaluated"
                : $"missing_{string.Join('_', gate.MissingEvidence.Select(static evidence => ToSnakeCase(evidence.ToString())))}"
        })];

    private static CostContracts.GenAiEtlOutputContract MapOutputContract(GenAiEtlAuditOutputShape value) =>
        value switch
        {
            GenAiEtlAuditOutputShape.Prose => CostContracts.GenAiEtlOutputContract.Prose,
            GenAiEtlAuditOutputShape.Label => CostContracts.GenAiEtlOutputContract.Label,
            GenAiEtlAuditOutputShape.SpanList => CostContracts.GenAiEtlOutputContract.SpanList,
            GenAiEtlAuditOutputShape.Record => CostContracts.GenAiEtlOutputContract.Record,
            GenAiEtlAuditOutputShape.CatalogReference => CostContracts.GenAiEtlOutputContract.CatalogReference,
            GenAiEtlAuditOutputShape.Number => CostContracts.GenAiEtlOutputContract.Number,
            GenAiEtlAuditOutputShape.Boolean => CostContracts.GenAiEtlOutputContract.Boolean,
            GenAiEtlAuditOutputShape.Embedding => CostContracts.GenAiEtlOutputContract.Vector,
            _ => CostContracts.GenAiEtlOutputContract.Unknown
        };

    private static CostContracts.GenAiEtlTaskFamily MapTaskFamily(GenAiEtlAuditTaskFamily value) => value switch
    {
        GenAiEtlAuditTaskFamily.OpenEndedReasoningAndGeneration => CostContracts.GenAiEtlTaskFamily.OpenReasoningGeneration,
        GenAiEtlAuditTaskFamily.Classification => CostContracts.GenAiEtlTaskFamily.Classification,
        GenAiEtlAuditTaskFamily.SequenceLabeling => CostContracts.GenAiEtlTaskFamily.SequenceLabeling,
        GenAiEtlAuditTaskFamily.StructuredExtraction => CostContracts.GenAiEtlTaskFamily.StructuredExtraction,
        GenAiEtlAuditTaskFamily.RetrievalAndEntityResolution => CostContracts.GenAiEtlTaskFamily.RetrievalEntityResolution,
        GenAiEtlAuditTaskFamily.SimilarityAndClustering => CostContracts.GenAiEtlTaskFamily.SimilarityClustering,
        GenAiEtlAuditTaskFamily.NormalizationAndTransformation => CostContracts.GenAiEtlTaskFamily.NormalizationTransformation,
        GenAiEtlAuditTaskFamily.NumericAndAnalyticalComputation => CostContracts.GenAiEtlTaskFamily.NumericAnalytical,
        _ => CostContracts.GenAiEtlTaskFamily.Unknown
    };

    private static CostContracts.GenAiEtlCandidatePath MapCandidatePath(GenAiEtlAuditCandidatePath value) => value switch
    {
        GenAiEtlAuditCandidatePath.KeepFrontierModel => CostContracts.GenAiEtlCandidatePath.FrontierModel,
        GenAiEtlAuditCandidatePath.DeterministicCode => CostContracts.GenAiEtlCandidatePath.DeterministicCode,
        GenAiEtlAuditCandidatePath.BoundedRetrievalOrSimilarityIndex => CostContracts.GenAiEtlCandidatePath.BoundedRetrieval,
        GenAiEtlAuditCandidatePath.ParserValidatorOrSmallExtractor or
            GenAiEtlAuditCandidatePath.SmallClassifierOrExtractor => CostContracts.GenAiEtlCandidatePath.SmallClassifierExtractor,
        _ => CostContracts.GenAiEtlCandidatePath.InsufficientEvidence
    };

    private static CostContracts.GenAiEtlCandidateStatus MapCandidateStatus(GenAiEtlAuditCandidateStatus value) =>
        value is GenAiEtlAuditCandidateStatus.HypothesisOnly
            ? CostContracts.GenAiEtlCandidateStatus.HypothesisOnly
            : CostContracts.GenAiEtlCandidateStatus.InsufficientEvidence;

    private static bool IsEtlCandidate(CostContracts.GenAiEtlCandidatePath path) => path is
        CostContracts.GenAiEtlCandidatePath.ExactCache or
        CostContracts.GenAiEtlCandidatePath.DeterministicCode or
        CostContracts.GenAiEtlCandidatePath.BoundedRetrieval or
        CostContracts.GenAiEtlCandidatePath.SmallClassifierExtractor or
        CostContracts.GenAiEtlCandidatePath.SmallerGenerativeModel;

    private static CostContracts.GenAiEtlCatalogTokenCostEstimate MapCatalogEstimate(
        GenAiEtlCatalogEstimateResult estimate)
    {
        var exclusions = estimate.Exclusions.Count > 0
            ? estimate.Exclusions.Select(MapCatalogExclusion).ToArray()
            : null;
        return estimate.Status switch
        {
            ModelPricingEstimateStatus.Calculated => new CostContracts.GenAiEtlCatalogTokenCalculatedEstimate
            {
                EstimatedCatalogTokenCostUsd = (double)estimate.EstimatedTokenCostUsd!.Value,
                EstimatedCatalogTokenCostPerCallUsd = (double)estimate.EstimatedTokenCostPerCallUsd!.Value,
                Provenance = MapCatalogProvenance(estimate.Provenance!),
                Components = [.. estimate.Components.Select(MapCatalogComponent)],
                Exclusions = exclusions
            },
            ModelPricingEstimateStatus.StaleSource =>
                new CostContracts.GenAiEtlCatalogTokenStaleSourceEstimate { Exclusions = exclusions },
            ModelPricingEstimateStatus.MissingModelIdentity =>
                new CostContracts.GenAiEtlCatalogTokenMissingModelIdentityEstimate { Exclusions = exclusions },
            ModelPricingEstimateStatus.ModelNotFound =>
                new CostContracts.GenAiEtlCatalogTokenModelNotFoundEstimate { Exclusions = exclusions },
            ModelPricingEstimateStatus.AmbiguousModel =>
                new CostContracts.GenAiEtlCatalogTokenAmbiguousModelEstimate { Exclusions = exclusions },
            ModelPricingEstimateStatus.IncompleteUsage =>
                new CostContracts.GenAiEtlCatalogTokenIncompleteUsageEstimate { Exclusions = exclusions },
            ModelPricingEstimateStatus.ConditionalPricingUnresolvable =>
                new CostContracts.GenAiEtlCatalogTokenConditionalPricingUnresolvableEstimate { Exclusions = exclusions },
            ModelPricingEstimateStatus.UnsupportedPricing =>
                new CostContracts.GenAiEtlCatalogTokenUnsupportedPricingEstimate { Exclusions = exclusions },
            _ => new CostContracts.GenAiEtlCatalogTokenSourceUnavailableEstimate { Exclusions = exclusions }
        };
    }

    private static CostContracts.ModelCatalogPriceProvenance MapCatalogProvenance(
        GenAiEtlCatalogPriceProvenance provenance) => new()
    {
        SourceId = provenance.SourceId,
        SourceEndpoint = provenance.SourceEndpoint.AbsoluteUri,
        SnapshotId = provenance.SnapshotId,
        PriceModelId = provenance.PriceModelId,
        ObservedModelId = provenance.ObservedModelId,
        ObservedModelIdentityBasis = provenance.ObservedModelIdentityBasis is GenAiEtlObservedModelIdentityBasis.ResponseModel
            ? CostContracts.ModelCatalogObservedIdentityBasis.ResponseModel
            : CostContracts.ModelCatalogObservedIdentityBasis.RequestModelFallback,
        ModelMatchKind = provenance.ModelMatchKind is ModelPricingMatchKind.ExactModelId
            ? CostContracts.ModelCatalogMatchKind.ExactModelId
            : CostContracts.ModelCatalogMatchKind.ExactCanonicalSlug,
        RetrievedAt = provenance.RetrievedAt,
        PriceSemantics = CostContracts.ModelCatalogPriceSemantics.MinimumAvailableRate
    };

    private static CostContracts.ModelCatalogTokenEstimateComponent MapCatalogComponent(
        ModelPricingEstimateComponent component) => component.RateRelation switch
    {
        ModelPricingRateRelation.BaseRate => new CostContracts.ModelCatalogTokenBaseRateComponent
        {
            Component = component.SourceMeter,
            UsageDimension = component.UsageDimension,
            Unit = component.Unit,
            SourceBillingMode = component.SourceBillingMode,
            BillingMode = MapBillingMode(component.UsageDimension),
            Quantity = (double)component.Quantity,
            UnitPriceUsd = (double)component.UsdPerUnit,
            EstimatedCostUsd = (double)component.AmountUsd
        },
        ModelPricingRateRelation.AdditiveSurcharge => new CostContracts.ModelCatalogTokenAdditiveSurchargeComponent
        {
            Component = component.SourceMeter,
            UsageDimension = component.UsageDimension,
            Unit = component.Unit,
            SourceBillingMode = component.SourceBillingMode,
            BillingMode = MapBillingMode(component.UsageDimension),
            Quantity = (double)component.Quantity,
            UnitPriceUsd = (double)component.UsdPerUnit,
            EstimatedCostUsd = (double)component.AmountUsd
        },
        ModelPricingRateRelation.ReplacesInclusiveBaseRate =>
            new CostContracts.ModelCatalogTokenInclusiveReplacementRateComponent
            {
                Component = component.SourceMeter,
                UsageDimension = component.UsageDimension,
                Unit = component.Unit,
                SourceBillingMode = component.SourceBillingMode,
                BillingMode = MapBillingMode(component.UsageDimension),
                Quantity = (double)component.Quantity,
                UnitPriceUsd = (double)component.UsdPerUnit,
                EstimatedCostUsd = (double)component.AmountUsd,
                ReplacesUsageDimension = component.ReplacesUsageDimension!,
                ConditionalEvidence = component.OverrideEvidence is { } evidence
                    ? MapConditionalEvidence(evidence)
                    : null
            },
        _ => new CostContracts.ModelCatalogTokenConditionalOverrideRateComponent
        {
            Component = component.SourceMeter,
            UsageDimension = component.UsageDimension,
            Unit = component.Unit,
            SourceBillingMode = component.SourceBillingMode,
            BillingMode = MapBillingMode(component.UsageDimension),
            Quantity = (double)component.Quantity,
            UnitPriceUsd = (double)component.UsdPerUnit,
            EstimatedCostUsd = (double)component.AmountUsd,
            ReplacesUsageDimension = component.ReplacesUsageDimension!,
            ConditionalEvidence = MapConditionalEvidence(component.OverrideEvidence!)
        }
    };

    private static CostContracts.ModelCatalogTokenEstimateExclusion MapCatalogExclusion(
        ModelPricingEstimateExclusion exclusion) => exclusion.Reason switch
    {
        "usage_not_observed" => new CostContracts.ModelCatalogUsageNotObservedExclusion
        {
            Component = exclusion.SourceMeter,
            UsageDimension = exclusion.UsageDimension,
            Unit = exclusion.Unit,
            SourceBillingMode = exclusion.SourceBillingMode,
            BillingMode = MapBillingMode(exclusion.UsageDimension),
            RateEvidence = MapExclusionRateEvidence(exclusion),
            UnitPriceUsd = (double)exclusion.UsdPerUnit
        },
        "conditional_adjustment_not_applied" => new CostContracts.ModelCatalogConditionalAdjustmentNotAppliedExclusion
        {
            Component = exclusion.SourceMeter,
            UsageDimension = exclusion.UsageDimension,
            Unit = exclusion.Unit,
            SourceBillingMode = exclusion.SourceBillingMode,
            BillingMode = exclusion.BillingMode is null ? null : MapBillingMode(exclusion.UsageDimension),
            RateEvidence = MapPublishedReplacementEvidence(exclusion),
            UnitPriceUsd = (double)exclusion.UsdPerUnit
        },
        "superseded_by_later_override" => new CostContracts.ModelCatalogSupersededOverrideExclusion
        {
            Component = exclusion.SourceMeter,
            UsageDimension = exclusion.UsageDimension,
            Unit = exclusion.Unit,
            SourceBillingMode = exclusion.SourceBillingMode,
            BillingMode = exclusion.BillingMode is null ? null : MapBillingMode(exclusion.UsageDimension),
            RateEvidence = MapPublishedReplacementEvidence(exclusion),
            UnitPriceUsd = (double)exclusion.UsdPerUnit
        },
        "unsupported_usage_dimension" => new CostContracts.ModelCatalogUnsupportedUsageExclusion
        {
            Component = exclusion.SourceMeter,
            UsageDimension = exclusion.UsageDimension,
            Unit = exclusion.Unit,
            SourceBillingMode = exclusion.SourceBillingMode,
            BillingMode = exclusion.BillingMode is null ? null : MapBillingMode(exclusion.UsageDimension),
            RateEvidence = MapExclusionRateEvidence(exclusion),
            UnitPriceUsd = (double)exclusion.UsdPerUnit
        },
        "unsupported_billing_mode" => new CostContracts.ModelCatalogUnsupportedBillingExclusion
        {
            Component = exclusion.SourceMeter,
            UsageDimension = exclusion.UsageDimension,
            Unit = exclusion.Unit,
            SourceBillingMode = exclusion.SourceBillingMode,
            BillingMode = exclusion.BillingMode is null ? null : MapBillingMode(exclusion.UsageDimension),
            RateEvidence = MapExclusionRateEvidence(exclusion),
            UnitPriceUsd = (double)exclusion.UsdPerUnit
        },
        _ => new CostContracts.ModelCatalogOutsideTokenScopeExclusion
        {
            Component = exclusion.SourceMeter,
            UsageDimension = exclusion.UsageDimension,
            Unit = exclusion.Unit,
            SourceBillingMode = exclusion.SourceBillingMode,
            BillingMode = exclusion.BillingMode is null ? null : MapBillingMode(exclusion.UsageDimension),
            RateEvidence = MapExclusionRateEvidence(exclusion),
            UnitPriceUsd = (double)exclusion.UsdPerUnit
        }
    };

    private static CostContracts.ModelCatalogExclusionRateEvidence MapExclusionRateEvidence(
        ModelPricingEstimateExclusion exclusion) => exclusion.RateRelation switch
    {
        ModelPricingRateRelation.BaseRate => new CostContracts.ModelCatalogBaseRateEvidence(),
        ModelPricingRateRelation.AdditiveSurcharge => new CostContracts.ModelCatalogAdditiveSurchargeEvidence(),
        ModelPricingRateRelation.ReplacesInclusiveBaseRate => new CostContracts.ModelCatalogInclusiveReplacementEvidence
        {
            ReplacesUsageDimension = exclusion.ReplacesUsageDimension!,
            ConditionalEvidence = exclusion.OverrideEvidence is { } evidence
                ? MapConditionalEvidence(evidence)
                : null
        },
        _ => MapPublishedReplacementEvidence(exclusion)
    };

    private static CostContracts.ModelCatalogPublishedReplacementEvidence MapPublishedReplacementEvidence(
        ModelPricingEstimateExclusion exclusion) => new()
    {
        ReplacesUsageDimension = exclusion.ReplacesUsageDimension!,
        ConditionalEvidence = MapConditionalEvidence(exclusion.OverrideEvidence!)
    };

    private static CostContracts.ModelCatalogConditionalRateEvidence MapConditionalEvidence(
        ModelPricingOverrideEvidence evidence) => new()
    {
        SourceOrder = evidence.SourceOrder,
        ConditionUsageDimension = evidence.ConditionUsageDimension,
        ExclusiveMinimumQuantity = (double)evidence.ExclusiveMinimumQuantity,
        ObservedPerCallQuantity = (double)evidence.ObservedQuantity
    };

    // The estimator prices token-shaped meters per measured unit; only the flat "requests"
    // dimension is charged once per observed call.
    private static CostContracts.ModelCatalogBillingMode MapBillingMode(string usageDimension) =>
        usageDimension is "requests"
            ? CostContracts.ModelCatalogBillingMode.PerRequest
            : CostContracts.ModelCatalogBillingMode.PerUnit;

    private static IReadOnlyList<CostContracts.ModelCatalogSource> MapCatalogSource(
        ModelPricingCatalogSourceStatus source) =>
        [
            new CostContracts.ModelCatalogSource
            {
                SourceId = source.SourceId,
                // Required by the released contract; there is only one configured catalog.
                Priority = OpenRouterModelPricingCatalogSource.ContractPriority,
                Status = source.Status switch
                {
                    "pending" => CostContracts.ModelCatalogSourceStatus.Pending,
                    "current" => CostContracts.ModelCatalogSourceStatus.Current,
                    "stale" => CostContracts.ModelCatalogSourceStatus.Stale,
                    _ => CostContracts.ModelCatalogSourceStatus.SyncFailed
                },
                PriceSemantics = source.PriceSemantics is "minimum_available_rate"
                    ? CostContracts.ModelCatalogPriceSemantics.MinimumAvailableRate
                    : null,
                SourceEndpoint = source.Endpoint.AbsoluteUri,
                LastAttemptAt = source.LastAttemptAt,
                LastVerifiedAt = source.LastVerifiedAt,
                RetrievedAt = source.RetrievedAt,
                ActiveSnapshotId = source.SnapshotId,
                ModelCount = source.ModelCount,
                FailureCategory = source.FailureCategory
            }
        ];

    private static IReadOnlyList<CostContracts.GenAiEtlValidationMetric> MapValidationMetrics(
        GenAiEtlAuditMetric value) => value switch
    {
        GenAiEtlAuditMetric.HumanRubricOrTaskSuccess =>
            [CostContracts.GenAiEtlValidationMetric.HumanPreference, CostContracts.GenAiEtlValidationMetric.TaskSuccess],
        GenAiEtlAuditMetric.MacroF1AndCalibration =>
            [CostContracts.GenAiEtlValidationMetric.Accuracy, CostContracts.GenAiEtlValidationMetric.MacroF1,
                CostContracts.GenAiEtlValidationMetric.CalibrationError],
        GenAiEtlAuditMetric.SpanPrecisionRecallF1 =>
            [CostContracts.GenAiEtlValidationMetric.SpanPrecision, CostContracts.GenAiEtlValidationMetric.SpanRecall,
                CostContracts.GenAiEtlValidationMetric.SpanF1],
        GenAiEtlAuditMetric.FieldExactMatchAndSchemaValidity =>
            [CostContracts.GenAiEtlValidationMetric.FieldExactMatch, CostContracts.GenAiEtlValidationMetric.SchemaValidity],
        GenAiEtlAuditMetric.MatchAccuracyRecallAtKOrMrr =>
            [CostContracts.GenAiEtlValidationMetric.MatchAccuracy, CostContracts.GenAiEtlValidationMetric.RecallAtK,
                CostContracts.GenAiEtlValidationMetric.MeanReciprocalRank],
        GenAiEtlAuditMetric.ExactMatch => [CostContracts.GenAiEtlValidationMetric.ExactMatch],
        GenAiEtlAuditMetric.ExactOrToleranceNumericMatch =>
            [CostContracts.GenAiEtlValidationMetric.ExactMatch, CostContracts.GenAiEtlValidationMetric.NumericTolerance],
        _ => [CostContracts.GenAiEtlValidationMetric.Unavailable]
    };

    private static CostContracts.GenAiEtlResidualPath MapResidualPath(GenAiEtlAuditResidualPath value) => value switch
    {
        GenAiEtlAuditResidualPath.NotApplicable => CostContracts.GenAiEtlResidualPath.None,
        GenAiEtlAuditResidualPath.FrontierModelBelowConfidence => CostContracts.GenAiEtlResidualPath.FrontierModel,
        GenAiEtlAuditResidualPath.FrontierModelOrHumanReviewOnValidationFailure => CostContracts.GenAiEtlResidualPath.HumanReview,
        _ => CostContracts.GenAiEtlResidualPath.Undetermined
    };

    private static CostContracts.ProviderBillingAttribution MapAttribution(string? value) => value switch
    {
        "provider_model_period" => CostContracts.ProviderBillingAttribution.ProviderModelPeriod,
        "provider_period" => CostContracts.ProviderBillingAttribution.ProviderPeriod,
        _ => CostContracts.ProviderBillingAttribution.Unavailable
    };

    private static string CreateStableId(string prefix, IEnumerable<string?> values)
    {
        var builder = new StringBuilder(prefix);
        foreach (var value in values)
        {
            builder.Append('|');
            if (value is null)
            {
                builder.Append("-1:");
            }
            else
            {
                builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(value);
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"{prefix}_{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string? NormalizeNullableKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeKey(value);

    private static string? NormalizeNullableIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsRatio(double value) => double.IsFinite(value) && value is >= 0 and <= 1;

    private static bool IsNonNegative(double value) => double.IsFinite(value) && value >= 0;

    private static string FormatNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0) builder.Append('_');
            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static long SumChecked(IEnumerable<long> values)
    {
        var sum = 0L;
        foreach (var value in values) sum = checked(sum + value);
        return sum;
    }

    private static GenAiEtlAuditEvaluationOutcome Invalid(
        string field,
        string message,
        string code,
        string? rejectedValue = null) =>
        new(null, new GenAiEtlAuditValidationFailure(field, message, code, rejectedValue));

    private sealed record ClusterSeed(
        string ClusterId,
        string WorkflowKey,
        string? Provider,
        string? Model,
        GenAiEtlAuditStorageRow Row);

    private sealed record CostGroup(
        string Provider,
        string? Model,
        string? ModelName,
        string SourceEndpoint,
        string SourceKind,
        string Attribution,
        string CurrencyCode,
        DateTimeOffset PeriodStart,
        DateTimeOffset PeriodEnd,
        decimal Amount);

    private sealed record AuditCluster(
        CostContracts.GenAiEtlAuditCluster Contract,
        double? EstimatedCatalogTokenCostUsd,
        bool IsEtlCandidate);

    private sealed record AuditData(
        long TotalCalls,
        long TotalInputTokens,
        long TotalOutputTokens,
        long PricedCalls,
        double? EstimatedCatalogTokenCostUsd,
        double? CandidateEtlSpendShare,
        IReadOnlyList<CostContracts.ProviderBillingSource> BillingSources,
        IReadOnlyList<CostContracts.ModelCatalogSource> CatalogSources,
        IReadOnlyList<AuditCluster> Clusters);
}

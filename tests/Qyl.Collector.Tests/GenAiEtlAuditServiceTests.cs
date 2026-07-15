using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Cost;
using Qyl.Collector.Cost;
using Qyl.Collector.Hosting;
using Qyl.Collector.Primitives;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class GenAiEtlAuditServiceTests
{
    private static readonly DateTimeOffset s_periodStart =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_periodEnd = s_periodStart.AddDays(1);
    private static readonly DateTimeOffset s_now = s_periodEnd.AddMinutes(5);

    [Fact]
    public async Task Report_keeps_official_provider_model_cost_source_level_and_exposes_missing_evidence()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workload", "claude-test", 2);
        await InsertCostAsync(store, "anthropic", "claude-test", 10m);
        var storedBucket = Assert.Single(await store.GetProviderCostBucketsAsync(
            "default", s_periodStart, s_periodEnd, TestContext.Current.CancellationToken));
        Assert.Equal(s_periodStart, storedBucket.PeriodStart);
        Assert.Equal(s_periodEnd, storedBucket.PeriodEnd);
        Assert.Equal("USD", storedBucket.CurrencyCode);
        Assert.Equal(10m, storedBucket.Amount);
        Assert.Equal(
            ProviderCostScope.ForIdentifier("workspace-qyl").CreateStableKey("anthropic"),
            storedBucket.ProviderScopeKey);
        var storedSync = Assert.Single(await store.GetProviderCostSyncAsync(
            "default", TestContext.Current.CancellationToken));
        Assert.Equal(s_periodStart, storedSync.PeriodStart);
        Assert.Equal(s_periodEnd, storedSync.PeriodEnd);
        var service = CreateService(
            store,
            anthropicKey: "configured-admin-key",
            anthropicWorkspaceId: "workspace-qyl");

        var report = await service.GetReportAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            25,
            TestContext.Current.CancellationToken);

        var cluster = Assert.Single(report.Clusters);
        Assert.Equal(2, report.Summary.TotalCalls);
        Assert.Equal(30, report.Summary.TotalInputTokens);
        Assert.Equal(10, report.Summary.TotalOutputTokens);
        Assert.Null(report.Summary.EstimatedCatalogTokenCostUsd);
        Assert.Equal(0, report.Summary.CatalogTokenPricedCallCoverage);
        Assert.Null(report.Summary.EstimatedTokenEconomicConcentration);
        Assert.Null(report.Summary.CandidateEtlEstimatedTokenSpendShare);
        Assert.IsType<GenAiEtlCatalogTokenSourceUnavailableEstimate>(cluster.CatalogTokenEstimate);
        Assert.Equal(GenAiEtlOutputContract.Record, cluster.OutputContract);
        Assert.Equal(GenAiEtlTaskFamily.StructuredExtraction, cluster.TaskFamily);
        Assert.Equal(GenAiEtlCandidatePath.SmallClassifierExtractor, cluster.CandidatePath);
        Assert.Equal(
            [GenAiEtlValidationMetric.FieldExactMatch, GenAiEtlValidationMetric.SchemaValidity],
            cluster.ValidationMetrics);
        Assert.Equal(GenAiEtlResidualPath.HumanReview, cluster.ResidualPath);
        Assert.Contains(GenAiEtlEvidenceSignal.OutputContract, cluster.EvidenceSignals);
        Assert.Contains(GenAiEtlEvidenceSignal.ProviderModel, cluster.EvidenceSignals);
        Assert.Contains(GenAiEtlEvidenceSignal.TokenUsage, cluster.EvidenceSignals);
        Assert.DoesNotContain(GenAiEtlEvidenceSignal.CatalogTokenEstimate, cluster.EvidenceSignals);
        Assert.Contains(GenAiEtlEvidenceSignal.CatalogTokenEstimate, cluster.MissingEvidence);
        Assert.Contains(GenAiEtlEvidenceSignal.WorkflowIdentity, cluster.MissingEvidence);
        Assert.Contains(GenAiEtlEvidenceSignal.AcceptedOutputReplay, cluster.MissingEvidence);
        Assert.Equal(6, cluster.PromotionGates.Count);
        Assert.All(cluster.PromotionGates, static gate =>
            Assert.Equal(GenAiEtlPromotionGateState.BlockedMissingEvidence, gate.State));

        var anthropic = Assert.Single(report.BillingSources, source => source.Provider == "anthropic");
        Assert.Equal(ProviderBillingSourceStatus.Current, anthropic.Status);
        Assert.Equal(10, anthropic.ReportedCostUsd);
        Assert.Equal("claude-test", anthropic.ModelName);
        var openAi = Assert.Single(report.BillingSources, source => source.Provider == "openai");
        Assert.Equal(ProviderBillingSourceStatus.Unconfigured, openAi.Status);
        Assert.Null(openAi.ReportedCostUsd);
        var catalogSource = Assert.Single(report.CatalogSources);
        Assert.Equal("openrouter", catalogSource.SourceId);
        Assert.Equal(ModelCatalogSourceStatus.Pending, catalogSource.Status);
    }

    [Fact]
    public async Task Report_refuses_to_split_one_provider_model_total_across_multiple_workflows()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workflow-a", "claude-test", 1);
        await InsertCallsAsync(store, "workflow-b", "claude-test", 1, traceOrdinal: 20);
        await InsertCostAsync(store, "anthropic", "claude-test", 10m);
        var service = CreateService(
            store,
            anthropicKey: "configured-admin-key",
            anthropicWorkspaceId: "workspace-qyl");

        var report = await service.GetReportAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            25,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Clusters.Count);
        Assert.Null(report.Summary.EstimatedCatalogTokenCostUsd);
        Assert.Equal(0, report.Summary.CatalogTokenPricedCallCoverage);
        Assert.All(report.Clusters, static cluster =>
        {
            Assert.IsType<GenAiEtlCatalogTokenSourceUnavailableEstimate>(cluster.CatalogTokenEstimate);
        });
        var source = Assert.Single(report.BillingSources, item => item.Provider == "anthropic");
        Assert.Equal(ProviderBillingSourceStatus.Current, source.Status);
        Assert.Equal(10, source.ReportedCostUsd);
    }

    [Fact]
    public async Task Report_does_not_attach_organization_cost_without_an_explicit_provider_scope()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workload", "claude-test", 2);
        await InsertCostAsync(
            store,
            "anthropic",
            "claude-test",
            10m,
            ProviderCostScope.Organization);
        var service = CreateService(store, anthropicKey: "configured-admin-key");

        var report = await service.GetReportAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            25,
            TestContext.Current.CancellationToken);

        var cluster = Assert.Single(report.Clusters);
        Assert.IsType<GenAiEtlCatalogTokenSourceUnavailableEstimate>(cluster.CatalogTokenEstimate);
        var source = Assert.Single(report.BillingSources, item => item.Provider == "anthropic");
        Assert.Equal(ProviderBillingSourceStatus.Current, source.Status);
        Assert.Equal(10, source.ReportedCostUsd);
    }

    [Fact]
    public async Task Report_ignores_buckets_from_a_previous_provider_scope()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workload", "claude-test", 1);
        await InsertCostAsync(
            store,
            "anthropic",
            "claude-test",
            10m,
            ProviderCostScope.ForIdentifier("workspace-old"));
        var service = CreateService(
            store,
            anthropicKey: "configured-admin-key",
            anthropicWorkspaceId: "workspace-new");

        var report = await service.GetReportAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            25,
            TestContext.Current.CancellationToken);

        Assert.IsType<GenAiEtlCatalogTokenSourceUnavailableEstimate>(
            Assert.Single(report.Clusters).CatalogTokenEstimate);
        var source = Assert.Single(report.BillingSources, item => item.Provider == "anthropic");
        Assert.Equal(ProviderBillingSourceStatus.Pending, source.Status);
        Assert.Null(source.ReportedCostUsd);
    }

    [Fact]
    public async Task Token_usage_is_evidence_only_when_every_call_in_the_cluster_reports_it()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workload", "claude-test", 2, completeTokenCoverage: false);
        var service = CreateService(store);

        var report = await service.GetReportAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            25,
            TestContext.Current.CancellationToken);

        var cluster = Assert.Single(report.Clusters);
        Assert.DoesNotContain(GenAiEtlEvidenceSignal.TokenUsage, cluster.EvidenceSignals);
        Assert.Contains(GenAiEtlEvidenceSignal.TokenUsage, cluster.MissingEvidence);
    }

    [Fact]
    public async Task Evaluation_requires_explicit_frontier_and_keeps_negative_value()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workload", "claude-test", 2);
        await InsertCostAsync(store, "anthropic", "claude-test", 10m);
        var service = CreateService(
            store,
            anthropicKey: "configured-admin-key",
            anthropicWorkspaceId: "workspace-qyl");
        var report = await service.GetReportAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            25,
            TestContext.Current.CancellationToken);
        var clusterId = Assert.Single(report.Clusters).ClusterId;

        var providerBaseline = await service.EvaluateAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            new GenAiEtlAuditEvaluationRequest
            {
                Scenarios =
                [
                    new GenAiEtlClusterScenario
                    {
                        ClusterId = clusterId,
                        Coverage = 0.5,
                        AlternativeCostPerCallUsd = 1,
                        PeriodMaintenanceCostUsd = 0.5,
                        PeriodErrorCostUsd = 0.5
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        Assert.Null(providerBaseline.Failure);
        // The fail-closed evaluation shape carries no cost fields at all; absence is structural.
        var providerResult = Assert.IsType<GenAiEtlUnavailableClusterEvaluation>(
            Assert.Single(providerBaseline.Response!.Results));
        Assert.Equal("missing_frontier_cost", providerResult.Status);
        Assert.Equal(clusterId, providerResult.ClusterId);
        Assert.Equal(2, providerResult.CallCount);
        Assert.Equal(0.5, providerResult.Coverage);
        Assert.Equal(1, providerResult.ServedCallCount);
        Assert.Equal(1, providerResult.ResidualCallCount);

        var explicitLoss = await service.EvaluateAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            new GenAiEtlAuditEvaluationRequest
            {
                Scenarios =
                [
                    new GenAiEtlClusterScenario
                    {
                        ClusterId = clusterId,
                        Coverage = 1,
                        FrontierCostPerCallUsd = 1,
                        AlternativeCostPerCallUsd = 2,
                        PeriodMaintenanceCostUsd = 1,
                        PeriodErrorCostUsd = 2
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var explicitResult = Assert.IsType<GenAiEtlScenarioClusterEvaluation>(
            Assert.Single(explicitLoss.Response!.Results));
        Assert.Equal("calculated", explicitResult.Status);
        Assert.Equal(1, explicitResult.FrontierCostPerCallUsd);
        Assert.Equal(2, explicitResult.CurrentPeriodCostUsd);
        Assert.Equal(-2, explicitResult.GrossReplaceableValueUsd);
        Assert.Equal(-5, explicitResult.NetReplaceableValueUsd);
    }

    [Fact]
    public async Task Evaluation_fails_closed_for_unknown_cluster_and_invalid_inputs()
    {
        await using var store = new DuckDbStore(":memory:");
        var service = CreateService(store);

        var invalidCoverage = await service.EvaluateAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            Request("missing", coverage: double.NaN),
            TestContext.Current.CancellationToken);
        Assert.Equal("scenario.coverage_out_of_range", invalidCoverage.Failure?.Code);

        var missing = await service.EvaluateAsync(
            "default",
            s_periodStart,
            s_periodEnd,
            Request("missing", coverage: 0.5),
            TestContext.Current.CancellationToken);
        Assert.Equal("scenario.cluster_not_found", missing.Failure?.Code);
        Assert.Null(missing.Response);
    }

    [Fact]
    public async Task Endpoints_serve_the_generated_contract_without_exposing_raw_span_names()
    {
        await using var store = new DuckDbStore(":memory:");
        await InsertCallsAsync(store, "workload", "claude-test", 2);
        await InsertCostAsync(store, "anthropic", "claude-test", 10m);
        var service = CreateService(
            store,
            anthropicKey: "configured-admin-key",
            anthropicWorkspaceId: "workspace-qyl");
        var timeProvider = new FixedTimeProvider(s_now);
        var getContext = CreateHttpContext();
        getContext.Request.QueryString = QueryString.Create(
        [
            new KeyValuePair<string, string?>("startTime", s_periodStart.ToString("O")),
            new KeyValuePair<string, string?>("endTime", s_periodEnd.ToString("O"))
        ]);

        var getResult = await CollectorEndpointExtensions.GetGenAiEtlAuditAsync(
            getContext,
            service,
            timeProvider,
            TestContext.Current.CancellationToken);
        await getResult.ExecuteAsync(getContext);
        getContext.Response.Body.Position = 0;
        using (var reader = new StreamReader(getContext.Response.Body, leaveOpen: true))
        {
            var json = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("raw span name", json, StringComparison.Ordinal);
        }

        getContext.Response.Body.Position = 0;
        var report = await JsonSerializer.DeserializeAsync(
            getContext.Response.Body,
            QylSerializerContext.Default.GenAiEtlAuditReport,
            TestContext.Current.CancellationToken);
        var clusterId = Assert.Single(Assert.IsType<GenAiEtlAuditReport>(report).Clusters).ClusterId;

        var postContext = CreateHttpContext();
        postContext.Request.QueryString = getContext.Request.QueryString;
        postContext.Request.ContentType = "application/json";
        postContext.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            Request(clusterId, 0.5),
            QylSerializerContext.Default.GenAiEtlAuditEvaluationRequest));
        var postResult = await CollectorEndpointExtensions.EvaluateGenAiEtlAuditAsync(
            postContext,
            service,
            timeProvider,
            TestContext.Current.CancellationToken);
        await postResult.ExecuteAsync(postContext);
        postContext.Response.Body.Position = 0;
        var evaluation = await JsonSerializer.DeserializeAsync(
            postContext.Response.Body,
            QylSerializerContext.Default.GenAiEtlAuditEvaluationResponse,
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, postContext.Response.StatusCode);
        var evaluationResult = Assert.IsType<GenAiEtlUnavailableClusterEvaluation>(
            Assert.Single(evaluation!.Results));
        Assert.Equal("missing_frontier_cost", evaluationResult.Status);
    }

    [Fact]
    public async Task Report_endpoint_defaults_to_thirty_completed_UTC_days()
    {
        await using var store = new DuckDbStore(":memory:");
        var service = CreateService(store);
        var context = CreateHttpContext();

        var result = await CollectorEndpointExtensions.GetGenAiEtlAuditAsync(
            context,
            service,
            new FixedTimeProvider(s_now),
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var report = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.GenAiEtlAuditReport,
            TestContext.Current.CancellationToken);

        var expectedEnd = new DateTimeOffset(s_now.UtcDateTime.Date, TimeSpan.Zero);
        Assert.Equal(expectedEnd.AddDays(-30), report!.PeriodStart);
        Assert.Equal(expectedEnd, report.PeriodEnd);
    }

    private static GenAiEtlAuditEvaluationRequest Request(string clusterId, double coverage) =>
        new()
        {
            Scenarios =
            [
                new GenAiEtlClusterScenario
                {
                    ClusterId = clusterId,
                    Coverage = coverage,
                    AlternativeCostPerCallUsd = 0,
                    PeriodMaintenanceCostUsd = 0,
                    PeriodErrorCostUsd = 0
                }
            ]
        };

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .ConfigureHttpJsonOptions(static options =>
                    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default))
                .BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task InsertCallsAsync(
        DuckDbStore store,
        string serviceName,
        string model,
        int count,
        int traceOrdinal = 1,
        bool completeTokenCoverage = true)
    {
        var startNano = TimeConversions.ToUnixNanoUnsigned(s_periodStart.AddHours(1));
        var rows = Enumerable.Range(0, count).Select(index => new SpanStorageRow
        {
            ProjectId = "default",
            TraceId = $"trace-{traceOrdinal + index}",
            SpanId = $"span-{traceOrdinal + index}",
            Name = "raw span name is intentionally not used",
            Kind = 3,
            StartTimeUnixNano = startNano + (ulong)index,
            EndTimeUnixNano = startNano + (ulong)index + 10_000_000,
            DurationNs = 10_000_000,
            StatusCode = 0,
            ServiceName = serviceName,
            GenAiOperationName = "chat",
            GenAiOutputType = "json",
            GenAiProviderName = "anthropic",
            GenAiRequestModel = model,
            GenAiInputTokens = 15,
            GenAiOutputTokens = completeTokenCoverage || index < count - 1 ? 5 : null
        }).ToArray();
        await store.EnqueueAsync(new SpanBatch(rows), TestContext.Current.CancellationToken);
    }

    private static async Task InsertCostAsync(
        DuckDbStore store,
        string provider,
        string model,
        decimal amount,
        ProviderCostScope? scope = null)
    {
        var providerScope = scope ?? ProviderCostScope.ForIdentifier("workspace-qyl");
        var providerScopeKey = providerScope.CreateStableKey(provider);
        await store.ReplaceProviderCostBucketsAsync(
            "default",
            provider,
            s_periodStart,
            s_periodEnd,
            [
                new ProviderCostBucketRow
                {
                    ProjectId = "default",
                    Provider = provider,
                    PeriodStart = s_periodStart,
                    PeriodEnd = s_periodEnd,
                    ModelKey = model,
                    SourceEndpoint = "https://api.anthropic.com/v1/organizations/cost_report",
                    ProviderScopeKey = providerScopeKey,
                    SourceKind = "actual_billed_cost",
                    Attribution = "provider_model_period",
                    CurrencyCode = "USD",
                    Amount = amount,
                    RetrievedAt = s_now
                }
            ],
            new ProviderCostSyncRow
            {
                ProjectId = "default",
                Provider = provider,
                SourceEndpoint = "https://api.anthropic.com/v1/organizations/cost_report",
                ProviderScopeKey = providerScopeKey,
                SourceKind = "actual_billed_cost",
                Attribution = "provider_model_period",
                Status = "current",
                LastAttemptAt = s_now,
                LastSuccessAt = s_now,
                PeriodStart = s_periodStart,
                PeriodEnd = s_periodEnd
            },
            TestContext.Current.CancellationToken);
    }

    private static GenAiEtlAuditService CreateService(
        IQylStore store,
        string? openAiKey = null,
        string? anthropicKey = null,
        string? openAiProjectId = null,
        string? anthropicWorkspaceId = null)
    {
        var timeProvider = new FixedTimeProvider(s_now);
        var catalogOptions = new ModelPricingCatalogOptions
        {
            SyncInterval = TimeSpan.FromHours(1),
            HttpTimeout = TimeSpan.FromSeconds(30),
            MaximumResponseBytes = 16 * 1024 * 1024,
            MaximumStaleAge = TimeSpan.FromHours(3),
            RetainedSnapshotsPerSource = 32
        };
        var catalogSources = new ModelPricingCatalogSourceRegistry([new StubCatalogSource()]);
        var catalogRepository = new ModelPricingCatalogRepository(
            store,
            catalogSources,
            catalogOptions,
            timeProvider);

        return new GenAiEtlAuditService(
            store,
            [
                new StubSource("openai", "https://api.openai.com/v1/organization/costs"),
                new StubSource("anthropic", "https://api.anthropic.com/v1/organizations/cost_report")
            ],
            new ProviderCostSyncOptions
            {
                ProjectId = "default",
                SyncInterval = TimeSpan.FromMinutes(15),
                LookbackDays = 31,
                OpenAiAdminKey = openAiKey,
                OpenAiProjectId = openAiProjectId,
                AnthropicAdminKey = anthropicKey,
                AnthropicWorkspaceScope = anthropicWorkspaceId is null
                    ? ProviderCostScope.Organization
                    : ProviderCostScope.ForIdentifier(anthropicWorkspaceId)
            },
            new GenAiEtlCatalogEstimator(catalogRepository, catalogSources),
            new ModelPricingCatalogStateService(catalogSources, catalogOptions, store, timeProvider),
            timeProvider);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubSource(string provider, string endpoint) : IProviderCostSource
    {
        public string Provider { get; } = provider;

        public Uri SourceEndpoint { get; } = new(endpoint);

        public Task<ProviderCostFetchResult> FetchAsync(
            DateTimeOffset periodStart,
            DateTimeOffset periodEnd,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The report service must not call provider APIs inline.");
    }

    private sealed class StubCatalogSource : IModelPricingCatalogSource
    {
        public string SourceId => "openrouter";

        public int Priority => 100;

        public string ConfigurationFingerprint => "test";

        public Uri SourceEndpoint { get; } = new("https://openrouter.ai/api/v1/models");

        public Task<ModelPricingCatalogFetchResult> FetchAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The report service must not call catalog APIs inline.");
    }
}
